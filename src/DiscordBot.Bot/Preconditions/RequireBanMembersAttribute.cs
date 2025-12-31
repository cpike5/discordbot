using Discord;
using Discord.Interactions;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that requires the user to have BanMembers permission in the guild.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireBanMembersAttribute : PreconditionAttribute
{
    /// <summary>
    /// Checks if the user executing the command has BanMembers permission.
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

        // Check if the user has BanMembers permission
        if (guildUser.GuildPermissions.BanMembers)
        {
            return Task.FromResult(PreconditionResult.FromSuccess());
        }

        return Task.FromResult(
            PreconditionResult.FromError("You must have BanMembers permission to use this command.")
        );
    }
}
