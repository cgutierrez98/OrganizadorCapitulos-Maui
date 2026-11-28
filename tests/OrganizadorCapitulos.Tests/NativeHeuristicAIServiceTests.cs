using System;
using System.Threading;
using System.Threading.Tasks;
using organizadorCapitulos.Infrastructure.Services;
using Xunit;

namespace OrganizadorCapitulos.Tests;

public class NativeHeuristicAIServiceTests
{
    private readonly NativeHeuristicAIService _service = new();

    [Fact]
    public void IsAvailable_ReturnsTrue()
    {
        Assert.True(_service.IsAvailable());
    }

    [Fact]
    public async Task AnalyzeFilenameAsync_SxExFormat_ExtractsMetadata()
    {
        var result = await _service.AnalyzeFilenameAsync("Show.Name.S01E02.Title.mkv");

        Assert.NotNull(result);
        Assert.Equal(1, result.Season);
        Assert.Equal(2, result.Episode);
        Assert.Contains("Show", result.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Title", result.EpisodeTitle);
    }

    [Fact]
    public async Task AnalyzeFilenameAsync_NxNNFormat_ExtractsMetadata()
    {
        var result = await _service.AnalyzeFilenameAsync("Series_Name 2x05 Episode.mkv");

        Assert.NotNull(result);
        Assert.Equal(2, result.Season);
        Assert.Equal(5, result.Episode);
        Assert.Equal("Series Name", result.Title);
        Assert.Equal("Episode", result.EpisodeTitle);
    }

    [Fact]
    public async Task AnalyzeFilenameAsync_AnimeFallback_ExtractsSeason1()
    {
        var result = await _service.AnalyzeFilenameAsync("GreatAnime.012.mkv");

        Assert.NotNull(result);
        Assert.Equal(1, result.Season);
        Assert.Equal(12, result.Episode);
        Assert.False(string.IsNullOrWhiteSpace(result.Title));
    }

    [Fact]
    public async Task AnalyzeFilenameAsync_NoisyName_StripsQualityTags()
    {
        var result = await _service.AnalyzeFilenameAsync("My.Show.1080p.Web-DL.x264.Eng.S02E03.mkv");

        Assert.NotNull(result);
        Assert.Equal(2, result.Season);
        Assert.Equal(3, result.Episode);
        Assert.Contains("My Show", result.Title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1080p", result.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeFilenameAsync_SpanishPattern_ExtractsMetadata()
    {
        var result = await _service.AnalyzeFilenameAsync("Mi.Serie.Temporada.2.Capitulo.07.mkv");

        Assert.NotNull(result);
        Assert.Equal(2, result.Season);
        Assert.Equal(7, result.Episode);
        Assert.Equal("Mi Serie", result.Title);
    }

    [Fact]
    public async Task AnalyzeFilenameAsync_ExtremelyLongInput_CompletesQuickly()
    {
        var longName = new string('a', 1500) + ".S01E04.mkv";
        var result = await _service.AnalyzeFilenameAsync(longName);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task AnalyzeFilenameAsync_CancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.AnalyzeFilenameAsync("Show.S01E01.mkv", cts.Token));
    }

    [Fact]
    public async Task SuggestTitleAsync_CleansQualityTags()
    {
        var title = await _service.SuggestTitleAsync("Breaking.Bad.1080p.BluRay.x264");
        Assert.Equal("Breaking Bad", title);
    }
}
