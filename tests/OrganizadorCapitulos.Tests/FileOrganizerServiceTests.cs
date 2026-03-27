using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using organizadorCapitulos.Application.Services;
using organizadorCapitulos.Application.Strategies;
using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Interfaces.Observers;
using organizadorCapitulos.Infrastructure.Repositories;
using Xunit;

namespace OrganizadorCapitulos.Tests
{
    [SupportedOSPlatform("windows10.0.17763.0")]
    public class FileOrganizerServiceTests
    {
        [Fact]
        public async Task MoveFilesAsync_CreatesUniqueNameWhenDestinationAlreadyExists()
        {
            var workspace = CreateTemporaryDirectory();
            var sourceDir = Directory.CreateDirectory(Path.Combine(workspace, "source")).FullName;
            var destinationDir = Directory.CreateDirectory(Path.Combine(workspace, "destination")).FullName;
            var sourcePath = Path.Combine(sourceDir, "episode.mkv");
            var existingDestinationPath = Path.Combine(destinationDir, "episode.mkv");

            await File.WriteAllTextAsync(sourcePath, "incoming-content");
            await File.WriteAllTextAsync(existingDestinationPath, "existing-content");

            var repo = new FileRepository();
            var progress = new FakeProgressObserver();
            var svc = new FileOrganizerService(repo, progress);

            var result = await svc.MoveFilesAsync(new List<string> { sourcePath }, destinationDir);
            var move = Assert.Single(result);
            var uniqueDestinationPath = Path.Combine(destinationDir, "episode (1).mkv");

            Assert.Equal(sourcePath, move.oldPath);
            Assert.Equal(uniqueDestinationPath, move.newPath);
            Assert.Equal("existing-content", await File.ReadAllTextAsync(existingDestinationPath));
            Assert.Equal("incoming-content", await File.ReadAllTextAsync(uniqueDestinationPath));

            Directory.Delete(workspace, true);
        }

        [Fact]
        public async Task RenameFileAsync_PreservesSeriesTitleWhenEpisodeTitleIsPresent()
        {
            var workspace = CreateTemporaryDirectory();
            var sourcePath = Path.Combine(workspace, "episode.mkv");
            await File.WriteAllTextAsync(sourcePath, "incoming-content");

            var repo = new FileRepository();
            var progress = new FakeProgressObserver();
            var svc = new FileOrganizerService(repo, progress);

            var result = await svc.RenameFileAsync(sourcePath, new ChapterInfo
            {
                Title = "Breaking Bad",
                Season = 1,
                Chapter = 1,
                EpisodeTitle = "Pilot"
            }, new ChangeStrategy());

            var expectedPath = Path.Combine(workspace, "S01E01 - Breaking Bad - Pilot.mkv");

            Assert.Equal(expectedPath, result);
            Assert.True(File.Exists(expectedPath));

            Directory.Delete(workspace, true);
        }

        private static string CreateTemporaryDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "OrganizadorCapitulos.Tests", Path.GetRandomFileName());
            Directory.CreateDirectory(path);
            return path;
        }

        private sealed class FakeProgressObserver : IProgressObserver
        {
            public void UpdateProgress(int current, int total, string filename) { }
            public void UpdateStatus(string status) { }
        }
    }
}
