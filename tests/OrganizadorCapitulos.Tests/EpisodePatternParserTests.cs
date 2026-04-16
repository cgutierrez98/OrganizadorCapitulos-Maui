using System.Runtime.Versioning;
using organizadorCapitulos.Application.Services;
using Xunit;

namespace OrganizadorCapitulos.Tests
{
    [SupportedOSPlatform("windows10.0.17763.0")]
    public class EpisodePatternParserTests
    {
        // ── TryExtractSeasonEpisode ──────────────────────────────────────────

        [Theory]
        [InlineData("Breaking.Bad.S01E05.720p.mkv", 1, 5)]
        [InlineData("Game.of.Thrones.S08E06.1080p.mkv", 8, 6)]
        [InlineData("show.S1E1.mp4", 1, 1)]
        [InlineData("SHOW_S12E120.mkv", 12, 120)]
        public void TryExtractSeasonEpisode_SxEx_ExtractsCorrectly(string filename, int expectedSeason, int expectedEpisode)
        {
            var result = EpisodePatternParser.TryExtractSeasonEpisode(filename, out var season, out var episode);

            Assert.True(result);
            Assert.Equal(expectedSeason, season);
            Assert.Equal(expectedEpisode, episode);
        }

        [Theory]
        [InlineData("The.Wire.1x01.mkv", 1, 1)]
        [InlineData("Series.02x10.mp4", 2, 10)]
        [InlineData("anime.12x100.mkv", 12, 100)]
        public void TryExtractSeasonEpisode_NxNN_ExtractsCorrectly(string filename, int expectedSeason, int expectedEpisode)
        {
            var result = EpisodePatternParser.TryExtractSeasonEpisode(filename, out var season, out var episode);

            Assert.True(result);
            Assert.Equal(expectedSeason, season);
            Assert.Equal(expectedEpisode, episode);
        }

        [Theory]
        [InlineData("My.Show.Season 1 Episode 3.mkv", 1, 3)]
        [InlineData("Series.Season.2.Episode.12.mp4", 2, 12)]
        public void TryExtractSeasonEpisode_EnglishWords_ExtractsCorrectly(string filename, int expectedSeason, int expectedEpisode)
        {
            var result = EpisodePatternParser.TryExtractSeasonEpisode(filename, out var season, out var episode);

            Assert.True(result);
            Assert.Equal(expectedSeason, season);
            Assert.Equal(expectedEpisode, episode);
        }

        [Theory]
        [InlineData("Mi.Serie.Temporada 1 Capitulo 5.mkv", 1, 5)]
        [InlineData("serie.Temp.2.Cap.08.mp4", 2, 8)]
        public void TryExtractSeasonEpisode_SpanishWords_ExtractsCorrectly(string filename, int expectedSeason, int expectedEpisode)
        {
            var result = EpisodePatternParser.TryExtractSeasonEpisode(filename, out var season, out var episode);

            Assert.True(result);
            Assert.Equal(expectedSeason, season);
            Assert.Equal(expectedEpisode, episode);
        }

        [Theory]
        [InlineData("Naruto - 150 - episode title.mkv", 1, 150)]
        [InlineData("Dragon Ball Z - 055.mkv", 1, 55)]
        public void TryExtractSeasonEpisode_AbsoluteNumber_UsesSeason1(string filename, int expectedSeason, int expectedEpisode)
        {
            var result = EpisodePatternParser.TryExtractSeasonEpisode(filename, out var season, out var episode);

            Assert.True(result);
            Assert.Equal(expectedSeason, season);
            Assert.Equal(expectedEpisode, episode);
        }

        [Theory]
        [InlineData("just a movie name.mkv")]
        [InlineData("random file.mp4")]
        [InlineData("no numbers at all.avi")]
        public void TryExtractSeasonEpisode_NoPattern_ReturnsFalse(string filename)
        {
            var result = EpisodePatternParser.TryExtractSeasonEpisode(filename, out var season, out var episode);

            Assert.False(result);
            Assert.Equal(0, season);
            Assert.Equal(0, episode);
        }

        // ── ExtractPossibleTitle ─────────────────────────────────────────────

        [Fact]
        public void ExtractPossibleTitle_AfterSxEx_ReturnsTitlePart()
        {
            var title = EpisodePatternParser.ExtractPossibleTitle("Breaking.Bad.S01E05.Crazy.Handful.mkv");

            Assert.False(string.IsNullOrWhiteSpace(title));
        }

        [Fact]
        public void ExtractPossibleTitle_WithQualityTags_StripsQualityTags()
        {
            var title = EpisodePatternParser.ExtractPossibleTitle("show.S02E03.1080p.WEB-DL.x264.mkv");

            Assert.DoesNotContain("1080p", title, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("WEB-DL", title, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("x264", title, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ExtractPossibleTitle_ExtensionRemoved_ResultHasNoExtension()
        {
            var title = EpisodePatternParser.ExtractPossibleTitle("show.S01E01.some.title.mkv");

            Assert.DoesNotContain(".mkv", title, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
