using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using organizadorCapitulos.Core.Exceptions;
using organizadorCapitulos.Core.Interfaces.Repositories;

namespace organizadorCapitulos.Infrastructure.Repositories
{
    public class FileRepository : IFileRepository
    {
        private readonly string[] _videoExtensions = { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".mpeg", ".webm" };

        public async Task<IEnumerable<string>> GetVideoFilesAsync(IEnumerable<string> folders, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var extensions = new HashSet<string>(_videoExtensions.Select(ext => ext.ToLower()));
                var allFiles = new List<string>();

                foreach (string folder in folders)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var folderFiles = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
                            .Where(file => extensions.Contains(Path.GetExtension(file).ToLower()));
                        allFiles.AddRange(folderFiles);
                    }
                    catch (System.Exception ex)
                    {
                        throw new DirectoryAccessException($"Error al acceder a {folder}", folder, ex);
                    }
                }
                return allFiles;
            }, ct);
        }

        public async Task MoveFileAsync(string sourcePath, string destinationPath, CancellationToken ct = default)
        {
            try
            {
                await Task.Run(() => File.Move(sourcePath, destinationPath), ct);
            }
            catch (System.Exception ex)
            {
                throw new FileOperationException($"Error al mover archivo: {sourcePath}", sourcePath, ex);
            }
        }

        public async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken ct = default)
        {
            try
            {
                await Task.Run(() => File.Copy(sourcePath, destinationPath, true), ct);
            }
            catch (System.Exception ex)
            {
                throw new FileOperationException($"Error al copiar archivo: {sourcePath}", sourcePath, ex);
            }
        }

        public bool FileExists(string path) => File.Exists(path);

        public void DeleteFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (System.Exception ex)
            {
                throw new FileOperationException($"Error al eliminar archivo: {path}", path, ex);
            }
        }

        public string[] GetVideoExtensions() => _videoExtensions;
    }
}
