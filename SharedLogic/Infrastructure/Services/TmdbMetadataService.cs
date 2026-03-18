using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Interfaces.Services;

namespace organizadorCapitulos.Infrastructure.Services
{
    public class TmdbMetadataService : IMetadataService
    {
        private string? _apiKey;
        private readonly HttpClient _http;

        public TmdbMetadataService()
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri("https://api.themoviedb.org/3/")
            };
        }

        public void Configure(string apiKey)
        {
            _apiKey = apiKey;
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(_apiKey);
        }

        public async Task<List<SeriesSearchResult>> SearchSeriesAsync(string query)
        {
            if (!IsConfigured()) return new List<SeriesSearchResult>();
            if (string.IsNullOrWhiteSpace(query)) return new List<SeriesSearchResult>();

            string url = $"search/tv?api_key={_apiKey}&query={Uri.EscapeDataString(query)}&language=es-ES";
            using var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return new List<SeriesSearchResult>();

            using var stream = await resp.Content.ReadAsStreamAsync();
            var doc = await JsonDocument.ParseAsync(stream);
            var results = new List<SeriesSearchResult>();
            if (doc.RootElement.TryGetProperty("results", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var r = new SeriesSearchResult
                    {
                        Id = item.GetProperty("id").GetInt32(),
                        Name = item.GetProperty("name").GetString() ?? string.Empty,
                        Overview = item.TryGetProperty("overview", out var ov) ? ov.GetString() ?? string.Empty : string.Empty,
                        FirstAirDate = item.TryGetProperty("first_air_date", out var fd) ? fd.GetString() : null
                    };
                    results.Add(r);
                }
            }

            return results;
        }

        public async Task<string?> GetEpisodeTitleAsync(int seriesId, int season, int episode)
        {
            if (!IsConfigured()) return null;
            string url = $"tv/{seriesId}/season/{season}/episode/{episode}?api_key={_apiKey}&language=es-ES";
            using var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;

            using var stream = await resp.Content.ReadAsStreamAsync();
            var doc = await JsonDocument.ParseAsync(stream);
            if (doc.RootElement.TryGetProperty("name", out var name))
            {
                return name.GetString();
            }

            return null;
        }

        public async Task<(int season, int episode, string title)?> FindEpisodeByTitleAsync(int seriesId, string title)
        {
            if (!IsConfigured()) return null;
            if (string.IsNullOrWhiteSpace(title)) return null;

            // Get series details to know seasons
            string seriesUrl = $"tv/{seriesId}?api_key={_apiKey}&language=es-ES";
            using var sresp = await _http.GetAsync(seriesUrl);
            if (!sresp.IsSuccessStatusCode) return null;

            using var sstream = await sresp.Content.ReadAsStreamAsync();
            var sdoc = await JsonDocument.ParseAsync(sstream);
            int seasonsCount = 0;
            if (sdoc.RootElement.TryGetProperty("number_of_seasons", out var ns) && ns.ValueKind == JsonValueKind.Number)
            {
                seasonsCount = ns.GetInt32();
            }

            // Search each season for matching episode title (case-insensitive contains)
            for (int season = 1; season <= Math.Max(1, seasonsCount); season++)
            {
                string seasonUrl = $"tv/{seriesId}/season/{season}?api_key={_apiKey}&language=es-ES";
                using var resp = await _http.GetAsync(seasonUrl);
                if (!resp.IsSuccessStatusCode) continue;

                using var stream = await resp.Content.ReadAsStreamAsync();
                var doc = await JsonDocument.ParseAsync(stream);
                if (doc.RootElement.TryGetProperty("episodes", out var eps) && eps.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ep in eps.EnumerateArray())
                    {
                        var epName = ep.TryGetProperty("name", out var en) ? en.GetString() ?? string.Empty : string.Empty;
                        var epNumber = ep.TryGetProperty("episode_number", out var enn) ? enn.GetInt32() : 0;
                        if (!string.IsNullOrWhiteSpace(epName) && epName.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return (season, epNumber, epName);
                        }
                    }
                }
            }

            return null;
        }

        public async Task<ChapterInfo?> GetEpisodeMetadataAsync(int seriesId, int season, int episode)
        {
            if (!IsConfigured()) return null;
            string url = $"tv/{seriesId}/season/{season}/episode/{episode}?api_key={_apiKey}&language=es-ES";
            using var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;

            using var stream = await resp.Content.ReadAsStreamAsync();
            var doc = await JsonDocument.ParseAsync(stream);
            var info = new ChapterInfo();
            info.Season = season;
            info.Episode = episode;
            if (doc.RootElement.TryGetProperty("name", out var name)) info.Title = name.GetString() ?? string.Empty;
            return info;
        }
    }
}
