using System.Collections.Generic;
using System.Threading.Tasks;
using organizadorCapitulos.Core.Interfaces.Commands;

namespace organizadorCapitulos.Application.Commands
{
    public class CommandManager
    {
        private readonly Stack<IFileOperationCommand> _undoStack = new Stack<IFileOperationCommand>();
        private readonly Stack<IFileOperationCommand> _redoStack = new Stack<IFileOperationCommand>();

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public async Task ExecuteCommandAsync(IFileOperationCommand command)
        {
            await command.ExecuteAsync();
            _undoStack.Push(command);
            _redoStack.Clear();
        }

        public async Task UndoAsync()
        {
            if (!CanUndo) return;

            var command = _undoStack.Pop();
            await command.UndoAsync();
            _redoStack.Push(command);
        }

        public async Task RedoAsync()
        {
            if (!CanRedo) return;

            var command = _redoStack.Pop();
            await command.ExecuteAsync();
            _undoStack.Push(command);
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        public string GetUndoDescription() => CanUndo ? _undoStack.Peek().Description : string.Empty;
        public string GetRedoDescription() => CanRedo ? _redoStack.Peek().Description : string.Empty;
    }
}
