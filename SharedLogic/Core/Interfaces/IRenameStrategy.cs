using organizadorCapitulos.Core.Entities;

namespace organizadorCapitulos.Core.Interfaces.Strategies
{
    public interface IRenameStrategy
    {
        string GetNewFileName(string originalFileName, ChapterInfo info);
        string GetNewFileName(string originalFileName, ChapterInfo info, string? template) =>
            string.IsNullOrWhiteSpace(template)
                ? GetNewFileName(originalFileName, info)
                : info.GenerateFileName(System.IO.Path.GetExtension(originalFileName), template);
        void UpdateAfterRename(ChapterInfo chapterInfo);
        string GetDescription();
    }
}
