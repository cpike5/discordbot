using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that requires the Rat Watch feature to be enabled globally.
/// Checks the Features:RatWatchEnabled setting.
/// </summary>
public class RequireRatWatchEnabledAttribute : PreconditionAttribute
{
    /// <summary>
    /// Checks if the Rat Watch feature is enabled.
    /// </summary>
    public override async Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        var settingsService = services.GetRequiredService<ISettingsService>();

        // Default to true (enabled) if setting doesn't exist
        var isEnabled = await settingsService.GetSettingValueAsync<bool?>("Features:RatWatchEnabled") ?? true;

        if (!isEnabled)
        {
            return PreconditionResult.FromError(
                "The Rat Watch feature has been disabled by an administrator.");
        }

        return PreconditionResult.FromSuccess();
    }
}
