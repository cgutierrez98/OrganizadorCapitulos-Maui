using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OrganizadorCapitulos.Maui.Services;
using organizadorCapitulos.Core.Interfaces.Repositories;
using Xunit;

namespace OrganizadorCapitulos.Tests;

public class UndoRedoServiceTests
{
    private static UndoRedoService CreateService(IFileRepository? repo = null)
    {
        if (repo == null)
        {
            var fake = new FakeFileRepository();
            repo = fake;
        }
        return new UndoRedoService(repo);
    }

    [Fact]
    public void Initially_CannotUndoOrRedo()
    {
        var svc = CreateService();
        Assert.False(svc.CanUndo);
        Assert.False(svc.CanRedo);
    }

    [Fact]
    public void AfterRecord_CanUndo_ButNotRedo()
    {
        var svc = CreateService();
        svc.RecordOperation("old.mkv", "new.mkv");
        Assert.True(svc.CanUndo);
        Assert.False(svc.CanRedo);
        Assert.Equal(1, svc.UndoCount);
    }

    [Fact]
    public async Task AfterUndo_CanRedo_ButNotUndo()
    {
        var repo = new FakeFileRepository();
        repo.ExistingFiles.Add("new.mkv"); // file that will be "undone"
        var svc = new UndoRedoService(repo);
        svc.RecordOperation("old.mkv", "new.mkv");

        await svc.UndoAsync();

        Assert.False(svc.CanUndo);
        Assert.True(svc.CanRedo);
    }

    [Fact]
    public async Task UndoThenRedo_RestoresUndoStack()
    {
        var repo = new FakeFileRepository();
        repo.ExistingFiles.Add("new.mkv");
        repo.ExistingFiles.Add("old.mkv"); // after undo, old path exists for redo
        var svc = new UndoRedoService(repo);
        svc.RecordOperation("old.mkv", "new.mkv");

        await svc.UndoAsync();
        // After undo, old.mkv is "restored" — now redo should re-do the rename
        repo.ExistingFiles.Clear();
        repo.ExistingFiles.Add("old.mkv");
        await svc.RedoAsync();

        Assert.True(svc.CanUndo);
        Assert.False(svc.CanRedo);
    }

    [Fact]
    public void RecordAfterUndo_ClearsRedoStack()
    {
        var repo = new FakeFileRepository();
        var svc = new UndoRedoService(repo);
        svc.RecordOperation("old.mkv", "new.mkv");
        // Manually push to simulate: undo was possible
        svc.RecordOperations(new List<RenameOperation>
        {
            new RenameOperation { OldPath = "a.mkv", NewPath = "b.mkv" }
        });
        Assert.Equal(2, svc.UndoCount);
        Assert.Equal(0, svc.RedoCount);
    }

    // Minimal fake repository for tests
    private class FakeFileRepository : IFileRepository
    {
        public HashSet<string> ExistingFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<IEnumerable<string>> GetVideoFilesAsync(IEnumerable<string> folders, CancellationToken ct = default)
            => Task.FromResult<IEnumerable<string>>(Array.Empty<string>());

        public Task MoveFileAsync(string sourcePath, string destinationPath, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken ct = default)
            => Task.CompletedTask;

        public bool FileExists(string path) => ExistingFiles.Contains(path);

        public void DeleteFile(string path) { }

        public string[] GetVideoExtensions() => Array.Empty<string>();
    }
}
