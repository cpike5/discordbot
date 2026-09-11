using Discord;
using Discord.Interactions;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that requires the not-X feature to be enabled globally, checking both the
/// <c>NotX:Enabled</c> configuration switch and the hot-swappable
/// <c>Features:NotXEnabled</c> application setting.
///
/// Unlike <see cref="RequireAudioEnabledAttribute"/> and
/// <see cref="RequireRatWatchEnabledAttribute"/>, this deliberately does NOT check the
/// per-guild <c>NotXGuildSettings.IsEnabled</c> flag: the commands it guards are the
/// per-guild configuration surface, so gating them on that flag would make
/// <c>/notx enable</c> impossible to run once a guild had disabled itself.
/// </summary>
public class RequireNotXEnabledAttribute : PreconditionAttribute
{
    /// <summary>
    /// Checks whether not-X is enabled globally, in configuration and in settings.
    /// </summary>
    public override async Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        // not-X commands require a guild context
        if (context.Guild == null)
        {
            return PreconditionResult.FromError("This command can only be used in a server.");
        }

        // Configuration switch. When this is off the modules are not registered at all, so
        // this check only matters for the window between a config change and the restart
        // that re-registers commands.
        var options = services.GetRequiredService<IOptions<NotXOptions>>().Value;
        if (!options.Enabled)
        {
            return PreconditionResult.FromError(
                "The not-X feature has been disabled in the bot's configuration.");
        }

        // Hot-swappable bot-level setting. Defaults to enabled when the row is absent.
        var settingsService = services.GetRequiredService<ISettingsService>();
        var isGloballyEnabled = await settingsService
            .GetSettingValueAsync<bool?>("Features:NotXEnabled") ?? true;

        if (!isGloballyEnabled)
        {
            return PreconditionResult.FromError(
                "The not-X feature has been disabled by an administrator.");
        }

        return PreconditionResult.FromSuccess();
    }
}
