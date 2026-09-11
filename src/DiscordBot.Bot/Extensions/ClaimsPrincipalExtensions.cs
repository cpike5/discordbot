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
}
