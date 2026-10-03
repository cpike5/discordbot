using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using FluentAssertions;

namespace DiscordBot.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="CommandPerformanceViewModel"/>.
/// </summary>
public class CommandPerformanceViewModelTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Arrange & Act
        var viewModel = new CommandPerformanceViewModel();

        // Assert
        viewModel.TotalCommands.Should().Be(0, "default total commands should be 0");
        viewModel.AvgResponseTimeMs.Should().Be(0, "default average response time should be 0");
        viewModel.ErrorRate.Should().Be(0, "default error rate should be 0");
        viewModel.P99ResponseTimeMs.Should().Be(0, "default P99 should be 0");
        viewModel.P50Ms.Should().Be(0, "default P50 should be 0");
        viewModel.P95Ms.Should().Be(0, "default P95 should be 0");
        viewModel.SlowestCommands.Should().BeEmpty("default slowest commands should be empty");
        viewModel.RecentTimeouts.Should().BeEmpty("default recent timeouts should be empty");
        viewModel.TimeoutCount.Should().Be(0, "default timeout count should be 0");
    }

    [Fact]
    public void GetLatencyClass_WithLowLatency_ReturnsSuccess()
    {
        // Arrange
        var latency = 50.0;

        // Act
        var result = CommandPerformanceViewModel.GetLatencyClass(latency);

        // Assert
        result.Should().Be("text-success", "latency under 100ms should be success");
    }

    [Fact]
    public void GetLatencyClass_WithMediumLatency_ReturnsWarning()
    {
        // Arrange
        var latency = 250.0;

        // Act
        var result = CommandPerformanceViewModel.GetLatencyClass(latency);

        // Assert
        result.Should().Be("text-warning", "latency 100-499ms should be warning");
    }

    [Fact]
    public void GetLatencyClass_WithHighLatency_ReturnsError()
    {
        // Arrange
        var latency = 1500.0;

        // Act
        var result = CommandPerformanceViewModel.GetLatencyClass(latency);

        // Assert
        result.Should().Be("text-error", "latency 500ms+ should be error");
    }

    [Fact]
    public void GetLatencyClass_WithBoundaryValue100_ReturnsWarning()
    {
        // Arrange
        var latency = 100.0;

        // Act
        var result = CommandPerformanceViewModel.GetLatencyClass(latency);

        // Assert
        result.Should().Be("text-warning", "latency exactly 100ms should be warning");
    }

    [Fact]
    public void GetLatencyClass_WithBoundaryValue500_ReturnsError()
    {
        // Arrange
        var latency = 500.0;

        // Act
        var result = CommandPerformanceViewModel.GetLatencyClass(latency);

        // Assert
        result.Should().Be("text-error", "latency exactly 500ms should be error");
    }

    [Fact]
    public void GetErrorRateClass_WithLowErrorRate_ReturnsSuccess()
    {
        // Arrange
        var errorRate = 0.5;

        // Act
        var result = CommandPerformanceViewModel.GetErrorRateClass(errorRate);

        // Assert
        result.Should().Be("text-success", "error rate under 1% should be success");
    }

    [Fact]
    public void GetErrorRateClass_WithMediumErrorRate_ReturnsWarning()
    {
        // Arrange
        var errorRate = 3.0;

        // Act
        var result = CommandPerformanceViewModel.GetErrorRateClass(errorRate);

        // Assert
        result.Should().Be("text-warning", "error rate 1-5% should be warning");
    }

    [Fact]
    public void GetErrorRateClass_WithHighErrorRate_ReturnsError()
    {
        // Arrange
        var errorRate = 10.0;

        // Act
        var result = CommandPerformanceViewModel.GetErrorRateClass(errorRate);

        // Assert
        result.Should().Be("text-error", "error rate 5%+ should be error");
    }

    [Fact]
    public void GetErrorRateClass_WithBoundaryValue1_ReturnsWarning()
    {
        // Arrange
        var errorRate = 1.0;

        // Act
        var result = CommandPerformanceViewModel.GetErrorRateClass(errorRate);

        // Assert
        result.Should().Be("text-warning", "error rate exactly 1% should be warning");
    }

    [Fact]
    public void GetErrorRateClass_WithBoundaryValue5_ReturnsError()
    {
        // Arrange
        var errorRate = 5.0;

        // Act
        var result = CommandPerformanceViewModel.GetErrorRateClass(errorRate);

        // Assert
        result.Should().Be("text-error", "error rate exactly 5% should be error");
    }

    [Fact]
    public void CommandTimeoutDto_Constructor_InitializesWithDefaultValues()
    {
        // Arrange & Act
        var dto = new CommandTimeoutDto();

        // Assert
        dto.CommandName.Should().Be(string.Empty, "default command name should be empty string");
        dto.TimeoutCount.Should().Be(0, "default timeout count should be 0");
        dto.LastTimeout.Should().Be(default(DateTime), "default last timeout should be default datetime");
        dto.AvgResponseBeforeTimeout.Should().Be(0, "default avg response should be 0");
        dto.Status.Should().Be("Investigating", "default status should be 'Investigating'");
    }

    [Fact]
    public void CommandTimeoutDto_WithInitializers_SetsAllProperties()
    {
        // Arrange
        var lastTimeout = DateTime.UtcNow.AddHours(-1);

        // Act
        var dto = new CommandTimeoutDto
        {
            CommandName = "slow-command",
            TimeoutCount = 5,
            LastTimeout = lastTimeout,
            AvgResponseBeforeTimeout = 3500.5,
            Status = "Resolved"
        };

        // Assert
        dto.CommandName.Should().Be("slow-command", "command name should be set");
        dto.TimeoutCount.Should().Be(5, "timeout count should be set");
        dto.LastTimeout.Should().Be(lastTimeout, "last timeout should be set");
        dto.AvgResponseBeforeTimeout.Should().Be(3500.5, "avg response should be set");
        dto.Status.Should().Be("Resolved", "status should be set");
    }

    [Fact]
    public void CommandPerformanceViewModel_IsRecordType_SupportsValueEquality()
    {
        // Arrange
        var viewModel1 = new CommandPerformanceViewModel
        {
            TotalCommands = 100,
            AvgResponseTimeMs = 50.5,
            ErrorRate = 2.5
        };

        var viewModel2 = new CommandPerformanceViewModel
        {
            TotalCommands = 100,
            AvgResponseTimeMs = 50.5,
            ErrorRate = 2.5
        };

        var viewModel3 = new CommandPerformanceViewModel
        {
            TotalCommands = 200,
            AvgResponseTimeMs = 50.5,
            ErrorRate = 2.5
        };

        // Assert
        viewModel1.Should().BeEquivalentTo(viewModel2, "view models with same values should be equivalent");
        viewModel1.Should().NotBeEquivalentTo(viewModel3, "view models with different values should not be equivalent");
    }

    [Fact]
    public void CommandTimeoutDto_IsRecordType_SupportsValueEquality()
    {
        // Arrange
        var lastTimeout = DateTime.UtcNow;

        var dto1 = new CommandTimeoutDto
        {
            CommandName = "test",
            TimeoutCount = 3,
            LastTimeout = lastTimeout,
            AvgResponseBeforeTimeout = 3200.0,
            Status = "Investigating"
        };

        var dto2 = new CommandTimeoutDto
        {
            CommandName = "test",
            TimeoutCount = 3,
            LastTimeout = lastTimeout,
            AvgResponseBeforeTimeout = 3200.0,
            Status = "Investigating"
        };

        var dto3 = new CommandTimeoutDto
        {
            CommandName = "other",
            TimeoutCount = 3,
            LastTimeout = lastTimeout,
            AvgResponseBeforeTimeout = 3200.0,
            Status = "Investigating"
        };

        // Assert
        dto1.Should().Be(dto2, "records with same values should be equal");
        dto1.Should().NotBe(dto3, "records with different command names should not be equal");
    }

    [Fact]
    public void CommandPerformanceViewModel_WithSlowestCommands_StoresReadOnlyList()
    {
        // Arrange
        var slowestCommands = new List<SlowestCommandDto>
        {
            new SlowestCommandDto
            {
                CommandName = "slow-cmd",
                ExecutedAt = DateTime.UtcNow,
                DurationMs = 2500.0,
                UserId = 123UL,
                GuildId = 456UL
            }
        };

        // Act
        var viewModel = new CommandPerformanceViewModel
        {
            SlowestCommands = slowestCommands.AsReadOnly()
        };

        // Assert
        viewModel.SlowestCommands.Should().HaveCount(1, "slowest commands list should be populated");
        viewModel.SlowestCommands.Should().BeAssignableTo<IReadOnlyList<SlowestCommandDto>>("should be read-only list");
    }

    [Fact]
    public void CommandPerformanceViewModel_WithRecentTimeouts_StoresReadOnlyList()
    {
        // Arrange
        var recentTimeouts = new List<CommandTimeoutDto>
        {
            new CommandTimeoutDto
            {
                CommandName = "timeout-cmd",
                TimeoutCount = 2,
                LastTimeout = DateTime.UtcNow,
                AvgResponseBeforeTimeout = 3100.0,
                Status = "Investigating"
            }
        };

        // Act
        var viewModel = new CommandPerformanceViewModel
        {
            RecentTimeouts = recentTimeouts.AsReadOnly()
        };

        // Assert
        viewModel.RecentTimeouts.Should().HaveCount(1, "recent timeouts list should be populated");
        viewModel.RecentTimeouts.Should().BeAssignableTo<IReadOnlyList<CommandTimeoutDto>>("should be read-only list");
    }
}
