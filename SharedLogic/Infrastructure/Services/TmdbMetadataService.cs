using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Interfaces.Services;

namespace organizadorCapitulos.Infrastructure.Services
{
    public partial class TmdbMetadataService : IMetadataService
    {
        private sealed record CachedEpisode(
            int Season,
            int Episode,
            string Title,
            string NormalizedTitle,
            HashSet<string> Tokens);

        [GeneratedRegex(@"\s{2,}", RegexOptions.NonBacktracking)]
        private static partial Regex MultiSpaceRegex();

        private string? _apiKey;
        private string _language = "es-ES";
        private readonly HttpClient _http;

        // Thread-safe session-level caches
        private readonly ConcurrentDictionary<(int seriesId, int season, string language), Dictionary<int, string>> _seasonCache = new();
        private readonly ConcurrentDictionary<(int seriesId, string language), List<CachedEpisode>> _allEpisodesCache = new();

        public TmdbMetadataService(HttpClient httpClient)
        {
            _http = httpClient;
            _http.BaseAddress = new Uri("https://api.themoviedb.org/3/");
        }

        public void Configure(string apiKey)
        {
            _apiKey = apiKey?.Trim();
            if (IsV3Key(_apiKey))
            {
                // Clear authorization header for v3 API Key to avoid TMDB 401 response
                _http.DefaultRequestHeaders.Authorization = null;
            }
            else if (!string.IsNullOrEmpty(_apiKey))
            {
                // Use Authorization header instead of query param for v4 token to avoid key exposure in logs/proxies
                _http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            }
            else
            {
                _http.DefaultRequestHeaders.Authorization = null;
            }
        }

        public void ConfigureLanguage(string language)
        {
            var normalized = string.IsNullOrWhiteSpace(language) ? "es-ES" : language.Trim();
            if (!string.Equals(_language, normalized, StringComparison.OrdinalIgnoreCase))
            {
                _language = normalized;
                _seasonCache.Clear();
                _allEpisodesCache.Clear();
            }
        }

        public bool IsConfigured() => !string.IsNullOrEmpty(_apiKey);

        private static bool IsV3Key(string? key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            // TMDB v4 Access Token is a JWT and always starts with "eyJ" (since it encodes JSON).
            // TMDB v3 API Key is a 32-character hex string and will not start with "eyJ" (neither 'y' nor 'J' are hex).
            return !key.StartsWith("eyJ", StringComparison.Ordinal);
        }

        /// <summary>Builds the request URL. Appends api_key for v3 API Keys, otherwise uses header auth.</summary>
        private string BuildUrl(string relativePath)
        {
            if (IsV3Key(_apiKey))
            {
                char separator = relativePath.Contains('?') ? '&' : '?';
                return $"{relativePath}{separator}api_key={_apiKey}";
            }
            return relativePath;
        }

        private async Task<HttpResponseMessage> SendWithRetryAsync(string url, CancellationToken ct)
        {
            var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var delay = resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(1);
                if (delay < TimeSpan.FromMilliseconds(100)) delay = TimeSpan.FromMilliseconds(100);
                if (delay > TimeSpan.FromSeconds(5)) delay = TimeSpan.FromSeconds(5);
                resp.Dispose();
                await Task.Delay(delay, ct).ConfigureAwait(false);
                resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            }
            return resp;
        }

        public async Task<List<SeriesSearchResult>> SearchSeriesAsync(string query, CancellationToken ct = default)
        {
            if (!IsConfigured()) return new List<SeriesSearchResult>();
            if (string.IsNullOrWhiteSpace(query)) return new List<SeriesSearchResult>();

            string url = BuildUrl($"search/tv?query={Uri.EscapeDataString(query)}&language={Uri.EscapeDataString(_language)}");
            using var resp = await SendWithRetryAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return new List<SeriesSearchResult>();

            using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var results = new List<SeriesSearchResult>();
            if (doc.RootElement.TryGetProperty("results", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    results.Add(new SeriesSearchResult
                    {
                        Id = item.GetProperty("id").GetInt32(),
                        Name = item.GetProperty("name").GetString() ?? string.Empty,
                        Overview = item.TryGetProperty("overview", out var ov) ? ov.GetString() ?? string.Empty : string.Empty,
                        FirstAirDate = item.TryGetProperty("first_air_date", out var fd) ? fd.GetString() : null,
                        PosterPath = item.TryGetProperty("poster_path", out var pp) ? pp.GetString() : null
                    });
                }
            }

            return results;
        }

        public async Task<List<MovieSearchResult>> SearchMoviesAsync(string query, CancellationToken ct = default)
        {
            if (!IsConfigured()) return new List<MovieSearchResult>();
            if (string.IsNullOrWhiteSpace(query)) return new List<MovieSearchResult>();

            string url = BuildUrl($"search/movie?query={Uri.EscapeDataString(query)}&language={Uri.EscapeDataString(_language)}");
            using var resp = await SendWithRetryAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return new List<MovieSearchResult>();

            using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var results = new List<MovieSearchResult>();
            if (doc.RootElement.TryGetProperty("results", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    results.Add(new MovieSearchResult
                    {
                        Id = item.GetProperty("id").GetInt32(),
                        Title = item.GetProperty("title").GetString() ?? string.Empty,
                        Overview = item.TryGetProperty("overview", out var ov) ? ov.GetString() ?? string.Empty : string.Empty,
                        ReleaseDate = item.TryGetProperty("release_date", out var rd) ? rd.GetString() : null,
                        PosterPath = item.TryGetProperty("poster_path", out var pp) ? pp.GetString() : null
                    });
                }
            }

            return results;
        }

        public async Task<string?> GetEpisodeTitleAsync(int seriesId, int season, int episode, CancellationToken ct = default)
        {
            if (!IsConfigured()) return null;
            var title = await FetchEpisodeNameAsync(seriesId, season, episode, _language, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(title) && !string.Equals(_language, "en-US", StringComparison.OrdinalIgnoreCase))
            {
                title = await FetchEpisodeNameAsync(seriesId, season, episode, "en-US", ct).ConfigureAwait(false);
            }
            return title;
        }

        private async Task<string?> FetchEpisodeNameAsync(int seriesId, int season, int episode, string language, CancellationToken ct)
        {
            var cacheKey = (seriesId, season, language);
            if (_seasonCache.TryGetValue(cacheKey, out var cachedSeason))
            {
                return cachedSeason.TryGetValue(episode, out var cachedTitle) && !string.IsNullOrWhiteSpace(cachedTitle)
                    ? cachedTitle
                    : null;
            }

            string url = BuildUrl($"tv/{seriesId}/season/{season}?language={Uri.EscapeDataString(language)}");
            using var resp = await SendWithRetryAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                if (resp.StatusCode == HttpStatusCode.NotFound)
                {
                    _seasonCache[cacheKey] = new Dictionary<int, string>();
                }
                return null;
            }

            using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            if (doc.RootElement.TryGetProperty("episodes", out var eps) && eps.ValueKind == JsonValueKind.Array)
            {
                var seasonEpisodes = new Dictionary<int, string>();
                foreach (var ep in eps.EnumerateArray())
                {
                    var epNumber = ep.TryGetProperty("episode_number", out var enn) ? enn.GetInt32() : 0;
                    var epName = ep.TryGetProperty("name", out var en) ? en.GetString() ?? string.Empty : string.Empty;
                    if (epNumber > 0)
                    {
                        seasonEpisodes[epNumber] = epName;
                    }
                }

                _seasonCache[cacheKey] = seasonEpisodes;
                return seasonEpisodes.TryGetValue(episode, out var title) && !string.IsNullOrWhiteSpace(title)
                    ? title
                    : null;
            }

            // Fallback compatibility for direct episode JSON responses
            return doc.RootElement.TryGetProperty("name", out var name) ? name.GetString() : null;
        }

        public async Task<(int season, int episode, string title)?> FindEpisodeByTitleAsync(int seriesId, string title, CancellationToken ct = default)
        {
            if (!IsConfigured()) return null;
            if (string.IsNullOrWhiteSpace(title)) return null;

            var allEpisodes = await GetOrFetchAllEpisodesAsync(seriesId, ct).ConfigureAwait(false);
            string normalizedQuery = NormalizeForMatch(title);
            if (string.IsNullOrWhiteSpace(normalizedQuery)) return null;

            // 1. Exact or bidirectional substring match on pre-normalized text (ignores accents & punctuation)
            foreach (var ep in allEpisodes)
            {
                if (string.IsNullOrWhiteSpace(ep.NormalizedTitle)) continue;
                if (ep.NormalizedTitle.Contains(normalizedQuery, StringComparison.Ordinal) ||
                    normalizedQuery.Contains(ep.NormalizedTitle, StringComparison.Ordinal))
                {
                    return (ep.Season, ep.Episode, ep.Title);
                }
            }

            // 2. Token overlap fuzzy fallback (requires at least 60% overlap) using pre-computed token sets
            var queryTokens = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            if (queryTokens.Count == 0) return null;

            (int season, int episode, string title)? best = null;
            double bestScore = 0.59;

            foreach (var ep in allEpisodes)
            {
                if (ep.Tokens.Count == 0) continue;

                int intersection = 0;
                foreach (var token in ep.Tokens)
                {
                    if (queryTokens.Contains(token)) intersection++;
                }

                double score = (double)intersection / Math.Max(ep.Tokens.Count, 1);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = (ep.Season, ep.Episode, ep.Title);
                }
            }

            return best;
        }

        public async Task<ChapterInfo?> GetEpisodeMetadataAsync(int seriesId, int season, int episode, CancellationToken ct = default)
        {
            if (!IsConfigured()) return null;
            var title = await GetEpisodeTitleAsync(seriesId, season, episode, ct).ConfigureAwait(false);
            if (title == null) return null;
            return new ChapterInfo
            {
                Season = season,
                Episode = episode,
                EpisodeTitle = title
            };
        }

        /// <summary>
        /// Returns all episodes for a series from the session cache, fetching seasons in parallel (max 4 concurrent).
        /// </summary>
        private async Task<List<CachedEpisode>> GetOrFetchAllEpisodesAsync(int seriesId, CancellationToken ct)
        {
            var allKey = (seriesId, _language);
            if (_allEpisodesCache.TryGetValue(allKey, out var cached))
                return cached;

            var allEpisodes = new List<CachedEpisode>();

            string seriesUrl = BuildUrl($"tv/{seriesId}?language={Uri.EscapeDataString(_language)}");
            using var sresp = await SendWithRetryAsync(seriesUrl, ct).ConfigureAwait(false);
            if (!sresp.IsSuccessStatusCode)
            {
                _allEpisodesCache[allKey] = allEpisodes;
                return allEpisodes;
            }

            using var sstream = await sresp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var sdoc = await JsonDocument.ParseAsync(sstream, cancellationToken: ct).ConfigureAwait(false);
            int seasonsCount = sdoc.RootElement.TryGetProperty("number_of_seasons", out var ns) && ns.ValueKind == JsonValueKind.Number
                ? ns.GetInt32()
                : 1;

            using var throttler = new SemaphoreSlim(4);
            var seasonTasks = Enumerable.Range(1, seasonsCount).Select(async seasonNum =>
            {
                await throttler.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    ct.ThrowIfCancellationRequested();
                    var seasonEps = new List<CachedEpisode>();
                    string seasonUrl = BuildUrl($"tv/{seriesId}/season/{seasonNum}?language={Uri.EscapeDataString(_language)}");
                    using var resp = await SendWithRetryAsync(seasonUrl, ct).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode) return seasonEps;

                    using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
                    if (doc.RootElement.TryGetProperty("episodes", out var eps) && eps.ValueKind == JsonValueKind.Array)
                    {
                        var seasonMap = new Dictionary<int, string>();
                        foreach (var ep in eps.EnumerateArray())
                        {
                            var epName = ep.TryGetProperty("name", out var en) ? en.GetString() ?? string.Empty : string.Empty;
                            var epNumber = ep.TryGetProperty("episode_number", out var enn) ? enn.GetInt32() : 0;
                            if (epNumber > 0)
                            {
                                seasonMap[epNumber] = epName;
                            }

                            var normalized = NormalizeForMatch(epName);
                            var tokens = string.IsNullOrWhiteSpace(normalized)
                                ? new HashSet<string>()
                                : normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

                            seasonEps.Add(new CachedEpisode(seasonNum, epNumber, epName, normalized, tokens));
                        }
                        _seasonCache[(seriesId, seasonNum, _language)] = seasonMap;
                    }
                    return seasonEps;
                }
                finally
                {
                    throttler.Release();
                }
            }).ToList();

            var results = await Task.WhenAll(seasonTasks).ConfigureAwait(false);
            foreach (var list in results)
            {
                allEpisodes.AddRange(list);
            }

            _allEpisodesCache[allKey] = allEpisodes;
            return allEpisodes;
        }

        private static string NormalizeForMatch(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            string decomposed = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
                }
            }
            return MultiSpaceRegex().Replace(sb.ToString(), " ").Trim();
        }
    }
}
