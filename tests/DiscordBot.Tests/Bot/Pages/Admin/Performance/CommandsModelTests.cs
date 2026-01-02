using DiscordBot.Bot.Pages.Admin.Performance;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Admin.Performance;

/// <summary>
/// Unit tests for <see cref="CommandsModel"/> Razor Page.
/// </summary>
public class CommandsModelTests
{
    private readonly Mock<ICommandPerformanceAggregator> _mockPerformanceAggregator;
    private readonly Mock<ILogger<CommandsModel>> _mockLogger;
    private readonly CommandsModel _commandsModel;

    public CommandsModelTests()
    {
        _mockPerformanceAggregator = new Mock<ICommandPerformanceAggregator>();
        _mockLogger = new Mock<ILogger<CommandsModel>>();

        _commandsModel = new CommandsModel(
            _mockPerformanceAggregator.Object,
            _mockLogger.Object);

        // Setup PageContext
        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim("sub", "test-user") },
                "test"));

        var modelState = new ModelStateDictionary();
        var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(
            httpContext, new RouteData(), new PageActionDescriptor(), modelState);
        var pageContext = new PageContext(actionContext);

        _commandsModel.PageContext = pageContext;
    }

    [Fact]
    public async Task OnGetAsync_WithDefault24Hours_LoadsViewModelCorrectly()
    {
        // Arrange
        var aggregates = new List<CommandPerformanceAggregateDto>
        {
            new CommandPerformanceAggregateDto
            {
                CommandName = "ping",
                ExecutionCount = 100,
                AvgMs = 50.5,
                MinMs = 10.0,
                MaxMs = 200.0,
                P50Ms = 45.0,
                P95Ms = 120.0,
                P99Ms = 180.0,
                ErrorRate = 2.5
            },
            new CommandPerformanceAggregateDto
            {
                CommandName = "status",
                ExecutionCount = 50,
                AvgMs = 75.0,
                MinMs = 20.0,
                MaxMs = 150.0,
                P50Ms = 70.0,
                P95Ms = 130.0,
                P99Ms = 145.0,
                ErrorRate = 1.0
            }
        };

        var slowestCommands = new List<SlowestCommandDto>
        {
            new SlowestCommandDto
            {
                CommandName = "slow-command",
                ExecutedAt = DateTime.UtcNow.AddHours(-2),
                DurationMs = 2500.0,
                UserId = 123UL,
                GuildId = 456UL
            }
        };

        _mockPerformanceAggregator
            .Setup(s => s.GetAggregatesAsync(24))
            .ReturnsAsync(aggregates.AsReadOnly());

        _mockPerformanceAggregator
            .Setup(s => s.GetSlowestCommandsAsync(10, 24))
            .ReturnsAsync(slowestCommands.AsReadOnly());

        // Act
        await _commandsModel.OnGetAsync();

        // Assert
        _commandsModel.ViewModel.Should().NotBeNull();
        _commandsModel.ViewModel.TotalCommands.Should().Be(150, "total of 100 + 50 commands");
        _commandsModel.ViewModel.AvgResponseTimeMs.Should().BeApproximately(62.75, 0.5, "average of averages (50.5 + 75.0)/2");
        _commandsModel.ViewModel.ErrorRate.Should().BeApproximately(2.0, 0.1, "weighted error rate (100*2.5 + 50*1.0)/150");
        _commandsModel.ViewModel.P99ResponseTimeMs.Should().Be(180.0, "max P99 from aggregates");
        _commandsModel.ViewModel.P95Ms.Should().Be(130.0, "max P95 from aggregates");
        _commandsModel.ViewModel.P50Ms.Should().BeApproximately(57.5, 0.5, "average P50 from aggregates (45.0 + 70.0)/2");
        _commandsModel.ViewModel.SlowestCommands.Should().HaveCount(1, "slowest commands should be populated");
        _commandsModel.ViewModel.TimeoutCount.Should().Be(0, "no timeouts when all commands < 3000ms");

        _mockPerformanceAggregator.Verify(
            s => s.GetAggregatesAsync(24),
            Times.Once,
            "aggregates should be fetched for 24 hours");

        _mockPerformanceAggregator.Verify(
            s => s.GetSlowestCommandsAsync(10, 24),
            Times.Once,
            "slowest commands should be fetched");
    }

    [Fact]
    public async Task OnGetAsync_With7Days_SetsCorrectGranularity()
    {
        // Arrange
        _commandsModel.Hours = 168; // 7 days

        _mockPerformanceAggregator
            .Setup(s => s.GetAggregatesAsync(168))
            .ReturnsAsync(new List<CommandPerformanceAggregateDto>().AsReadOnly());

        _mockPerformanceAggregator
            .Setup(s => s.GetSlowestCommandsAsync(10, 168))
            .ReturnsAsync(new List<SlowestCommandDto>().AsReadOnly());

        // Act
        await _commandsModel.OnGetAsync();

        // Assert
        _mockPerformanceAggregator.Verify(
            s => s.GetAggregatesAsync(168),
            Times.Once,
            "aggregates should be fetched for 168 hours (7 days)");
    }

    [Fact]
    public async Task OnGetAsync_With30Days_SetsCorrectGranularity()
    {
        // Arrange
        _commandsModel.Hours = 720; // 30 days

        _mockPerformanceAggregator
            .Setup(s => s.GetAggregatesAsync(720))
            .ReturnsAsync(new List<CommandPerformanceAggregateDto>().AsReadOnly());

        _mockPerformanceAggregator
            .Setup(s => s.GetSlowestCommandsAsync(10, 720))
            .ReturnsAsync(new List<SlowestCommandDto>().AsReadOnly());

        // Act
        await _commandsModel.OnGetAsync();

        // Assert
        _mockPerformanceAggregator.Verify(
            s => s.GetAggregatesAsync(720),
            Times.Once,
            "aggregates should be fetched for 720 hours (30 days)");
    }

    [Fact]
    public async Task OnGetAsync_IdentifiesTimeouts_WhenCommandsExceed3000Ms()
    {
        // Arrange
        var aggregates = new List<CommandPerformanceAggregateDto>
        {
            new CommandPerformanceAggregateDto
            {
                CommandName = "normal",
                ExecutionCount = 50,
                AvgMs = 100.0,
                MinMs = 50.0,
                MaxMs = 200.0,
                P50Ms = 95.0,
                P95Ms = 180.0,
                P99Ms = 195.0,
                ErrorRate = 0.0
            }
        };

        var slowestCommands = new List<SlowestCommandDto>
        {
            new SlowestCommandDto
            {
                CommandName = "timeout-cmd",
                ExecutedAt = DateTime.UtcNow.AddMinutes(-30),
                DurationMs = 3500.0,
                UserId = 123UL,
                GuildId = 456UL
            },
            new SlowestCommandDto
            {
                CommandName = "timeout-cmd",
                ExecutedAt = DateTime.UtcNow.AddMinutes(-15),
                DurationMs = 3200.0,
                UserId = 789UL,
                GuildId = 456UL
            },
            new SlowestCommandDto
            {
                CommandName = "fast-cmd",
                ExecutedAt = DateTime.UtcNow.AddMinutes(-10),
                DurationMs = 500.0,
                UserId = 111UL,
                GuildId = 222UL
            }
        };

        _mockPerformanceAggregator
            .Setup(s => s.GetAggregatesAsync(24))
            .ReturnsAsync(aggregates.AsReadOnly());

        _mockPerformanceAggregator
            .Setup(s => s.GetSlowestCommandsAsync(10, 24))
            .ReturnsAsync(slowestCommands.AsReadOnly());

        // Act
        await _commandsModel.OnGetAsync();

        // Assert
        _commandsModel.ViewModel.TimeoutCount.Should().Be(2, "two commands exceeded 3000ms threshold");
        _commandsModel.ViewModel.RecentTimeouts.Should().HaveCount(1, "timeouts should be grouped by command name");

        var timeout = _commandsModel.ViewModel.RecentTimeouts.First();
        timeout.CommandName.Should().Be("timeout-cmd");
        timeout.TimeoutCount.Should().Be(2);
        timeout.AvgResponseBeforeTimeout.Should().Be(3350.0, "(3500 + 3200) / 2");
        timeout.Status.Should().Be("Investigating", "recent timeout (within 2 hours) should be investigating");
    }

    [Fact]
    public async Task OnGetAsync_MarksOldTimeoutsAsResolved()
    {
        // Arrange
        var aggregates = new List<CommandPerformanceAggregateDto>();

        var slowestCommands = new List<SlowestCommandDto>
        {
            new SlowestCommandDto
            {
                CommandName = "old-timeout",
                ExecutedAt = DateTime.UtcNow.AddHours(-5),
                DurationMs = 4000.0,
                UserId = 123UL,
                GuildId = 456UL
            }
        };

        _mockPerformanceAggregator
            .Setup(s => s.GetAggregatesAsync(24))
            .ReturnsAsync(aggregates.AsReadOnly());

        _mockPerformanceAggregator
            .Setup(s => s.GetSlowestCommandsAsync(10, 24))
            .ReturnsAsync(slowestCommands.AsReadOnly());

        // Act
        await _commandsModel.OnGetAsync();

        // Assert
        _commandsModel.ViewModel.RecentTimeouts.Should().HaveCount(1);
        _commandsModel.ViewModel.RecentTimeouts.First().Status.Should().Be("Resolved", "timeout older than 2 hours should be resolved");
    }

    [Fact]
    public async Task OnGetAsync_WithEmptyData_ReturnsEmptyViewModel()
    {
        // Arrange
        _mockPerformanceAggregator
            .Setup(s => s.GetAggregatesAsync(24))
            .ReturnsAsync(new List<CommandPerformanceAggregateDto>().AsReadOnly());

        _mockPerformanceAggregator
            .Setup(s => s.GetSlowestCommandsAsync(10, 24))
            .ReturnsAsync(new List<SlowestCommandDto>().AsReadOnly());

        // Act
        await _commandsModel.OnGetAsync();

        // Assert
        _commandsModel.ViewModel.Should().NotBeNull();
        _commandsModel.ViewModel.TotalCommands.Should().Be(0, "no commands should result in 0 total");
        _commandsModel.ViewModel.AvgResponseTimeMs.Should().Be(0, "no commands should result in 0 average");
        _commandsModel.ViewModel.ErrorRate.Should().Be(0, "no commands should result in 0 error rate");
        _commandsModel.ViewModel.P99ResponseTimeMs.Should().Be(0);
        _commandsModel.ViewModel.P95Ms.Should().Be(0);
        _commandsModel.ViewModel.P50Ms.Should().Be(0);
        _commandsModel.ViewModel.SlowestCommands.Should().BeEmpty();
        _commandsModel.ViewModel.RecentTimeouts.Should().BeEmpty();
        _commandsModel.ViewModel.TimeoutCount.Should().Be(0);
    }

    [Fact]
    public async Task OnGetAsync_WithServiceException_CreatesEmptyViewModel()
    {
        // Arrange
        _mockPerformanceAggregator
            .Setup(s => s.GetAggregatesAsync(It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        await _commandsModel.OnGetAsync();

        // Assert
        _commandsModel.ViewModel.Should().NotBeNull();
        _commandsModel.ViewModel.TotalCommands.Should().Be(0, "exception should result in empty view model");
        _commandsModel.ViewModel.SlowestCommands.Should().BeEmpty();
        _commandsModel.ViewModel.RecentTimeouts.Should().BeEmpty();

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to load command performance data")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "an error log should be written when service fails");
    }

    [Fact]
    public async Task OnGetAsync_CalculatesWeightedErrorRate()
    {
        // Arrange
        var aggregates = new List<CommandPerformanceAggregateDto>
        {
            new CommandPerformanceAggregateDto
            {
                CommandName = "cmd1",
                ExecutionCount = 100,
                AvgMs = 50.0,
                P50Ms = 45.0,
                P95Ms = 100.0,
                P99Ms = 150.0,
                ErrorRate = 10.0 // 10% error rate
            },
            new CommandPerformanceAggregateDto
            {
                CommandName = "cmd2",
                ExecutionCount = 200,
                AvgMs = 60.0,
                P50Ms = 55.0,
                P95Ms = 110.0,
                P99Ms = 140.0,
                ErrorRate = 5.0 // 5% error rate
            }
        };

        _mockPerformanceAggregator
            .Setup(s => s.GetAggregatesAsync(24))
            .ReturnsAsync(aggregates.AsReadOnly());

        _mockPerformanceAggregator
            .Setup(s => s.GetSlowestCommandsAsync(10, 24))
            .ReturnsAsync(new List<SlowestCommandDto>().AsReadOnly());

        // Act
        await _commandsModel.OnGetAsync();

        // Assert
        // Weighted error rate: (100 * 10 + 200 * 5) / 300 = (1000 + 1000) / 300 = 6.67%
        _commandsModel.ViewModel.ErrorRate.Should().BeApproximately(6.67, 0.1, "error rate should be weighted by execution count");
    }

    [Fact]
    public async Task OnGetAsync_LogsDebugMessage()
    {
        // Arrange
        _mockPerformanceAggregator
            .Setup(s => s.GetAggregatesAsync(24))
            .ReturnsAsync(new List<CommandPerformanceAggregateDto>().AsReadOnly());

        _mockPerformanceAggregator
            .Setup(s => s.GetSlowestCommandsAsync(10, 24))
            .ReturnsAsync(new List<SlowestCommandDto>().AsReadOnly());

        // Act
        await _commandsModel.OnGetAsync();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Command Performance page accessed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a debug log should be written when page is accessed");
    }

    [Fact]
    public async Task OnGetAsync_LogsDebugAfterLoading()
    {
        // Arrange
        var aggregates = new List<CommandPerformanceAggregateDto>
        {
            new CommandPerformanceAggregateDto
            {
                CommandName = "ping",
                ExecutionCount = 10,
                AvgMs = 50.0,
                P50Ms = 45.0,
                P95Ms = 100.0,
                P99Ms = 120.0,
                ErrorRate = 0.0
            }
        };

        _mockPerformanceAggregator
            .Setup(s => s.GetAggregatesAsync(24))
            .ReturnsAsync(aggregates.AsReadOnly());

        _mockPerformanceAggregator
            .Setup(s => s.GetSlowestCommandsAsync(10, 24))
            .ReturnsAsync(new List<SlowestCommandDto>().AsReadOnly());

        // Act
        await _commandsModel.OnGetAsync();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Command Performance ViewModel loaded") &&
                    v.ToString()!.Contains("TotalCommands=10")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a debug log should be written with view model metrics after loading");
    }

    [Fact]
    public void Constructor_InitializesViewModelProperty()
    {
        // Arrange & Act
        var model = new CommandsModel(
            _mockPerformanceAggregator.Object,
            _mockLogger.Object);

        // Assert
        model.ViewModel.Should().NotBeNull("ViewModel should be initialized");
        model.ViewModel.Should().BeOfType<CommandPerformanceViewModel>();
    }

    [Fact]
    public void Hours_DefaultsTo24()
    {
        // Arrange & Act
        var model = new CommandsModel(
            _mockPerformanceAggregator.Object,
            _mockLogger.Object);

        // Assert
        model.Hours.Should().Be(24, "default hours should be 24");
    }

    [Fact]
    public async Task OnGetAsync_SetsP99AsMaxFromAllAggregates()
    {
        // Arrange
        var aggregates = new List<CommandPerformanceAggregateDto>
        {
            new CommandPerformanceAggregateDto
            {
                CommandName = "cmd1",
                ExecutionCount = 10,
                AvgMs = 50.0,
                P50Ms = 45.0,
                P95Ms = 100.0,
                P99Ms = 120.0,
                ErrorRate = 0.0
            },
            new CommandPerformanceAggregateDto
            {
                CommandName = "cmd2",
                ExecutionCount = 10,
                AvgMs = 75.0,
                P50Ms = 70.0,
                P95Ms = 150.0,
                P99Ms = 200.0, // Highest P99
                ErrorRate = 0.0
            }
        };

        _mockPerformanceAggregator
            .Setup(s => s.GetAggregatesAsync(24))
            .ReturnsAsync(aggregates.AsReadOnly());

        _mockPerformanceAggregator
            .Setup(s => s.GetSlowestCommandsAsync(10, 24))
            .ReturnsAsync(new List<SlowestCommandDto>().AsReadOnly());

        // Act
        await _commandsModel.OnGetAsync();

        // Assert
        _commandsModel.ViewModel.P99ResponseTimeMs.Should().Be(200.0, "P99 should be the maximum P99 from all aggregates");
    }

    [Fact]
    public async Task OnGetAsync_SetsP95AsMaxFromAllAggregates()
    {
        // Arrange
        var aggregates = new List<CommandPerformanceAggregateDto>
        {
            new CommandPerformanceAggregateDto
            {
                CommandName = "cmd1",
                ExecutionCount = 10,
                AvgMs = 50.0,
                P50Ms = 45.0,
                P95Ms = 180.0, // Highest P95
                P99Ms = 190.0,
                ErrorRate = 0.0
            },
            new CommandPerformanceAggregateDto
            {
                CommandName = "cmd2",
                ExecutionCount = 10,
                AvgMs = 75.0,
                P50Ms = 70.0,
                P95Ms = 150.0,
                P99Ms = 200.0,
                ErrorRate = 0.0
            }
        };

        _mockPerformanceAggregator
            .Setup(s => s.GetAggregatesAsync(24))
            .ReturnsAsync(aggregates.AsReadOnly());

        _mockPerformanceAggregator
            .Setup(s => s.GetSlowestCommandsAsync(10, 24))
            .ReturnsAsync(new List<SlowestCommandDto>().AsReadOnly());

        // Act
        await _commandsModel.OnGetAsync();

        // Assert
        _commandsModel.ViewModel.P95Ms.Should().Be(180.0, "P95 should be the maximum P95 from all aggregates");
    }

    [Fact]
    public async Task OnGetAsync_SetsP50AsAverageFromAllAggregates()
    {
        // Arrange
        var aggregates = new List<CommandPerformanceAggregateDto>
        {
            new CommandPerformanceAggregateDto
            {
                CommandName = "cmd1",
                ExecutionCount = 10,
                AvgMs = 50.0,
                P50Ms = 40.0,
                P95Ms = 100.0,
                P99Ms = 120.0,
                ErrorRate = 0.0
            },
            new CommandPerformanceAggregateDto
            {
                CommandName = "cmd2",
                ExecutionCount = 10,
                AvgMs = 75.0,
                P50Ms = 60.0,
                P95Ms = 150.0,
                P99Ms = 200.0,
                ErrorRate = 0.0
            }
        };

        _mockPerformanceAggregator
            .Setup(s => s.GetAggregatesAsync(24))
            .ReturnsAsync(aggregates.AsReadOnly());

        _mockPerformanceAggregator
            .Setup(s => s.GetSlowestCommandsAsync(10, 24))
            .ReturnsAsync(new List<SlowestCommandDto>().AsReadOnly());

        // Act
        await _commandsModel.OnGetAsync();

        // Assert
        _commandsModel.ViewModel.P50Ms.Should().Be(50.0, "P50 should be the average: (40 + 60) / 2");
    }

    [Fact]
    public async Task OnGetAsync_SetsTrendValuesToZero()
    {
        // Arrange
        var aggregates = new List<CommandPerformanceAggregateDto>
        {
            new CommandPerformanceAggregateDto
            {
                CommandName = "cmd",
                ExecutionCount = 10,
                AvgMs = 50.0,
                P50Ms = 45.0,
                P95Ms = 100.0,
                P99Ms = 120.0,
                ErrorRate = 0.0
            }
        };

        _mockPerformanceAggregator
            .Setup(s => s.GetAggregatesAsync(24))
            .ReturnsAsync(aggregates.AsReadOnly());

        _mockPerformanceAggregator
            .Setup(s => s.GetSlowestCommandsAsync(10, 24))
            .ReturnsAsync(new List<SlowestCommandDto>().AsReadOnly());

        // Act
        await _commandsModel.OnGetAsync();

        // Assert
        _commandsModel.ViewModel.AvgResponseTimeTrend.Should().Be(0, "trend comparison not yet implemented");
        _commandsModel.ViewModel.ErrorRateTrend.Should().Be(0, "trend comparison not yet implemented");
        _commandsModel.ViewModel.P99Trend.Should().Be(0, "trend comparison not yet implemented");
    }
}
