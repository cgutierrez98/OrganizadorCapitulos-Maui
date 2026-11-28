using System;
using System.Collections.Frozen;
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
        private static readonly string[] VideoExtensions =
            { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".mpeg", ".webm", ".m4v" };

        private static readonly FrozenSet<string> VideoExtensionSet =
            VideoExtensions.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        public async Task<IEnumerable<string>> GetVideoFilesAsync(IEnumerable<string> folders, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                const FileAttributes hiddenSystem = FileAttributes.Hidden | FileAttributes.System;
                var allFiles = new List<string>();
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var stack = new Stack<string>();
                var enumOptions = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.None
                };

                foreach (string folder in folders)
                {
                    if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) continue;
                    var canonicalRoot = GetCanonicalDirectoryPath(new DirectoryInfo(folder));
                    if (visited.Add(canonicalRoot))
                    {
                        stack.Push(folder);
                    }
                }

                while (stack.Count > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    var currentDir = stack.Pop();

                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(currentDir, "*.*", enumOptions))
                        {
                            if (VideoExtensionSet.Contains(Path.GetExtension(file)))
                            {
                                allFiles.Add(file);
                            }
                        }

                        var dirInfo = new DirectoryInfo(currentDir);
                        foreach (var subDir in dirInfo.EnumerateDirectories("*", enumOptions))
                        {
                            if ((subDir.Attributes & hiddenSystem) == hiddenSystem) continue;

                            var canonicalSub = GetCanonicalDirectoryPath(subDir);
                            if (visited.Add(canonicalSub))
                            {
                                stack.Push(subDir.FullName);
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        // Log and continue with remaining folders instead of aborting
                        System.Diagnostics.Debug.WriteLine($"[FileRepository] Error al acceder a {currentDir}: {ex.Message}");
                    }
                }

                return allFiles;
            }, ct);
        }

        private static string GetCanonicalDirectoryPath(DirectoryInfo dirInfo)
        {
            try
            {
                if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    var target = dirInfo.ResolveLinkTarget(returnFinalTarget: true);
                    if (target != null && !string.IsNullOrWhiteSpace(target.FullName))
                    {
                        return Path.GetFullPath(target.FullName);
                    }
                }
            }
            catch
            {
                // Fallback to full path for OneDrive cloud placeholders or restricted links
            }

            return Path.GetFullPath(dirInfo.FullName);
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

        public string[] GetVideoExtensions() => (string[])VideoExtensions.Clone();
    }
}
