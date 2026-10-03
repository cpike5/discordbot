using System.Security.Claims;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for <see cref="ClaimsPrincipal"/> to simplify access to claims data.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The claim type carrying the Discord snowflake, added by
    /// <see cref="DiscordBot.Bot.Authorization.DiscordClaimsTransformation"/> on every
    /// authenticated request for a user with a linked Discord account.
    /// </summary>
    public const string DiscordUserIdClaimType = "discord:user_id";

    /// <summary>
    /// Retrieves the Discord user ID from claims.
    /// </summary>
    /// <param name="principal">The claims principal.</param>
    /// <returns>The Discord user snowflake ID, or 0 if not found or invalid.</returns>
    public static ulong GetDiscordUserId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(DiscordUserIdClaimType);
        if (claim != null && ulong.TryParse(claim.Value, out var userId))
        {
            return userId;
        }

        return 0;
    }

    /// <summary>
    /// Retrieves the Discord user ID from claims, and says whether there was one. Use this where
    /// recording the ID 0 would be wrong, such as the reviewer of a moderation event: an
    /// account without a linked Discord account has no ID to record.
    /// </summary>
    /// <param name="principal">The claims principal.</param>
    /// <param name="discordUserId">The Discord user snowflake ID when one is linked.</param>
    /// <returns>True when the principal carries a non-zero Discord user ID.</returns>
    public static bool TryGetDiscordUserId(this ClaimsPrincipal principal, out ulong discordUserId)
    {
        discordUserId = principal.GetDiscordUserId();
        return discordUserId != 0;
    }
}
