using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace organizadorCapitulos.Core.Interfaces.Repositories
{
    public interface IFileRepository
    {
        Task<IEnumerable<string>> GetVideoFilesAsync(IEnumerable<string> folders, CancellationToken ct = default);
        Task MoveFileAsync(string sourcePath, string destinationPath, CancellationToken ct = default);
        Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken ct = default);
        bool FileExists(string path);
        void DeleteFile(string path);
        string[] GetVideoExtensions();
    }
}
