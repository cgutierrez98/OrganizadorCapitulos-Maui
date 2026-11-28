using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Interfaces.Observers;
using organizadorCapitulos.Core.Interfaces.Repositories;
using organizadorCapitulos.Core.Interfaces.Strategies;

namespace organizadorCapitulos.Application.Services
{
    public class FileOrganizerService
    {
        private static readonly FrozenSet<string> CompanionExtensions = new[]
        {
            ".srt", ".ass", ".ssa", ".vtt", ".sub", ".idx", ".nfo"
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        private readonly IFileRepository fileRepository;
        private readonly IProgressObserver? progressObserver;

        public FileOrganizerService(IFileRepository fileRepository, IProgressObserver? progressObserver)
        {
            this.fileRepository = fileRepository;
            this.progressObserver = progressObserver;
        }

        public sealed class CompanionIndex
        {
            private readonly Dictionary<string, List<(string path, string suffix)>> _byVideoPath;

            internal CompanionIndex(Dictionary<string, List<(string path, string suffix)>> byVideoPath)
            {
                _byVideoPath = byVideoPath;
            }

            public IReadOnlyList<(string path, string suffix)> GetCompanions(string videoPath)
            {
                return _byVideoPath.TryGetValue(videoPath, out var list)
                    ? list
                    : Array.Empty<(string path, string suffix)>();
            }
        }

        public static CompanionIndex BuildCompanionIndex(IEnumerable<string> videoPaths)
        {
            var byVideoPath = new Dictionary<string, List<(string path, string suffix)>>(StringComparer.OrdinalIgnoreCase);
            var videosByDirectory = new Dictionary<string, List<(string fullPath, string baseName)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var videoPath in videoPaths)
            {
                if (string.IsNullOrWhiteSpace(videoPath)) continue;
                var dir = Path.GetDirectoryName(videoPath);
                if (string.IsNullOrEmpty(dir)) continue;

                if (!videosByDirectory.TryGetValue(dir, out var list))
                {
                    list = new List<(string, string)>();
                    videosByDirectory[dir] = list;
                }
                list.Add((videoPath, Path.GetFileNameWithoutExtension(videoPath)));
            }

            foreach (var (directory, videos) in videosByDirectory)
            {
                if (!Directory.Exists(directory)) continue;

                var byBaseName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var (fullPath, baseName) in videos)
                {
                    if (!byBaseName.TryGetValue(baseName, out var videoList))
                    {
                        videoList = new List<string>();
                        byBaseName[baseName] = videoList;
                    }
                    videoList.Add(fullPath);
                }

                var lookup = byBaseName.GetAlternateLookup<ReadOnlySpan<char>>();
                try
                {
                    foreach (var candidate in Directory.EnumerateFiles(directory))
                    {
                        var ext = Path.GetExtension(candidate);
                        if (!CompanionExtensions.Contains(ext)) continue;

                        var candidateName = Path.GetFileName(candidate);
                        int maxPrefixLen = candidateName.Length - ext.Length;
                        int dotIdx = candidateName.IndexOf('.');
                        while (dotIdx > 0 && dotIdx <= maxPrefixLen)
                        {
                            if (lookup.TryGetValue(candidateName.AsSpan(0, dotIdx), out var matchingVideos))
                            {
                                var suffix = candidateName[dotIdx..];
                                foreach (var videoPath in matchingVideos)
                                {
                                    if (!byVideoPath.TryGetValue(videoPath, out var companions))
                                    {
                                        companions = new List<(string, string)>();
                                        byVideoPath[videoPath] = companions;
                                    }
                                    companions.Add((candidate, suffix));
                                }
                            }
                            dotIdx = candidateName.IndexOf('.', dotIdx + 1);
                        }
                    }
                }
                catch
                {
                    // Ignore directory access issues for companions
                }
            }

            return new CompanionIndex(byVideoPath);
        }

        public async Task<List<string>> LoadVideoFilesAsync(IEnumerable<string> folders, CancellationToken ct = default)
        {
            progressObserver?.UpdateStatus("Buscando archivos de video...");
            var files = await fileRepository.GetVideoFilesAsync(folders, ct);
            return files.ToList();
        }

        public async Task<string> RenameFileAsync(string sourcePath, ChapterInfo chapterInfo, IRenameStrategy strategy, CancellationToken ct = default, string? fileNameTemplate = null)
        {
            var (newPath, _) = await RenameFileWithCompanionsAsync(sourcePath, chapterInfo, strategy, ct, fileNameTemplate, renameCompanions: true);
            return newPath;
        }

        public Task<(string newPath, List<(string oldPath, string newPath)> companionMoves)> RenameFileWithCompanionsAsync(
            string sourcePath,
            ChapterInfo chapterInfo,
            IRenameStrategy strategy,
            CancellationToken ct = default,
            string? fileNameTemplate = null,
            bool renameCompanions = true) =>
            RenameFileWithCompanionsAsync(sourcePath, chapterInfo, strategy, ct, fileNameTemplate, renameCompanions, companionIndex: null);

        public async Task<(string newPath, List<(string oldPath, string newPath)> companionMoves)> RenameFileWithCompanionsAsync(
            string sourcePath,
            ChapterInfo chapterInfo,
            IRenameStrategy strategy,
            CancellationToken ct,
            string? fileNameTemplate,
            bool renameCompanions,
            CompanionIndex? companionIndex)
        {
            if (!chapterInfo.IsValid())
            {
                throw new ArgumentException("La información del capítulo no es válida");
            }

            string extension = Path.GetExtension(sourcePath);
            string newFileName = chapterInfo.GenerateFileName(extension, fileNameTemplate);
            string? directory = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("No se pudo obtener el directorio del archivo.");
            string destinationPath = Path.Combine(directory, newFileName);

            if (!string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase) && fileRepository.FileExists(destinationPath))
            {
                destinationPath = GetUniqueFilePath(destinationPath);
            }

            var companionMoves = new List<(string oldPath, string newPath)>();
            IReadOnlyList<(string path, string suffix)> companions = !renameCompanions
                ? Array.Empty<(string path, string suffix)>()
                : companionIndex != null
                    ? companionIndex.GetCompanions(sourcePath)
                    : FindCompanionFiles(sourcePath);

            if (!string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
            {
                await fileRepository.MoveFileAsync(sourcePath, destinationPath, ct);
            }

            string newBaseName = Path.GetFileNameWithoutExtension(destinationPath);
            foreach (var (companionPath, suffix) in companions)
            {
                ct.ThrowIfCancellationRequested();
                if (companionIndex != null && !fileRepository.FileExists(companionPath)) continue;
                string companionDest = Path.Combine(directory, newBaseName + suffix);
                if (string.Equals(companionPath, companionDest, StringComparison.OrdinalIgnoreCase)) continue;
                if (fileRepository.FileExists(companionDest))
                {
                    companionDest = GetUniqueFilePath(companionDest);
                }

                await fileRepository.MoveFileAsync(companionPath, companionDest, ct);
                companionMoves.Add((companionPath, companionDest));
            }

            strategy.UpdateAfterRename(chapterInfo);

            return (destinationPath, companionMoves);
        }

        public async Task<List<(string oldPath, string newPath)>> MoveFilesAsync(List<string> sourcePaths, string destinationFolder, CancellationToken ct = default)
        {
            var items = sourcePaths.Select(p => (p, (ChapterInfo?)null)).ToList();
            return await MoveFilesStructuredAsync(items, destinationFolder, organizeIntoSubfolders: false, ct);
        }

        public async Task<List<(string oldPath, string newPath)>> MoveFilesStructuredAsync(
            List<(string sourcePath, ChapterInfo? info)> items,
            string destinationRoot,
            bool organizeIntoSubfolders,
            CancellationToken ct = default)
        {
            var movedFiles = new List<(string oldPath, string newPath)>();
            progressObserver?.UpdateStatus("Moviendo archivos...");

            if (!Directory.Exists(destinationRoot))
            {
                Directory.CreateDirectory(destinationRoot);
            }

            var companionIndex = BuildCompanionIndex(items.Select(i => i.sourcePath));
            var movedCompanions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int totalFiles = items.Count;
            int processedFiles = 0;

            foreach (var (sourcePath, info) in items)
            {
                ct.ThrowIfCancellationRequested();
                processedFiles++;
                string fileName = Path.GetFileName(sourcePath);
                string targetFolder = organizeIntoSubfolders
                    ? BuildStructuredFolder(destinationRoot, info)
                    : destinationRoot;

                if (!Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                string destinationPath = Path.Combine(targetFolder, fileName);

                progressObserver?.UpdateProgress(processedFiles, totalFiles, fileName);

                var companions = companionIndex.GetCompanions(sourcePath);

                // Ensure we don't overwrite existing files
                if (!string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase) && fileRepository.FileExists(destinationPath))
                {
                    destinationPath = GetUniqueFilePath(destinationPath);
                }

                if (!string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
                {
                    await fileRepository.MoveFileAsync(sourcePath, destinationPath, ct);
                    movedFiles.Add((sourcePath, destinationPath));
                }

                string newBaseName = Path.GetFileNameWithoutExtension(destinationPath);
                foreach (var (companionPath, suffix) in companions)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!movedCompanions.Add(companionPath)) continue;
                    string companionDest = Path.Combine(targetFolder, newBaseName + suffix);
                    if (string.Equals(companionPath, companionDest, StringComparison.OrdinalIgnoreCase)) continue;
                    if (fileRepository.FileExists(companionDest))
                    {
                        companionDest = GetUniqueFilePath(companionDest);
                    }

                    await fileRepository.MoveFileAsync(companionPath, companionDest, ct);
                    movedFiles.Add((companionPath, companionDest));
                }
            }

            return movedFiles;
        }

        public static string BuildStructuredFolder(string rootFolder, ChapterInfo? info)
        {
            if (info == null || string.IsNullOrWhiteSpace(info.Title))
            {
                return rootFolder;
            }

            string safeTitle = ChapterInfo.SanitizeFileName(info.Title).Trim();
            if (string.IsNullOrWhiteSpace(safeTitle))
            {
                return rootFolder;
            }

            if (info.Season == 0 && info.Episode == 0)
            {
                string movieFolder = info.Year > 0 ? $"{safeTitle} ({info.Year})" : safeTitle;
                return Path.Combine(rootFolder, movieFolder);
            }

            string seasonFolder = info.Season > 0 ? $"Temporada {info.Season:00}" : "Especiales";
            return Path.Combine(rootFolder, safeTitle, seasonFolder);
        }

        private static List<(string path, string suffix)> FindCompanionFiles(string videoPath)
        {
            var result = new List<(string path, string suffix)>();
            string? directory = Path.GetDirectoryName(videoPath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return result;
            }

            string baseName = Path.GetFileNameWithoutExtension(videoPath);
            try
            {
                foreach (var candidate in Directory.EnumerateFiles(directory))
                {
                    if (string.Equals(candidate, videoPath, StringComparison.OrdinalIgnoreCase)) continue;
                    string ext = Path.GetExtension(candidate);
                    if (!CompanionExtensions.Contains(ext)) continue;

                    string candidateName = Path.GetFileName(candidate);
                    if (candidateName.StartsWith(baseName + ".", StringComparison.OrdinalIgnoreCase))
                    {
                        string suffix = candidateName.Substring(baseName.Length);
                        result.Add((candidate, suffix));
                    }
                }
            }
            catch
            {
                // Ignore directory access issues for companions
            }

            return result;
        }

        private string GetUniqueFilePath(string fullPath)
        {
            int count = 1;
            string directory = Path.GetDirectoryName(fullPath) ?? "";
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);
            string extension = Path.GetExtension(fullPath);
            string uniquePath = fullPath;

            while (fileRepository.FileExists(uniquePath))
            {
                string newFileName = $"{fileNameWithoutExtension} ({count++}){extension}";
                uniquePath = Path.Combine(directory, newFileName);
            }

            return uniquePath;
        }
    }
}
