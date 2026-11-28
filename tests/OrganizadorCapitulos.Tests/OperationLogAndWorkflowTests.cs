using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using organizadorCapitulos.Application.Services;
using organizadorCapitulos.Application.Strategies;
using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Enums;
using organizadorCapitulos.Core.Interfaces.Services;
using organizadorCapitulos.Infrastructure.Repositories;
using organizadorCapitulos.Infrastructure.Services;
using OrganizadorCapitulos.Maui.Services;
using OrganizadorCapitulos.Maui.Services.Workflows;
using Xunit;

namespace OrganizadorCapitulos.Tests;

[SupportedOSPlatform("windows10.0.17763.0")]
public class OperationLogAndWorkflowTests
{
    [Fact]
    public async Task OperationLogService_LogBatchAndFlush_PersistsNewestFirst()
    {
        var dir = Path.Combine(Path.GetTempPath(), "OrganizadorCapitulos.Tests", Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            var log = new OperationLogService(dir);
            log.LogBatch(new[]
            {
                OperationLogService.CreateRenameEntry("a1.mkv", "b1.mkv"),
                OperationLogService.CreateRenameEntry("a2.mkv", "b2.mkv")
            });

            Assert.Equal(2, log.Entries.Count);
            Assert.Equal("a2.mkv", log.Entries[0].OldName);
            Assert.Equal("a1.mkv", log.Entries[1].OldName);

            await log.FlushAsync();

            var reloaded = new OperationLogService(dir);
            await reloaded.InitializeAsync();
            Assert.Equal(2, reloaded.Entries.Count);
            Assert.Equal("a2.mkv", reloaded.Entries[0].OldName);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task FileAnalysisWorkflow_LoadAndAnalyzeLocalAsync_DetectsEpisodesAndMovies()
    {
        var dir = Path.Combine(Path.GetTempPath(), "OrganizadorCapitulos.Tests", Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "Dark.S01E03.mkv"), "ep");
            await File.WriteAllTextAsync(Path.Combine(dir, "Dune.2021.1080p.mkv"), "mv");
            await File.WriteAllTextAsync(Path.Combine(dir, "unknown.mkv"), "uk");

            var organizer = new FileOrganizerService(new FileRepository(), null);
            var workflow = new FileAnalysisWorkflow(organizer, new NativeHeuristicAIService());

            var result = await workflow.LoadAndAnalyzeLocalAsync(new[] { dir });

            Assert.Equal(3, result.Files.Count);
            Assert.Equal(2, result.AutoAnalyzedCount);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void FileOrganizationWorkflow_BuildDryRunPreview_SequentialMaintain()
    {
        var organizer = new FileOrganizerService(new FileRepository(), null);
        var workflow = new FileOrganizationWorkflow(organizer, new RenameStrategyFactory());
        var items = new List<RenameBatchItem>
        {
            new("a.mkv", "a.mkv", "Dark", 1, 5, "", 0),
            new("b.mkv", "b.mkv", "Other", 2, 9, "", 0)
        };

        var previews = workflow.BuildDryRunPreview(items, RenameMode.Maintain, null);

        Assert.Equal(2, previews.Count);
        Assert.Equal("S01E05 - Dark.mkv", previews[0].preview);
        Assert.Equal("S01E06 - Dark.mkv", previews[1].preview);
    }

    [Fact]
    public async Task TmdbMatchingWorkflow_AutoIdentifyMoviesAsync_DeduplicatesQueries()
    {
        var metadata = new CountingFakeMetadataService();
        var workflow = new TmdbMatchingWorkflow(metadata);

        var inputs = new List<MovieMatchInput>
        {
            new(0, "Inception.2010.1080p.mkv"),
            new(1, "Inception.2010.720p.mp4")
        };

        var outcomes = await workflow.AutoIdentifyMoviesAsync(inputs);

        Assert.Equal(2, outcomes.Count);
        Assert.Equal(1, metadata.MovieSearchCalls);
        Assert.All(outcomes, o =>
        {
            Assert.Equal("Inception", o.Title);
            Assert.Equal(2010, o.Year);
        });
    }

    private sealed class CountingFakeMetadataService : IMetadataService
    {
        public int MovieSearchCalls { get; private set; }
        public void Configure(string apiKey) { }
        public bool IsConfigured() => true;
        public Task<List<SeriesSearchResult>> SearchSeriesAsync(string query, CancellationToken ct = default) =>
            Task.FromResult(new List<SeriesSearchResult>());

        public Task<List<MovieSearchResult>> SearchMoviesAsync(string query, CancellationToken ct = default)
        {
            MovieSearchCalls++;
            return Task.FromResult(new List<MovieSearchResult>
            {
                new() { Id = 1, Title = "Inception", ReleaseDate = "2010-07-16" }
            });
        }

        public Task<string?> GetEpisodeTitleAsync(int seriesId, int season, int episode, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);

        public Task<(int season, int episode, string title)?> FindEpisodeByTitleAsync(int seriesId, string title, CancellationToken ct = default) =>
            Task.FromResult<(int season, int episode, string title)?>(null);

        public Task<ChapterInfo?> GetEpisodeMetadataAsync(int seriesId, int season, int episode, CancellationToken ct = default) =>
            Task.FromResult<ChapterInfo?>(null);
    }
}
