using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using organizadorCapitulos.Application.Services;
using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Interfaces.Services;

namespace OrganizadorCapitulos.Maui.Services.Workflows
{
    public sealed record MovieMatchInput(int Index, string OriginalName);

    public sealed record MovieMatchOutcome(int Index, string Title, int Year);

    public sealed record EpisodeMatchInput(int Index, string OriginalName, int Season, int Episode, string EpisodeTitle);

    public sealed record EpisodeMatchOutcome(int Index, int Season, int Episode, string EpisodeTitle);

    public sealed class TmdbMatchingWorkflow
    {
        private readonly IMetadataService _metadataService;

        public TmdbMatchingWorkflow(IMetadataService metadataService)
        {
            _metadataService = metadataService;
        }

        public async Task<List<MovieMatchOutcome>> AutoIdentifyMoviesAsync(
            IReadOnlyList<MovieMatchInput> items,
            Action<int, int, string>? onProgress = null,
            CancellationToken ct = default)
        {
            var outcomes = new List<MovieMatchOutcome>();
            if (items.Count == 0) return outcomes;

            var extracted = new List<(MovieMatchInput item, string query)>(items.Count);
            var uniqueQueries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                var query = EpisodePatternParser.ExtractMovieTitle(item.OriginalName);
                extracted.Add((item, query));
                if (!string.IsNullOrWhiteSpace(query))
                {
                    uniqueQueries.Add(query);
                }
            }

            var queryCache = new ConcurrentDictionary<string, MovieSearchResult?>(StringComparer.OrdinalIgnoreCase);
            using var throttler = new SemaphoreSlim(4);
            int totalQueries = uniqueQueries.Count;
            int completedQueries = 0;
            long lastReportTicks = 0;

            var searchTasks = uniqueQueries.Select(async query =>
            {
                await throttler.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    ct.ThrowIfCancellationRequested();
                    var results = await _metadataService.SearchMoviesAsync(query, ct).ConfigureAwait(false);
                    queryCache[query] = results.FirstOrDefault();

                    int done = Interlocked.Increment(ref completedQueries);
                    long now = Environment.TickCount64;
                    if (done == 1 || done == totalQueries || now - lastReportTicks >= 60)
                    {
                        lastReportTicks = now;
                        onProgress?.Invoke(done, totalQueries, query);
                    }
                }
                finally
                {
                    throttler.Release();
                }
            }).ToList();

            await Task.WhenAll(searchTasks).ConfigureAwait(false);

            foreach (var (item, query) in extracted)
            {
                if (string.IsNullOrWhiteSpace(query)) continue;
                if (!queryCache.TryGetValue(query, out var bestMatch) || bestMatch == null) continue;

                int year = 0;
                if (!int.TryParse(bestMatch.ReleaseDate?.Split('-')[0], out year))
                {
                    EpisodePatternParser.TryExtractYear(item.OriginalName, out year);
                }

                outcomes.Add(new MovieMatchOutcome(item.Index, bestMatch.Title, year));
            }

            return outcomes;
        }

        public async Task<List<EpisodeMatchOutcome>> FetchEpisodesAsync(
            int seriesId,
            bool lookupByTitle,
            IReadOnlyList<EpisodeMatchInput> items,
            Action<int, int, string>? onProgress = null,
            CancellationToken ct = default)
        {
            var outcomes = new List<EpisodeMatchOutcome>();
            int total = items.Count;
            long lastReportTicks = 0;

            for (int i = 0; i < total; i++)
            {
                ct.ThrowIfCancellationRequested();
                var item = items[i];
                int current = i + 1;

                long now = Environment.TickCount64;
                if (current == 1 || current == total || now - lastReportTicks >= 60)
                {
                    lastReportTicks = now;
                    onProgress?.Invoke(current, total, item.OriginalName);
                }

                if (lookupByTitle)
                {
                    (int season, int episode, string title)? match = null;
                    if (!string.IsNullOrEmpty(item.EpisodeTitle))
                    {
                        match = await _metadataService.FindEpisodeByTitleAsync(seriesId, item.EpisodeTitle, ct).ConfigureAwait(false);
                    }

                    if (!match.HasValue)
                    {
                        string possibleTitle = EpisodePatternParser.ExtractPossibleTitle(item.OriginalName);
                        if (!string.IsNullOrEmpty(possibleTitle))
                        {
                            match = await _metadataService.FindEpisodeByTitleAsync(seriesId, possibleTitle, ct).ConfigureAwait(false);
                        }
                    }

                    if (match.HasValue)
                    {
                        outcomes.Add(new EpisodeMatchOutcome(item.Index, match.Value.season, match.Value.episode, match.Value.title));
                    }
                }
                else
                {
                    int season = item.Season;
                    int episode = item.Episode;
                    bool hasNumbers = season > 0 && episode > 0;
                    if (!hasNumbers)
                    {
                        hasNumbers = EpisodePatternParser.TryExtractSeasonEpisode(item.OriginalName, out season, out episode);
                    }

                    if (hasNumbers)
                    {
                        var title = await _metadataService.GetEpisodeTitleAsync(seriesId, season, episode, ct).ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(title))
                        {
                            outcomes.Add(new EpisodeMatchOutcome(item.Index, season, episode, title));
                        }
                    }
                }
            }

            return outcomes;
        }
    }
}
