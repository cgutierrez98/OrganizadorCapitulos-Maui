using System.Threading;
using System.Threading.Tasks;

namespace organizadorCapitulos.Core.Interfaces.Services
{
    public interface IAIService
    {
        Task<string?> SuggestTitleAsync(string filePath, CancellationToken ct = default);
        bool IsAvailable();
        Task<organizadorCapitulos.Core.Entities.ChapterInfo?> AnalyzeFilenameAsync(string filename, CancellationToken ct = default);
    }
}
