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
            new(@"(?<=[.\s_-])(\d{2,4})(?=[.\s_-])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _extensionPattern =
            new(@"\.(mkv|mp4|avi|wmv|flv|webm|mpeg|mov|m4v)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _bracketsPattern =
            new(@"\[[^\]]*\]|\([^)]*\)", RegexOptions.Compiled);
        private static readonly Regex _titleAfterCodePattern =
            new(@"(?:[Ss]\d{1,2}[.\s_-]?[Ee]\d{1,3}(?:-[Ee]?\d{1,3})?|\d{1,2}x\d{2,3})[.\s_-]+([^.\d][^.]+?)(?:\s+\d+p|\s+[Ww]eb|\s+[Hh]d|\s+[Bb]lu|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        // Captures text BEFORE the S##E##/##x## code (e.g. "Title Name S01E05" → "Title Name")
        private static readonly Regex _titleBeforeCodePattern =
            new(@"^(.*?)[.\s_-]+(?:[Ss]\d{1,2}[.\s_-]?[Ee]\d{1,3}|\d{1,2}x\d{2,3}|Season[.\s_-]?\d{1,2}|(?:Temp|Temporada)[.\s_-]?\d{1,2})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _qualityTagPattern =
            new(@"\b(2160p|1080p|720p|480p|4k|uhd|fhd|hd|sd|hdr10plus|hdr10|hdr|dv|dovi|dolby|vision|WEB-DL|WEBDL|WEBRip|BluRay|Blu-Ray|BDRip|BRRip|DVDRip|HDTV|x264|x265|h264|h265|HEVC|AVC|AV1|VP9|XviD|AAC|AC3|EAC3|DTS-HD|DTS|FLAC|Atmos|TrueHD|DUAL|MULTI|Spa|Eng|Spanish|English|Latino|Castellano|Sub|Subs|Subbed|AMZN|NF|Netflix|DSNP|ATVP|HMAX|REPACK|PROPER|EXTENDED|UNCUT|IMAX)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _yearPattern =
            new(@"\b((?:19|20)\d{2})\b", RegexOptions.Compiled);

        /// <summary>
        /// Attempts to extract a 4-digit year (1900-2100) from a filename.
        /// </summary>
        public static bool TryExtractYear(string filename, out int year)
        {
            year = 0;
            var matches = _yearPattern.Matches(filename);
            foreach (Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, out int y) && y >= 1900 && y <= 2100)
                {
                    year = y;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Attempts to extract a movie title from a filename.
        /// </summary>
        public static string ExtractMovieTitle(string filename)
        {
            filename = _extensionPattern.Replace(filename, "");
            filename = _yearPattern.Replace(filename, "");

            string cleaned = filename.Replace("_", " ").Replace(".", " ");
            cleaned = _qualityTagPattern.Replace(cleaned, "");

            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\(\s*\)", "");
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\[\s*\]", "");
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s*-\s*", " ");
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s{2,}", " ").Trim().Trim('-').Trim();

            return cleaned;
        }

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
            var containsYear = TryExtractYear(filename, out var year);
            foreach (Match absoluteMatch in _absoluteNumberPattern.Matches(filename))
            {
                var absoluteEpisode = int.Parse(absoluteMatch.Groups[1].Value);
                if (containsYear && absoluteEpisode == year)
                {
                    continue;
                }

                season = 1;
                episode = absoluteEpisode;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Extracts the series title (text before SxxExx / NxNN / absolute episode number).
        /// </summary>
        public static string ExtractSeriesTitle(string filename)
        {
            string cleaned = _extensionPattern.Replace(filename, "");
            cleaned = _bracketsPattern.Replace(cleaned, " ");

            var beforeMatch = _titleBeforeCodePattern.Match(cleaned);
            if (beforeMatch.Success && beforeMatch.Groups[1].Value.Trim().Length > 1)
            {
                return CleanCandidateText(beforeMatch.Groups[1].Value);
            }

            // Handle "Show Name - 05" or "Show Name - 150 - Title"
            var dashMatch = Regex.Match(cleaned, @"^(.*?)\s*-\s*\d{2,4}(?:\b|$)");
            if (dashMatch.Success && dashMatch.Groups[1].Value.Trim().Length > 1)
            {
                return CleanCandidateText(dashMatch.Groups[1].Value);
            }

            return CleanCandidateText(ExtractPossibleTitle(filename));
        }

        /// <summary>
        /// Extracts the episode title (text after SxxExx / NxNN, stripped of quality tags).
        /// </summary>
        public static string ExtractEpisodeTitle(string filename)
        {
            string cleaned = _extensionPattern.Replace(filename, "");
            cleaned = _bracketsPattern.Replace(cleaned, " ");

            var afterMatch = _titleAfterCodePattern.Match(cleaned);
            if (afterMatch.Success && afterMatch.Groups[1].Value.Trim().Length > 1)
            {
                return CleanCandidateText(afterMatch.Groups[1].Value);
            }

            return string.Empty;
        }

        private static string CleanCandidateText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string cleaned = text.Replace("_", " ").Replace(".", " ");
            cleaned = _qualityTagPattern.Replace(cleaned, "");
            cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim().Trim('-', ' ').Trim();
            return cleaned;
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

            // Try to find title before S##E##/##x## code (e.g. "Title Name S01E05")
            var beforeMatch = _titleBeforeCodePattern.Match(filename);
            if (beforeMatch.Success && beforeMatch.Groups[1].Value.Length > 2)
            {
                return beforeMatch.Groups[1].Value.Replace("_", " ").Replace(".", " ").Trim();
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
