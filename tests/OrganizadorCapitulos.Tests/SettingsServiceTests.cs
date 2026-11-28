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

        [Fact]
        public async Task Update_PersistsAllFieldsAndCachesApiKey()
        {
            var settingsDirectory = CreateTemporaryDirectory();
            var service = new SettingsService(settingsDirectory);

            service.Update(s =>
            {
                s.TmdbApiKey = "my-secret-key";
                s.TmdbLanguage = "en-US";
                s.FileNameTemplate = "{Title} - {Season}{Episode}";
                s.OrganizeIntoSubfolders = true;
                s.RenameCompanionFiles = false;
            });
            await service.FlushAsync();

            var reloaded = new SettingsService(settingsDirectory);

            Assert.Equal("my-secret-key", service.TmdbApiKey);
            Assert.Equal("my-secret-key", reloaded.TmdbApiKey);
            Assert.Equal("en-US", reloaded.TmdbLanguage);
            Assert.Equal("{Title} - {Season}{Episode}", reloaded.FileNameTemplate);
            Assert.True(reloaded.OrganizeIntoSubfolders);
            Assert.False(reloaded.RenameCompanionFiles);

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