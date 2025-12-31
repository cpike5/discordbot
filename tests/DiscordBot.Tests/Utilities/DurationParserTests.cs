using DiscordBot.Bot.Utilities;
using Xunit;

namespace DiscordBot.Tests.Utilities;

public class DurationParserTests
{
    [Theory]
    [InlineData("10m", 600)]
    [InlineData("1h", 3600)]
    [InlineData("1h30m", 5400)]
    [InlineData("7d", 604800)]
    [InlineData("2w", 1209600)]
    [InlineData("1w3d", 864000)]
    [InlineData("1d12h", 129600)]
    [InlineData("30s", 30)]
    [InlineData("1h30m45s", 5445)]
    public void Parse_ValidInput_ReturnsCorrectTimeSpan(string input, int expectedSeconds)
    {
        // Act
        var result = DurationParser.Parse(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedSeconds, result.Value.TotalSeconds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid")]
    [InlineData("10x")]
    [InlineData("abc")]
    public void Parse_InvalidInput_ReturnsNull(string input)
    {
        // Act
        var result = DurationParser.Parse(input);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(600, "10 minutes")]
    [InlineData(3600, "1 hour")]
    [InlineData(5400, "1 hour 30 minutes")]
    [InlineData(604800, "1 week")]
    [InlineData(86400, "1 day")]
    [InlineData(90061, "1 day 1 hour 1 minute 1 second")]
    [InlineData(1209600, "2 weeks")]
    [InlineData(864000, "1 week 3 days")]
    public void Format_ReturnsHumanReadableString(int seconds, string expected)
    {
        // Arrange
        var duration = TimeSpan.FromSeconds(seconds);

        // Act
        var result = DurationParser.Format(duration);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(600, "10m")]
    [InlineData(3600, "1h")]
    [InlineData(5400, "1h30m")]
    [InlineData(604800, "1w")]
    [InlineData(86400, "1d")]
    [InlineData(90061, "1d1h1m1s")]
    [InlineData(1209600, "2w")]
    [InlineData(864000, "1w3d")]
    public void FormatShort_ReturnsCompactString(int seconds, string expected)
    {
        // Arrange
        var duration = TimeSpan.FromSeconds(seconds);

        // Act
        var result = DurationParser.FormatShort(duration);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Parse_CaseInsensitive_Works()
    {
        // Arrange
        var inputs = new[] { "10M", "1H", "7D", "2W", "30S" };

        // Act & Assert
        foreach (var input in inputs)
        {
            var result = DurationParser.Parse(input);
            Assert.NotNull(result);
        }
    }

    [Fact]
    public void Format_ZeroDuration_ReturnsSeconds()
    {
        // Arrange
        var duration = TimeSpan.Zero;

        // Act
        var result = DurationParser.Format(duration);

        // Assert
        Assert.Equal("0 seconds", result);
    }

    [Fact]
    public void FormatShort_ZeroDuration_ReturnsSeconds()
    {
        // Arrange
        var duration = TimeSpan.Zero;

        // Act
        var result = DurationParser.FormatShort(duration);

        // Assert
        Assert.Equal("0s", result);
    }
}
