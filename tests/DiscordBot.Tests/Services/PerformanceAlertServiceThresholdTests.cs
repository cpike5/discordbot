using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Services;

/// <summary>
/// UX plan F-3: alert thresholds are validated on the server too (the form checks them, but the
/// API is open to scripts): numbers are finite and not negative, and warning is below critical.
/// </summary>
public class PerformanceAlertServiceThresholdTests
{
    private static PerformanceAlertConfig Config(double? warning = 70, double? critical = 90) => new()
    {
        MetricName = "memory_usage",
        DisplayName = "Memory Usage",
        WarningThreshold = warning,
        CriticalThreshold = critical
    };

    [Fact]
    public void ValidUpdate_Passes()
    {
        var act = () => PerformanceAlertService.ValidateThresholds(Config(), new AlertConfigUpdateDto { WarningThreshold = 60, CriticalThreshold = 95 });

        act.Should().NotThrow();
    }

    [Fact]
    public void WarningAboveCritical_IsRejected()
    {
        var act = () => PerformanceAlertService.ValidateThresholds(Config(), new AlertConfigUpdateDto { WarningThreshold = 95, CriticalThreshold = 90 });

        act.Should().Throw<ArgumentException>().WithMessage("*warning threshold must be lower*");
    }

    [Fact]
    public void WarningEqualToCritical_IsRejected()
    {
        var act = () => PerformanceAlertService.ValidateThresholds(Config(), new AlertConfigUpdateDto { WarningThreshold = 90, CriticalThreshold = 90 });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RaisingWarningAboveTheSavedCritical_IsRejected_EvenWhenCriticalIsNotSent()
    {
        var act = () => PerformanceAlertService.ValidateThresholds(Config(warning: 70, critical: 90), new AlertConfigUpdateDto { WarningThreshold = 95 });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LoweringCriticalBelowTheSavedWarning_IsRejected_EvenWhenWarningIsNotSent()
    {
        var act = () => PerformanceAlertService.ValidateThresholds(Config(warning: 70, critical: 90), new AlertConfigUpdateDto { CriticalThreshold = 50 });

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void NegativeOrNonFiniteThresholds_AreRejected(double value)
    {
        var act = () => PerformanceAlertService.ValidateThresholds(Config(), new AlertConfigUpdateDto { CriticalThreshold = value });

        act.Should().Throw<ArgumentException>().WithMessage("*zero or greater*");
    }

    [Fact]
    public void EventMetricsWithoutAWarning_AreStillEditable()
    {
        var config = Config(warning: null, critical: 1);

        var act = () => PerformanceAlertService.ValidateThresholds(config, new AlertConfigUpdateDto { CriticalThreshold = 2 });

        act.Should().NotThrow();
    }

    [Fact]
    public void TogglingEnabled_NeverFailsValidation()
    {
        var act = () => PerformanceAlertService.ValidateThresholds(Config(), new AlertConfigUpdateDto { IsEnabled = false });

        act.Should().NotThrow();
    }
}
