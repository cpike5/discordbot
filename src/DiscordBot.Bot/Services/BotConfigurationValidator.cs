using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Validates <see cref="BotConfiguration"/> at startup. <see cref="BotConfiguration.Token"/> is
/// required only when <see cref="BotConfiguration.Enabled"/> is true, so the web portal can run
/// without a Discord bot token when <c>Discord:Enabled</c> is set to false (web-only mode; see
/// CLAUDE.md "Running it locally" and docs/articles/configuration-guide.md).
/// </summary>
public class BotConfigurationValidator : IValidateOptions<BotConfiguration>
{
    public ValidateOptionsResult Validate(string? name, BotConfiguration options)
    {
        if (options.Enabled && string.IsNullOrWhiteSpace(options.Token))
        {
            return ValidateOptionsResult.Fail(
                "Discord:Token is required when Discord:Enabled is true. Set it via environment " +
                "variable Discord__Token or user secrets, or set Discord:Enabled to false to run " +
                "web-only.");
        }

        return ValidateOptionsResult.Success;
    }
}
