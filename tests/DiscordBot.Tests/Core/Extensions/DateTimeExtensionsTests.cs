using DiscordBot.Core.Extensions;
using FluentAssertions;

namespace DiscordBot.Tests.Core.Extensions;

/// <summary>
/// Unit tests for <see cref="DateTimeExtensions"/>.
/// </summary>
public class DateTimeExtensionsTests
{
    #region ToUtcIso Tests

    [Fact]
    public void ToUtcIso_UtcDateTime_ReturnsCorrectIso8601Format()
    {
        // Arrange
        var dt = new DateTime(2024, 1, 15, 12, 30, 0, DateTimeKind.Utc);

        // Act
        var result = dt.ToUtcIso();

        // Assert
        result.Should().Be("2024-01-15T12:30:00.0000000Z");
    }

    [Fact]
    public void ToUtcIso_UtcDateTime_EndsWithZ()
    {
        // Arrange
        var dt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = dt.ToUtcIso();

        // Assert
        result.Should().EndWith("Z");
    }

    [Fact]
    public void ToUtcIso_UnspecifiedKind_TreatsAsUtcAndEndsWithZ()
    {
        // Arrange - DateTimeKind.Unspecified should be treated as UTC
        var dt = new DateTime(2024, 3, 10, 8, 45, 0, DateTimeKind.Unspecified);

        // Act
        var result = dt.ToUtcIso();

        // Assert
        result.Should().Be("2024-03-10T08:45:00.0000000Z");
        result.Should().EndWith("Z");
    }

    [Fact]
    public void ToUtcIso_UnspecifiedKind_PreservesDateAndTimeComponents()
    {
        // Arrange
        var dt = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Unspecified);

        // Act
        var result = dt.ToUtcIso();

        // Assert
        result.Should().StartWith("2025-12-31T23:59:59");
    }

    [Fact]
    public void ToUtcIso_WithSubsecondPrecision_IncludesSubseconds()
    {
        // Arrange
        var dt = new DateTime(2024, 1, 1, 0, 0, 0, 500, DateTimeKind.Utc);

        // Act
        var result = dt.ToUtcIso();

        // Assert
        result.Should().Contain("5000000");
    }

    #endregion

    #region ToDiscordTimestamp DateTime Tests

    [Fact]
    public void ToDiscordTimestamp_DateTime_DefaultStyleIsRelative()
    {
        // Arrange
        var dt = new DateTime(2024, 1, 15, 12, 30, 0, DateTimeKind.Utc);
        var expectedUnix = new DateTimeOffset(dt).ToUnixTimeSeconds();

        // Act
        var result = dt.ToDiscordTimestamp();

        // Assert
        result.Should().Be($"<t:{expectedUnix}:R>");
    }

    [Fact]
    public void ToDiscordTimestamp_DateTime_WithStyleF_ProducesCorrectTag()
    {
        // Arrange
        var dt = new DateTime(2024, 1, 15, 12, 30, 0, DateTimeKind.Utc);
        var expectedUnix = new DateTimeOffset(dt).ToUnixTimeSeconds();

        // Act
        var result = dt.ToDiscordTimestamp("F");

        // Assert
        result.Should().Be($"<t:{expectedUnix}:F>");
    }

    [Theory]
    [InlineData("t")]
    [InlineData("T")]
    [InlineData("d")]
    [InlineData("D")]
    [InlineData("f")]
    [InlineData("F")]
    [InlineData("R")]
    public void ToDiscordTimestamp_DateTime_AllValidStyles_ProduceCorrectFormat(string style)
    {
        // Arrange
        var dt = new DateTime(2024, 1, 15, 12, 30, 0, DateTimeKind.Utc);
        var expectedUnix = new DateTimeOffset(dt).ToUnixTimeSeconds();

        // Act
        var result = dt.ToDiscordTimestamp(style);

        // Assert
        result.Should().Be($"<t:{expectedUnix}:{style}>");
        result.Should().StartWith("<t:");
        result.Should().EndWith(">");
    }

    [Fact]
    public void ToDiscordTimestamp_DateTime_UnspecifiedKind_TreatsAsUtc()
    {
        // Arrange - Unspecified kind should be treated as UTC, same as explicit UTC
        var dtUnspecified = new DateTime(2024, 1, 15, 12, 30, 0, DateTimeKind.Unspecified);
        var dtUtc = new DateTime(2024, 1, 15, 12, 30, 0, DateTimeKind.Utc);

        // Act
        var resultUnspecified = dtUnspecified.ToDiscordTimestamp();
        var resultUtc = dtUtc.ToDiscordTimestamp();

        // Assert
        resultUnspecified.Should().Be(resultUtc);
    }

    [Fact]
    public void ToDiscordTimestamp_DateTime_KnownEpoch_ReturnsZero()
    {
        // Arrange - Unix epoch should produce timestamp of 0
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = epoch.ToDiscordTimestamp("R");

        // Assert
        result.Should().Be("<t:0:R>");
    }

    #endregion

    #region ToDiscordTimestamp DateTimeOffset Tests

    [Fact]
    public void ToDiscordTimestamp_DateTimeOffset_DefaultStyleIsRelative()
    {
        // Arrange
        var dto = new DateTimeOffset(2024, 1, 15, 12, 30, 0, TimeSpan.Zero);
        var expectedUnix = dto.ToUnixTimeSeconds();

        // Act
        var result = dto.ToDiscordTimestamp();

        // Assert
        result.Should().Be($"<t:{expectedUnix}:R>");
    }

    [Fact]
    public void ToDiscordTimestamp_DateTimeOffset_WithStyleF_ProducesCorrectTag()
    {
        // Arrange
        var dto = new DateTimeOffset(2024, 1, 15, 12, 30, 0, TimeSpan.Zero);
        var expectedUnix = dto.ToUnixTimeSeconds();

        // Act
        var result = dto.ToDiscordTimestamp("F");

        // Assert
        result.Should().Be($"<t:{expectedUnix}:F>");
    }

    [Fact]
    public void ToDiscordTimestamp_DateTimeOffset_WithNonUtcOffset_UsesCorrectUnixTime()
    {
        // Arrange - DateTimeOffset with non-UTC offset; Unix time is always UTC-based
        var dto = new DateTimeOffset(2024, 1, 15, 14, 30, 0, TimeSpan.FromHours(2)); // UTC+2
        var expectedUnix = dto.ToUnixTimeSeconds(); // Should be same as 12:30 UTC

        // Act
        var result = dto.ToDiscordTimestamp("R");

        // Assert
        result.Should().Be($"<t:{expectedUnix}:R>");
    }

    [Fact]
    public void ToDiscordTimestamp_DateTimeOffset_Epoch_ReturnsZero()
    {
        // Arrange
        var epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = epoch.ToDiscordTimestamp("R");

        // Assert
        result.Should().Be("<t:0:R>");
    }

    [Theory]
    [InlineData("t")]
    [InlineData("T")]
    [InlineData("d")]
    [InlineData("D")]
    [InlineData("f")]
    [InlineData("F")]
    [InlineData("R")]
    public void ToDiscordTimestamp_DateTimeOffset_AllValidStyles_ProduceCorrectFormat(string style)
    {
        // Arrange
        var dto = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var expectedUnix = dto.ToUnixTimeSeconds();

        // Act
        var result = dto.ToDiscordTimestamp(style);

        // Assert
        result.Should().Be($"<t:{expectedUnix}:{style}>");
        result.Should().StartWith("<t:");
        result.Should().EndWith(">");
    }

    #endregion
}
