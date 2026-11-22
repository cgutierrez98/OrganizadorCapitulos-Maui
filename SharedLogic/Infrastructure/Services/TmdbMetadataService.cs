using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    public class TmdbMetadataService : IMetadataService
    {
        private string? _apiKey;
        private string _language = "es-ES";
        private readonly HttpClient _http;

        // Session-level cache: seriesId -> list of (season, episode, title)
        private readonly Dictionary<int, List<(int season, int episode, string title)>> _episodeCache = new();

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
                _episodeCache.Clear();
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

        public async Task<List<SeriesSearchResult>> SearchSeriesAsync(string query, CancellationToken ct = default)
        {
            if (!IsConfigured()) return new List<SeriesSearchResult>();
            if (string.IsNullOrWhiteSpace(query)) return new List<SeriesSearchResult>();

            string url = BuildUrl($"search/tv?query={Uri.EscapeDataString(query)}&language={Uri.EscapeDataString(_language)}");
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return new List<SeriesSearchResult>();

            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
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
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return new List<MovieSearchResult>();

            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
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
            var title = await FetchEpisodeNameAsync(seriesId, season, episode, _language, ct);
            if (string.IsNullOrWhiteSpace(title) && !string.Equals(_language, "en-US", StringComparison.OrdinalIgnoreCase))
            {
                title = await FetchEpisodeNameAsync(seriesId, season, episode, "en-US", ct);
            }
            return title;
        }

        private async Task<string?> FetchEpisodeNameAsync(int seriesId, int season, int episode, string language, CancellationToken ct)
        {
            string url = BuildUrl($"tv/{seriesId}/season/{season}/episode/{episode}?language={Uri.EscapeDataString(language)}");
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return doc.RootElement.TryGetProperty("name", out var name) ? name.GetString() : null;
        }

        public async Task<(int season, int episode, string title)?> FindEpisodeByTitleAsync(int seriesId, string title, CancellationToken ct = default)
        {
            if (!IsConfigured()) return null;
            if (string.IsNullOrWhiteSpace(title)) return null;

            var allEpisodes = await GetOrFetchAllEpisodesAsync(seriesId, ct);
            string normalizedQuery = NormalizeForMatch(title);
            if (string.IsNullOrWhiteSpace(normalizedQuery)) return null;

            // 1. Exact or bidirectional substring match on normalized text (ignores accents & punctuation)
            var directMatch = allEpisodes.FirstOrDefault(e =>
            {
                if (string.IsNullOrWhiteSpace(e.title)) return false;
                string normalizedEp = NormalizeForMatch(e.title);
                if (string.IsNullOrWhiteSpace(normalizedEp)) return false;
                return normalizedEp.Contains(normalizedQuery, StringComparison.Ordinal) ||
                       normalizedQuery.Contains(normalizedEp, StringComparison.Ordinal);
            });

            if (directMatch != default)
                return directMatch;

            // 2. Token overlap fuzzy fallback (requires at least 60% overlap)
            var queryTokens = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            if (queryTokens.Count == 0) return null;

            (int season, int episode, string title)? best = null;
            double bestScore = 0.59;

            foreach (var ep in allEpisodes)
            {
                if (string.IsNullOrWhiteSpace(ep.title)) continue;
                var epTokens = NormalizeForMatch(ep.title).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                if (epTokens.Count == 0) continue;

                int intersection = epTokens.Count(t => queryTokens.Contains(t));
                double score = (double)intersection / Math.Max(epTokens.Count, 1);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = ep;
                }
            }

            return best;
        }

        public async Task<ChapterInfo?> GetEpisodeMetadataAsync(int seriesId, int season, int episode, CancellationToken ct = default)
        {
            if (!IsConfigured()) return null;
            var title = await GetEpisodeTitleAsync(seriesId, season, episode, ct);
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
        private async Task<List<(int season, int episode, string title)>> GetOrFetchAllEpisodesAsync(int seriesId, CancellationToken ct)
        {
            if (_episodeCache.TryGetValue(seriesId, out var cached))
                return cached;

            var allEpisodes = new List<(int season, int episode, string title)>();

            string seriesUrl = BuildUrl($"tv/{seriesId}?language={Uri.EscapeDataString(_language)}");
            using var sresp = await _http.GetAsync(seriesUrl, ct);
            if (!sresp.IsSuccessStatusCode)
            {
                _episodeCache[seriesId] = allEpisodes;
                return allEpisodes;
            }

            using var sstream = await sresp.Content.ReadAsStreamAsync(ct);
            var sdoc = await JsonDocument.ParseAsync(sstream, cancellationToken: ct);
            int seasonsCount = sdoc.RootElement.TryGetProperty("number_of_seasons", out var ns) && ns.ValueKind == JsonValueKind.Number
                ? ns.GetInt32()
                : 1;

            using var throttler = new SemaphoreSlim(4);
            var seasonTasks = Enumerable.Range(1, seasonsCount).Select(async seasonNum =>
            {
                await throttler.WaitAsync(ct);
                try
                {
                    ct.ThrowIfCancellationRequested();
                    var seasonEps = new List<(int season, int episode, string title)>();
                    string seasonUrl = BuildUrl($"tv/{seriesId}/season/{seasonNum}?language={Uri.EscapeDataString(_language)}");
                    using var resp = await _http.GetAsync(seasonUrl, ct);
                    if (!resp.IsSuccessStatusCode) return seasonEps;

                    using var stream = await resp.Content.ReadAsStreamAsync(ct);
                    var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                    if (doc.RootElement.TryGetProperty("episodes", out var eps) && eps.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var ep in eps.EnumerateArray())
                        {
                            var epName = ep.TryGetProperty("name", out var en) ? en.GetString() ?? string.Empty : string.Empty;
                            var epNumber = ep.TryGetProperty("episode_number", out var enn) ? enn.GetInt32() : 0;
                            seasonEps.Add((seasonNum, epNumber, epName));
                        }
                    }
                    return seasonEps;
                }
                finally
                {
                    throttler.Release();
                }
            }).ToList();

            var results = await Task.WhenAll(seasonTasks);
            foreach (var list in results)
            {
                allEpisodes.AddRange(list);
            }

            _episodeCache[seriesId] = allEpisodes;
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
            return Regex.Replace(sb.ToString(), @"\s{2,}", " ").Trim();
        }
    }
}
