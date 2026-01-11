using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that requires TTS features to be enabled for the guild.
/// Commands using this attribute will fail if the guild has disabled TTS.
/// </summary>
public class RequireTtsEnabledAttribute : PreconditionAttribute
{
    /// <summary>
    /// Checks if TTS features are enabled for the guild.
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
