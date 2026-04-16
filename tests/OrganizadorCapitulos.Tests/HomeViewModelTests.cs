using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using organizadorCapitulos.Application.Services;
using organizadorCapitulos.Application.Strategies;
using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Interfaces.Services;
using organizadorCapitulos.Infrastructure.Repositories;
using OrganizadorCapitulos.Maui.Models;
using OrganizadorCapitulos.Maui.Services;
using OrganizadorCapitulos.Maui.Services.Interfaces;
using OrganizadorCapitulos.Maui.ViewModels;
using Xunit;

namespace OrganizadorCapitulos.Tests
{
    [SupportedOSPlatform("windows10.0.17763.0")]
    public class HomeViewModelTests
    {
        [Fact]
        public async Task MoveFilesAsync_UsesUniqueDestinationAndRecordsUndoAndLog()
        {
            var workspace = CreateTemporaryDirectory();
            var sourceDir = Directory.CreateDirectory(Path.Combine(workspace, "source")).FullName;
            var destinationDir = Directory.CreateDirectory(Path.Combine(workspace, "destination")).FullName;
            var sourcePath = Path.Combine(sourceDir, "episode.mkv");
            var existingDestinationPath = Path.Combine(destinationDir, "episode.mkv");

            await File.WriteAllTextAsync(sourcePath, "new-content");
            await File.WriteAllTextAsync(existingDestinationPath, "existing-content");

            var logService = new OperationLogService();
            var viewModel = CreateViewModel(logService);
            viewModel.Files.Add(new FileViewModel
            {
                FullPath = sourcePath,
                OriginalName = Path.GetFileName(sourcePath)
            });

            await viewModel.MoveFilesCommand.ExecuteAsync(new List<string> { destinationDir });

            var movedFile = Assert.Single(viewModel.Files);
            var uniqueDestinationPath = Path.Combine(destinationDir, "episode (1).mkv");

            Assert.Equal(uniqueDestinationPath, movedFile.FullPath);
            Assert.True(File.Exists(existingDestinationPath));
            Assert.Equal("existing-content", await File.ReadAllTextAsync(existingDestinationPath));
            Assert.True(File.Exists(uniqueDestinationPath));
            Assert.Equal("new-content", await File.ReadAllTextAsync(uniqueDestinationPath));
            Assert.Equal(1, viewModel.UndoRedo.UndoCount);
            Assert.Contains(logService.Entries, entry => entry.Action == "Move" && entry.OldName == "episode.mkv");
            Assert.Equal(uniqueDestinationPath, movedFile.FullPath);
            Assert.Equal(organizadorCapitulos.Core.Enums.FileStatus.Done, movedFile.Status);

            await viewModel.UndoCommand.ExecuteAsync(null);

            Assert.True(File.Exists(sourcePath));
            Assert.False(File.Exists(uniqueDestinationPath));
            Assert.Equal(sourcePath, movedFile.FullPath);
            Assert.Equal(organizadorCapitulos.Core.Enums.FileStatus.Pending, movedFile.Status);

            await viewModel.RedoCommand.ExecuteAsync(null);

            Assert.False(File.Exists(sourcePath));
            Assert.True(File.Exists(uniqueDestinationPath));
            Assert.Equal(uniqueDestinationPath, movedFile.FullPath);
            Assert.Equal(organizadorCapitulos.Core.Enums.FileStatus.Done, movedFile.Status);

            Directory.Delete(workspace, true);
        }

        [Fact]
        public void SetDragging_UpdatesIsDraggingAndRaisesPropertyChanged()
        {
            var vm = CreateViewModel(new OperationLogService());
            var notified = false;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "IsDragging")
                {
                    notified = true;
                }
            };

            vm.SetDragging(true);

            Assert.True(vm.IsDragging);
            Assert.True(notified);
        }

        [Fact]
        public async Task HandleDropAsync_ClearsIsDragging()
        {
            var vm = CreateViewModel(new OperationLogService());
            vm.SetDragging(true);

            await vm.HandleDropAsync();

            Assert.False(vm.IsDragging);
        }

        private static HomeViewModel CreateViewModel(OperationLogService logService)
        {
            var repo = new FileRepository();
            return new HomeViewModel(
                new FileOrganizerService(repo, null),
                new FakeAIService(),
                new RenameStrategyFactory(),
                new FakeMetadataService(),
                new SettingsService(CreateTemporaryDirectory()),
                new UndoRedoService(repo),
                logService,
                new FakeThemeService(),
                new FakeDragDropService());
        }

        private static string CreateTemporaryDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "OrganizadorCapitulos.Tests", Path.GetRandomFileName());
            Directory.CreateDirectory(path);
            return path;
        }

        private sealed class FakeAIService : IAIService
        {
            public Task<string?> SuggestTitleAsync(string filePath, CancellationToken ct = default) => Task.FromResult<string?>(null);

            public bool IsAvailable() => true;

            public Task<ChapterInfo?> AnalyzeFilenameAsync(string filename, CancellationToken ct = default) => Task.FromResult<ChapterInfo?>(null);
        }

        private sealed class FakeMetadataService : IMetadataService
        {
            public void Configure(string apiKey) { }

            public bool IsConfigured() => true;

            public Task<List<SeriesSearchResult>> SearchSeriesAsync(string query, CancellationToken ct = default) => Task.FromResult(new List<SeriesSearchResult>());

            public Task<string?> GetEpisodeTitleAsync(int seriesId, int season, int episode, CancellationToken ct = default) => Task.FromResult<string?>(null);

            public Task<(int season, int episode, string title)?> FindEpisodeByTitleAsync(int seriesId, string title, CancellationToken ct = default) => Task.FromResult<(int season, int episode, string title)?>(null);

            public Task<ChapterInfo?> GetEpisodeMetadataAsync(int seriesId, int season, int episode, CancellationToken ct = default) => Task.FromResult<ChapterInfo?>(null);
        }

        private sealed class FakeDragDropService : OrganizadorCapitulos.Maui.Services.Interfaces.IDragDropService
        {
            public event Action<IReadOnlyList<string>>? FoldersDropped;

            public void NotifyFoldersDropped(IReadOnlyList<string> folderPaths) =>
                FoldersDropped?.Invoke(folderPaths);
        }

        private sealed class FakeThemeService : IThemeService
        {
            public event Action<bool>? ThemeChanged;

            public bool IsDarkTheme => false;

            public Task InitializeAsync() => Task.CompletedTask;

            public Task ToggleThemeAsync() => Task.CompletedTask;

            public void SetJsRuntime(object jsRuntime) { }
        }
    }
}
