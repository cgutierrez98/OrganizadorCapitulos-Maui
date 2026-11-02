using organizadorCapitulos.Core.Entities;
using Xunit;

namespace OrganizadorCapitulos.Tests;

public class ChapterInfoTests
{
    [Fact]
    public void GenerateFileName_WithSeasonEpisodeAndTitle_ProducesExpectedFormat()
    {
        var info = new ChapterInfo { Season = 2, Episode = 5, Title = "My Series" };
        var result = info.GenerateFileName(".mkv");
        Assert.Equal("S02E05 - My Series.mkv", result);
    }

    [Fact]
    public void GenerateFileName_NoTitle_OmitsTitlePart()
    {
        var info = new ChapterInfo { Season = 1, Episode = 3 };
        var result = info.GenerateFileName(".mp4");
        Assert.Equal("S01E03.mp4", result);
    }

    [Fact]
    public void GenerateFileName_EpisodeTitleIncluded_AppendedAfterDash()
    {
        var info = new ChapterInfo { Season = 1, Episode = 1, Title = "Show", EpisodeTitle = "Pilot" };
        var result = info.GenerateFileName(".mkv");
        Assert.Equal("S01E01 - Show - Pilot.mkv", result);
    }

    [Fact]
    public void GenerateFileName_InvalidCharsInTitle_AreSanitized()
    {
        var info = new ChapterInfo { Season = 1, Episode = 2, Title = "Title: With/Slashes*" };
        var result = info.GenerateFileName(".mkv");
        // Invalid filename chars should be replaced with '_'
        Assert.DoesNotContain(":", result);
        Assert.DoesNotContain("/", result);
        Assert.DoesNotContain("*", result);
    }

    [Fact]
    public void GenerateFileName_EpisodeOnlyNoSeason_IncludesEpisodeCode()
    {
        var info = new ChapterInfo { Episode = 10, Title = "Anime" };
        var result = info.GenerateFileName(".mkv");
        Assert.Contains("E10", result);
        Assert.DoesNotContain("S00", result);
    }

    [Fact]
    public void IsValid_WithEpisodeNumber_ReturnsTrue()
    {
        var info = new ChapterInfo { Episode = 1 };
        Assert.True(info.IsValid());
    }

    [Fact]
    public void IsValid_AllEmpty_ReturnsFalse()
    {
        var info = new ChapterInfo { Season = 0, Episode = 0 };
        Assert.False(info.IsValid());
    }

    [Fact]
    public void GenerateFileName_MovieWithTitleAndYear_ProducesExpectedFormat()
    {
        var info = new ChapterInfo { Title = "Inception", Year = 2010 };
        var result = info.GenerateFileName(".mkv");
        Assert.Equal("Inception (2010).mkv", result);
    }

    [Fact]
    public void GenerateFileName_MovieWithTitleOnly_ProducesExpectedFormat()
    {
        var info = new ChapterInfo { Title = "Inception" };
        var result = info.GenerateFileName(".mkv");
        Assert.Equal("Inception.mkv", result);
    }

    [Fact]
    public void IsValid_MovieWithYear_ReturnsTrue()
    {
        var info = new ChapterInfo { Title = "Inception", Year = 2010 };
        Assert.True(info.IsValid());
    }
}
