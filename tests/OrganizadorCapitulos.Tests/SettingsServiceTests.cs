using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using OrganizadorCapitulos.Maui.Services;
using Xunit;

namespace OrganizadorCapitulos.Tests
{
    [SupportedOSPlatform("windows10.0.17763.0")]
    public class SettingsServiceTests
    {
        [Fact]
        public void Constructor_LoadsPersistedSettingsImmediately()
        {
            var settingsDirectory = CreateTemporaryDirectory();
            var settingsPath = Path.Combine(settingsDirectory, "settings.json");

            File.WriteAllText(settingsPath, JsonSerializer.Serialize(new AppSettings
            {
                TmdbApiKey = "tmdb-key",
                Theme = "dark"
            }));

            var service = new SettingsService(settingsDirectory);

            Assert.Equal("tmdb-key", service.TmdbApiKey);
            Assert.Equal("dark", service.Theme);

            Directory.Delete(settingsDirectory, true);
        }

        private static string CreateTemporaryDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "OrganizadorCapitulos.Tests", Path.GetRandomFileName());
            Directory.CreateDirectory(path);
            return path;
        }
    }
}