using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that requires the Rat Watch feature to be enabled both globally and for the guild.
/// Commands using this attribute will fail if Rat Watch is disabled at either level.
/// </summary>
public class RequireRatWatchEnabledAttribute : PreconditionAttribute
{
    /// <summary>
    /// Checks if the Rat Watch feature is enabled globally and for the guild.
    /// </summary>
    public override async Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        // Rat Watch commands require a guild context
        if (context.Guild == null)
        {
            return PreconditionResult.FromError("This command can only be used in a server.");
        }

        // Check bot-level setting first
        var settingsService = services.GetRequiredService<ISettingsService>();

        // Default to true (enabled) if setting doesn't exist
        var isEnabled = await settingsService.GetSettingValueAsync<bool?>("Features:RatWatchEnabled") ?? true;

        if (!isEnabled)
        {
            return PreconditionResult.FromError(
                "The Rat Watch feature has been disabled by an administrator.");
        }

        // Check guild-level setting
        var ratWatchSettingsRepo = services.GetRequiredService<IGuildRatWatchSettingsRepository>();
        var settings = await ratWatchSettingsRepo.GetOrCreateAsync(context.Guild.Id);

        if (!settings.IsEnabled)
        {
            return PreconditionResult.FromError(
                "Rat Watch is disabled for this server.");
        }

        return PreconditionResult.FromSuccess();
    }
}
