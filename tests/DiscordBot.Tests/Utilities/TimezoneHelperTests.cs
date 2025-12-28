using DiscordBot.Core.Utilities;
using FluentAssertions;

namespace DiscordBot.Tests.Utilities;

/// <summary>
/// Unit tests for <see cref="TimezoneHelper"/>.
/// </summary>
public class TimezoneHelperTests
{
    #region IsValidTimezone Tests

    [Theory]
    [InlineData("America/New_York")]
    [InlineData("Europe/London")]
    [InlineData("Asia/Tokyo")]
    [InlineData("UTC")]
    [InlineData("America/Los_Angeles")]
    [InlineData("Australia/Sydney")]
    public void IsValidTimezone_ValidIanaTimezone_ReturnsTrue(string timezone)
    {
        // Act
        var result = TimezoneHelper.IsValidTimezone(timezone);

        // Assert
        result.Should().BeTrue($"{timezone} is a valid IANA timezone identifier");
    }

    [Theory]
    [InlineData("Invalid/Zone")]
    [InlineData("NotATimezone")]
    [InlineData("America/FakeCity")]
    [InlineData("Etc/GMT+25")]
    public void IsValidTimezone_InvalidTimezone_ReturnsFalse(string timezone)
    {
        // Act
        var result = TimezoneHelper.IsValidTimezone(timezone);

        // Assert
        result.Should().BeFalse($"{timezone} is not a valid IANA timezone identifier");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidTimezone_NullOrEmptyString_ReturnsFalse(string? timezone)
    {
        // Act
        var result = TimezoneHelper.IsValidTimezone(timezone);

        // Assert
        result.Should().BeFalse("null, empty, or whitespace timezone should be invalid");
    }

    #endregion

    #region GetTimeZoneInfo Tests

    [Fact]
    public void GetTimeZoneInfo_ValidIanaTimezone_ReturnsCorrectTimeZoneInfo()
    {
        // Arrange
        const string timezone = "America/New_York";

        // Act
        var result = TimezoneHelper.GetTimeZoneInfo(timezone);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("America/New_York");
    }

    [Fact]
    public void GetTimeZoneInfo_UtcTimezone_ReturnsUtcTimeZoneInfo()
    {
        // Arrange
        const string timezone = "UTC";

        // Act
        var result = TimezoneHelper.GetTimeZoneInfo(timezone);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("UTC");
    }

    [Theory]
    [InlineData("Invalid/Zone")]
    [InlineData("NotATimezone")]
    [InlineData("America/FakeCity")]
    public void GetTimeZoneInfo_InvalidTimezone_ReturnsUtcAsFallback(string timezone)
    {
        // Act
        var result = TimezoneHelper.GetTimeZoneInfo(timezone);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("UTC", "invalid timezones should fallback to UTC");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetTimeZoneInfo_NullOrEmpty_ReturnsUtc(string? timezone)
    {
        // Act
        var result = TimezoneHelper.GetTimeZoneInfo(timezone);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("UTC", "null or empty timezone should fallback to UTC");
    }

    #endregion

    #region ConvertToUtc Tests

    [Fact]
    public void ConvertToUtc_EstTimeToUtc_ConvertsCorrectly()
    {
        // Arrange - January 15, 2024 12:00 PM EST (UTC-5 during winter)
        var localDateTime = new DateTime(2024, 1, 15, 12, 0, 0);
        const string timezone = "America/New_York";

        // Act
        var result = TimezoneHelper.ConvertToUtc(localDateTime, timezone);

        // Assert
        result.Kind.Should().Be(DateTimeKind.Utc, "result should be marked as UTC");
        result.Should().Be(new DateTime(2024, 1, 15, 17, 0, 0, DateTimeKind.Utc),
            "12:00 PM EST should be 5:00 PM UTC (EST is UTC-5)");
    }

    [Fact]
    public void ConvertToUtc_EdtTimeToUtc_ConvertsCorrectlyDuringDst()
    {
        // Arrange - July 15, 2024 12:00 PM EDT (UTC-4 during summer DST)
        var localDateTime = new DateTime(2024, 7, 15, 12, 0, 0);
        const string timezone = "America/New_York";

        // Act
        var result = TimezoneHelper.ConvertToUtc(localDateTime, timezone);

        // Assert
        result.Kind.Should().Be(DateTimeKind.Utc, "result should be marked as UTC");
        result.Should().Be(new DateTime(2024, 7, 15, 16, 0, 0, DateTimeKind.Utc),
            "12:00 PM EDT should be 4:00 PM UTC (EDT is UTC-4)");
    }

    [Fact]
    public void ConvertToUtc_InvalidTimezone_UsesUtcAsFallback()
    {
        // Arrange
        var localDateTime = new DateTime(2024, 1, 15, 12, 0, 0);
        const string timezone = "Invalid/Zone";

        // Act
        var result = TimezoneHelper.ConvertToUtc(localDateTime, timezone);

        // Assert
        result.Kind.Should().Be(DateTimeKind.Utc, "result should be marked as UTC");
        result.Should().Be(new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc),
            "invalid timezone should treat input as already UTC");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertToUtc_NullOrEmptyTimezone_TreatsInputAsUtc(string? timezone)
    {
        // Arrange
        var localDateTime = new DateTime(2024, 1, 15, 12, 0, 0);

        // Act
        var result = TimezoneHelper.ConvertToUtc(localDateTime, timezone);

        // Assert
        result.Kind.Should().Be(DateTimeKind.Utc, "result should be marked as UTC");
        result.Should().Be(new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc),
            "null or empty timezone should treat input as already UTC");
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc)]
    public void ConvertToUtc_HandlesDateTimeKindCorrectly(DateTimeKind kind)
    {
        // Arrange - The method should ignore the input DateTimeKind
        var localDateTime = new DateTime(2024, 1, 15, 12, 0, 0, kind);
        const string timezone = "America/New_York";

        // Act
        var result = TimezoneHelper.ConvertToUtc(localDateTime, timezone);

        // Assert
        result.Kind.Should().Be(DateTimeKind.Utc, "result should always be marked as UTC");
        result.Should().Be(new DateTime(2024, 1, 15, 17, 0, 0, DateTimeKind.Utc),
            "conversion should work regardless of input DateTimeKind");
    }

    [Fact]
    public void ConvertToUtc_PstTimeToUtc_ConvertsCorrectly()
    {
        // Arrange - January 15, 2024 12:00 PM PST (UTC-8 during winter)
        var localDateTime = new DateTime(2024, 1, 15, 12, 0, 0);
        const string timezone = "America/Los_Angeles";

        // Act
        var result = TimezoneHelper.ConvertToUtc(localDateTime, timezone);

        // Assert
        result.Kind.Should().Be(DateTimeKind.Utc, "result should be marked as UTC");
        result.Should().Be(new DateTime(2024, 1, 15, 20, 0, 0, DateTimeKind.Utc),
            "12:00 PM PST should be 8:00 PM UTC (PST is UTC-8)");
    }

    #endregion

    #region ConvertFromUtc Tests

    [Fact]
    public void ConvertFromUtc_UtcToEst_ConvertsCorrectly()
    {
        // Arrange - January 15, 2024 5:00 PM UTC
        var utcDateTime = new DateTime(2024, 1, 15, 17, 0, 0, DateTimeKind.Utc);
        const string timezone = "America/New_York";

        // Act
        var result = TimezoneHelper.ConvertFromUtc(utcDateTime, timezone);

        // Assert
        result.Should().Be(new DateTime(2024, 1, 15, 12, 0, 0),
            "5:00 PM UTC should be 12:00 PM EST (EST is UTC-5)");
    }

    [Fact]
    public void ConvertFromUtc_UtcToEdt_ConvertsCorrectlyDuringDst()
    {
        // Arrange - July 15, 2024 4:00 PM UTC
        var utcDateTime = new DateTime(2024, 7, 15, 16, 0, 0, DateTimeKind.Utc);
        const string timezone = "America/New_York";

        // Act
        var result = TimezoneHelper.ConvertFromUtc(utcDateTime, timezone);

        // Assert
        result.Should().Be(new DateTime(2024, 7, 15, 12, 0, 0),
            "4:00 PM UTC should be 12:00 PM EDT (EDT is UTC-4)");
    }

    [Fact]
    public void ConvertFromUtc_InvalidTimezone_ReturnsUtcUnchanged()
    {
        // Arrange
        var utcDateTime = new DateTime(2024, 1, 15, 17, 0, 0, DateTimeKind.Utc);
        const string timezone = "Invalid/Zone";

        // Act
        var result = TimezoneHelper.ConvertFromUtc(utcDateTime, timezone);

        // Assert
        result.Should().Be(new DateTime(2024, 1, 15, 17, 0, 0, DateTimeKind.Utc),
            "invalid timezone should return UTC time unchanged");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertFromUtc_NullOrEmptyTimezone_ReturnsUtcUnchanged(string? timezone)
    {
        // Arrange
        var utcDateTime = new DateTime(2024, 1, 15, 17, 0, 0, DateTimeKind.Utc);

        // Act
        var result = TimezoneHelper.ConvertFromUtc(utcDateTime, timezone);

        // Assert
        result.Should().Be(utcDateTime, "null or empty timezone should return UTC time unchanged");
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    public void ConvertFromUtc_InputWithWrongKind_ConvertsCorrectly(DateTimeKind kind)
    {
        // Arrange - The method should convert the kind to UTC before conversion
        var utcDateTime = new DateTime(2024, 1, 15, 17, 0, 0, kind);
        const string timezone = "America/New_York";

        // Act
        var result = TimezoneHelper.ConvertFromUtc(utcDateTime, timezone);

        // Assert
        result.Should().Be(new DateTime(2024, 1, 15, 12, 0, 0),
            "conversion should work even if input DateTimeKind is not UTC");
    }

    [Fact]
    public void ConvertFromUtc_UtcToPst_ConvertsCorrectly()
    {
        // Arrange - January 15, 2024 8:00 PM UTC
        var utcDateTime = new DateTime(2024, 1, 15, 20, 0, 0, DateTimeKind.Utc);
        const string timezone = "America/Los_Angeles";

        // Act
        var result = TimezoneHelper.ConvertFromUtc(utcDateTime, timezone);

        // Assert
        result.Should().Be(new DateTime(2024, 1, 15, 12, 0, 0),
            "8:00 PM UTC should be 12:00 PM PST (PST is UTC-8)");
    }

    #endregion

    #region GetTimezoneAbbreviation Tests

    [Fact]
    public void GetTimezoneAbbreviation_NewYorkInWinter_ReturnsEstOrStandardName()
    {
        // Arrange - January 15, 2024 is during EST (not DST)
        var dateTime = new DateTime(2024, 1, 15, 12, 0, 0);
        const string timezone = "America/New_York";

        // Act
        var result = TimezoneHelper.GetTimezoneAbbreviation(timezone, dateTime);

        // Assert
        result.Should().NotBeNullOrEmpty("abbreviation should be returned for valid timezone");
        // Note: On some systems this returns "Eastern Standard Time" instead of "EST"
        // We just verify it's not empty and doesn't say "Daylight"
        result.Should().NotContain("Daylight", "January should use standard time, not daylight time");
    }

    [Fact]
    public void GetTimezoneAbbreviation_NewYorkInSummer_ReturnsEdtOrDaylightName()
    {
        // Arrange - July 15, 2024 is during EDT (DST active)
        var dateTime = new DateTime(2024, 7, 15, 12, 0, 0);
        const string timezone = "America/New_York";

        // Act
        var result = TimezoneHelper.GetTimezoneAbbreviation(timezone, dateTime);

        // Assert
        result.Should().NotBeNullOrEmpty("abbreviation should be returned for valid timezone");
        // Note: On some systems this returns "Eastern Daylight Time" instead of "EDT"
        // We just verify it contains "Daylight"
        result.Should().Contain("Daylight", "July should use daylight saving time");
    }

    [Theory]
    [InlineData("Invalid/Zone")]
    [InlineData("NotATimezone")]
    [InlineData("America/FakeCity")]
    public void GetTimezoneAbbreviation_InvalidTimezone_ReturnsUtc(string timezone)
    {
        // Arrange
        var dateTime = new DateTime(2024, 1, 15, 12, 0, 0);

        // Act
        var result = TimezoneHelper.GetTimezoneAbbreviation(timezone, dateTime);

        // Assert
        result.Should().Be("UTC", "invalid timezone should fallback to UTC");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetTimezoneAbbreviation_NullOrEmptyTimezone_ReturnsUtc(string? timezone)
    {
        // Arrange
        var dateTime = new DateTime(2024, 1, 15, 12, 0, 0);

        // Act
        var result = TimezoneHelper.GetTimezoneAbbreviation(timezone, dateTime);

        // Assert
        result.Should().Be("UTC", "null or empty timezone should return UTC");
    }

    [Fact]
    public void GetTimezoneAbbreviation_UtcTimezone_ReturnsUtc()
    {
        // Arrange
        var dateTime = new DateTime(2024, 1, 15, 12, 0, 0);
        const string timezone = "UTC";

        // Act
        var result = TimezoneHelper.GetTimezoneAbbreviation(timezone, dateTime);

        // Assert
        result.Should().Be("UTC", "UTC timezone should return UTC abbreviation");
    }

    [Fact]
    public void GetTimezoneAbbreviation_TokyoTimezone_ReturnsAppropriateAbbreviation()
    {
        // Arrange - Japan doesn't observe DST
        var dateTime = new DateTime(2024, 1, 15, 12, 0, 0);
        const string timezone = "Asia/Tokyo";

        // Act
        var result = TimezoneHelper.GetTimezoneAbbreviation(timezone, dateTime);

        // Assert
        result.Should().NotBeNullOrEmpty("abbreviation should be returned for valid timezone");
        // Japan uses JST (Japan Standard Time) year-round
        result.Should().NotContain("Daylight", "Tokyo does not observe daylight saving time");
    }

    #endregion

    #region Round-Trip Conversion Tests

    [Fact]
    public void ConvertToUtcAndBack_RoundTrip_ProducesOriginalDateTime()
    {
        // Arrange
        var originalDateTime = new DateTime(2024, 1, 15, 12, 0, 0);
        const string timezone = "America/New_York";

        // Act
        var utcDateTime = TimezoneHelper.ConvertToUtc(originalDateTime, timezone);
        var roundTripDateTime = TimezoneHelper.ConvertFromUtc(utcDateTime, timezone);

        // Assert
        roundTripDateTime.Should().Be(originalDateTime,
            "converting to UTC and back should produce the original datetime");
    }

    [Fact]
    public void ConvertFromUtcAndBack_RoundTrip_ProducesOriginalDateTime()
    {
        // Arrange
        var originalUtcDateTime = new DateTime(2024, 1, 15, 17, 0, 0, DateTimeKind.Utc);
        const string timezone = "America/New_York";

        // Act
        var localDateTime = TimezoneHelper.ConvertFromUtc(originalUtcDateTime, timezone);
        var roundTripUtcDateTime = TimezoneHelper.ConvertToUtc(localDateTime, timezone);

        // Assert
        roundTripUtcDateTime.Should().Be(originalUtcDateTime,
            "converting from UTC and back should produce the original UTC datetime");
    }

    [Theory]
    [InlineData("America/New_York")]
    [InlineData("Europe/London")]
    [InlineData("Asia/Tokyo")]
    [InlineData("America/Los_Angeles")]
    [InlineData("Australia/Sydney")]
    public void ConvertToUtcAndBack_MultipleTimezones_RoundTripSucceeds(string timezone)
    {
        // Arrange
        var originalDateTime = new DateTime(2024, 1, 15, 12, 0, 0);

        // Act
        var utcDateTime = TimezoneHelper.ConvertToUtc(originalDateTime, timezone);
        var roundTripDateTime = TimezoneHelper.ConvertFromUtc(utcDateTime, timezone);

        // Assert
        roundTripDateTime.Should().Be(originalDateTime,
            $"round-trip conversion for {timezone} should preserve the original datetime");
    }

    #endregion
}
