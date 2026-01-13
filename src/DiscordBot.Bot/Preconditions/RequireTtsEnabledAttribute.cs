using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that requires TTS features to be enabled globally (audio) and for the guild.
/// Commands using this attribute will fail if audio is disabled globally or TTS is disabled for the guild.
/// </summary>
public class RequireTtsEnabledAttribute : PreconditionAttribute
{
    /// <summary>
    /// Checks if audio features are enabled globally and TTS is enabled for the guild.
    /// </summary>
    public override async Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        // TTS commands require a guild context
        if (context.Guild == null)
        {
            return PreconditionResult.FromError("This command can only be used in a server.");
        }

        // Check bot-level audio setting first (TTS is part of audio features)
        var settingsService = services.GetRequiredService<ISettingsService>();
        var isGloballyEnabled = await settingsService.GetSettingValueAsync<bool?>("Features:AudioEnabled") ?? true;

        if (!isGloballyEnabled)
        {
            return PreconditionResult.FromError(
                "Audio features have been disabled by an administrator.");
        }

        // Check guild-level TTS setting
        var ttsSettingsService = services.GetRequiredService<ITtsSettingsService>();
        var isEnabled = await ttsSettingsService.IsTtsEnabledAsync(context.Guild.Id);

        if (!isEnabled)
        {
            return PreconditionResult.FromError(
                "Text-to-speech is disabled for this server.");
        }

        return PreconditionResult.FromSuccess();
    }
}
