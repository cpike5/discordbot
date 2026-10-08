using System.Reflection;
using Discord.Interactions;
using DiscordBot.Bot.Commands;
using DiscordBot.Bot.Preconditions;
using FluentAssertions;

namespace DiscordBot.Tests.Commands;

/// <summary>
/// Guards the preconditions on <see cref="VoiceModule"/>'s commands.
/// </summary>
public class VoiceModulePreconditionTests
{
    [Fact]
    public void JoinChannel_RequiresAModerator()
    {
        // /join-channel places the bot in any voice channel, so without a precondition any member
        // could pull it away from the people using it.
        var method = typeof(VoiceModule).GetMethod(nameof(VoiceModule.JoinChannelAsync))!;

        method.GetCustomAttribute<SlashCommandAttribute>()!.Name.Should().Be("join-channel");
        method.GetCustomAttribute<RequireModeratorAttribute>().Should().NotBeNull();
    }

    [Fact]
    public void Join_RequiresTheCallerToBeInAVoiceChannel()
    {
        var method = typeof(VoiceModule).GetMethod(nameof(VoiceModule.JoinAsync))!;

        method.GetCustomAttribute<RequireVoiceChannelAttribute>().Should().NotBeNull();
    }
}
