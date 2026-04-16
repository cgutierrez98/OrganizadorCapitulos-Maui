using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using organizadorCapitulos.Core.Interfaces.Repositories;

namespace OrganizadorCapitulos.Maui.Services
{
    public class RenameOperation
    {
        public string OldPath { get; set; } = "";
        public string NewPath { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class UndoRedoService
    {
        private readonly IFileRepository _fileRepository;
        private readonly Stack<List<RenameOperation>> _undoStack = new();
        private readonly Stack<List<RenameOperation>> _redoStack = new();

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public int UndoCount => _undoStack.Count;
        public int RedoCount => _redoStack.Count;

        public UndoRedoService(IFileRepository fileRepository)
        {
            _fileRepository = fileRepository;
        }

        public void RecordOperation(string oldPath, string newPath)
        {
            RecordOperations(new List<RenameOperation>
            {
                new RenameOperation { OldPath = oldPath, NewPath = newPath }
            });
        }

        public void RecordOperations(List<RenameOperation> operations)
        {
            if (operations.Count > 0)
            {
                _undoStack.Push(operations);
                _redoStack.Clear();
            }
        }

        public async Task<(bool success, string message, int count, IReadOnlyList<RenameOperation> appliedOperations)> UndoAsync()
        {
            if (!CanUndo)
                return (false, "No hay operaciones para deshacer", 0, Array.Empty<RenameOperation>());

            var operations = _undoStack.Pop();
            var applied = new List<RenameOperation>();
            var forRedo = new List<RenameOperation>();

            try
            {
                foreach (var op in operations)
                {
                    if (!_fileRepository.FileExists(op.NewPath)) continue;

                    string targetPath = op.OldPath;
                    if (_fileRepository.FileExists(op.OldPath))
                    {
                        targetPath = GetUniqueConflictPath(op.OldPath);
                    }

                    await _fileRepository.MoveFileAsync(op.NewPath, targetPath);
                    applied.Add(new RenameOperation { OldPath = op.NewPath, NewPath = targetPath });
                    forRedo.Add(new RenameOperation { OldPath = targetPath, NewPath = op.NewPath });
                }

                if (applied.Count > 0)
                    _redoStack.Push(forRedo);

                return (true, $"Deshecho: {applied.Count} archivo(s)", applied.Count, applied);
            }
            catch (Exception ex)
            {
                _undoStack.Push(operations);
                return (false, $"Error al deshacer: {ex.Message}", 0, Array.Empty<RenameOperation>());
            }
        }

        public async Task<(bool success, string message, int count, IReadOnlyList<RenameOperation> appliedOperations)> RedoAsync()
        {
            if (!CanRedo)
                return (false, "No hay operaciones para rehacer", 0, Array.Empty<RenameOperation>());

            var operations = _redoStack.Pop();
            var redone = new List<RenameOperation>();

            try
            {
                foreach (var op in operations)
                {
                    if (!_fileRepository.FileExists(op.OldPath)) continue;

                    string targetPath = op.NewPath;
                    if (_fileRepository.FileExists(op.NewPath))
                    {
                        targetPath = GetUniqueConflictPath(op.NewPath);
                    }

                    await _fileRepository.MoveFileAsync(op.OldPath, targetPath);
                    redone.Add(new RenameOperation { OldPath = op.OldPath, NewPath = targetPath });
                }

                if (redone.Count > 0)
                    _undoStack.Push(redone);

                return (true, $"Rehecho: {redone.Count} archivo(s)", redone.Count, redone);
            }
            catch (Exception ex)
            {
                _redoStack.Push(operations);
                return (false, $"Error al rehacer: {ex.Message}", 0, Array.Empty<RenameOperation>());
            }
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        private static string GetUniqueConflictPath(string path)
        {
            var dir = Path.GetDirectoryName(path) ?? "";
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            var counter = 1;
            var target = path;
            while (File.Exists(target))
            {
                target = Path.Combine(dir, $"{name}_{counter++}{ext}");
            }
            return target;
        }
    }
}
