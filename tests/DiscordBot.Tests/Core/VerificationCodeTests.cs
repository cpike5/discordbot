using System.Text.RegularExpressions;
using DiscordBot.Core.Entities;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Core;

/// <summary>
/// What the LinkDiscord countdown reads from a <see cref="VerificationCode"/>.
/// </summary>
public class VerificationCodeTests
{
    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    public void ExpiresAtUtcIso_HasThreeFractionalDigitsAndAZ_SoAStrictDateParseAcceptsIt(DateTimeKind kind)
    {
        // Ticks that would print seven fractional digits with the "o" format
        var expiresAt = DateTime.SpecifyKind(new DateTime(2026, 10, 3, 12, 15, 0, kind).AddTicks(1234567), kind);

        var iso = new VerificationCode { ExpiresAt = expiresAt }.ExpiresAtUtcIso;

        iso.Should().Be("2026-10-03T12:15:00.123Z");
        iso.Should().MatchRegex(new Regex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$"));
    }

    [Fact]
    public void SecondsRemaining_IsMeasuredByTheServerClock_AndNeverNegative()
    {
        new VerificationCode { ExpiresAt = DateTime.UtcNow.AddMinutes(15) }.SecondsRemaining
            .Should().BeInRange(14 * 60 + 58, 15 * 60);

        new VerificationCode { ExpiresAt = DateTime.UtcNow.AddMinutes(-5) }.SecondsRemaining
            .Should().Be(0);
    }
}
