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
                Episode = 1,
                EpisodeTitle = "Pilot"
            }, new ChangeStrategy());

            var expectedPath = Path.Combine(workspace, "S01E01 - Breaking Bad - Pilot.mkv");

            Assert.Equal(expectedPath, result);
            Assert.True(File.Exists(expectedPath));

            Directory.Delete(workspace, true);
        }

        [Fact]
        public async Task RenameFileWithCompanionsAsync_RenamesCompanionSubtitles()
        {
            var workspace = CreateTemporaryDirectory();
            var videoPath = Path.Combine(workspace, "Breaking.Bad.S01E01.mkv");
            var srtPath = Path.Combine(workspace, "Breaking.Bad.S01E01.es.srt");
            await File.WriteAllTextAsync(videoPath, "video");
            await File.WriteAllTextAsync(srtPath, "subtitle");

            var repo = new FileRepository();
            var svc = new FileOrganizerService(repo, new FakeProgressObserver());

            var (newPath, companions) = await svc.RenameFileWithCompanionsAsync(videoPath, new ChapterInfo
            {
                Title = "Breaking Bad",
                Season = 1,
                Episode = 1,
                EpisodeTitle = "Pilot"
            }, new ChangeStrategy());

            var expectedVideo = Path.Combine(workspace, "S01E01 - Breaking Bad - Pilot.mkv");
            var expectedSrt = Path.Combine(workspace, "S01E01 - Breaking Bad - Pilot.es.srt");

            Assert.Equal(expectedVideo, newPath);
            Assert.Single(companions);
            Assert.True(File.Exists(expectedVideo));
            Assert.True(File.Exists(expectedSrt));
            Assert.False(File.Exists(srtPath));

            Directory.Delete(workspace, true);
        }

        [Fact]
        public async Task MoveFilesStructuredAsync_CreatesSeriesSeasonSubfolders()
        {
            var workspace = CreateTemporaryDirectory();
            var sourceDir = Directory.CreateDirectory(Path.Combine(workspace, "src")).FullName;
            var destRoot = Directory.CreateDirectory(Path.Combine(workspace, "library")).FullName;
            var videoPath = Path.Combine(sourceDir, "S02E05 - Breaking Bad.mkv");
            await File.WriteAllTextAsync(videoPath, "video");

            var repo = new FileRepository();
            var svc = new FileOrganizerService(repo, new FakeProgressObserver());

            var moves = await svc.MoveFilesStructuredAsync(
                new List<(string, ChapterInfo?)> { (videoPath, new ChapterInfo { Title = "Breaking Bad", Season = 2, Episode = 5 }) },
                destRoot,
                organizeIntoSubfolders: true);

            var expectedPath = Path.Combine(destRoot, "Breaking Bad", "Temporada 02", "S02E05 - Breaking Bad.mkv");
            Assert.Single(moves);
            Assert.Equal(expectedPath, moves[0].newPath);
            Assert.True(File.Exists(expectedPath));

            Directory.Delete(workspace, true);
        }

        [Fact]
        public async Task BuildCompanionIndex_IndexesMultipleCompanionsAcrossVideos()
        {
            var workspace = CreateTemporaryDirectory();
            var ep1 = Path.Combine(workspace, "Show.S01E01.mkv");
            var ep1Srt = Path.Combine(workspace, "Show.S01E01.es.srt");
            var ep1Nfo = Path.Combine(workspace, "Show.S01E01.nfo");
            var ep2 = Path.Combine(workspace, "Show.S01E02.mkv");
            var ep2Srt = Path.Combine(workspace, "Show.S01E02.srt");

            await File.WriteAllTextAsync(ep1, "v1");
            await File.WriteAllTextAsync(ep1Srt, "s1");
            await File.WriteAllTextAsync(ep1Nfo, "n1");
            await File.WriteAllTextAsync(ep2, "v2");
            await File.WriteAllTextAsync(ep2Srt, "s2");

            var index = FileOrganizerService.BuildCompanionIndex(new[] { ep1, ep2 });
            var repo = new FileRepository();
            var svc = new FileOrganizerService(repo, new FakeProgressObserver());

            var (newPath1, moves1) = await svc.RenameFileWithCompanionsAsync(
                ep1,
                new ChapterInfo { Title = "Show", Season = 1, Episode = 1 },
                new ChangeStrategy(),
                CancellationToken.None,
                fileNameTemplate: null,
                renameCompanions: true,
                companionIndex: index);

            Assert.Equal(Path.Combine(workspace, "S01E01 - Show.mkv"), newPath1);
            Assert.Equal(2, moves1.Count);
            Assert.True(File.Exists(Path.Combine(workspace, "S01E01 - Show.es.srt")));
            Assert.True(File.Exists(Path.Combine(workspace, "S01E01 - Show.nfo")));

            Directory.Delete(workspace, true);
        }

        [Fact]
        public async Task FileRepository_GetVideoFilesAsync_IncludesSystemOrReadOnlySubfolders_AndSkipsHiddenSystem()
        {
            var workspace = CreateTemporaryDirectory();
            var systemFolder = Directory.CreateDirectory(Path.Combine(workspace, "CustomSystemFolder"));
            var readOnlyFolder = Directory.CreateDirectory(Path.Combine(workspace, "ReadOnlyFolder"));
            var hiddenSystemFolder = Directory.CreateDirectory(Path.Combine(workspace, "ProtectedOsFolder"));

            var vid1 = Path.Combine(systemFolder.FullName, "ep1.mkv");
            var vid2 = Path.Combine(readOnlyFolder.FullName, "ep2.m4v");
            var vidIgnored = Path.Combine(hiddenSystemFolder.FullName, "ignored.mkv");

            await File.WriteAllTextAsync(vid1, "v1");
            await File.WriteAllTextAsync(vid2, "v2");
            await File.WriteAllTextAsync(vidIgnored, "v3");

            File.SetAttributes(systemFolder.FullName, FileAttributes.Directory | FileAttributes.System);
            File.SetAttributes(readOnlyFolder.FullName, FileAttributes.Directory | FileAttributes.ReadOnly);
            File.SetAttributes(hiddenSystemFolder.FullName, FileAttributes.Directory | FileAttributes.Hidden | FileAttributes.System);

            try
            {
                var repo = new FileRepository();
                var found = (await repo.GetVideoFilesAsync(new[] { workspace })).ToList();

                Assert.Contains(vid1, found);
                Assert.Contains(vid2, found);
                Assert.DoesNotContain(vidIgnored, found);
            }
            finally
            {
                File.SetAttributes(systemFolder.FullName, FileAttributes.Directory);
                File.SetAttributes(readOnlyFolder.FullName, FileAttributes.Directory);
                File.SetAttributes(hiddenSystemFolder.FullName, FileAttributes.Directory);
                Directory.Delete(workspace, true);
            }
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
