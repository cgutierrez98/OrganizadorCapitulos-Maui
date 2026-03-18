using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Interfaces.Observers;
using organizadorCapitulos.Core.Interfaces.Repositories;
using organizadorCapitulos.Core.Interfaces.Strategies;

namespace organizadorCapitulos.Application.Services
{
    public class FileOrganizerService
    {
        private readonly IFileRepository fileRepository;
        private readonly IProgressObserver? progressObserver;

        public FileOrganizerService(IFileRepository fileRepository, IProgressObserver? progressObserver)
        {
            this.fileRepository = fileRepository;
            this.progressObserver = progressObserver;
        }

        public async Task<List<string>> LoadVideoFilesAsync(IEnumerable<string> folders)
        {
            progressObserver?.UpdateStatus("Buscando archivos de video...");
            var files = await fileRepository.GetVideoFilesAsync(folders);
            return files.ToList();
        }

        public async Task<string> RenameFileAsync(string sourcePath, ChapterInfo chapterInfo, IRenameStrategy strategy)
        {
            if (!chapterInfo.IsValid())
            {
                throw new ArgumentException("La información del capítulo no es válida");
            }

            string extension = Path.GetExtension(sourcePath);
            string newFileName = chapterInfo.GenerateFileName(extension);
            string? directory = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("No se pudo obtener el directorio del archivo.");
            string destinationPath = Path.Combine(directory, newFileName);

            if (fileRepository.FileExists(destinationPath))
            {
                destinationPath = GetUniqueFilePath(destinationPath);
            }

            await fileRepository.MoveFileAsync(sourcePath, destinationPath);
            strategy.UpdateAfterRename(chapterInfo);

            return destinationPath;
        }

        public async Task<List<(string oldPath, string newPath)>> MoveFilesAsync(List<string> sourcePaths, string destinationFolder)
        {
            var movedFiles = new List<(string oldPath, string newPath)>();
            progressObserver?.UpdateStatus("Moviendo archivos...");

            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            int totalFiles = sourcePaths.Count;
            int processedFiles = 0;

            foreach (string sourcePath in sourcePaths)
            {
                processedFiles++;
                string fileName = Path.GetFileName(sourcePath);
                string destinationPath = Path.Combine(destinationFolder, fileName);

                progressObserver?.UpdateProgress(processedFiles, totalFiles, fileName);

                // Ensure we don't overwrite existing files
                if (fileRepository.FileExists(destinationPath))
                {
                    destinationPath = GetUniqueFilePath(destinationPath);
                }

                await fileRepository.MoveFileAsync(sourcePath, destinationPath);
                movedFiles.Add((sourcePath, destinationPath));
            }

            return movedFiles;
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
