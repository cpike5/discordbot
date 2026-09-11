using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that requires the virtual currency feature to be enabled.
/// <para>
/// Two gates, mirroring <see cref="RequireRatWatchEnabledAttribute"/>: the configuration flag
/// <c>Currency:Enabled</c>, which decides whether <c>AddCurrency</c> ran at all (so the currency
/// services are simply absent when it is off), and the bot-level setting
/// <c>Features:CurrencyEnabled</c>, which an administrator can flip at runtime.
/// </para>
/// </summary>
public class RequireCurrencyEnabledAttribute : PreconditionAttribute
{
    /// <summary>
    /// Checks that the currency services are registered and the feature is not switched off.
    /// </summary>
    public override async Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        // Currency commands are guild-scoped: which currencies are visible depends on the guild.
        if (context.Guild == null)
        {
            return PreconditionResult.FromError("This command can only be used in a server.");
        }

        // With Currency:Enabled false the feature's DI extension never runs, so the service is
        // missing rather than disabled. That is the configuration rollback path.
        if (services.GetService<ICurrencyService>() == null)
        {
            return PreconditionResult.FromError(
                "The currency feature is not enabled on this bot.");
        }

        var settingsService = services.GetRequiredService<ISettingsService>();
        var isEnabled = await settingsService.GetSettingValueAsync<bool?>("Features:CurrencyEnabled") ?? true;

        if (!isEnabled)
        {
            return PreconditionResult.FromError(
                "The currency feature has been disabled by an administrator.");
        }

        return PreconditionResult.FromSuccess();
    }
}
