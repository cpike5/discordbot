using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that requires the user to be in a voice channel.
/// Verifies the user is a guild member with an active voice channel connection.
/// </summary>
public class RequireVoiceChannelAttribute : PreconditionAttribute
{
    /// <summary>
    /// Checks if the user is in a voice channel.
    /// </summary>
    public override Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        if (context.Guild == null)
        {
            return Task.FromResult(PreconditionResult.FromError(
                "This command can only be used in a server."));
        }

        if (context.User is not SocketGuildUser guildUser)
        {
            return Task.FromResult(PreconditionResult.FromError(
                "Could not verify user's voice state."));
        }

        if (guildUser.VoiceChannel == null)
        {
            return Task.FromResult(PreconditionResult.FromError(
                "You need to be in a voice channel to use this command."));
        }

        return Task.FromResult(PreconditionResult.FromSuccess());
    }
}
