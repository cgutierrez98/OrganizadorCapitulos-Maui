using System;
using System.Buffers;
using System.IO;
using System.Text.RegularExpressions;

namespace organizadorCapitulos.Core.Entities
{
    public partial class ChapterInfo
    {
        private static readonly SearchValues<char> InvalidFileNameChars = SearchValues.Create(Path.GetInvalidFileNameChars());

        [GeneratedRegex(@"\(\s*\)", RegexOptions.NonBacktracking)]
        private static partial Regex EmptyParensRegex();

        [GeneratedRegex(@"\[\s*\]", RegexOptions.NonBacktracking)]
        private static partial Regex EmptyBracketsRegex();

        [GeneratedRegex(@"\s*-\s*-\s*", RegexOptions.NonBacktracking)]
        private static partial Regex DoubleDashRegex();

        [GeneratedRegex(@"\s{2,}", RegexOptions.NonBacktracking)]
        private static partial Regex MultiSpaceRegex();

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

            // Clean up empty parentheses, double spaces, or trailing dashes from empty substitutions
            result = EmptyParensRegex().Replace(result, string.Empty);
            result = EmptyBracketsRegex().Replace(result, string.Empty);
            result = DoubleDashRegex().Replace(result, " - ");
            result = MultiSpaceRegex().Replace(result, " ").Trim().Trim('-').Trim();

            return SanitizeFileName(result) + extension;
        }

        public static string SanitizeFileName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            if (input.AsSpan().IndexOfAny(InvalidFileNameChars) < 0) return input;

            return string.Create(input.Length, input, static (span, src) =>
            {
                var invalid = InvalidFileNameChars;
                for (int i = 0; i < src.Length; i++)
                {
                    char c = src[i];
                    span[i] = invalid.Contains(c) ? '_' : c;
                }
            });
        }
    }
}
