using System;
using System.Threading.Tasks;
using organizadorCapitulos.Core.Interfaces.Commands;
using organizadorCapitulos.Core.Interfaces.Repositories;

namespace organizadorCapitulos.Application.Commands
{
    public class RenameFileCommand : IFileOperationCommand
    {
        private readonly IFileRepository _fileRepository;
        private readonly string _sourcePath;
        private readonly string _destinationPath;
        private bool _executed = false;

        public string Description => $"Renombrar: {System.IO.Path.GetFileName(_sourcePath)} → {System.IO.Path.GetFileName(_destinationPath)}";
        public bool CanUndo => _executed;

        public RenameFileCommand(IFileRepository fileRepository, string sourcePath, string destinationPath)
        {
            _fileRepository = fileRepository;
            _sourcePath = sourcePath;
            _destinationPath = destinationPath;
        }

        public async Task ExecuteAsync()
        {
            if (_fileRepository.FileExists(_destinationPath))
            {
                throw new InvalidOperationException("El archivo destino ya existe");
            }

            await _fileRepository.MoveFileAsync(_sourcePath, _destinationPath);
            _executed = true;
        }

        public async Task UndoAsync()
        {
            if (!_executed) return;

            await _fileRepository.MoveFileAsync(_destinationPath, _sourcePath);
            _executed = false;
        }
    }
}
