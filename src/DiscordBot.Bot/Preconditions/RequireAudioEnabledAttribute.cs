using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that requires audio features to be enabled for the guild.
/// Commands using this attribute will fail if the guild has disabled audio features.
/// </summary>
public class RequireAudioEnabledAttribute : PreconditionAttribute
{
    /// <summary>
    /// Checks if audio features are enabled for the guild.
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
