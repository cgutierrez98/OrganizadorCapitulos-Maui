using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Interfaces.Strategies;

namespace organizadorCapitulos.Application.Strategies
{
    public class MaintainStrategy : IRenameStrategy
    {
        public string GetNewFileName(string originalFileName, ChapterInfo info)
        {
            // Keep original file name by default
            return originalFileName;
        }

        public void UpdateAfterRename(ChapterInfo chapterInfo)
        {
            // Maintain doesn't change numbering
        }

        public string GetDescription()
        {
            return "Mantener - Conserva el nombre del archivo.";
        }
    }
}
