using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

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
    }

    public class SettingsService
    {
        private readonly string _settingsPath;
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private AppSettings _settings = new();
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public SettingsService(string? settingsDirectory = null)
        {
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
            get => DecryptApiKey(_settings.TmdbApiKey);
            set
            {
                _settings.TmdbApiKey = EncryptApiKey(value);
                _ = SaveSettingsAsync();
            }
        }

        public string Theme
        {
            get => _settings.Theme;
            set
            {
                _settings.Theme = value;
                _ = SaveSettingsAsync();
            }
        }

        public string? DefaultOutputFolder
        {
            get => _settings.DefaultOutputFolder;
            set
            {
                _settings.DefaultOutputFolder = string.IsNullOrWhiteSpace(value) ? null : value;
                _ = SaveSettingsAsync();
            }
        }

        public string? FileNameTemplate
        {
            get => _settings.FileNameTemplate;
            set
            {
                _settings.FileNameTemplate = string.IsNullOrWhiteSpace(value) ? null : value;
                _ = SaveSettingsAsync();
            }
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

        private static string DecryptApiKey(string stored)
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
                catch
                {
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
            catch
            {
                _settings = new AppSettings();
            }
        }

        private async Task SaveSettingsAsync()
        {
            await _saveLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var json = JsonSerializer.Serialize(_settings, _jsonOptions);
                await File.WriteAllTextAsync(_settingsPath, json).ConfigureAwait(false);
            }
            catch
            {
                // Ignore save errors
            }
            finally
            {
                _saveLock.Release();
            }
        }
    }
}
