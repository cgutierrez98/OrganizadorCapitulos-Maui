using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OrganizadorCapitulos.Maui.Services
{
    public class AppSettings
    {
        /// <summary>TMDB API key, stored encrypted (DPAPI) on Windows; plain text fallback on other platforms.</summary>
        public string TmdbApiKey { get; set; } = "";

        public string Theme { get; set; } = "light";

        // Future optional settings can be added here with a default value; older settings.json files
        // will deserialize correctly because JsonIgnore(WhenWritingDefault) omits them on save.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? DefaultOutputFolder { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? FileNameTemplate { get; set; }

        public string TmdbLanguage { get; set; } = "es-ES";

        public bool OrganizeIntoSubfolders { get; set; } = false;

        public bool RenameCompanionFiles { get; set; } = true;
    }

    public class SettingsService
    {
        private readonly string _settingsPath;
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private readonly object _debounceLock = new();
        private readonly ILogger<SettingsService> _logger;
        private AppSettings _settings = new();
        private string _decryptedApiKey = "";
        private CancellationTokenSource? _debounceCts;
        private const int SaveDebounceMs = 200;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public SettingsService() : this(null, null) { }

        public SettingsService(ILogger<SettingsService> logger) : this(null, logger) { }

        public SettingsService(string? settingsDirectory, ILogger<SettingsService>? logger = null)
        {
            _logger = logger ?? NullLogger<SettingsService>.Instance;
            // Store settings in user's AppData folder
            var appFolder = settingsDirectory;
            if (string.IsNullOrWhiteSpace(appFolder))
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                appFolder = Path.Combine(appDataPath, "OrganizadorCapitulos");
            }

            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            _settingsPath = Path.Combine(appFolder, "settings.json");
            LoadSettings();
        }

        public string TmdbApiKey
        {
            get => _decryptedApiKey;
            set
            {
                var normalized = value ?? "";
                if (string.Equals(_decryptedApiKey, normalized, StringComparison.Ordinal)) return;
                _decryptedApiKey = normalized;
                _settings.TmdbApiKey = EncryptApiKey(normalized);
                ScheduleSave();
            }
        }

        public string Theme
        {
            get => _settings.Theme;
            set
            {
                if (string.Equals(_settings.Theme, value, StringComparison.Ordinal)) return;
                _settings.Theme = value;
                ScheduleSave();
            }
        }

        public string? DefaultOutputFolder
        {
            get => _settings.DefaultOutputFolder;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? null : value;
                if (string.Equals(_settings.DefaultOutputFolder, normalized, StringComparison.Ordinal)) return;
                _settings.DefaultOutputFolder = normalized;
                ScheduleSave();
            }
        }

        public string? FileNameTemplate
        {
            get => _settings.FileNameTemplate;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? null : value;
                if (string.Equals(_settings.FileNameTemplate, normalized, StringComparison.Ordinal)) return;
                _settings.FileNameTemplate = normalized;
                ScheduleSave();
            }
        }

        public string TmdbLanguage
        {
            get => string.IsNullOrWhiteSpace(_settings.TmdbLanguage) ? "es-ES" : _settings.TmdbLanguage;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? "es-ES" : value.Trim();
                if (string.Equals(_settings.TmdbLanguage, normalized, StringComparison.Ordinal)) return;
                _settings.TmdbLanguage = normalized;
                ScheduleSave();
            }
        }

        public bool OrganizeIntoSubfolders
        {
            get => _settings.OrganizeIntoSubfolders;
            set
            {
                if (_settings.OrganizeIntoSubfolders == value) return;
                _settings.OrganizeIntoSubfolders = value;
                ScheduleSave();
            }
        }

        public bool RenameCompanionFiles
        {
            get => _settings.RenameCompanionFiles;
            set
            {
                if (_settings.RenameCompanionFiles == value) return;
                _settings.RenameCompanionFiles = value;
                ScheduleSave();
            }
        }

        /// <summary>Applies multiple setting changes and schedules a single disk write.</summary>
        public void Update(Action<AppSettings> mutate)
        {
            var plainKeyBefore = _decryptedApiKey;
            var editable = new AppSettings
            {
                TmdbApiKey = plainKeyBefore,
                Theme = _settings.Theme,
                DefaultOutputFolder = _settings.DefaultOutputFolder,
                FileNameTemplate = _settings.FileNameTemplate,
                TmdbLanguage = _settings.TmdbLanguage,
                OrganizeIntoSubfolders = _settings.OrganizeIntoSubfolders,
                RenameCompanionFiles = _settings.RenameCompanionFiles
            };

            mutate(editable);

            _decryptedApiKey = editable.TmdbApiKey ?? "";
            _settings.TmdbApiKey = EncryptApiKey(_decryptedApiKey);
            _settings.Theme = string.IsNullOrWhiteSpace(editable.Theme) ? "light" : editable.Theme;
            _settings.DefaultOutputFolder = string.IsNullOrWhiteSpace(editable.DefaultOutputFolder) ? null : editable.DefaultOutputFolder;
            _settings.FileNameTemplate = string.IsNullOrWhiteSpace(editable.FileNameTemplate) ? null : editable.FileNameTemplate.Trim();
            _settings.TmdbLanguage = string.IsNullOrWhiteSpace(editable.TmdbLanguage) ? "es-ES" : editable.TmdbLanguage.Trim();
            _settings.OrganizeIntoSubfolders = editable.OrganizeIntoSubfolders;
            _settings.RenameCompanionFiles = editable.RenameCompanionFiles;

            ScheduleSave();
        }

        public async Task FlushAsync()
        {
            CancellationTokenSource? cts;
            lock (_debounceLock)
            {
                cts = _debounceCts;
                _debounceCts = null;
            }

            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
            }

            await SaveSettingsCoreAsync().ConfigureAwait(false);
        }

        // ── DPAPI helpers ─────────────────────────────────────────────────────
        private const string DpapiPrefix = "DPAPI:";

        private static string EncryptApiKey(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return "";
#if WINDOWS
            try
            {
                var data = System.Text.Encoding.UTF8.GetBytes(plainText);
                var encrypted = System.Security.Cryptography.ProtectedData.Protect(
                    data, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                return DpapiPrefix + Convert.ToBase64String(encrypted);
            }
            catch
            {
                // Fallback: store plain text if DPAPI is unavailable
                return plainText;
            }
#else
            return plainText;
#endif
        }

        private string DecryptApiKey(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return "";
#if WINDOWS
            if (stored.StartsWith(DpapiPrefix, StringComparison.Ordinal))
            {
                try
                {
                    var encrypted = Convert.FromBase64String(stored.Substring(DpapiPrefix.Length));
                    var decrypted = System.Security.Cryptography.ProtectedData.Unprotect(
                        encrypted, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                    return System.Text.Encoding.UTF8.GetString(decrypted);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Cannot decrypt stored TMDB API key via DPAPI");
                    return ""; // Cannot decrypt; return empty rather than exposing garbage
                }
            }
#endif
            // Legacy plain-text value or non-Windows: return as-is
            return stored;
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    _settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load settings from {SettingsPath}", _settingsPath);
                _settings = new AppSettings();
            }

            _decryptedApiKey = DecryptApiKey(_settings.TmdbApiKey);
        }

        private void ScheduleSave()
        {
            lock (_debounceLock)
            {
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _debounceCts = new CancellationTokenSource();
                var token = _debounceCts.Token;
                _ = DebouncedSaveAsync(token);
            }
        }

        private async Task DebouncedSaveAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(SaveDebounceMs, token).ConfigureAwait(false);
                if (!token.IsCancellationRequested)
                {
                    await SaveSettingsCoreAsync().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Debounced by newer setting change
            }
        }

        private async Task SaveSettingsCoreAsync()
        {
            await _saveLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var json = JsonSerializer.Serialize(_settings, _jsonOptions);
                var tempPath = _settingsPath + ".tmp";
                await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);
                File.Move(tempPath, _settingsPath, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save settings to {SettingsPath}", _settingsPath);
            }
            finally
            {
                _saveLock.Release();
            }
        }
    }
}
