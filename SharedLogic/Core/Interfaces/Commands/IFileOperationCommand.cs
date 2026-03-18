using System.Threading.Tasks;

namespace organizadorCapitulos.Core.Interfaces.Commands
{
    public interface IFileOperationCommand
    {
        Task ExecuteAsync();
        Task UndoAsync();
        string Description { get; }
        bool CanUndo { get; }
    }
}
