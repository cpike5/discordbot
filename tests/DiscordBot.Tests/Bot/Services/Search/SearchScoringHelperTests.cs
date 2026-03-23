using DiscordBot.Bot.Helpers;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Services.Search;

/// <summary>
/// Unit tests for <see cref="SearchScoringHelper"/>.
/// </summary>
public class SearchScoringHelperTests
{
    [Fact]
    public void CalculateRelevanceScore_ExactMatch_Returns100()
    {
        var score = SearchScoringHelper.CalculateRelevanceScore("hello", "hello");
        score.Should().Be(100);
    }

    [Fact]
    public void CalculateRelevanceScore_ExactMatchDifferentCase_Returns100()
    {
        var score = SearchScoringHelper.CalculateRelevanceScore("Hello", "hello");
        score.Should().Be(100);
    }

    [Fact]
    public void CalculateRelevanceScore_StartsWith_Returns75()
    {
        var score = SearchScoringHelper.CalculateRelevanceScore("helloworld", "hello");
        score.Should().Be(75);
    }

    [Fact]
    public void CalculateRelevanceScore_Contains_Returns50()
    {
        var score = SearchScoringHelper.CalculateRelevanceScore("say hello there", "hello");
        score.Should().Be(50);
    }

    [Fact]
    public void CalculateRelevanceScore_NoMatch_Returns0()
    {
        var score = SearchScoringHelper.CalculateRelevanceScore("goodbye", "hello");
        score.Should().Be(0);
    }

    [Fact]
    public void CalculateRelevanceScore_EmptyField_Returns0()
    {
        var score = SearchScoringHelper.CalculateRelevanceScore(string.Empty, "hello");
        score.Should().Be(0);
    }

    [Fact]
    public void CalculateRelevanceScore_NullField_Returns0()
    {
        var score = SearchScoringHelper.CalculateRelevanceScore(null!, "hello");
        score.Should().Be(0);
    }

    [Fact]
    public void Clamp_ScoreAbove100_Returns100()
    {
        SearchScoringHelper.Clamp(250).Should().Be(100);
    }

    [Fact]
    public void Clamp_ScoreBelow100_ReturnsOriginal()
    {
        SearchScoringHelper.Clamp(75).Should().Be(75);
    }

    [Fact]
    public void Clamp_Score100_Returns100()
    {
        SearchScoringHelper.Clamp(100).Should().Be(100);
    }
}
