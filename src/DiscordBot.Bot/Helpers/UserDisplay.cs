namespace DiscordBot.Bot.Helpers;

/// <summary>
/// How a Discord user the bot could not look up is shown. <c>IDiscordUserResolver</c> answers
/// <c>Unknown#&lt;id&gt;</c> for those; a raw snowflake is not something an admin can act on, so the
/// screens say "Unknown user" and keep the id (where it matters) out of the main text.
/// </summary>
public static class UserDisplay
{
    /// <summary>The text for a user that could not be resolved.</summary>
    public const string UnknownName = "Unknown user";

    private const string ResolverFallbackPrefix = "Unknown#";

    /// <summary>The name to show for a resolver result.</summary>
    public static string Name(string? resolvedUsername)
    {
        return string.IsNullOrWhiteSpace(resolvedUsername)
               || resolvedUsername.StartsWith(ResolverFallbackPrefix, StringComparison.Ordinal)
            ? UnknownName
            : resolvedUsername;
    }

    /// <summary>True when <see cref="Name"/> had to fall back because the user could not be resolved.</summary>
    public static bool IsUnknown(string? resolvedUsername) => Name(resolvedUsername) == UnknownName;
}
