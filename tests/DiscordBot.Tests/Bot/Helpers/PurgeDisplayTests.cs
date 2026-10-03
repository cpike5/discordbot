using DiscordBot.Bot.Helpers;
using DiscordBot.Core.Enums;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Bot.Helpers;

public class PurgeDisplayTests
{
    [Theory]
    [InlineData(BulkPurgeEntityType.Messages, "Message logs")]
    [InlineData(BulkPurgeEntityType.AuditLogs, "Audit logs")]
    [InlineData(BulkPurgeEntityType.CommandLogs, "Command logs")]
    [InlineData(BulkPurgeEntityType.ModerationCases, "Moderation cases")]
    public void EntityLabel_IsPlainLanguage(BulkPurgeEntityType type, string expected)
    {
        PurgeDisplay.EntityLabel(type).Should().Be(expected);
    }

    [Fact]
    public void EntityNoun_IsLowerCase_ForSentences()
    {
        PurgeDisplay.EntityNoun(BulkPurgeEntityType.ModerationCases).Should().Be("moderation cases");
    }

    [Fact]
    public void EveryEntityType_HasADescription()
    {
        foreach (var type in Enum.GetValues<BulkPurgeEntityType>())
        {
            PurgeDisplay.EntityDescription(type).Should().NotBeNullOrEmpty($"{type} needs a description on its card");
        }
    }

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
