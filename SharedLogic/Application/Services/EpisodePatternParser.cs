using System.Text.RegularExpressions;

namespace organizadorCapitulos.Application.Services
{
    /// <summary>
    /// Parses season/episode numbers and episode titles from video filenames.
    /// </summary>
    public static class EpisodePatternParser
    {
        // Pre-compiled regexes for performance
        private static readonly Regex _sXeYPattern =
            new(@"[Ss](\d{1,2})[.\s_-]?[Ee](\d{1,3})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _1x01Pattern =
            new(@"(\d{1,2})x(\d{2,3})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _seasonEpisodeWordPattern =
            new(@"Season[.\s_-]?(\d{1,2})[.\s_-]+Episode[.\s_-]?(\d{1,3})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _spanishPattern =
            new(@"(?:Temp|Temporada)[.\s_-]?(\d{1,2})[.\s_-]+(?:Cap|Capitulo)[.\s_-]?(\d{1,3})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _absoluteNumberPattern =
            new(@"[.\s_-]+(\d{2,4})[.\s_-]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _extensionPattern =
            new(@"\.(mkv|mp4|avi|wmv|flv|webm|mpeg|mov)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _titleAfterCodePattern =
            new(@"(?:[Ss]\d+[Ee]\d+|\d+x\d+)[.\s_-]+([^.\d][^.]+?)(?:\s+\d+p|\s+[Ww]eb|\s+[Hh]d|\s+[Bb]lu|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _qualityTagPattern =
            new(@"\b(1080p|720p|480p|WEB-DL|WEBDL|WEBRip|BluRay|HDTV|x264|x265|HEVC|AAC|AC3|DUAL|MULTI|Spa|Eng|Sub)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Attempts to extract season and episode numbers from a filename using multiple patterns.
        /// Returns true and sets <paramref name="season"/>/<paramref name="episode"/> on success.
        /// </summary>
        public static bool TryExtractSeasonEpisode(string filename, out int season, out int episode)
        {
            season = 0;
            episode = 0;

            // Pattern 1: S01E01, S1E1
            var match = _sXeYPattern.Match(filename);
            if (match.Success)
            {
                season = int.Parse(match.Groups[1].Value);
                episode = int.Parse(match.Groups[2].Value);
                return true;
            }

            // Pattern 2: 1x01, 01x01
            match = _1x01Pattern.Match(filename);
            if (match.Success)
            {
                season = int.Parse(match.Groups[1].Value);
                episode = int.Parse(match.Groups[2].Value);
                return true;
            }

            // Pattern 3: Season 1 Episode 1 (English)
            match = _seasonEpisodeWordPattern.Match(filename);
            if (match.Success)
            {
                season = int.Parse(match.Groups[1].Value);
                episode = int.Parse(match.Groups[2].Value);
                return true;
            }

            // Pattern 4: Temporada X Capitulo Y (Spanish)
            match = _spanishPattern.Match(filename);
            if (match.Success)
            {
                season = int.Parse(match.Groups[1].Value);
                episode = int.Parse(match.Groups[2].Value);
                return true;
            }

            // Pattern 5: Absolute episode number fallback (e.g., "Series - 150 -" for anime)
            match = _absoluteNumberPattern.Match(filename);
            if (match.Success)
            {
                season = 1;
                episode = int.Parse(match.Groups[1].Value);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to extract a human-readable episode title from a filename.
        /// Returns an empty string if no title can be detected.
        /// </summary>
        public static string ExtractPossibleTitle(string filename)
        {
            // Remove video extension
            filename = _extensionPattern.Replace(filename, "");

            // Try to find title after S##E##/##x## code
            var match = _titleAfterCodePattern.Match(filename);
            if (match.Success && match.Groups[1].Value.Length > 2)
            {
                return match.Groups[1].Value.Replace("_", " ").Replace(".", " ").Trim();
            }

            // Clean underscores and dots, then strip quality tags
            string cleaned = filename.Replace("_", " ").Replace(".", " ");
            cleaned = _qualityTagPattern.Replace(cleaned, "");

            // Return the last meaningful words as candidate title
            var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2)
                return string.Join(" ", words.TakeLast(Math.Min(words.Length, 4))).Trim();

            return string.Empty;
        }
    }
}
