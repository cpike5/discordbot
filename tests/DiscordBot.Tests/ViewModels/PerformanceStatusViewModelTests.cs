using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.ViewModels;

/// <summary>
/// UX plan S-9: status words and colours on the Performance tabs come from the data, not from
/// hard-coded "Normal" / green.
/// </summary>
public class PerformanceStatusViewModelTests
{
    [Theory]
    [InlineData("Connected", "Healthy", "text-success")]
    [InlineData("Connected", "Warning", "text-warning")]
    [InlineData("Connected", "Critical", "text-error")]
    [InlineData("Connecting", "Healthy", "text-warning")]
    [InlineData("Disconnected", "Healthy", "text-error")]
    [InlineData("Disconnected", "Critical", "text-error")]
    public void BotHealthStatusClass_FollowsTheGatewayState(string connection, string serviceStatus, string expected)
    {
        var vm = new PerformanceOverviewViewModel
        {
            BotHealth = new PerformanceHealthDto { ConnectionState = connection, Status = serviceStatus }
        };

        vm.BotHealthStatusClass.Should().Be(expected);
    }

    [Theory]
    [InlineData(40, 10, "Normal", "text-success")]
    [InlineData(76, 10, "Elevated", "text-warning")]
    [InlineData(10, 80, "Elevated", "text-warning")]
    [InlineData(95, 10, "Critical", "text-error")]
    [InlineData(10, 91, "Critical", "text-error")]
    public void SystemStatus_IsTheWorseOfMemoryAndCpu(double memory, double cpu, string text, string cls)
    {
        var vm = new PerformanceOverviewViewModel { MemoryUsagePercent = memory, CpuUsagePercent = cpu };

        vm.SystemStatusText.Should().Be(text);
        vm.SystemStatusClass.Should().Be(cls);
    }

    [Fact]
    public void ApiStatusClass_IsAmberOnlyWhenRateLimited()
    {
        new PerformanceOverviewViewModel { ApiRateLimitPercent = 0 }.ApiStatusClass.Should().Be("text-success");
        new PerformanceOverviewViewModel { ApiRateLimitPercent = 30 }.ApiStatusClass.Should().Be("text-warning");
    }

    [Theory]
    [InlineData(24, "Last 24 hours")]
    [InlineData(168, "Last 7 days")]
    [InlineData(720, "Last 30 days")]
    public void TimeRangeCaption_NamesTheRange(int hours, string expected)
    {
        new PerformanceOverviewViewModel { TimeRangeHours = hours }.TimeRangeCaption.Should().Be(expected);
    }

    [Theory]
    [InlineData(100, "Normal", "status-badge-connected")]
    [InlineData(600, "Elevated", "status-badge-warning")]
    [InlineData(900, "High", "status-badge-error")]
    public void MemoryStatus_UsesTheGaugeThresholds(long workingSetMb, string text, string badge)
    {
        var vm = new HealthMetricsViewModel { WorkingSetMB = workingSetMb };

        vm.MemoryStatusText.Should().Be(text);
        vm.MemoryStatusClass.Should().Be(badge);
    }

    [Theory]
    [InlineData(10, "Normal", "status-badge-connected")]
    [InlineData(60, "Elevated", "status-badge-warning")]
    [InlineData(95, "High", "status-badge-error")]
    public void CpuStatus_UsesTheGaugeThresholds(double cpu, string text, string badge)
    {
        var vm = new HealthMetricsViewModel { CpuUsagePercent = cpu };

        vm.CpuStatusText.Should().Be(text);
        vm.CpuStatusClass.Should().Be(badge);
    }

    [Theory]
    [InlineData("Connected", "text-success")]
    [InlineData("Connecting", "text-warning")]
    [InlineData("Disconnected", "text-error")]
    public void ConnectionTextClass_IsGreenOnlyWhileConnected(string state, string expected)
    {
        var vm = new HealthMetricsViewModel { Health = new PerformanceHealthDto { ConnectionState = state } };

        vm.ConnectionTextClass.Should().Be(expected);
    }

    [Theory]
    [InlineData(20, 0, "Healthy", "status-badge-connected")]
    [InlineData(150, 0, "Slow", "status-badge-warning")]
    [InlineData(20, 3, "Slow", "status-badge-warning")]
    [InlineData(250, 0, "Degraded", "status-badge-error")]
    [InlineData(20, 11, "Degraded", "status-badge-error")]
    public void DatabaseStatus_FollowsQueryTimeAndErrors(double avgMs, int errors, string text, string badge)
    {
        var vm = new SystemHealthViewModel
        {
            DatabaseMetrics = new DatabaseMetricsDto { AvgQueryTimeMs = avgMs },
            DatabaseErrorCount = errors
        };

        vm.DatabaseStatusText.Should().Be(text);
        vm.DatabaseStatusClass.Should().Be(badge);
    }
}
