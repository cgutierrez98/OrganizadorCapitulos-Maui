/* Temporarily disabled while SharedLogic is factored into a separate testable library.
   Re-enable after extracting SharedLogic into a non-MAUI class library and referencing it
   from the test project. */

#if false
using System.Collections.Generic;
using System.Threading.Tasks;
using organizadorCapitulos.Application.Services;
using organizadorCapitulos.Core.Interfaces.Repositories;
using organizadorCapitulos.Core.Interfaces.Observers;
using Xunit;
using System.Linq;

namespace OrganizadorCapitulos.Tests
{
    public class FileOrganizerServiceTests
    {
        [Fact]
        public async Task LoadVideoFilesAsync_ReturnsFilesFromRepository()
        {
            var repo = new FakeFileRepository(new[] { "C:\\folder\\a.mp4", "C:\\folder\\b.mkv" });
            var progress = new FakeProgressObserver();
            var svc = new FileOrganizerService(repo, progress);

            var result = await svc.LoadVideoFilesAsync(new[] { "C:\\folder" });
            var list = result.ToList();

            Assert.Equal(2, list.Count);
            Assert.Contains("C:\\folder\\a.mp4", list);
            Assert.Contains("C:\\folder\\b.mkv", list);
        }

        private class FakeFileRepository : IFileRepository
        {
            private readonly IEnumerable<string> _files;
            public FakeFileRepository(IEnumerable<string> files) { _files = files; }
            public Task<IEnumerable<string>> GetVideoFilesAsync(IEnumerable<string> folders) => Task.FromResult(_files);
            public Task MoveFileAsync(string sourcePath, string destinationPath) => Task.CompletedTask;
            public Task CopyFileAsync(string sourcePath, string destinationPath) => Task.CompletedTask;
            public Task CopyLargeFileAsync(string sourcePath, string destinationPath) => Task.CompletedTask;
            public bool FileExists(string path) => false;
            public void DeleteFile(string path) { }
            public string[] GetVideoExtensions() => new[] { ".mp4", ".mkv" };
        }

        private class FakeProgressObserver : IProgressObserver
        {
            public void UpdateProgress(int current, int total, string filename) { }
            public void UpdateStatus(string status) { }
        }
    }
}
#endif
