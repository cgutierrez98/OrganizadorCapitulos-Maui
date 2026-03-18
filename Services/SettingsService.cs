using System.Text.Json;

namespace OrganizadorCapitulos.Maui.Services
{
    public class AppSettings
    {
        public string TmdbApiKey { get; set; } = "";
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
            LoadSettings();
        }

        public string TmdbApiKey
        {
            get => _settings.TmdbApiKey;
            set
            {
                _settings.TmdbApiKey = value;
                SaveSettings();
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch
            {
                _settings = new AppSettings();
            }
        }

        private void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(_settingsPath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }
    }
}
