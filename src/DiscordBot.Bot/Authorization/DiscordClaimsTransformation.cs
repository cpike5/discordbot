using System.Security.Claims;
using DiscordBot.Core.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace DiscordBot.Bot.Authorization;

/// <summary>
/// Transforms claims for Discord-linked users to add Discord-specific claims.
/// </summary>
public class DiscordClaimsTransformation : IClaimsTransformation
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DiscordClaimsTransformation> _logger;

    public DiscordClaimsTransformation(
        UserManager<ApplicationUser> userManager,
        ILogger<DiscordClaimsTransformation> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Skip if not authenticated
        if (principal.Identity?.IsAuthenticated != true)
        {
            return principal;
        }

        // Check if Discord claims already exist (prevent duplicate transformation)
        if (principal.HasClaim(c => c.Type == "discord:user_id"))
        {
            return principal;
        }

        var user = await _userManager.GetUserAsync(principal);
        if (user == null)
        {
            return principal;
        }

        // Create new identity with additional Discord claims
        var claimsIdentity = principal.Identity as ClaimsIdentity;
        if (claimsIdentity == null)
        {
            return principal;
        }

        // Add Discord-specific claims if user has linked their account
        if (user.DiscordUserId.HasValue)
        {
            claimsIdentity.AddClaim(new Claim("discord:user_id", user.DiscordUserId.Value.ToString()));
            claimsIdentity.AddClaim(new Claim("discord:linked", "true"));

            if (!string.IsNullOrEmpty(user.DiscordUsername))
            {
                claimsIdentity.AddClaim(new Claim("discord:username", user.DiscordUsername));
            }

            if (!string.IsNullOrEmpty(user.DiscordAvatarUrl))
            {
                claimsIdentity.AddClaim(new Claim("discord:avatar_url", user.DiscordAvatarUrl));
            }

            _logger.LogDebug("Added Discord claims for user {UserId} (Discord: {DiscordUserId})",
                user.Id, user.DiscordUserId.Value);
        }
        else
        {
            claimsIdentity.AddClaim(new Claim("discord:linked", "false"));
        }

        // Add display name claim
        if (!string.IsNullOrEmpty(user.DisplayName))
        {
            claimsIdentity.AddClaim(new Claim("display_name", user.DisplayName));
        }

        return principal;
    }
}
