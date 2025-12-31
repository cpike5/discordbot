using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that requires the user to have ManageMessages permission OR a "Moderator" role.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireModeratorAttribute : PreconditionAttribute
{
    /// <summary>
    /// Checks if the user executing the command has ManageMessages permission or Moderator role.
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

        // Cast the user to SocketGuildUser to access guild permissions and roles
        if (context.User is not SocketGuildUser guildUser)
        {
            return Task.FromResult(
                PreconditionResult.FromError("Unable to retrieve guild user information.")
            );
        }

        // Check if the user has ManageMessages permission
        if (guildUser.GuildPermissions.ManageMessages)
        {
            return Task.FromResult(PreconditionResult.FromSuccess());
        }

        // Check if the user has a "Moderator" role (case-insensitive)
        var hasModeratorRole = guildUser.Roles.Any(r =>
            r.Name.Equals("Moderator", StringComparison.OrdinalIgnoreCase));

        if (hasModeratorRole)
        {
            return Task.FromResult(PreconditionResult.FromSuccess());
        }

        return Task.FromResult(
            PreconditionResult.FromError("You must have ManageMessages permission or a Moderator role to use this command.")
        );
    }
}
