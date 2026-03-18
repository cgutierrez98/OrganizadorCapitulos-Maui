namespace organizadorCapitulos.Core.Entities
{
    public class ChapterInfo
    {
        public int Season { get; set; }
        public int Episode { get; set; }
        public string Title { get; set; } = string.Empty;

        // Backwards-compatible aliases expected by MAUI code
        public int Chapter
        {
            get => Episode;
            set => Episode = value;
        }

        public string EpisodeTitle
        {
            get => Title;
            set => Title = value;
        }

        public bool IsValid()
        {
            return Episode > 0 || !string.IsNullOrWhiteSpace(Title);
        }

        public string GenerateFileName(string extension)
        {
            string seasonStr = Season > 0 ? $"S{Season:00}" : string.Empty;
            string episodeStr = Episode > 0 ? $"E{Episode:00}" : string.Empty;
            return $"{seasonStr}{episodeStr} - {Title}{extension}".Trim();
        }
    }
}
