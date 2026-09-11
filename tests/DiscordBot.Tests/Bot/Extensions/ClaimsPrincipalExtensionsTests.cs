using System.Security.Claims;
using DiscordBot.Bot.Extensions;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Bot.Extensions;

/// <summary>
/// Tests for <see cref="ClaimsPrincipalExtensions"/>. The claim type must match what
/// <c>DiscordClaimsTransformation</c> actually issues — a mismatch silently degrades every
/// caller to user ID 0 (audio moderation log entries with no user, skipped VOX history).
/// </summary>
public class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "TestAuth"));

    [Fact]
    public void GetDiscordUserId_ReturnsSnowflake_FromTransformationClaim()
    {
        var principal = PrincipalWith(new Claim("discord:user_id", "123456789012345678"));

        principal.GetDiscordUserId().Should().Be(123456789012345678UL);
    }

    [Fact]
    public void GetDiscordUserId_UsesClaimTypeConstant()
    {
        var principal = PrincipalWith(
            new Claim(ClaimsPrincipalExtensions.DiscordUserIdClaimType, "987654321098765432"));

        principal.GetDiscordUserId().Should().Be(987654321098765432UL);
    }

    [Fact]
    public void GetDiscordUserId_ReturnsZero_WhenClaimMissing()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.Name, "TestUser"));

        principal.GetDiscordUserId().Should().Be(0UL);
    }

    [Fact]
    public void GetDiscordUserId_ReturnsZero_WhenClaimNotAnUnsignedLong()
    {
        var principal = PrincipalWith(new Claim("discord:user_id", "not-a-snowflake"));

        principal.GetDiscordUserId().Should().Be(0UL);
    }
}
