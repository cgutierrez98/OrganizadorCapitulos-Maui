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
        private readonly Stack<List<RenameOperation>> _undoStack = new();
        private readonly Stack<List<RenameOperation>> _redoStack = new();

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public int UndoCount => _undoStack.Count;
        public int RedoCount => _redoStack.Count;

        public void RecordOperation(string oldPath, string newPath)
        {
            // Record single operation as a batch of one
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
                _redoStack.Clear(); // Clear redo stack when new operation is recorded
            }
        }

        public async Task<(bool success, string message, int count)> UndoAsync()
        {
            if (!CanUndo)
                return (false, "No hay operaciones para deshacer", 0);

            var operations = _undoStack.Pop();

            try
            {
                var (success, successCount, reversedOps, error) = await Task.Run(() =>
                {
                    var localReversed = new List<RenameOperation>();
                    int localSuccess = 0;

                    foreach (var op in operations)
                    {
                        if (File.Exists(op.NewPath))
                        {
                            if (File.Exists(op.OldPath))
                            {
                                var dir = Path.GetDirectoryName(op.OldPath) ?? "";
                                var name = Path.GetFileNameWithoutExtension(op.OldPath);
                                var ext = Path.GetExtension(op.OldPath);
                                var counter = 1;
                                var targetPath = op.OldPath;
                                while (File.Exists(targetPath))
                                {
                                    targetPath = Path.Combine(dir, $"{name}_{counter}{ext}");
                                    counter++;
                                }
                                File.Move(op.NewPath, targetPath);
                                localReversed.Add(new RenameOperation { OldPath = op.NewPath, NewPath = targetPath });
                            }
                            else
                            {
                                File.Move(op.NewPath, op.OldPath);
                                localReversed.Add(new RenameOperation { OldPath = op.NewPath, NewPath = op.OldPath });
                            }
                            localSuccess++;
                        }
                    }

                    return (true, localSuccess, localReversed, (string?)null);
                }).ConfigureAwait(false);

                if (success && successCount > 0)
                {
                    _redoStack.Push(reversedOps);
                }

                return (true, $"Deshecho: {successCount} archivo(s)", successCount);
            }
            catch (Exception ex)
            {
                // Put back on undo stack if failed
                _undoStack.Push(operations);
                return (false, $"Error al deshacer: {ex.Message}", 0);
            }
        }

        public async Task<(bool success, string message, int count)> RedoAsync()
        {
            if (!CanRedo)
                return (false, "No hay operaciones para rehacer", 0);

            var operations = _redoStack.Pop();

            try
            {
                var (success, successCount, redoneOps, error) = await Task.Run(() =>
                {
                    var localRedone = new List<RenameOperation>();
                    int localSuccess = 0;

                    foreach (var op in operations)
                    {
                        if (File.Exists(op.OldPath))
                        {
                            if (File.Exists(op.NewPath))
                            {
                                var dir = Path.GetDirectoryName(op.NewPath) ?? "";
                                var name = Path.GetFileNameWithoutExtension(op.NewPath);
                                var ext = Path.GetExtension(op.NewPath);
                                var counter = 1;
                                var targetPath = op.NewPath;
                                while (File.Exists(targetPath))
                                {
                                    targetPath = Path.Combine(dir, $"{name}_{counter}{ext}");
                                    counter++;
                                }
                                File.Move(op.OldPath, targetPath);
                                localRedone.Add(new RenameOperation { OldPath = op.OldPath, NewPath = targetPath });
                            }
                            else
                            {
                                File.Move(op.OldPath, op.NewPath);
                                localRedone.Add(new RenameOperation { OldPath = op.OldPath, NewPath = op.NewPath });
                            }
                            localSuccess++;
                        }
                    }

                    return (true, localSuccess, localRedone, (string?)null);
                }).ConfigureAwait(false);

                if (success && successCount > 0)
                {
                    _undoStack.Push(redoneOps);
                }

                return (true, $"Rehecho: {successCount} archivo(s)", successCount);
            }
            catch (Exception ex)
            {
                // Put back on redo stack if failed
                _redoStack.Push(operations);
                return (false, $"Error al rehacer: {ex.Message}", 0);
            }
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
}
