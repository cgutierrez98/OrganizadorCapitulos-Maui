using organizadorCapitulos.Core.Entities;

namespace organizadorCapitulos.Core.Interfaces.Strategies
{
    public interface IRenameStrategy
    {
        string GetNewFileName(string originalFileName, ChapterInfo info);
        void UpdateAfterRename(ChapterInfo chapterInfo);
        string GetDescription();
    }
}
