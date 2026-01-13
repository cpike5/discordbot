using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that requires audio features to be enabled both globally and for the guild.
/// Commands using this attribute will fail if audio is disabled at either level.
/// </summary>
public class RequireAudioEnabledAttribute : PreconditionAttribute
{
    /// <summary>
    /// Checks if audio features are enabled globally and for the guild.
    /// </summary>
    public override async Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        // Audio commands require a guild context
        if (context.Guild == null)
        {
            return PreconditionResult.FromError("This command can only be used in a server.");
        }

        // Check bot-level setting first
        var settingsService = services.GetRequiredService<ISettingsService>();
        var isGloballyEnabled = await settingsService.GetSettingValueAsync<bool?>("Features:AudioEnabled") ?? true;

        if (!isGloballyEnabled)
        {
            return PreconditionResult.FromError(
                "Audio features have been disabled by an administrator.");
        }

        // Check guild-level setting
        var audioSettingsService = services.GetRequiredService<IGuildAudioSettingsService>();
        var settings = await audioSettingsService.GetSettingsAsync(context.Guild.Id);

        if (!settings.AudioEnabled)
        {
            return PreconditionResult.FromError(
                "Audio features are disabled for this server. An administrator can enable them in the admin panel.");
        }

        return PreconditionResult.FromSuccess();
    }
}
