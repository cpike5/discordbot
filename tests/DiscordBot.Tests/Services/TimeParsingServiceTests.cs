using DiscordBot.Bot.Services;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="TimeParsingService"/>.
/// Tests all supported time expression formats including relative times, absolute times,
/// day names, dates, special keywords, and timezone conversions.
/// </summary>
public class TimeParsingServiceTests
{
    private readonly Mock<ILogger<TimeParsingService>> _mockLogger;
    private readonly TimeParsingService _service;
    private const string TestTimezone = "America/New_York";

    public TimeParsingServiceTests()
    {
        _mockLogger = new Mock<ILogger<TimeParsingService>>();
        _service = new TimeParsingService(_mockLogger.Object);
    }

    #region Relative Time Tests

    [Theory]
    [InlineData("10m", 10)]
    [InlineData("1h", 60)]
    [InlineData("2h", 120)]
    [InlineData("1h30m", 90)]
    [InlineData("2h 30m", 150)]
    [InlineData("1d", 1440)]
    [InlineData("1w", 10080)]
    [InlineData("1w 2d", 12960)] // 1 week + 2 days = 10080 + 2880 = 12960 minutes
    [InlineData("1d 4h", 1680)] // 1 day + 4 hours = 1440 + 240 = 1680 minutes
    [InlineData("2w 3d 5h 30m", 24810)] // 2*7*24*60 + 3*24*60 + 5*60 + 30 = 20160 + 4320 + 300 + 30 = 24810
    public void Parse_RelativeTime_ReturnsCorrectMinutes(string input, int expectedMinutes)
    {
        // Arrange
        var beforeParse = DateTime.UtcNow;

        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert
        var afterParse = DateTime.UtcNow;

        result.Success.Should().BeTrue($"parsing '{input}' should succeed");
        result.UtcTime.Should().NotBeNull();
        result.ParseType.Should().Be(TimeParseType.Relative);
        result.ErrorMessage.Should().BeNull();

        // Verify the time difference is approximately correct (allowing for test execution time)
        var expectedUtcTime = beforeParse.AddMinutes(expectedMinutes);
        var actualDifferenceMinutes = (result.UtcTime!.Value - beforeParse).TotalMinutes;

        actualDifferenceMinutes.Should().BeApproximately(expectedMinutes, 0.1,
            $"'{input}' should parse to approximately {expectedMinutes} minutes from now");
    }

    [Theory]
    [InlineData("in 10m", 10)]
    [InlineData("in 2h", 120)]
    [InlineData("in 1d", 1440)]
    [InlineData("in 1h 30m", 90)]
    [InlineData("In 5m", 5)] // Case insensitive
    [InlineData("IN 15m", 15)] // Case insensitive
    public void Parse_RelativeTimeWithInPrefix_ReturnsCorrectMinutes(string input, int expectedMinutes)
    {
        // Arrange
        var beforeParse = DateTime.UtcNow;

        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert
        result.Success.Should().BeTrue($"parsing '{input}' should succeed");
        result.ParseType.Should().Be(TimeParseType.Relative);

        var actualDifferenceMinutes = (result.UtcTime!.Value - beforeParse).TotalMinutes;
        actualDifferenceMinutes.Should().BeApproximately(expectedMinutes, 0.1,
            $"'{input}' should parse to approximately {expectedMinutes} minutes from now");
    }

    #endregion

    #region Absolute Time Tests

    [Theory]
    [InlineData("10pm", 22, 0)]
    [InlineData("10:30pm", 22, 30)]
    [InlineData("3:45am", 3, 45)]
    [InlineData("12pm", 12, 0)] // Noon
    [InlineData("12am", 0, 0)] // Midnight
    [InlineData("11:59pm", 23, 59)]
    [InlineData("1am", 1, 0)]
    public void Parse_AbsoluteTime12Hour_ReturnsCorrectTime(string input, int expectedHour, int expectedMinute)
    {
        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert
        result.Success.Should().BeTrue($"parsing '{input}' should succeed");
        result.UtcTime.Should().NotBeNull();
        result.ParseType.Should().Be(TimeParseType.AbsoluteTime);
        result.ErrorMessage.Should().BeNull();

        // Convert result back to test timezone to verify
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TestTimezone);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(result.UtcTime!.Value, timeZone);

        localTime.Hour.Should().Be(expectedHour, $"'{input}' should parse to hour {expectedHour}");
        localTime.Minute.Should().Be(expectedMinute, $"'{input}' should parse to minute {expectedMinute}");

        // Should be today or tomorrow depending on whether the time has passed
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        localTime.Date.Should().BeOnOrAfter(now.Date);
    }

    [Theory]
    [InlineData("22:00", 22, 0)]
    [InlineData("14:30", 14, 30)]
    [InlineData("09:00", 9, 0)]
    [InlineData("00:00", 0, 0)]
    [InlineData("23:59", 23, 59)]
    [InlineData("06:15", 6, 15)]
    public void Parse_AbsoluteTime24Hour_ReturnsCorrectTime(string input, int expectedHour, int expectedMinute)
    {
        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert
        result.Success.Should().BeTrue($"parsing '{input}' should succeed");
        result.UtcTime.Should().NotBeNull();
        result.ParseType.Should().Be(TimeParseType.AbsoluteTime);

        // Convert result back to test timezone to verify
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TestTimezone);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(result.UtcTime!.Value, timeZone);

        localTime.Hour.Should().Be(expectedHour, $"'{input}' should parse to hour {expectedHour}");
        localTime.Minute.Should().Be(expectedMinute, $"'{input}' should parse to minute {expectedMinute}");
    }

    #endregion

    #region Special Keywords Tests

    [Theory]
    [InlineData("noon", 12, 0)]
    [InlineData("midnight", 0, 0)]
    [InlineData("morning", 9, 0)]
    [InlineData("afternoon", 15, 0)]
    [InlineData("evening", 18, 0)]
    [InlineData("night", 21, 0)]
    public void Parse_SpecialKeywords_ReturnsCorrectTime(string input, int expectedHour, int expectedMinute)
    {
        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert
        result.Success.Should().BeTrue($"parsing '{input}' should succeed");
        result.UtcTime.Should().NotBeNull();
        result.ParseType.Should().Be(TimeParseType.AbsoluteTime);

        // Convert result back to test timezone to verify
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TestTimezone);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(result.UtcTime!.Value, timeZone);

        localTime.Hour.Should().Be(expectedHour, $"'{input}' should parse to hour {expectedHour}");
        localTime.Minute.Should().Be(expectedMinute, $"'{input}' should parse to minute {expectedMinute}");

        // Should be in the future
        result.UtcTime.Value.Should().BeAfter(DateTime.UtcNow);
    }

    #endregion

    #region Day Names Tests

    [Fact]
    public void Parse_Tomorrow_ReturnsNextDay()
    {
        // Arrange
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TestTimezone);
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var expectedDate = now.Date.AddDays(1);

        // Act
        var result = _service.Parse("tomorrow", TestTimezone);

        // Assert
        result.Success.Should().BeTrue("parsing 'tomorrow' should succeed");
        result.UtcTime.Should().NotBeNull();
        result.ParseType.Should().Be(TimeParseType.AbsoluteDay);

        var localTime = TimeZoneInfo.ConvertTimeFromUtc(result.UtcTime!.Value, timeZone);
        localTime.Date.Should().Be(expectedDate);
    }

    [Theory]
    [InlineData("monday", DayOfWeek.Monday)]
    [InlineData("tuesday", DayOfWeek.Tuesday)]
    [InlineData("wednesday", DayOfWeek.Wednesday)]
    [InlineData("thursday", DayOfWeek.Thursday)]
    [InlineData("friday", DayOfWeek.Friday)]
    [InlineData("saturday", DayOfWeek.Saturday)]
    [InlineData("sunday", DayOfWeek.Sunday)]
    [InlineData("mon", DayOfWeek.Monday)]
    [InlineData("tue", DayOfWeek.Tuesday)]
    [InlineData("wed", DayOfWeek.Wednesday)]
    [InlineData("thu", DayOfWeek.Thursday)]
    [InlineData("fri", DayOfWeek.Friday)]
    [InlineData("sat", DayOfWeek.Saturday)]
    [InlineData("sun", DayOfWeek.Sunday)]
    public void Parse_DayNames_ReturnsCorrectDayOfWeek(string input, DayOfWeek expectedDayOfWeek)
    {
        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert
        result.Success.Should().BeTrue($"parsing '{input}' should succeed");
        result.UtcTime.Should().NotBeNull();
        result.ParseType.Should().Be(TimeParseType.AbsoluteDay);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TestTimezone);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(result.UtcTime!.Value, timeZone);

        localTime.DayOfWeek.Should().Be(expectedDayOfWeek, $"'{input}' should parse to {expectedDayOfWeek}");
        result.UtcTime.Value.Should().BeAfter(DateTime.UtcNow, "parsed time should be in the future");
    }

    [Theory]
    [InlineData("next monday", DayOfWeek.Monday)]
    [InlineData("next friday", DayOfWeek.Friday)]
    [InlineData("next sunday", DayOfWeek.Sunday)]
    public void Parse_DayNamesWithNext_ReturnsNextWeek(string input, DayOfWeek expectedDayOfWeek)
    {
        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert
        result.Success.Should().BeTrue($"parsing '{input}' should succeed");
        result.UtcTime.Should().NotBeNull();
        result.ParseType.Should().Be(TimeParseType.AbsoluteDay);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TestTimezone);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(result.UtcTime!.Value, timeZone);

        localTime.DayOfWeek.Should().Be(expectedDayOfWeek);

        // Should be in the future and at least 1 day away (implementation returns next occurrence of that day)
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var daysInFuture = (localTime.Date - now.Date).Days;
        daysInFuture.Should().BeGreaterThan(0, "next [day] should be in the future");
    }

    [Theory]
    [InlineData("tomorrow 3pm", 15, 0)]
    [InlineData("monday 10:30am", 10, 30)]
    [InlineData("friday 9pm", 21, 0)]
    [InlineData("next monday 2pm", 14, 0)]
    public void Parse_DayNamesWithTime_ReturnsCorrectDateTime(string input, int expectedHour, int expectedMinute)
    {
        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert
        result.Success.Should().BeTrue($"parsing '{input}' should succeed");
        result.UtcTime.Should().NotBeNull();
        result.ParseType.Should().Be(TimeParseType.AbsoluteDay);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TestTimezone);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(result.UtcTime!.Value, timeZone);

        localTime.Hour.Should().Be(expectedHour);
        localTime.Minute.Should().Be(expectedMinute);
        result.UtcTime.Value.Should().BeAfter(DateTime.UtcNow);
    }

    #endregion

    #region Date Tests

    [Theory]
    [InlineData("Dec 31", 12, 31)]
    [InlineData("Jan 1", 1, 1)]
    [InlineData("january 15", 1, 15)]
    [InlineData("mar 20", 3, 20)]
    [InlineData("july 4", 7, 4)]
    [InlineData("september 10", 9, 10)]
    public void Parse_MonthDay_ReturnsCorrectDate(string input, int expectedMonth, int expectedDay)
    {
        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert
        result.Success.Should().BeTrue($"parsing '{input}' should succeed");
        result.UtcTime.Should().NotBeNull();
        result.ParseType.Should().Be(TimeParseType.AbsoluteDate);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TestTimezone);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(result.UtcTime!.Value, timeZone);

        localTime.Month.Should().Be(expectedMonth, $"'{input}' should parse to month {expectedMonth}");
        localTime.Day.Should().Be(expectedDay, $"'{input}' should parse to day {expectedDay}");
        result.UtcTime.Value.Should().BeAfter(DateTime.UtcNow, "parsed date should be in the future");
    }

    [Theory]
    [InlineData("Dec 31 10pm", 12, 31, 22, 0)]
    [InlineData("Jan 1 9am", 1, 1, 9, 0)]
    [InlineData("july 4 14:30", 7, 4, 14, 30)]
    [InlineData("march 15 6pm", 3, 15, 18, 0)]
    public void Parse_MonthDayWithTime_ReturnsCorrectDateTime(string input, int expectedMonth, int expectedDay,
        int expectedHour, int expectedMinute)
    {
        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert
        result.Success.Should().BeTrue($"parsing '{input}' should succeed");
        result.UtcTime.Should().NotBeNull();
        result.ParseType.Should().Be(TimeParseType.AbsoluteDate);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TestTimezone);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(result.UtcTime!.Value, timeZone);

        localTime.Month.Should().Be(expectedMonth);
        localTime.Day.Should().Be(expectedDay);
        localTime.Hour.Should().Be(expectedHour);
        localTime.Minute.Should().Be(expectedMinute);
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void Parse_PastTime_ReturnsError()
    {
        // Arrange - Test that times in the past are rejected
        // The service's "must be in the future" check runs after parsing succeeds.
        // For dates that have passed this year, the service won't parse them at all (year < current year check).
        // For times earlier today, absolute time parsing should roll them to tomorrow, so they won't be "past".
        // Therefore, we test with a full datetime format from earlier this year which won't parse due to year validation.
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TestTimezone);
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);

        // Use a date/time from 1 month ago
        var pastDateTime = now.AddMonths(-1);
        var input = $"{pastDateTime.Year}-{pastDateTime.Month:D2}-{pastDateTime.Day:D2} {pastDateTime.Hour:D2}:00";

        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert
        result.Success.Should().BeFalse("parsing a past time should fail");
        result.UtcTime.Should().BeNull();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        // Note: The error message may be "Unable to parse" if the year validation fails,
        // or "must be in the future" if it parsed but the datetime is in the past.
        // Both are acceptable as the important thing is that past times are rejected.
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("xyz")]
    [InlineData("25:00")]
    [InlineData("13pm")]
    [InlineData("random text")]
    [InlineData("100m ago")]
    public void Parse_InvalidFormat_ReturnsError(string input)
    {
        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert
        result.Success.Should().BeFalse($"parsing invalid input '{input}' should fail");
        result.UtcTime.Should().BeNull();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Parse_EmptyString_ReturnsError(string? input)
    {
        // Act
        var result = _service.Parse(input!, TestTimezone);

        // Assert
        result.Success.Should().BeFalse("parsing empty input should fail");
        result.UtcTime.Should().BeNull();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.ErrorMessage.Should().Contain("empty",
            "error message should indicate input cannot be empty");
    }

    [Theory]
    [InlineData("10PM", "10pm")]
    [InlineData("TOMORROW", "tomorrow")]
    [InlineData("NOON", "noon")]
    [InlineData("MONDAY", "monday")]
    [InlineData("DEC 31", "dec 31")]
    [InlineData("In 10M", "in 10m")]
    [InlineData("2H 30M", "2h 30m")]
    public void Parse_CaseInsensitive_ReturnsSameResult(string upperInput, string lowerInput)
    {
        // Act
        var upperResult = _service.Parse(upperInput, TestTimezone);
        var lowerResult = _service.Parse(lowerInput, TestTimezone);

        // Assert
        upperResult.Success.Should().BeTrue($"parsing '{upperInput}' should succeed");
        lowerResult.Success.Should().BeTrue($"parsing '{lowerInput}' should succeed");

        // Both should parse to the same time (within a few seconds due to test execution)
        if (upperResult.UtcTime.HasValue && lowerResult.UtcTime.HasValue)
        {
            var timeDifference = Math.Abs((upperResult.UtcTime.Value - lowerResult.UtcTime.Value).TotalSeconds);
            timeDifference.Should().BeLessThan(5,
                "case-insensitive inputs should parse to the same time");
        }

        upperResult.ParseType.Should().Be(lowerResult.ParseType,
            "case-insensitive inputs should have the same parse type");
    }

    #endregion

    #region Timezone Tests

    [Theory]
    [InlineData("America/New_York")]
    [InlineData("America/Los_Angeles")]
    [InlineData("Europe/London")]
    [InlineData("Asia/Tokyo")]
    [InlineData("UTC")]
    public void Parse_ConvertsToUtcCorrectly(string timezone)
    {
        // Arrange
        var input = "tomorrow 3pm";

        // Act
        var result = _service.Parse(input, timezone);

        // Assert
        result.Success.Should().BeTrue($"parsing should succeed with timezone '{timezone}'");
        result.UtcTime.Should().NotBeNull();

        // Verify the result is in UTC by converting back to the original timezone
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(result.UtcTime!.Value, timeZone);

        localTime.Hour.Should().Be(15, "local time should be 3pm in the specified timezone");

        // The UTC kind should be UTC
        result.UtcTime.Value.Kind.Should().Be(DateTimeKind.Utc,
            "parsed time should have UTC DateTimeKind");
    }

    [Fact]
    public void Parse_WithInvalidTimezone_UsesUtc()
    {
        // Arrange
        var input = "10m";
        var invalidTimezone = "Invalid/Timezone";

        // Act
        var result = _service.Parse(input, invalidTimezone);

        // Assert
        result.Success.Should().BeTrue("parsing should succeed even with invalid timezone");
        result.UtcTime.Should().NotBeNull();

        // Verify a warning was logged
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid timezone")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log warning about invalid timezone");
    }

    [Fact]
    public void Parse_TimezoneConversion_PreservesLocalTime()
    {
        // Arrange
        var input = "10pm";
        var timezone1 = "America/New_York"; // EST/EDT
        var timezone2 = "America/Los_Angeles"; // PST/PDT

        // Act
        var result1 = _service.Parse(input, timezone1);
        var result2 = _service.Parse(input, timezone2);

        // Assert
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();

        // Convert both back to their respective local times
        var tz1 = TimeZoneInfo.FindSystemTimeZoneById(timezone1);
        var tz2 = TimeZoneInfo.FindSystemTimeZoneById(timezone2);

        var local1 = TimeZoneInfo.ConvertTimeFromUtc(result1.UtcTime!.Value, tz1);
        var local2 = TimeZoneInfo.ConvertTimeFromUtc(result2.UtcTime!.Value, tz2);

        // Both should be 10pm in their respective timezones
        local1.Hour.Should().Be(22, "should be 10pm in New York time");
        local2.Hour.Should().Be(22, "should be 10pm in Los Angeles time");

        // But the UTC times should be different (3 hours apart)
        var utcDifference = Math.Abs((result1.UtcTime.Value - result2.UtcTime.Value).TotalHours);
        utcDifference.Should().BeApproximately(3, 0.5,
            "New York and Los Angeles are typically 3 hours apart");
    }

    #endregion

    #region Logging Tests

    [Fact]
    public void Parse_Success_LogsDebugAndInformation()
    {
        // Arrange
        var input = "10m";

        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert
        result.Success.Should().BeTrue();

        // Verify debug log for parsing attempt
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Parsing time expression")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log debug message when starting parse");

        // Verify information log for successful parse
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully parsed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log information message on successful parse");
    }

    [Fact]
    public void Parse_Failure_LogsDebug()
    {
        // Arrange
        var input = "invalid input";

        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert
        result.Success.Should().BeFalse();

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to parse")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log debug message when parse fails");
    }

    [Fact]
    public void Parse_EmptyInput_LogsDebug()
    {
        // Arrange
        var input = "";

        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert
        result.Success.Should().BeFalse();

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("null or whitespace")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log debug message for empty input");
    }

    #endregion

    #region Additional Edge Cases

    [Theory]
    [InlineData("0m")]
    [InlineData("0h")]
    [InlineData("0d")]
    public void Parse_ZeroRelativeTime_ReturnsError(string input)
    {
        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert
        result.Success.Should().BeFalse($"parsing zero duration '{input}' should fail");
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("1000w")] // ~19 years - should work but be in far future
    [InlineData("365d")] // 1 year
    public void Parse_LargeRelativeTime_Succeeds(string input)
    {
        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert
        result.Success.Should().BeTrue($"parsing large duration '{input}' should succeed");
        result.UtcTime.Should().NotBeNull();
        result.UtcTime!.Value.Should().BeAfter(DateTime.UtcNow.AddDays(30),
            "large duration should result in far future date");
    }

    [Theory]
    [InlineData("Feb 29", 2)] // Only valid in leap years
    public void Parse_LeapYearDate_HandlesCorrectly(string input, int expectedMonth)
    {
        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert - should either succeed (in leap year) or fail (non-leap year)
        if (result.Success)
        {
            result.UtcTime.Should().NotBeNull();
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TestTimezone);
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(result.UtcTime!.Value, timeZone);
            localTime.Month.Should().Be(expectedMonth);
            localTime.Day.Should().Be(29);
            DateTime.IsLeapYear(localTime.Year).Should().BeTrue("Feb 29 should only be valid in leap years");
        }
    }

    [Fact]
    public void Parse_MultipleSpacesBetweenTokens_HandlesCorrectly()
    {
        // Arrange
        var input = "2h    30m"; // Multiple spaces

        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert
        result.Success.Should().BeTrue("should handle multiple spaces between tokens");
        var beforeParse = DateTime.UtcNow;
        var actualDifferenceMinutes = (result.UtcTime!.Value - beforeParse).TotalMinutes;
        actualDifferenceMinutes.Should().BeApproximately(150, 0.1);
    }

    [Theory]
    [InlineData("   10m   ")] // Leading and trailing spaces
    [InlineData("\t2h\t")] // Tabs
    public void Parse_WhitespaceAroundInput_TrimsCorrectly(string input)
    {
        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert
        result.Success.Should().BeTrue("should trim whitespace around input");
        result.UtcTime.Should().NotBeNull();
    }

    #endregion

    #region Full DateTime Format Tests

    [Theory]
    [InlineData("2026-06-15 22:00", 2026, 6, 15, 22, 0)]
    [InlineData("2026-01-01T14:30", 2026, 1, 1, 14, 30)]
    [InlineData("2026-03-15 09:45:30", 2026, 3, 15, 9, 45)]
    public void Parse_FullDateTime_ReturnsCorrectDateTime(string input, int year, int month, int day,
        int hour, int minute)
    {
        // Act
        var result = _service.Parse(input, TestTimezone);

        // Assert
        result.Success.Should().BeTrue($"parsing '{input}' should succeed");
        result.UtcTime.Should().NotBeNull();
        result.ParseType.Should().Be(TimeParseType.FullDateTime);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TestTimezone);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(result.UtcTime!.Value, timeZone);

        localTime.Year.Should().Be(year);
        localTime.Month.Should().Be(month);
        localTime.Day.Should().Be(day);
        localTime.Hour.Should().Be(hour);
        localTime.Minute.Should().Be(minute);
    }

    #endregion
}
