using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that requires moderation features to be enabled both globally and for the guild.
/// Commands using this attribute will fail if moderation is disabled at either level.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireModerationEnabledAttribute : PreconditionAttribute
{
    /// <summary>
    /// Checks if moderation features are enabled globally and for the guild.
    /// </summary>
    public override async Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        // Moderation commands require a guild context
        if (context.Guild == null)
        {
            return PreconditionResult.FromError("This command can only be used in a server.");
        }

        // Check bot-level setting first
        var settingsService = services.GetRequiredService<ISettingsService>();
        var isGloballyEnabled = await settingsService.GetSettingValueAsync<bool?>("Features:ModerationEnabled") ?? true;

        if (!isGloballyEnabled)
        {
            return PreconditionResult.FromError(
                "Moderation features have been disabled by an administrator.");
        }

        // Check guild-level setting
        var moderationConfigService = services.GetRequiredService<IGuildModerationConfigService>();
        var config = await moderationConfigService.GetConfigAsync(context.Guild.Id);

        if (!config.IsEnabled)
        {
            return PreconditionResult.FromError(
                "Moderation features are disabled for this server. An administrator can enable them in the admin panel.");
        }

        return PreconditionResult.FromSuccess();
    }
}
