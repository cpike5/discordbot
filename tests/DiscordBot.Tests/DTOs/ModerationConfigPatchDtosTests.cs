using DiscordBot.Core.DTOs;
using FluentAssertions;

namespace DiscordBot.Tests.DTOs;

/// <summary>
/// The moderation settings page saves only the fields a moderator edited. These tests pin down
/// that a patch changes what it carries and nothing else, and that bad numbers are named by field.
/// </summary>
public class ModerationConfigPatchDtosTests
{
    #region Spam

    [Fact]
    public void SpamPatch_ApplyTo_ChangesOnlyTheFieldsSent()
    {
        var saved = new SpamDetectionConfigDto
        {
            Enabled = true,
            MaxMessagesPerWindow = 5,
            WindowSeconds = 5,
            MaxMentionsPerMessage = 5,
            DuplicateMessageThreshold = 0.8,
            AutoAction = AutoAction.Delete
        };

        new SpamConfigPatchDto { MaxMessagesPerWindow = 12 }.ApplyTo(saved);

        saved.MaxMessagesPerWindow.Should().Be(12);
        saved.Enabled.Should().BeTrue();
        saved.WindowSeconds.Should().Be(5);
        saved.MaxMentionsPerMessage.Should().Be(5);
        saved.DuplicateMessageThreshold.Should().Be(0.8);
        saved.AutoAction.Should().Be(AutoAction.Delete);
    }

    [Fact]
    public void SpamPatch_ApplyTo_CanTurnTheSwitchOff()
    {
        var saved = new SpamDetectionConfigDto { Enabled = true };

        new SpamConfigPatchDto { Enabled = false }.ApplyTo(saved);

        saved.Enabled.Should().BeFalse("false is a value that was sent, not a missing one");
    }

    [Fact]
    public void SpamPatch_EmptyPatch_IsValidAndChangesNothing()
    {
        var saved = new SpamDetectionConfigDto { MaxMessagesPerWindow = 7 };
        var patch = new SpamConfigPatchDto();

        patch.Validate().Should().BeEmpty();
        patch.ApplyTo(saved);

        saved.MaxMessagesPerWindow.Should().Be(7);
    }

    [Theory]
    [InlineData(0, "maxMessagesPerWindow")]
    [InlineData(101, "maxMessagesPerWindow")]
    [InlineData(-3, "maxMessagesPerWindow")]
    public void SpamPatch_Validate_RejectsMessageRateOutOfRange(int value, string field)
    {
        var errors = new SpamConfigPatchDto { MaxMessagesPerWindow = value }.Validate();

        errors.Should().ContainKey(field);
        errors[field].Should().Contain("between 1 and 100");
    }

    [Theory]
    [InlineData(0.49)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void SpamPatch_Validate_RejectsSimilarityOutsideFiftyToHundredPercent(double value)
    {
        new SpamConfigPatchDto { DuplicateMessageThreshold = value }.Validate()
            .Should().ContainKey("duplicateMessageThreshold");
    }

    [Fact]
    public void SpamPatch_Validate_RejectsAnActionThatDoesNotExist()
    {
        new SpamConfigPatchDto { AutoAction = (AutoAction)99 }.Validate()
            .Should().ContainKey("autoAction");
    }

    [Fact]
    public void SpamPatch_Validate_ReportsEveryBadFieldAtOnce()
    {
        var errors = new SpamConfigPatchDto { MaxMessagesPerWindow = 0, WindowSeconds = 61, MaxMentionsPerMessage = 51 }.Validate();

        errors.Keys.Should().BeEquivalentTo("maxMessagesPerWindow", "windowSeconds", "maxMentionsPerMessage");
    }

    #endregion

    #region Content filter

    [Fact]
    public void ContentPatch_ApplyTo_NeverTouchesSettingsThePageDoesNotShow()
    {
        var saved = new ContentFilterConfigDto
        {
            Enabled = true,
            ProhibitedWords = new List<string> { "old" },
            AllowedLinkDomains = new List<string> { "example.com" },
            BlockUnlistedLinks = true,
            BlockInviteLinks = false,
            AutoAction = AutoAction.Delete
        };

        new ContentFilterPatchDto { BlockInviteLinks = true, ProhibitedWords = new List<string> { "spam" } }.ApplyTo(saved);

        saved.AllowedLinkDomains.Should().Equal("example.com");
        saved.BlockUnlistedLinks.Should().BeTrue("a save used to reset this to false");
        saved.BlockInviteLinks.Should().BeTrue();
        saved.ProhibitedWords.Should().Equal("spam");
        saved.Enabled.Should().BeTrue();
        saved.AutoAction.Should().Be(AutoAction.Delete);
    }

    [Fact]
    public void ContentPatch_ApplyTo_TrimsAndDeduplicatesTheBlocklist()
    {
        var saved = new ContentFilterConfigDto();

        new ContentFilterPatchDto { ProhibitedWords = new List<string> { " spam ", "SPAM", "", "  ", "scam" } }.ApplyTo(saved);

        saved.ProhibitedWords.Should().Equal("spam", "scam");
    }

    [Fact]
    public void ContentPatch_ApplyTo_AnEmptyBlocklistClearsIt()
    {
        var saved = new ContentFilterConfigDto { ProhibitedWords = new List<string> { "old" } };

        new ContentFilterPatchDto { ProhibitedWords = new List<string>() }.ApplyTo(saved);

        saved.ProhibitedWords.Should().BeEmpty();
    }

    [Fact]
    public void ContentPatch_ApplyTo_LeavesTheBlocklistAloneWhenNotSent()
    {
        var saved = new ContentFilterConfigDto { ProhibitedWords = new List<string> { "keep" } };

        new ContentFilterPatchDto { Enabled = false }.ApplyTo(saved);

        saved.ProhibitedWords.Should().Equal("keep");
        saved.Enabled.Should().BeFalse();
    }

    [Fact]
    public void ContentPatch_Validate_RejectsTooManyEntries()
    {
        var words = Enumerable.Range(0, ContentFilterPatchDto.MaxProhibitedWords + 1).Select(i => $"w{i}").ToList();

        new ContentFilterPatchDto { ProhibitedWords = words }.Validate().Should().ContainKey("prohibitedWords");
    }

    [Fact]
    public void ContentPatch_Validate_RejectsAnEntryThatIsTooLong()
    {
        var patch = new ContentFilterPatchDto { ProhibitedWords = new List<string> { new('x', ContentFilterPatchDto.MaxWordLength + 1) } };

        patch.Validate().Should().ContainKey("prohibitedWords");
    }

    [Fact]
    public void ContentPatch_Validate_RejectsKickBecauseThePageDoesNotOfferIt()
    {
        new ContentFilterPatchDto { AutoAction = AutoAction.Kick }.Validate().Should().ContainKey("autoAction");
    }

    [Theory]
    [InlineData(AutoAction.None)]
    [InlineData(AutoAction.Delete)]
    [InlineData(AutoAction.Warn)]
    [InlineData(AutoAction.Mute)]
    [InlineData(AutoAction.Ban)]
    public void ContentPatch_Validate_AcceptsTheActionsThePageOffers(AutoAction action)
    {
        new ContentFilterPatchDto { AutoAction = action }.Validate().Should().BeEmpty();
    }

    #endregion

    #region Raid protection

    [Fact]
    public void RaidPatch_ApplyTo_ChangesOnlyTheFieldsSent()
    {
        var saved = new RaidProtectionConfigDto
        {
            Enabled = true,
            MaxJoinsPerWindow = 10,
            WindowSeconds = 10,
            MinAccountAgeHours = 24,
            AutoAction = RaidAutoAction.LockServer
        };

        new RaidProtectionPatchDto { MinAccountAgeHours = 0 }.ApplyTo(saved);

        saved.MinAccountAgeHours.Should().Be(0, "zero is a real value");
        saved.MaxJoinsPerWindow.Should().Be(10);
        saved.AutoAction.Should().Be(RaidAutoAction.LockServer);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(101)]
    public void RaidPatch_Validate_RejectsJoinThresholdOutOfRange(int value)
    {
        new RaidProtectionPatchDto { MaxJoinsPerWindow = value }.Validate().Should().ContainKey("maxJoinsPerWindow");
    }

    [Theory]
    [InlineData(9)]
    [InlineData(301)]
    public void RaidPatch_Validate_RejectsWindowOutOfRange(int value)
    {
        new RaidProtectionPatchDto { WindowSeconds = value }.Validate().Should().ContainKey("windowSeconds");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(721)]
    public void RaidPatch_Validate_RejectsAccountAgeOutOfRange(int value)
    {
        new RaidProtectionPatchDto { MinAccountAgeHours = value }.Validate().Should().ContainKey("minAccountAgeHours");
    }

    [Fact]
    public void RaidPatch_Validate_AcceptsTheLimits()
    {
        new RaidProtectionPatchDto { MaxJoinsPerWindow = 5, WindowSeconds = 300, MinAccountAgeHours = 720 }.Validate()
            .Should().BeEmpty();
    }

    #endregion
}
