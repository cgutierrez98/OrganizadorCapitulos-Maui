using System.IO;
using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Interfaces.Strategies;

namespace organizadorCapitulos.Application.Strategies
{
    public class MovieStrategy : IRenameStrategy
    {
        public string GetNewFileName(string originalFileName, ChapterInfo info)
        {
            string extension = Path.GetExtension(originalFileName);
            return info.GenerateFileName(extension);
        }

        public void UpdateAfterRename(ChapterInfo chapterInfo)
        {
            // Movie strategy doesn't change next episode or numbering sequentially
        }

        public string GetDescription()
        {
            return "Películas - Renombra usando el título de la película y el año opcional.";
        }
    }
}
