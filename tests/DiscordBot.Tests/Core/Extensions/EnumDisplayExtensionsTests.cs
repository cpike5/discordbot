using System.ComponentModel.DataAnnotations;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Extensions;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Core.Extensions;

/// <summary>The one enum-to-words helper (UX plan C-2).</summary>
public class EnumDisplayExtensionsTests
{
    public enum Sample
    {
        Plain,
        TwoWords,
        [Display(Name = "Custom name", Description = "What it means")]
        Renamed
    }

    [Flags]
    public enum Bits
    {
        None = 0,
        ReadOnly = 1,
        [Display(Name = "Write access")]
        WriteAccess = 2
    }

    [Theory]
    [InlineData(Sample.Plain, "Plain")]
    [InlineData(Sample.TwoWords, "Two words")]
    [InlineData(Sample.Renamed, "Custom name")]
    public void DisplayName_UsesTheAttributeThenSplitsTheName(Sample value, string expected)
    {
        value.DisplayName().Should().Be(expected);
    }

    [Fact]
    public void Description_ComesFromTheAttribute_OrIsEmpty()
    {
        Sample.Renamed.Description().Should().Be("What it means");
        Sample.Plain.Description().Should().BeEmpty();
    }

    [Fact]
    public void DisplayNameLower_LowersTheFirstWord_ButKeepsAnAcronym()
    {
        Sample.TwoWords.DisplayNameLower().Should().Be("two words");
        LlmMode.DmAssistant.DisplayNameLower().Should().Be("DM assistant");
    }

    [Fact]
    public void NullableOverload_ReturnsTheFallbackForNull()
    {
        ((Sample?)null).DisplayName().Should().Be("—");
        ((Sample?)null).DisplayName("None").Should().Be("None");
        ((Sample?)Sample.TwoWords).DisplayName().Should().Be("Two words");
    }

    [Fact]
    public void ACombinedFlagsValue_NamesEachPart()
    {
        (Bits.ReadOnly | Bits.WriteAccess).DisplayName().Should().Be("Read only, write access");
    }

    [Fact]
    public void AnUndefinedValue_StillReadsAsText()
    {
        ((Sample)42).DisplayName().Should().Be("42");
    }

    [Theory]
    [InlineData("ModerationCases", "Moderation cases")]
    [InlineData("Users", "Users")]
    [InlineData("snake_case_key", "Snake case key")]
    [InlineData("", "")]
    public void Humanize_MakesPascalCaseReadable(string input, string expected)
    {
        EnumDisplayExtensions.Humanize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(RatWatchStatus.ClearedEarly, "Cleared early")]
    [InlineData(RatWatchStatus.NotGuilty, "Not guilty")]
    [InlineData(BulkPurgeEntityType.Messages, "Message logs")]
    [InlineData(BulkPurgeEntityType.ModerationCases, "Moderation cases")]
    [InlineData(FeatureRequestStatus.GeneratingDocs, "Writing documentation")]
    [InlineData(LlmMode.GuildAssistant, "Server assistant")]
    [InlineData(MessageSource.DirectMessage, "Direct message")]
    public void ExistingNames_AreUnchanged(Enum value, string expected)
    {
        value.DisplayName().Should().Be(expected);
    }

    [Fact]
    public void EveryBulkPurgeEntityType_HasADescriptionForItsCard()
    {
        foreach (var type in Enum.GetValues<BulkPurgeEntityType>())
        {
            type.Description().Should().NotBeNullOrEmpty($"{type} needs a description on its card");
        }
    }

    [Fact]
    public void NoCoreEnumMember_ReadsAsRunTogetherWords()
    {
        var enums = typeof(RatWatchStatus).Assembly.GetTypes()
            .Where(t => t.IsEnum && t.Namespace == "DiscordBot.Core.Enums");

        foreach (var type in enums)
        {
            foreach (Enum value in Enum.GetValues(type))
            {
                var name = value.DisplayName();
                name.Should().NotBeNullOrWhiteSpace($"{type.Name}.{value}");
                name.Should().NotMatch("*[a-z][A-Z]*", $"{type.Name}.{value} must not show its PascalCase name");
            }
        }
    }
}
