namespace organizadorCapitulos.Core.Entities
{
    public class ChapterInfo
    {
        public int Season { get; set; }
        public int Episode { get; set; }
        public string Title { get; set; } = string.Empty;
        public string EpisodeTitle { get; set; } = string.Empty;

        // Backwards-compatible aliases expected by MAUI code
        public int Chapter
        {
            get => Episode;
            set => Episode = value;
        }

        public bool IsValid()
        {
            return Episode > 0 || !string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(EpisodeTitle);
        }

        public string GenerateFileName(string extension)
        {
            string seasonStr = Season > 0 ? $"S{Season:00}" : string.Empty;
            string episodeStr = Episode > 0 ? $"E{Episode:00}" : string.Empty;
            var titlePart = Title;

            if (!string.IsNullOrWhiteSpace(EpisodeTitle))
            {
                titlePart = string.IsNullOrWhiteSpace(titlePart)
                    ? EpisodeTitle
                    : $"{titlePart} - {EpisodeTitle}";
            }

            return string.IsNullOrWhiteSpace(titlePart)
                ? $"{seasonStr}{episodeStr}{extension}".Trim()
                : $"{seasonStr}{episodeStr} - {titlePart}{extension}".Trim();
        }
    }
}
