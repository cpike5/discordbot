using Discord.WebSocket;
using DiscordBot.Bot.Helpers;
using DiscordBot.Core.Enums;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Helpers;

/// <summary>
/// Unit tests for <see cref="ModLogSettings"/>, the validation shared by the settings page and the API.
/// </summary>
public class ModLogSettingsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParseChannel_Blank_MeansNoChannel(string? submitted)
    {
        var error = ModLogSettings.TryParseChannel(submitted, out var channelId);

        error.Should().BeNull();
        channelId.Should().BeNull();
    }

    [Fact]
    public void TryParseChannel_Snowflake_IsParsed()
    {
        var error = ModLogSettings.TryParseChannel(" 123456789012345678 ", out var channelId);

        error.Should().BeNull();
        channelId.Should().Be(123456789012345678UL);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    public void TryParseChannel_NotASnowflake_IsRejected(string submitted)
    {
        var error = ModLogSettings.TryParseChannel(submitted, out var channelId);

        error.Should().Be(ModLogSettings.InvalidChannelMessage);
        channelId.Should().BeNull();
    }

    [Fact]
    public void ValidateChannel_NoChannel_IsFine()
    {
        var client = new DiscordSocketClient();

        ModLogSettings.ValidateChannel(client, 100, null).Should().BeNull();
    }

    [Fact]
    public void ValidateChannel_GuildNotVisible_IsSkipped()
    {
        // An offline bot must not block every save; the notifier copes with a wrong channel later.
        var client = new DiscordSocketClient();

        ModLogSettings.ValidateChannel(client, 100, 555).Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public void ValidateEvents_KnownBits_AreAccepted(int events)
    {
        ModLogSettings.ValidateEvents(events).Should().BeNull();
    }

    [Theory]
    [InlineData(8)]
    [InlineData(-1)]
    [InlineData(255)]
    public void ValidateEvents_UnknownBits_AreRejected(int events)
    {
        ModLogSettings.ValidateEvents(events).Should().Be(ModLogSettings.InvalidEventsMessage);
    }

    [Fact]
    public void All_IsEveryKind()
    {
        ((int)ModLogEventKinds.All).Should().Be(7);
    }
}
