using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that requires the guild to be active (bot not disabled for this guild).
/// Allows DM commands to pass through. If the guild is not in the database, the command is allowed.
/// </summary>
public class RequireGuildActiveAttribute : PreconditionAttribute
{
    /// <summary>
    /// Checks if the bot is active for the guild where the command is being executed.
    /// </summary>
    public override async Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        // Allow DM commands to pass through
        if (context.Guild == null)
        {
            return PreconditionResult.FromSuccess();
        }

        var guildService = services.GetRequiredService<IGuildService>();
        var guild = await guildService.GetGuildByIdAsync(context.Guild.Id, CancellationToken.None);

        // Guild not in database - allow (will be added on first interaction)
        if (guild == null)
        {
            return PreconditionResult.FromSuccess();
        }

        // Check if the bot is active for this guild
        if (!guild.IsActive)
        {
            return PreconditionResult.FromError(
                "The bot has been disabled for this server by an administrator.");
        }

        return PreconditionResult.FromSuccess();
    }
}
