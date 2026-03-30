using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OrganizadorCapitulos.Maui.Services
{
    public class AppSettings
    {
        public string TmdbApiKey { get; set; } = "";
        public string Theme { get; set; } = "light";
    }

    public class SettingsService
    {
        private readonly string _settingsPath;
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private AppSettings _settings = new();
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

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
            get => _settings.TmdbApiKey;
            set
            {
                _settings.TmdbApiKey = value;
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
