using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Interfaces.Strategies;

namespace organizadorCapitulos.Application.Strategies
{
    public class MaintainStrategy : IRenameStrategy
    {
        public string GetNewFileName(string originalFileName, ChapterInfo info)
        {
            string extension = System.IO.Path.GetExtension(originalFileName);
            return info.GenerateFileName(extension);
        }

        public void UpdateAfterRename(ChapterInfo chapterInfo)
        {
            // Maintain doesn't change numbering
        }

        public string GetDescription()
        {
            return "Incrementar - Conserva serie y temporada; numera episodios en orden secuencial.";
        }
    }
}
