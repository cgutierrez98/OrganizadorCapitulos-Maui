using System.Collections.Generic;
using System.Threading.Tasks;

namespace organizadorCapitulos.Core.Interfaces.Repositories
{
    public interface IFileRepository
    {
        Task<IEnumerable<string>> GetVideoFilesAsync(IEnumerable<string> folders);
        Task MoveFileAsync(string sourcePath, string destinationPath);
        Task CopyFileAsync(string sourcePath, string destinationPath);
        Task CopyLargeFileAsync(string sourcePath, string destinationPath);
        bool FileExists(string path);
        void DeleteFile(string path);
        string[] GetVideoExtensions();
    }
}
