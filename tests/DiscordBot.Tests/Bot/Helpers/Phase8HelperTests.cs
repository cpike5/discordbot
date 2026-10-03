using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DiscordBot.Tests.Bot.Helpers;

public class FormFieldStateTests
{
    private static ChannelSelectItem Channel(ulong id, string name, ChannelDisplayType type = ChannelDisplayType.Text) =>
        new() { Id = id, Name = name, Type = type };

    [Fact]
    public void ChannelOptions_ListsANoneChoiceThenTheChannels()
    {
        var options = FormFieldState.ChannelOptions(new[] { Channel(1, "general"), Channel(2, "news", ChannelDisplayType.Announcement) },
            selectedId: 1, "No channel", out var missing);

        missing.Should().BeFalse();
        options.Select(o => o.Value).Should().Equal("", "1", "2");
        options[0].Text.Should().Be("No channel");
        options[1].Text.Should().Be("# general");
    }

    [Fact]
    public void ChannelOptions_KeepsASavedChannelTheBotCannotSee_InsteadOfDroppingIt()
    {
        var options = FormFieldState.ChannelOptions(new[] { Channel(1, "general") }, selectedId: 999, "No channel", out var missing);

        missing.Should().BeTrue();
        options.Should().Contain(o => o.Value == "999", "saving must not clear a channel just because the bot cannot list it");
        options.Single(o => o.Value == "999").Text.Should().Contain("Unknown channel");
    }

    [Fact]
    public void ChannelOptions_WithNoSavedChannel_ReportsNothingMissing()
    {
        FormFieldState.ChannelOptions(new[] { Channel(1, "general") }, selectedId: null, "None", out var missing);

        missing.Should().BeFalse();
    }

    [Fact]
    public void ChannelOptions_WhenTheBotSeesNoChannelsAtAll_StillKeepsTheSavedOne()
    {
        var options = FormFieldState.ChannelOptions(Array.Empty<ChannelSelectItem>(), selectedId: 5, "None", out var missing);

        missing.Should().BeTrue();
        options.Select(o => o.Value).Should().Equal("", "5");
    }

    [Fact]
    public void FieldError_ReturnsTheFirstMessageForTheKey()
    {
        var state = new ModelStateDictionary();
        state.AddModelError("Input.Title", "Title is required.");
        state.AddModelError("Input.Title", "Second.");

        state.FieldError("Input.Title").Should().Be("Title is required.");
        state.FieldError("Input.Other").Should().BeNull();
        FormFieldState.StateOf("x").Should().Be(ValidationState.Error);
        FormFieldState.StateOf(null).Should().Be(ValidationState.None);
    }
}

public class UserDisplayTests
{
    [Theory]
    [InlineData("alice", "alice")]
    [InlineData("Unknown#123456789012345678", "Unknown user")]
    [InlineData("", "Unknown user")]
    [InlineData(null, "Unknown user")]
    public void Name_HidesTheResolversRawIdFallback(string? resolved, string expected)
    {
        UserDisplay.Name(resolved).Should().Be(expected);
    }

    [Fact]
    public void IsUnknown_IsTrueOnlyForAFallback()
    {
        UserDisplay.IsUnknown("Unknown#5").Should().BeTrue();
        UserDisplay.IsUnknown("bob").Should().BeFalse();
    }
}

public class FeatureRequestDisplayTests
{
    [Theory]
    [InlineData(FeatureRequestStatus.DocGenFailed, "Documentation failed")]
    [InlineData(FeatureRequestStatus.GeneratingDocs, "Writing documentation")]
    [InlineData(FeatureRequestStatus.Approved, "Approved")]
    public void Label_UsesPlainWords(FeatureRequestStatus status, string expected)
    {
        FeatureRequestDisplay.Label(status).Should().Be(expected);
    }

    [Fact]
    public void Excerpt_CollapsesLinesAndTrims()
    {
        FeatureRequestDisplay.Excerpt("one\r\n\r\ntwo\nthree").Should().Be("one two three");
    }

    [Fact]
    public void Excerpt_TruncatesLongTextWithoutSplittingTheEnd()
    {
        var text = new string('a', 100);

        FeatureRequestDisplay.Excerpt(text, 80).Should().HaveLength(83).And.EndWith("...");
    }
}
