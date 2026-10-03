using DiscordBot.Bot.Helpers;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Bot.Helpers;

public class PurgeDisplayTests
{
    [Theory]
    [InlineData("MessageLogs", "Message logs")]
    [InlineData("RatRecords_Anonymized", "Rat Watch records (anonymized)")]
    [InlineData("RatWatches_Anonymized", "Rat Watches (anonymized)")]
    [InlineData("ApplicationUser", "Linked web account")]
    public void CountLabel_UsesTheFriendlyName(string key, string expected)
    {
        PurgeDisplay.CountLabel(key).Should().Be(expected);
    }

    [Fact]
    public void CountLabel_FallsBackToSplittingUnknownKeys()
    {
        PurgeDisplay.CountLabel("SomeNewTable").Should().Be("Some new table");
        PurgeDisplay.CountLabel("SomeNewTable_Anonymized").Should().Be("Some new table (anonymized)");
    }

    [Fact]
    public void IsAnonymized_ReadsTheSuffix()
    {
        PurgeDisplay.IsAnonymized("RatRecords_Anonymized").Should().BeTrue();
        PurgeDisplay.IsAnonymized("RatRecords").Should().BeFalse();
    }

    [Theory]
    [InlineData("ModerationCases", "Moderation cases")]
    [InlineData("Users", "Users")]
    [InlineData("", "")]
    public void Split_MakesPascalCaseReadable(string input, string expected)
    {
        PurgeDisplay.Split(input).Should().Be(expected);
    }
}
