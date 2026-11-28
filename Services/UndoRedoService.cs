using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
        private const int MaxHistoryBatches = 50;

        private readonly IFileRepository _fileRepository;
        private readonly ILogger<UndoRedoService> _logger;
        private readonly Stack<List<RenameOperation>> _undoStack = new();
        private readonly Stack<List<RenameOperation>> _redoStack = new();
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private readonly string _persistPath;
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public int UndoCount => _undoStack.Count;
        public int RedoCount => _redoStack.Count;

        public UndoRedoService(IFileRepository fileRepository) : this(fileRepository, null, null) { }

        public UndoRedoService(IFileRepository fileRepository, ILogger<UndoRedoService> logger) : this(fileRepository, null, logger) { }

        public UndoRedoService(IFileRepository fileRepository, string? persistDirectory, ILogger<UndoRedoService>? logger = null)
        {
            _fileRepository = fileRepository;
            _logger = logger ?? NullLogger<UndoRedoService>.Instance;
            var appFolder = persistDirectory;
            if (string.IsNullOrWhiteSpace(appFolder))
            {
                appFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "OrganizadorCapitulos");
            }
            Directory.CreateDirectory(appFolder);
            _persistPath = Path.Combine(appFolder, "undo_history.json");
        }

        public async Task LoadHistoryAsync()
        {
            if (!File.Exists(_persistPath)) return;
            try
            {
                using var stream = File.OpenRead(_persistPath);
                var batches = await JsonSerializer.DeserializeAsync<List<List<RenameOperation>>>(stream, _jsonOptions).ConfigureAwait(false);
                if (batches == null) return;
                // Restore in original order: first pushed = bottom of stack, keep up to MaxHistoryBatches most recent
                int start = Math.Max(0, batches.Count - MaxHistoryBatches);
                for (int i = start; i < batches.Count; i++)
                {
                    _undoStack.Push(batches[i]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load undo history from {PersistPath}", _persistPath);
            }
        }

        private async Task SaveHistoryAsync()
        {
            await _saveLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Stack enumerates top-first; reverse to store in push order
                var batches = _undoStack.Reverse().ToList();
                var tempPath = _persistPath + ".tmp";
                await using (var stream = File.Create(tempPath))
                {
                    await JsonSerializer.SerializeAsync(stream, batches, _jsonOptions).ConfigureAwait(false);
                }
                File.Move(tempPath, _persistPath, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save undo history to {PersistPath}", _persistPath);
            }
            finally
            {
                _saveLock.Release();
            }
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
                TrimUndoHistoryIfNeeded();
                _redoStack.Clear();
                _ = SaveHistoryAsync();
            }
        }

        private void TrimUndoHistoryIfNeeded()
        {
            if (_undoStack.Count <= MaxHistoryBatches) return;

            // Stack enumerates top-first; take most recent MaxHistoryBatches and restore push order
            var kept = _undoStack.Take(MaxHistoryBatches).Reverse().ToList();
            _undoStack.Clear();
            foreach (var batch in kept)
            {
                _undoStack.Push(batch);
            }
        }

        public Task<(bool success, string message, int count, IReadOnlyList<RenameOperation> appliedOperations)> UndoAsync() =>
            UndoSingleAsync(persist: true);

        public async Task<(bool success, string message, int count, IReadOnlyList<RenameOperation> appliedOperations)> UndoAllAsync()
        {
            if (!CanUndo)
                return (false, "No hay operaciones para deshacer", 0, Array.Empty<RenameOperation>());

            var totalApplied = new List<RenameOperation>();
            while (CanUndo)
            {
                var result = await UndoSingleAsync(persist: false).ConfigureAwait(false);
                if (!result.success) break;
                totalApplied.AddRange(result.appliedOperations);
            }

            if (totalApplied.Count > 0)
            {
                await SaveHistoryAsync().ConfigureAwait(false);
            }

            return totalApplied.Count > 0
                ? (true, $"Deshecho: {totalApplied.Count} archivo(s)", totalApplied.Count, totalApplied)
                : (false, "No hubo operaciones deshechas", 0, Array.Empty<RenameOperation>());
        }

        private async Task<(bool success, string message, int count, IReadOnlyList<RenameOperation> appliedOperations)> UndoSingleAsync(bool persist)
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

                    await _fileRepository.MoveFileAsync(op.NewPath, targetPath).ConfigureAwait(false);
                    applied.Add(new RenameOperation { OldPath = op.NewPath, NewPath = targetPath });
                    forRedo.Add(new RenameOperation { OldPath = targetPath, NewPath = op.NewPath });
                }

                if (applied.Count > 0)
                    _redoStack.Push(forRedo);

                if (persist)
                    await SaveHistoryAsync().ConfigureAwait(false);

                return (true, $"Deshecho: {applied.Count} archivo(s)", applied.Count, applied);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during Undo operation");
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

                    await _fileRepository.MoveFileAsync(op.OldPath, targetPath).ConfigureAwait(false);
                    redone.Add(new RenameOperation { OldPath = op.OldPath, NewPath = targetPath });
                }

                if (redone.Count > 0)
                {
                    _undoStack.Push(redone);
                    TrimUndoHistoryIfNeeded();
                }

                await SaveHistoryAsync().ConfigureAwait(false);
                return (true, $"Rehecho: {redone.Count} archivo(s)", redone.Count, redone);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during Redo operation");
                _redoStack.Push(operations);
                return (false, $"Error al rehacer: {ex.Message}", 0, Array.Empty<RenameOperation>());
            }
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            _ = SaveHistoryAsync();
        }

        private string GetUniqueConflictPath(string path)
        {
            var dir = Path.GetDirectoryName(path) ?? "";
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            var counter = 1;
            var target = path;
            while (_fileRepository.FileExists(target))
            {
                target = Path.Combine(dir, $"{name}_{counter++}{ext}");
            }
            return target;
        }
    }
}
