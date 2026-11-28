using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Interfaces.Services;

namespace organizadorCapitulos.Infrastructure.Services
{
    public sealed partial class NativeHeuristicAIService : IAIService
    {
        private const int MaxInputLength = 800;

        private static readonly FrozenSet<string> QualityTags = new[]
        {
            // Resolution
            "2160p", "1080p", "720p", "480p", "4k", "uhd", "fhd", "hd", "sd",
            // HDR
            "hdr", "hdr10", "hdr10plus", "dv", "dovi", "dolby", "vision",
            // Source
            "web-dl", "webdl", "webrip", "web", "hdtv", "hdtvrip", "pdtv", "dsr", "dvb",
            "bluray", "bdrip", "brrip", "blu-ray", "dvdrip", "dvdr", "dvd",
            "cam", "ts", "tc", "hdcam", "hdts",
            // Codec
            "x264", "x265", "hevc", "h264", "h265", "avc", "xvid", "divx", "vp9", "av1",
            // Audio
            "aac", "ac3", "dts", "dts-hd", "flac", "mp3", "eac3", "atmos", "truehd",
            "stereo", "dual", "multi", "surround",
            // Language
            "spa", "eng", "spanish", "english", "latino", "castellano",
            "sub", "subs", "subbed", "engsub", "spasub",
            // Container
            "mkv", "mp4", "avi", "wmv", "m4v",
            // Streaming
            "amzn", "nf", "netflix", "dsnp", "atvp", "hmax", "hulu", "pcok", "max",
            "amazon", "disney", "hbo", "appletv",
            // Misc
            "repack", "proper", "real", "rerip", "internal", "limited", "unrated",
            "extended", "complete", "final", "uncut", "dc", "imax"
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        private static readonly char[] TrimChars = { '-', '_', '.' };
        private static readonly char[] TokenSplitChars = { '.', ' ', '_', '-' };

        [GeneratedRegex(@"\.(mkv|mp4|avi|wmv|mov|flv|webm|m4v)$", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
        private static partial Regex VideoExtensionRegex();

        [GeneratedRegex(@"^m?-?\d+p", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
        private static partial Regex ResolutionTagRegex();

        [GeneratedRegex(@"^\d\.\d$", RegexOptions.NonBacktracking)]
        private static partial Regex AudioChannelRegex();

        [GeneratedRegex(@"^[a-z]{2,4}-[a-z]{2,6}(sub)?$", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
        private static partial Regex CombinedLangTagRegex();

        [GeneratedRegex(@"\[.*?\]|\(.*?\)", RegexOptions.NonBacktracking)]
        private static partial Regex BracketsRegex();

        [GeneratedRegex(@"\s+(?:19|20)\d{2}\s*$", RegexOptions.NonBacktracking)]
        private static partial Regex TrailingYearRegex();

        [GeneratedRegex(@"\s+")]
        private static partial Regex MultiSpaceRegex();

        [GeneratedRegex(@"(?<series>.+?)[.\s_-]+[Ss](?<season>\d{1,2})[.\s_-]?[Ee](?<episode>\d{1,3})(?:[.\s_-]+(?<title>.+))?", RegexOptions.IgnoreCase)]
        private static partial Regex SxExPattern();

        [GeneratedRegex(@"(?<series>.+?)[_.\s-]+(?<season>\d{1,2})x(?<episode>\d{2,3})(?:[_.\s-]+(?<title>.+))?", RegexOptions.IgnoreCase)]
        private static partial Regex NxNnPattern();

        [GeneratedRegex(@"(?<series>.+?)[.\s_-]+Season[.\s_-]?(?<season>\d{1,2})[.\s_-]+Episode[.\s_-]?(?<episode>\d{1,3})(?:[.\s_-]+(?<title>.+))?", RegexOptions.IgnoreCase)]
        private static partial Regex SeasonEpisodePattern();

        [GeneratedRegex(@"(?<series>.+?)[.\s_-]+(?:Temp|Temporada)[.\s_-]?(?<season>\d{1,2})[.\s_-]+(?:Cap|Capitulo)[.\s_-]?(?<episode>\d{1,3})(?:[.\s_-]+(?<title>.+))?", RegexOptions.IgnoreCase)]
        private static partial Regex SpanishPattern();

        [GeneratedRegex(@"(?<series>.+?)[\s-]+-(?:\s)?(?<episode>\d{1,3})(?:[\s-]+-(?:\s)?(?<title>.+))?", RegexOptions.IgnoreCase)]
        private static partial Regex DashEpisodePattern();

        [GeneratedRegex(@"(?<series>.+?)[.\s_-]+(?<episode>\d{2,4})(?:[.\s_-]+(?<title>.+))?$")]
        private static partial Regex AnimeFallbackPattern();

        [GeneratedRegex(@"^[Ss]?\d{1,2}[Ee]\d{1,3}$", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
        private static partial Regex SxExTokenRegex();

        [GeneratedRegex(@"^\d{1,3}x\d{2,3}$", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
        private static partial Regex NxNnTokenRegex();

        [GeneratedRegex(@"^(season|temp|temporada|capitulo|ep)$", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
        private static partial Regex MarkerTokenRegex();

        [GeneratedRegex(@"^\d{1,3}$", RegexOptions.NonBacktracking)]
        private static partial Regex NumericTokenRegex();

        public bool IsAvailable() => true;

        public Task<string?> SuggestTitleAsync(string filePath, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return Task.FromResult<string?>(null);
            }

            var normalized = CleanSeriesName(filePath);
            return Task.FromResult<string?>(normalized);
        }

        public Task<ChapterInfo?> AnalyzeFilenameAsync(string filename, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(filename))
            {
                return Task.FromResult<ChapterInfo?>(null);
            }

            var input = filename.Length > MaxInputLength ? filename[..MaxInputLength] : filename;
            var name = VideoExtensionRegex().Replace(input, string.Empty);

            ChapterInfo? parsed = TryMatchPattern(SxExPattern(), name)
                ?? TryMatchPattern(NxNnPattern(), name)
                ?? TryMatchPattern(SeasonEpisodePattern(), name)
                ?? TryMatchPattern(SpanishPattern(), name)
                ?? TryMatchPattern(DashEpisodePattern(), name);

            if (parsed != null)
            {
                return Task.FromResult<ChapterInfo?>(parsed);
            }

            // Anime fallback (absolute numbering)
            var animeMatch = AnimeFallbackPattern().Match(name);
            if (animeMatch.Success)
            {
                var series = CleanSeriesName(animeMatch.Groups["series"].Value);
                if (series.Length > 2 && int.TryParse(animeMatch.Groups["episode"].Value, out int ep) && ep > 0)
                {
                    var epTitle = CleanEpisodeTitle(animeMatch.Groups["title"].Value);
                    return Task.FromResult<ChapterInfo?>(new ChapterInfo
                    {
                        Title = series,
                        Season = 1,
                        Episode = ep,
                        EpisodeTitle = epTitle
                    });
                }
            }

            // Heuristic fallback: accumulate tokens until a quality tag or SxxExx marker
            var tokens = name.Split(TokenSplitChars, StringSplitOptions.RemoveEmptyEntries);
            var seriesTokens = new List<string>(tokens.Length);
            foreach (var t in tokens)
            {
                if (IsQualityWord(t) || SxExTokenRegex().IsMatch(t) || NxNnTokenRegex().IsMatch(t) || MarkerTokenRegex().IsMatch(t))
                {
                    break;
                }
                seriesTokens.Add(t);
            }

            var info = new ChapterInfo();
            if (seriesTokens.Count > 0)
            {
                var candidate = CleanSeriesName(string.Join(' ', seriesTokens));
                if (candidate.Length > 2)
                {
                    info.Title = candidate;
                    var last = tokens[^1];
                    if (NumericTokenRegex().IsMatch(last) && int.TryParse(last, out int ep))
                    {
                        info.Episode = ep;
                        info.Season = 1;
                    }
                }
            }

            return Task.FromResult<ChapterInfo?>(info);
        }

        private static ChapterInfo? TryMatchPattern(Regex regex, string name)
        {
            var match = regex.Match(name);
            if (!match.Success) return null;

            var series = CleanSeriesName(match.Groups["series"].Value);
            var season = match.Groups["season"].Success && int.TryParse(match.Groups["season"].Value, out int s) ? s : 0;
            var episode = int.TryParse(match.Groups["episode"].Value, out int e) ? e : 0;

            if (string.IsNullOrEmpty(series) || episode <= 0)
            {
                return null;
            }

            var rawTitle = match.Groups["title"].Success ? match.Groups["title"].Value : string.Empty;
            return new ChapterInfo
            {
                Title = series,
                Season = season,
                Episode = episode,
                EpisodeTitle = CleanEpisodeTitle(rawTitle)
            };
        }

        private static bool IsQualityWord(string word)
        {
            var w = word.Trim(TrimChars);
            if (w.Length == 0) return false;
            if (QualityTags.Contains(w)) return true;
            if (ResolutionTagRegex().IsMatch(w)) return true;
            if (AudioChannelRegex().IsMatch(w)) return true;
            if (CombinedLangTagRegex().IsMatch(w)) return true;
            return false;
        }

        private static string CleanSeriesName(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var cleaned = BracketsRegex().Replace(text, string.Empty)
                .Replace('_', ' ')
                .Replace('.', ' ');

            var words = MultiSpaceRegex().Split(cleaned.Trim());
            var cleanWords = new List<string>(words.Length);
            foreach (var word in words)
            {
                var trimmed = word.Trim(TrimChars);
                if (trimmed.Length > 0 && !IsQualityWord(trimmed))
                {
                    cleanWords.Add(trimmed);
                }
            }

            var result = string.Join(' ', cleanWords);
            result = TrailingYearRegex().Replace(result, string.Empty);
            return result.Trim();
        }

        private static string CleanEpisodeTitle(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var cleaned = BracketsRegex().Replace(text, string.Empty)
                .Replace('_', ' ')
                .Replace('.', ' ');

            var words = MultiSpaceRegex().Split(cleaned.Trim());
            var cleanWords = new List<string>(words.Length);
            foreach (var word in words)
            {
                var trimmed = word.Trim(TrimChars);
                if (trimmed.Length == 0) continue;
                if (IsQualityWord(trimmed)) break;
                cleanWords.Add(trimmed);
            }

            return string.Join(' ', cleanWords).Trim();
        }
    }
}
