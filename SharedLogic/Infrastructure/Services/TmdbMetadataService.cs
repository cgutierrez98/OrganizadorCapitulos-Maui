using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Interfaces.Services;

namespace organizadorCapitulos.Infrastructure.Services
{
    public class TmdbMetadataService : IMetadataService
    {
        private string? _apiKey;
        private readonly HttpClient _http;

        // Session-level cache: seriesId â†’ list of (season, episode, title)
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
            else
            {
                // Use Authorization header instead of query param for v4 token to avoid key exposure in logs/proxies
                _http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
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

            string url = BuildUrl($"search/tv?query={Uri.EscapeDataString(query)}&language=es-ES");
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
                        FirstAirDate = item.TryGetProperty("first_air_date", out var fd) ? fd.GetString() : null
                    });
                }
            }

            return results;
        }

        public async Task<List<MovieSearchResult>> SearchMoviesAsync(string query, CancellationToken ct = default)
        {
            if (!IsConfigured()) return new List<MovieSearchResult>();
            if (string.IsNullOrWhiteSpace(query)) return new List<MovieSearchResult>();

            string url = BuildUrl($"search/movie?query={Uri.EscapeDataString(query)}&language=es-ES");
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
                        ReleaseDate = item.TryGetProperty("release_date", out var rd) ? rd.GetString() : null
                    });
                }
            }

            return results;
        }

        public async Task<string?> GetEpisodeTitleAsync(int seriesId, int season, int episode, CancellationToken ct = default)
        {
            if (!IsConfigured()) return null;
            string url = BuildUrl($"tv/{seriesId}/season/{season}/episode/{episode}?language=es-ES");
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

            // Bidirectional match: TMDB title contains query, OR query contains TMDB title.
            // The second branch handles formats like "Series Title Episode Title S01E50"
            // where the extracted query is longer than the TMDB episode title.
            var match = allEpisodes.FirstOrDefault(e =>
                !string.IsNullOrWhiteSpace(e.title) &&
                (e.title.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 title.IndexOf(e.title, StringComparison.OrdinalIgnoreCase) >= 0));

            return match == default ? null : match;
        }

        public async Task<ChapterInfo?> GetEpisodeMetadataAsync(int seriesId, int season, int episode, CancellationToken ct = default)
        {
            if (!IsConfigured()) return null;
            string url = BuildUrl($"tv/{seriesId}/season/{season}/episode/{episode}?language=es-ES");
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var info = new ChapterInfo { Season = season, Episode = episode };
            if (doc.RootElement.TryGetProperty("name", out var name))
                info.EpisodeTitle = name.GetString() ?? string.Empty;
            return info;
        }

        /// <summary>
        /// Returns all episodes for a series from the session cache, fetching from TMDB if not yet cached.
        /// </summary>
        private async Task<List<(int season, int episode, string title)>> GetOrFetchAllEpisodesAsync(int seriesId, CancellationToken ct)
        {
            if (_episodeCache.TryGetValue(seriesId, out var cached))
                return cached;

            var allEpisodes = new List<(int season, int episode, string title)>();

            string seriesUrl = BuildUrl($"tv/{seriesId}?language=es-ES");
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

            for (int s = 1; s <= seasonsCount; s++)
            {
                ct.ThrowIfCancellationRequested();
                string seasonUrl = BuildUrl($"tv/{seriesId}/season/{s}?language=es-ES");
                using var resp = await _http.GetAsync(seasonUrl, ct);
                if (!resp.IsSuccessStatusCode) continue;

                using var stream = await resp.Content.ReadAsStreamAsync(ct);
                var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (doc.RootElement.TryGetProperty("episodes", out var eps) && eps.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ep in eps.EnumerateArray())
                    {
                        var epName = ep.TryGetProperty("name", out var en) ? en.GetString() ?? string.Empty : string.Empty;
                        var epNumber = ep.TryGetProperty("episode_number", out var enn) ? enn.GetInt32() : 0;
                        allEpisodes.Add((s, epNumber, epName));
                    }
                }
            }

            _episodeCache[seriesId] = allEpisodes;
            return allEpisodes;
        }
    }
}
