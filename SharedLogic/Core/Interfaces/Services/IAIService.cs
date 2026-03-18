using System.Threading.Tasks;

namespace organizadorCapitulos.Core.Interfaces.Services
{
    public interface IAIService
    {
        Task<string?> SuggestTitleAsync(string filePath);
        bool IsAvailable();
        Task<organizadorCapitulos.Core.Entities.ChapterInfo?> AnalyzeFilenameAsync(string filename);
    }
}
