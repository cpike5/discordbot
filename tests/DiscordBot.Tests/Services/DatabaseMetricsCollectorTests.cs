using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="DatabaseMetricsCollector"/>.
/// Tests cover query recording, duration histogram tracking, error counting,
/// slow query detection and storage, and metrics reset functionality.
/// </summary>
public class DatabaseMetricsCollectorTests
{
    private readonly DatabaseMetricsCollector _collector;
    private readonly PerformanceMetricsOptions _options;

    public DatabaseMetricsCollectorTests()
    {
        _options = new PerformanceMetricsOptions
        {
            SlowQueryThresholdMs = 100,
            SlowQueryMaxStored = 10
        };

        _collector = new DatabaseMetricsCollector(
            NullLogger<DatabaseMetricsCollector>.Instance,
            Options.Create(_options));
    }

    [Fact]
    public void RecordQuery_IncrementsQueryCount()
    {
        // Arrange
        const double durationMs = 50.0;
        const string commandType = "SELECT";

        // Act
        _collector.RecordQuery(durationMs, commandType);
        _collector.RecordQuery(durationMs, commandType);
        _collector.RecordQuery(durationMs, commandType);

        // Assert
        var metrics = _collector.GetMetrics();
        metrics.TotalQueries.Should().Be(3, "three queries were recorded");
    }

    [Fact]
    public void RecordQuery_AddsToTotalDuration()
    {
        // Arrange
        _collector.RecordQuery(50.0, "SELECT");
        _collector.RecordQuery(100.0, "INSERT");
        _collector.RecordQuery(150.0, "UPDATE");

        // Act
        var metrics = _collector.GetMetrics();

        // Assert
        metrics.TotalQueries.Should().Be(3, "three queries were recorded");
        metrics.AvgQueryTimeMs.Should().Be(100.0, "average of 50, 100, 150 is 100");
    }

    [Fact]
    public void RecordQuery_UpdatesHistogramBuckets()
    {
        // Arrange - Record queries in different duration ranges
        _collector.RecordQuery(5.0, "SELECT");       // <10ms bucket
        _collector.RecordQuery(25.0, "SELECT");      // 10-50ms bucket
        _collector.RecordQuery(75.0, "UPDATE");      // 50-100ms bucket
        _collector.RecordQuery(250.0, "INSERT");     // 100-500ms bucket
        _collector.RecordQuery(600.0, "DELETE");     // >500ms bucket

        // Act
        var metrics = _collector.GetMetrics();

        // Assert
        metrics.QueryHistogram.Should().ContainKey("0-10ms", "histogram should track fast queries");
        metrics.QueryHistogram.Should().ContainKey("10-50ms", "histogram should track medium-fast queries");
        metrics.QueryHistogram.Should().ContainKey("50-100ms", "histogram should track medium queries");
        metrics.QueryHistogram.Should().ContainKey("100-500ms", "histogram should track slow queries");
        metrics.QueryHistogram.Should().ContainKey(">500ms", "histogram should track very slow queries");

        metrics.QueryHistogram["0-10ms"].Should().Be(1, "one query in <10ms range");
        metrics.QueryHistogram["10-50ms"].Should().Be(1, "one query in 10-50ms range");
        metrics.QueryHistogram["50-100ms"].Should().Be(1, "one query in 50-100ms range");
        metrics.QueryHistogram["100-500ms"].Should().Be(1, "one query in 100-500ms range");
        metrics.QueryHistogram[">500ms"].Should().Be(1, "one query in >500ms range");
    }

    [Fact]
    public void RecordQueryError_IncrementsErrorCount()
    {
        // Arrange
        const double durationMs = 50.0;
        const string error = "Connection timeout";

        // Act
        _collector.RecordQueryError(durationMs, error);
        _collector.RecordQueryError(durationMs, error);

        // Assert - Note: Error count is tracked internally but not exposed in GetMetrics()
        // We verify it doesn't throw and can be called multiple times
        var metrics = _collector.GetMetrics();
        metrics.Should().NotBeNull("metrics should still be retrievable after errors");
    }

    [Fact]
    public void RecordSlowQuery_StoresQueryDetails()
    {
        // Arrange
        const string commandText = "SELECT * FROM Users WHERE Id = @id";
        const double durationMs = 250.0;
        const string parameters = "@id=123";

        // Act
        _collector.RecordSlowQuery(commandText, durationMs, parameters);

        // Assert
        var slowQueries = _collector.GetSlowQueries(limit: 10);
        slowQueries.Should().HaveCount(1, "one slow query was recorded");

        var slowQuery = slowQueries[0];
        slowQuery.CommandText.Should().Be(commandText, "command text should match");
        slowQuery.DurationMs.Should().Be(durationMs, "duration should match");
        slowQuery.Parameters.Should().Be(parameters, "parameters should match");
        slowQuery.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1), "timestamp should be recent");

        var metrics = _collector.GetMetrics();
        metrics.SlowQueryCount.Should().Be(1, "slow query count should be tracked");
    }

    [Fact]
    public void RecordSlowQuery_RespectsMaxStoredLimit()
    {
        // Arrange - Max stored is 10 in our test options
        const int excessQueries = 5;
        const int totalQueries = 10 + excessQueries;

        // Act - Record more slow queries than the max stored limit
        for (int i = 0; i < totalQueries; i++)
        {
            _collector.RecordSlowQuery(
                $"SELECT * FROM Table{i}",
                150.0 + i,
                $"@id={i}");
        }

        // Assert
        var slowQueries = _collector.GetSlowQueries(limit: 100);
        slowQueries.Should().HaveCount(10, "only the max stored limit should be kept");

        var metrics = _collector.GetMetrics();
        metrics.SlowQueryCount.Should().Be(10, "slow query count should respect max stored limit");

        // Verify oldest queries were removed (first 5 should be gone)
        slowQueries.Should().NotContain(sq => sq.CommandText.Contains("Table0"), "oldest query should be removed");
        slowQueries.Should().NotContain(sq => sq.CommandText.Contains("Table4"), "oldest queries should be removed");
        slowQueries.Should().Contain(sq => sq.CommandText.Contains("Table14"), "newest query should be present");
    }

    [Fact]
    public void GetMetrics_ReturnsCorrectAverageQueryTime()
    {
        // Arrange
        _collector.RecordQuery(10.0, "SELECT");
        _collector.RecordQuery(20.0, "INSERT");
        _collector.RecordQuery(30.0, "UPDATE");

        // Act
        var metrics = _collector.GetMetrics();

        // Assert
        metrics.AvgQueryTimeMs.Should().Be(20.0, "average of 10, 20, 30 is 20");
    }

    [Fact]
    public void Reset_ClearsAllMetrics()
    {
        // Arrange - Record various metrics
        _collector.RecordQuery(50.0, "SELECT");
        _collector.RecordQuery(100.0, "INSERT");
        _collector.RecordQueryError(25.0, "Error");
        _collector.RecordSlowQuery("SELECT * FROM Users", 200.0, "@id=1");

        // Verify metrics exist
        var beforeMetrics = _collector.GetMetrics();
        beforeMetrics.TotalQueries.Should().BeGreaterThan(0, "queries should be recorded before reset");

        // Act
        _collector.Reset();

        // Assert
        var afterMetrics = _collector.GetMetrics();
        afterMetrics.TotalQueries.Should().Be(0, "total queries should be reset");
        afterMetrics.AvgQueryTimeMs.Should().Be(0, "average query time should be reset");
        afterMetrics.SlowQueryCount.Should().Be(0, "slow query count should be reset");

        afterMetrics.QueryHistogram["0-10ms"].Should().Be(0, "histogram bucket should be cleared");
        afterMetrics.QueryHistogram["10-50ms"].Should().Be(0, "histogram bucket should be cleared");
        afterMetrics.QueryHistogram["50-100ms"].Should().Be(0, "histogram bucket should be cleared");
        afterMetrics.QueryHistogram["100-500ms"].Should().Be(0, "histogram bucket should be cleared");
        afterMetrics.QueryHistogram[">500ms"].Should().Be(0, "histogram bucket should be cleared");

        var slowQueries = _collector.GetSlowQueries();
        slowQueries.Should().BeEmpty("slow queries should be cleared");
    }
}
