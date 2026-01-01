using DiscordBot.Core.Configuration;
using FluentAssertions;

namespace DiscordBot.Tests.Configuration;

/// <summary>
/// Unit tests for AnalyticsRetentionOptions.
/// </summary>
public class AnalyticsRetentionOptionsTests
{
    [Fact]
    public void SectionName_HasCorrectValue()
    {
        // Assert
        AnalyticsRetentionOptions.SectionName.Should().Be("AnalyticsRetention");
    }

    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        // Arrange
        var options = new AnalyticsRetentionOptions();

        // Assert
        options.HourlyRetentionDays.Should().Be(14, "hourly snapshots should be retained for 14 days by default");
        options.DailyRetentionDays.Should().Be(365, "daily snapshots should be retained for 365 days by default");
        options.Enabled.Should().BeTrue("analytics aggregation should be enabled by default");
        options.CleanupBatchSize.Should().Be(1000, "cleanup batch size should be 1000 by default");
        options.CleanupIntervalHours.Should().Be(24, "cleanup should run every 24 hours by default");
    }

    [Fact]
    public void HourlyRetentionDays_CanBeSet()
    {
        // Arrange
        var options = new AnalyticsRetentionOptions();

        // Act
        options.HourlyRetentionDays = 7;

        // Assert
        options.HourlyRetentionDays.Should().Be(7);
    }

    [Fact]
    public void DailyRetentionDays_CanBeSet()
    {
        // Arrange
        var options = new AnalyticsRetentionOptions();

        // Act
        options.DailyRetentionDays = 180;

        // Assert
        options.DailyRetentionDays.Should().Be(180);
    }

    [Fact]
    public void Enabled_CanBeSet()
    {
        // Arrange
        var options = new AnalyticsRetentionOptions();

        // Act
        options.Enabled = false;

        // Assert
        options.Enabled.Should().BeFalse();
    }

    [Fact]
    public void CleanupBatchSize_CanBeSet()
    {
        // Arrange
        var options = new AnalyticsRetentionOptions();

        // Act
        options.CleanupBatchSize = 500;

        // Assert
        options.CleanupBatchSize.Should().Be(500);
    }

    [Fact]
    public void CleanupIntervalHours_CanBeSet()
    {
        // Arrange
        var options = new AnalyticsRetentionOptions();

        // Act
        options.CleanupIntervalHours = 12;

        // Assert
        options.CleanupIntervalHours.Should().Be(12);
    }

    [Fact]
    public void Properties_CanBeSetViaObjectInitializer()
    {
        // Act
        var options = new AnalyticsRetentionOptions
        {
            HourlyRetentionDays = 7,
            DailyRetentionDays = 180,
            Enabled = false,
            CleanupBatchSize = 500,
            CleanupIntervalHours = 12
        };

        // Assert
        options.HourlyRetentionDays.Should().Be(7);
        options.DailyRetentionDays.Should().Be(180);
        options.Enabled.Should().BeFalse();
        options.CleanupBatchSize.Should().Be(500);
        options.CleanupIntervalHours.Should().Be(12);
    }

    [Fact]
    public void DefaultValues_AreReasonableForProduction()
    {
        // Arrange
        var options = new AnalyticsRetentionOptions();

        // Assert
        options.HourlyRetentionDays.Should().BeGreaterThan(0, "hourly retention should be positive");
        options.DailyRetentionDays.Should().BeGreaterThan(options.HourlyRetentionDays, "daily retention should be longer than hourly");
        options.CleanupBatchSize.Should().BeGreaterThan(0, "batch size should be positive");
        options.CleanupIntervalHours.Should().BeGreaterThan(0, "cleanup interval should be positive");
    }
}
