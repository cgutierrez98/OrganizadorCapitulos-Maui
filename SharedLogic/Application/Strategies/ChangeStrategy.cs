using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Interfaces.Strategies;

namespace organizadorCapitulos.Application.Strategies
{
    public class ChangeStrategy : IRenameStrategy
    {
        public string GetNewFileName(string originalFileName, ChapterInfo info)
        {
            string extension = System.IO.Path.GetExtension(originalFileName);
            return info.GenerateFileName(extension);
        }

        public void UpdateAfterRename(ChapterInfo chapterInfo)
        {
            // No increment logic here
        }

        public string GetDescription()
        {
            return "Modo Estricto - Usa el número de capítulo detectado o editado manualmente.";
        }
    }
}
