using Discord;
using Discord.Interactions;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that requires the user to have Administrator permission in the guild.
/// </summary>
public class RequireAdminAttribute : PreconditionAttribute
{
    /// <summary>
    /// Checks if the user executing the command has Administrator permission.
    /// </summary>
    public override Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        // Check if the command is being used in a guild (not DM)
        if (context.Guild == null)
        {
            return Task.FromResult(
                PreconditionResult.FromError("This command can only be used in a guild (server).")
            );
        }

        // Cast the user to IGuildUser to access guild permissions
        if (context.User is not IGuildUser guildUser)
        {
            return Task.FromResult(
                PreconditionResult.FromError("Unable to retrieve guild user information.")
            );
        }

        // Check if the user has Administrator permission
        if (guildUser.GuildPermissions.Administrator)
        {
            return Task.FromResult(PreconditionResult.FromSuccess());
        }

        return Task.FromResult(
            PreconditionResult.FromError("You must have Administrator permission to use this command.")
        );
    }
}
