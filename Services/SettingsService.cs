using System.Text.Json;
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
        private AppSettings _settings = new();

        public SettingsService()
        {
            // Store settings in user's AppData folder
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "OrganizadorCapitulos");
            
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }
            
            _settingsPath = Path.Combine(appFolder, "settings.json");
            // Load settings in background to avoid blocking UI during startup
            _ = Task.Run(async () => await LoadSettingsAsync().ConfigureAwait(false));
        }

        public string TmdbApiKey
        {
            get => _settings.TmdbApiKey;
            set
            {
                _settings.TmdbApiKey = value;
                // Fire-and-forget save to avoid blocking caller; log internally on error
                _ = Task.Run(async () => await SaveSettingsAsync().ConfigureAwait(false));
            }
        }

        public string Theme
        {
            get => _settings.Theme;
            set
            {
                _settings.Theme = value;
                _ = Task.Run(async () => await SaveSettingsAsync().ConfigureAwait(false));
            }
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = await File.ReadAllTextAsync(_settingsPath).ConfigureAwait(false);
                    _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch
            {
                _settings = new AppSettings();
            }
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                await File.WriteAllTextAsync(_settingsPath, json).ConfigureAwait(false);
            }
            catch
            {
                // Ignore save errors
            }
        }
    }
}
