using System.Threading.Tasks;
using organizadorCapitulos.Core.Interfaces.Services;

namespace organizadorCapitulos.Infrastructure.Services
{
    public class PythonAIService : IAIService
    {
        public async Task<string?> SuggestTitleAsync(string filePath)
        {
            await Task.CompletedTask;
            return null; // Stub: no AI suggestion in copied logic
        }

        public bool IsAvailable()
        {
            return false;
        }

        public async Task<organizadorCapitulos.Core.Entities.ChapterInfo?> AnalyzeFilenameAsync(string filename)
        {
            await Task.CompletedTask;
            return null;
        }
    }
}
