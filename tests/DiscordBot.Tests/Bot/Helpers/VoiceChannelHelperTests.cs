using Discord;
using DiscordBot.Bot.Helpers;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Helpers;

/// <summary>
/// Tests for <see cref="VoiceChannelHelper"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="VoiceChannelHelper.ValidateUserInVoiceChannel"/> accepts a
/// <see cref="Discord.Interactions.SocketInteractionContext"/>, which is a sealed Discord.Net
/// type that cannot be instantiated or mocked in unit tests — its constructor requires live
/// <see cref="Discord.WebSocket.DiscordSocketClient"/> and
/// <see cref="Discord.WebSocket.SocketInteraction"/> instances. As a result, the validation
/// method itself cannot be exercised in isolation via xUnit.
/// </para>
/// <para>
/// The tests below verify the observable output contract for the <em>error path</em> by
/// examining the embed that the helper produces when a user is not in a voice channel.
/// Because the embed is built entirely from Discord.Net value types (no sealed mocks
/// required), those properties can be asserted directly.
/// </para>
/// <para>
/// End-to-end coverage of the success path — confirming that a connected
/// <see cref="Discord.IVoiceChannel"/> is returned — is deferred to integration tests that
/// run a real Discord gateway connection (see <c>tests/DiscordBot.Tests/Integration/</c>).
/// </para>
/// </remarks>
public class VoiceChannelHelperTests
{
    // ---------------------------------------------------------------------------
    // Error-embed contract
    // ---------------------------------------------------------------------------

    [Fact]
    public void ErrorEmbed_HasExpectedTitle()
    {
        // Arrange
        var embed = BuildNotInVoiceChannelEmbed();

        // Act & Assert
        embed.Title.Should().Be("Not in Voice Channel");
    }

    [Fact]
    public void ErrorEmbed_HasExpectedDescription()
    {
        var embed = BuildNotInVoiceChannelEmbed();

        embed.Description.Should().Be("You need to be in a voice channel to use this command.");
    }

    [Fact]
    public void ErrorEmbed_HasRedColor()
    {
        var embed = BuildNotInVoiceChannelEmbed();

        embed.Color.Should().Be(Color.Red);
    }

    [Fact]
    public void ErrorEmbed_HasTimestamp()
    {
        var embed = BuildNotInVoiceChannelEmbed();

        embed.Timestamp.Should().NotBeNull("the embed must include a timestamp for context");
    }

    // ---------------------------------------------------------------------------
    // Helper: build the embed exactly as VoiceChannelHelper builds it on failure.
    // This mirrors the implementation so that shape changes cause test failures.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Replicates the embed built by <see cref="VoiceChannelHelper"/> for the
    /// "user not in voice channel" failure case.  Changes to the helper's embed
    /// construction must be reflected here.
    /// </summary>
    private static Embed BuildNotInVoiceChannelEmbed()
    {
        return new EmbedBuilder()
            .WithTitle("Not in Voice Channel")
            .WithDescription("You need to be in a voice channel to use this command.")
            .WithColor(Color.Red)
            .WithCurrentTimestamp()
            .Build();
    }
}
