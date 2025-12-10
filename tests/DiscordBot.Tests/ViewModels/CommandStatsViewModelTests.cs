using DiscordBot.Bot.ViewModels.Pages;
using FluentAssertions;

namespace DiscordBot.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="CommandStatsViewModel"/>.
/// </summary>
public class CommandStatsViewModelTests
{
    [Fact]
    public void FromStats_WithEmptyStats_ReturnsZeroTotalAndEmptyList()
    {
        // Arrange
        var emptyStats = new Dictionary<string, int>();

        // Act
        var result = CommandStatsViewModel.FromStats(emptyStats);

        // Assert
        result.Should().NotBeNull();
        result.TotalCommands.Should().Be(0, "there are no commands in the stats");
        result.TopCommands.Should().BeEmpty("there are no commands to display");
        result.TimeRangeLabel.Should().Be("All Time", "default time range is all time");
        result.TimeRangeHours.Should().BeNull("no time range filter is specified");
    }

    [Fact]
    public void FromStats_WithStats_CalculatesTotalCorrectly()
    {
        // Arrange
        var stats = new Dictionary<string, int>
        {
            { "ping", 10 },
            { "status", 5 },
            { "help", 3 },
            { "info", 7 }
        };

        // Act
        var result = CommandStatsViewModel.FromStats(stats);

        // Assert
        result.Should().NotBeNull();
        result.TotalCommands.Should().Be(25, "the sum of all command counts is 10 + 5 + 3 + 7 = 25");
        result.TopCommands.Should().HaveCount(4, "there are 4 commands in the stats");
    }

    [Fact]
    public void FromStats_WithMoreThan10Commands_ReturnsOnlyTop10()
    {
        // Arrange
        var stats = new Dictionary<string, int>
        {
            { "command1", 100 },
            { "command2", 90 },
            { "command3", 80 },
            { "command4", 70 },
            { "command5", 60 },
            { "command6", 50 },
            { "command7", 40 },
            { "command8", 30 },
            { "command9", 20 },
            { "command10", 10 },
            { "command11", 5 },
            { "command12", 3 },
            { "command13", 1 }
        };

        // Act
        var result = CommandStatsViewModel.FromStats(stats);

        // Assert
        result.Should().NotBeNull();
        result.TotalCommands.Should().Be(559, "the sum of all 13 commands");
        result.TopCommands.Should().HaveCount(10, "only the top 10 commands should be returned");
        result.TopCommands.Should().NotContain(c => c.CommandName == "command11", "command11 is 11th by count");
        result.TopCommands.Should().NotContain(c => c.CommandName == "command12", "command12 is 12th by count");
        result.TopCommands.Should().NotContain(c => c.CommandName == "command13", "command13 is 13th by count");
    }

    [Fact]
    public void FromStats_OrdersByCountDescending()
    {
        // Arrange
        var stats = new Dictionary<string, int>
        {
            { "help", 3 },
            { "ping", 10 },
            { "info", 7 },
            { "status", 5 }
        };

        // Act
        var result = CommandStatsViewModel.FromStats(stats);

        // Assert
        result.Should().NotBeNull();
        result.TopCommands.Should().BeInDescendingOrder(c => c.Count, "commands should be ordered by count descending");
        result.TopCommands[0].CommandName.Should().Be("ping", "ping has the highest count (10)");
        result.TopCommands[0].Count.Should().Be(10);
        result.TopCommands[1].CommandName.Should().Be("info", "info has the second highest count (7)");
        result.TopCommands[1].Count.Should().Be(7);
        result.TopCommands[2].CommandName.Should().Be("status", "status has the third highest count (5)");
        result.TopCommands[2].Count.Should().Be(5);
        result.TopCommands[3].CommandName.Should().Be("help", "help has the lowest count (3)");
        result.TopCommands[3].Count.Should().Be(3);
    }

    [Fact]
    public void FromStats_AssignsCorrectRanks()
    {
        // Arrange
        var stats = new Dictionary<string, int>
        {
            { "command1", 100 },
            { "command2", 90 },
            { "command3", 80 },
            { "command4", 70 },
            { "command5", 60 }
        };

        // Act
        var result = CommandStatsViewModel.FromStats(stats);

        // Assert
        result.Should().NotBeNull();
        result.TopCommands.Should().HaveCount(5);

        // Verify each command has the correct rank (1-based index)
        for (int i = 0; i < result.TopCommands.Count; i++)
        {
            result.TopCommands[i].Rank.Should().Be(i + 1, $"command at index {i} should have rank {i + 1}");
        }

        result.TopCommands[0].Rank.Should().Be(1, "first command should have rank 1");
        result.TopCommands[1].Rank.Should().Be(2, "second command should have rank 2");
        result.TopCommands[2].Rank.Should().Be(3, "third command should have rank 3");
        result.TopCommands[3].Rank.Should().Be(4, "fourth command should have rank 4");
        result.TopCommands[4].Rank.Should().Be(5, "fifth command should have rank 5");
    }

    [Fact]
    public void FromStats_With24HoursTimeRange_SetsCorrectLabel()
    {
        // Arrange
        var stats = new Dictionary<string, int>
        {
            { "ping", 10 }
        };

        // Act
        var result = CommandStatsViewModel.FromStats(stats, timeRangeHours: 24);

        // Assert
        result.Should().NotBeNull();
        result.TimeRangeHours.Should().Be(24, "time range is set to 24 hours");
        result.TimeRangeLabel.Should().Be("Last 24 Hours", "24 hours should map to 'Last 24 Hours'");
    }

    [Fact]
    public void FromStats_With168HoursTimeRange_SetsCorrectLabel()
    {
        // Arrange
        var stats = new Dictionary<string, int>
        {
            { "ping", 10 }
        };

        // Act
        var result = CommandStatsViewModel.FromStats(stats, timeRangeHours: 168);

        // Assert
        result.Should().NotBeNull();
        result.TimeRangeHours.Should().Be(168, "time range is set to 168 hours (7 days)");
        result.TimeRangeLabel.Should().Be("Last 7 Days", "168 hours should map to 'Last 7 Days'");
    }

    [Fact]
    public void FromStats_With720HoursTimeRange_SetsCorrectLabel()
    {
        // Arrange
        var stats = new Dictionary<string, int>
        {
            { "ping", 10 }
        };

        // Act
        var result = CommandStatsViewModel.FromStats(stats, timeRangeHours: 720);

        // Assert
        result.Should().NotBeNull();
        result.TimeRangeHours.Should().Be(720, "time range is set to 720 hours (30 days)");
        result.TimeRangeLabel.Should().Be("Last 30 Days", "720 hours should map to 'Last 30 Days'");
    }

    [Fact]
    public void FromStats_WithNullTimeRange_SetsAllTimeLabel()
    {
        // Arrange
        var stats = new Dictionary<string, int>
        {
            { "ping", 10 }
        };

        // Act
        var result = CommandStatsViewModel.FromStats(stats, timeRangeHours: null);

        // Assert
        result.Should().NotBeNull();
        result.TimeRangeHours.Should().BeNull("no time range filter is specified");
        result.TimeRangeLabel.Should().Be("All Time", "null time range should map to 'All Time'");
    }

    [Fact]
    public void FromStats_WithUnrecognizedTimeRange_SetsAllTimeLabel()
    {
        // Arrange
        var stats = new Dictionary<string, int>
        {
            { "ping", 10 }
        };

        // Act
        var result = CommandStatsViewModel.FromStats(stats, timeRangeHours: 999);

        // Assert
        result.Should().NotBeNull();
        result.TimeRangeHours.Should().Be(999, "time range is set to 999 hours");
        result.TimeRangeLabel.Should().Be("All Time", "unrecognized time range values should default to 'All Time'");
    }

    [Fact]
    public void FromStats_WithSingleCommand_ReturnsCorrectData()
    {
        // Arrange
        var stats = new Dictionary<string, int>
        {
            { "ping", 42 }
        };

        // Act
        var result = CommandStatsViewModel.FromStats(stats);

        // Assert
        result.Should().NotBeNull();
        result.TotalCommands.Should().Be(42, "total should equal the single command count");
        result.TopCommands.Should().ContainSingle("there is only one command");
        result.TopCommands[0].CommandName.Should().Be("ping");
        result.TopCommands[0].Count.Should().Be(42);
        result.TopCommands[0].Rank.Should().Be(1, "the single command should have rank 1");
    }

    [Fact]
    public void FromStats_WithCommandsHavingEqualCounts_PreservesStableOrder()
    {
        // Arrange
        var stats = new Dictionary<string, int>
        {
            { "alpha", 10 },
            { "beta", 10 },
            { "gamma", 10 }
        };

        // Act
        var result = CommandStatsViewModel.FromStats(stats);

        // Assert
        result.Should().NotBeNull();
        result.TotalCommands.Should().Be(30, "total is 10 + 10 + 10 = 30");
        result.TopCommands.Should().HaveCount(3, "all three commands should be included");
        result.TopCommands.Should().AllSatisfy(c => c.Count.Should().Be(10, "all commands have count 10"));

        // All ranks should be sequential even with equal counts
        result.TopCommands[0].Rank.Should().Be(1);
        result.TopCommands[1].Rank.Should().Be(2);
        result.TopCommands[2].Rank.Should().Be(3);
    }

    [Fact]
    public void CommandUsageStat_IsRecordType_SupportsValueEquality()
    {
        // Arrange
        var stat1 = new CommandUsageStat("ping", 10, 1);
        var stat2 = new CommandUsageStat("ping", 10, 1);
        var stat3 = new CommandUsageStat("status", 10, 1);

        // Act & Assert
        stat1.Should().Be(stat2, "records with same values should be equal");
        stat1.Should().NotBe(stat3, "records with different values should not be equal");
        stat1.GetHashCode().Should().Be(stat2.GetHashCode(), "equal records should have same hash code");
    }

    [Fact]
    public void CommandStatsViewModel_IsRecordType_SupportsValueEquality()
    {
        // Arrange
        var topCommands = new List<CommandUsageStat>
        {
            new CommandUsageStat("ping", 10, 1)
        };

        var viewModel1 = new CommandStatsViewModel
        {
            TotalCommands = 10,
            TopCommands = topCommands,
            TimeRangeHours = 24,
            TimeRangeLabel = "Last 24 Hours"
        };

        var viewModel2 = new CommandStatsViewModel
        {
            TotalCommands = 10,
            TopCommands = topCommands,
            TimeRangeHours = 24,
            TimeRangeLabel = "Last 24 Hours"
        };

        // Act & Assert
        viewModel1.Should().BeEquivalentTo(viewModel2, "view models with same values should be equivalent");
    }

    [Fact]
    public void FromStats_WithExactly10Commands_ReturnsAll10()
    {
        // Arrange
        var stats = new Dictionary<string, int>();
        for (int i = 1; i <= 10; i++)
        {
            stats[$"command{i}"] = 100 - (i * 5); // Descending counts
        }

        // Act
        var result = CommandStatsViewModel.FromStats(stats);

        // Assert
        result.Should().NotBeNull();
        result.TopCommands.Should().HaveCount(10, "all 10 commands should be returned");
        result.TopCommands.Should().OnlyHaveUniqueItems(c => c.Rank, "each rank should be unique");
        result.TopCommands.Select(c => c.Rank).Should().BeInAscendingOrder("ranks should be 1-10 in order");
    }

    [Fact]
    public void FromStats_WithZeroCountCommands_IncludesThemInStats()
    {
        // Arrange
        var stats = new Dictionary<string, int>
        {
            { "ping", 10 },
            { "unused", 0 },
            { "status", 5 }
        };

        // Act
        var result = CommandStatsViewModel.FromStats(stats);

        // Assert
        result.Should().NotBeNull();
        result.TotalCommands.Should().Be(15, "total includes all counts including zero");
        result.TopCommands.Should().HaveCount(3, "all commands should be included even with zero count");
        result.TopCommands.Should().Contain(c => c.CommandName == "unused" && c.Count == 0,
            "zero-count commands should be included");
    }
}
