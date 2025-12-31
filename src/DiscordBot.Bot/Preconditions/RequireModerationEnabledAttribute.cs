using Discord;
using Discord.Interactions;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that requires the moderation system to be enabled for the guild.
/// Configuration is created on demand if it doesn't exist, so this always succeeds for valid guilds.
/// TODO: Implement when IGuildModerationConfigService is available.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireModerationEnabledAttribute : PreconditionAttribute
{
    /// <summary>
    /// Checks if the moderation system is enabled for the guild.
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

        // TODO: Once IGuildModerationConfigService is implemented, uncomment this:
        // var moderationConfigService = services.GetRequiredService<IGuildModerationConfigService>();
        // var config = await moderationConfigService.GetConfigAsync(context.Guild.Id);

        // For now, always allow (moderation is enabled by default)
        return Task.FromResult(PreconditionResult.FromSuccess());
    }
}
