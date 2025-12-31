using System.Security.Claims;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for <see cref="ClaimsPrincipal"/> to simplify access to claims data.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Retrieves the Discord user ID from claims.
    /// </summary>
    /// <param name="principal">The claims principal.</param>
    /// <returns>The Discord user snowflake ID, or 0 if not found or invalid.</returns>
    public static ulong GetDiscordUserId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst("DiscordId");
        if (claim != null && ulong.TryParse(claim.Value, out var userId))
        {
            return userId;
        }

        return 0;
    }
}
