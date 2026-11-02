namespace organizadorCapitulos.Core.Entities
{
    public class ChapterInfo
    {
        public int Season { get; set; }
        public int Episode { get; set; }
        public string Title { get; set; } = string.Empty;
        public string EpisodeTitle { get; set; } = string.Empty;
        public int Year { get; set; }

        public bool IsValid()
        {
            return Episode > 0 || !string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(EpisodeTitle) || Year > 0;
        }

        public string GenerateFileName(string extension, string? template = null)
        {
            if (!string.IsNullOrWhiteSpace(template))
                return GenerateFileNameFromTemplate(template, extension);

            if (Season == 0 && Episode == 0)
            {
                var titlePartClean = SanitizeFileName(Title);
                if (Year > 0)
                {
                    return string.IsNullOrWhiteSpace(titlePartClean)
                        ? $"{Year}{extension}"
                        : $"{titlePartClean} ({Year}){extension}";
                }
                return string.IsNullOrWhiteSpace(titlePartClean)
                    ? extension
                    : $"{titlePartClean}{extension}";
            }

            string seasonStr = Season > 0 ? $"S{Season:00}" : string.Empty;
            string episodeStr = Episode > 0 ? $"E{Episode:00}" : string.Empty;
            var titlePart = SanitizeFileName(Title);

            if (!string.IsNullOrWhiteSpace(EpisodeTitle))
            {
                var episodeTitleSanitized = SanitizeFileName(EpisodeTitle);
                titlePart = string.IsNullOrWhiteSpace(titlePart)
                    ? episodeTitleSanitized
                    : $"{titlePart} - {episodeTitleSanitized}";
            }

            return string.IsNullOrWhiteSpace(titlePart)
                ? $"{seasonStr}{episodeStr}{extension}".Trim()
                : $"{seasonStr}{episodeStr} - {titlePart}{extension}".Trim();
        }

        private string GenerateFileNameFromTemplate(string template, string extension)
        {
            var result = template
                .Replace("{Season}", Season > 0 ? $"S{Season:00}" : string.Empty)
                .Replace("{Episode}", Episode > 0 ? $"E{Episode:00}" : string.Empty)
                .Replace("{Title}", SanitizeFileName(Title))
                .Replace("{EpisodeTitle}", SanitizeFileName(EpisodeTitle))
                .Replace("{Year}", Year > 0 ? Year.ToString() : string.Empty);

            // Clean up double spaces or trailing dashes from empty substitutions
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\s*-\s*-\s*", " - ");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\s{2,}", " ").Trim().Trim('-').Trim();

            return SanitizeFileName(result) + extension;
        }

        private static string SanitizeFileName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            return string.Concat(input.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c));
        }
    }
}
