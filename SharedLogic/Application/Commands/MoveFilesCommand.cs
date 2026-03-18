using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using organizadorCapitulos.Core.Interfaces.Commands;
using organizadorCapitulos.Core.Interfaces.Observers;
using organizadorCapitulos.Core.Interfaces.Repositories;

namespace organizadorCapitulos.Application.Commands
{
    public class MoveFilesCommand : IFileOperationCommand
    {
        private readonly IFileRepository _fileRepository;
        private readonly IProgressObserver _progressObserver;
        private readonly List<string> _sourcePaths;
        private readonly string _destinationFolder;
        private readonly Dictionary<string, string> _movedFiles = new Dictionary<string, string>();
        private bool _executed = false;

        public string Description => $"Mover {_sourcePaths.Count} archivos a {_destinationFolder}";
        public bool CanUndo => _executed;

        public MoveFilesCommand(IFileRepository fileRepository, IProgressObserver progressObserver,
                              List<string> sourcePaths, string destinationFolder)
        {
            _fileRepository = fileRepository;
            _progressObserver = progressObserver;
            _sourcePaths = sourcePaths;
            _destinationFolder = destinationFolder;
        }

        public async Task ExecuteAsync()
        {
            int totalFiles = _sourcePaths.Count;
            int processedFiles = 0;

            foreach (string sourcePath in _sourcePaths)
            {
                processedFiles++;
                string fileName = Path.GetFileName(sourcePath);
                string destinationPath = Path.Combine(_destinationFolder, fileName);

                _progressObserver?.UpdateProgress(processedFiles, totalFiles, fileName);

                if (_fileRepository.FileExists(destinationPath))
                {
                    _fileRepository.DeleteFile(destinationPath);
                }

                await _fileRepository.MoveFileAsync(sourcePath, destinationPath);
                _movedFiles[sourcePath] = destinationPath;
            }

            _executed = true;
        }

        public async Task UndoAsync()
        {
            if (!_executed) return;

            int totalFiles = _movedFiles.Count;
            int processedFiles = 0;

            foreach (var kvp in _movedFiles)
            {
                processedFiles++;
                string fileName = Path.GetFileName(kvp.Key);
                _progressObserver?.UpdateProgress(processedFiles, totalFiles, fileName);

                await _fileRepository.MoveFileAsync(kvp.Value, kvp.Key);
            }

            _movedFiles.Clear();
            _executed = false;
        }
    }
}
