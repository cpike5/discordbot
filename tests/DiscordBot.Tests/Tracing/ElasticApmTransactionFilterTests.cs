using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using Elastic.Apm.Api;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Tracing;

/// <summary>
/// Unit tests for <see cref="ElasticApmTransactionFilter"/>.
/// Tests intelligent filtering decisions based on operation priority, error status, and rate limiting.
/// </summary>
public class ElasticApmTransactionFilterTests
{
    private readonly Mock<ILogger<ElasticApmTransactionFilter>> _mockLogger;
    private readonly SamplingOptions _defaultOptions;

    public ElasticApmTransactionFilterTests()
    {
        _mockLogger = new Mock<ILogger<ElasticApmTransactionFilter>>();
        _defaultOptions = new SamplingOptions
        {
            DefaultRate = 0.1,
            ErrorRate = 1.0,
            SlowThresholdMs = 5000,
            HighPriorityRate = 0.5,
            LowPriorityRate = 0.01
        };
    }

    private ElasticApmTransactionFilter CreateFilter(SamplingOptions? options = null)
    {
        var samplingOptions = options ?? _defaultOptions;
        var optionsMock = new Mock<IOptions<SamplingOptions>>();
        optionsMock.Setup(o => o.Value).Returns(samplingOptions);
        return new ElasticApmTransactionFilter(optionsMock.Object, _mockLogger.Object);
    }

    private Mock<ITransaction> CreateMockTransaction(
        string name,
        string type = "request",
        Dictionary<string, string>? labels = null)
    {
        var mockTransaction = new Mock<ITransaction>();
        mockTransaction.SetupGet(t => t.Name).Returns(name);
        mockTransaction.SetupGet(t => t.Type).Returns(type);

        // Setup Labels dictionary (deprecated but needed for reading)
        var labelsDict = labels ?? new Dictionary<string, string>();
        mockTransaction.SetupGet(t => t.Labels).Returns(labelsDict);

        return mockTransaction;
    }

    #region Always Sample (100%) Tests

    [Fact]
    public void Filter_RateLimitHit_AlwaysSamples()
    {
        // Arrange
        var filter = CreateFilter();
        var labels = new Dictionary<string, string>
        {
            { TracingConstants.Attributes.DiscordApiRateLimitRemaining, "0" }
        };
        var mockTransaction = CreateMockTransaction("discord.api.POST /channels/123/messages", labels: labels);

        // Act
        var result = filter.Filter(mockTransaction.Object);

        // Assert
        result.Should().NotBeNull("rate limit hits should always be sampled for debugging");
        mockTransaction.Verify(t => t.SetLabel("sampling.rate", "1.00"), Times.Once);
        mockTransaction.Verify(t => t.SetLabel("sampling.decision", "sampled"), Times.Once);
    }

    [Fact]
    public void Filter_DiscordApiError_AlwaysSamples()
    {
        // Arrange
        var filter = CreateFilter();
        var labels = new Dictionary<string, string>
        {
            { TracingConstants.Attributes.DiscordApiErrorCode, "50001" },
            { TracingConstants.Attributes.DiscordApiErrorMessage, "Missing Access" }
        };
        var mockTransaction = CreateMockTransaction("discord.api.GET /guilds/123", labels: labels);

        // Act
        var result = filter.Filter(mockTransaction.Object);

        // Assert
        result.Should().NotBeNull("Discord API errors should always be sampled for diagnostics");
        mockTransaction.Verify(t => t.SetLabel("sampling.rate", "1.00"), Times.Once);
        mockTransaction.Verify(t => t.SetLabel("sampling.decision", "sampled"), Times.Once);
    }

    [Fact]
    public void Filter_DiscordApiErrorCode_AlwaysSamples()
    {
        // Arrange
        var filter = CreateFilter();
        var labels = new Dictionary<string, string>
        {
            { TracingConstants.Attributes.DiscordApiErrorCode, "50013" }
        };
        var mockTransaction = CreateMockTransaction("discord.api.DELETE /messages/123", labels: labels);

        // Act
        var result = filter.Filter(mockTransaction.Object);

        // Assert
        result.Should().NotBeNull("Discord API error codes should always be sampled");
        mockTransaction.Verify(t => t.SetLabel("sampling.rate", "1.00"), Times.Once);
    }

    [Fact]
    public void Filter_DiscordApiErrorMessage_AlwaysSamples()
    {
        // Arrange
        var filter = CreateFilter();
        var labels = new Dictionary<string, string>
        {
            { TracingConstants.Attributes.DiscordApiErrorMessage, "Unknown Guild" }
        };
        var mockTransaction = CreateMockTransaction("discord.api.GET /guilds/456", labels: labels);

        // Act
        var result = filter.Filter(mockTransaction.Object);

        // Assert
        result.Should().NotBeNull("Discord API error messages should always be sampled");
        mockTransaction.Verify(t => t.SetLabel("sampling.rate", "1.00"), Times.Once);
    }

    [Fact]
    public void Filter_AutoModSpamDetected_AlwaysSamples()
    {
        // Arrange
        var filter = CreateFilter();
        var mockTransaction = CreateMockTransaction(TracingConstants.Spans.DiscordEventAutoModSpamDetected);

        // Act
        var result = filter.Filter(mockTransaction.Object);

        // Assert
        result.Should().NotBeNull("auto-moderation spam detection should always be sampled for security");
        mockTransaction.Verify(t => t.SetLabel("sampling.rate", "1.00"), Times.Once);
        mockTransaction.Verify(t => t.SetLabel("sampling.decision", "sampled"), Times.Once);
    }

    [Fact]
    public void Filter_AutoModRaidDetected_AlwaysSamples()
    {
        // Arrange
        var filter = CreateFilter();
        var mockTransaction = CreateMockTransaction(TracingConstants.Spans.DiscordEventAutoModRaidDetected);

        // Act
        var result = filter.Filter(mockTransaction.Object);

        // Assert
        result.Should().NotBeNull("auto-moderation raid detection should always be sampled for security");
        mockTransaction.Verify(t => t.SetLabel("sampling.rate", "1.00"), Times.Once);
        mockTransaction.Verify(t => t.SetLabel("sampling.decision", "sampled"), Times.Once);
    }

    [Fact]
    public void Filter_AutoModContentFiltered_AlwaysSamples()
    {
        // Arrange
        var filter = CreateFilter();
        var mockTransaction = CreateMockTransaction(TracingConstants.Spans.DiscordEventAutoModContentFiltered);

        // Act
        var result = filter.Filter(mockTransaction.Object);

        // Assert
        result.Should().NotBeNull("auto-moderation content filtering should always be sampled for security");
        mockTransaction.Verify(t => t.SetLabel("sampling.rate", "1.00"), Times.Once);
        mockTransaction.Verify(t => t.SetLabel("sampling.decision", "sampled"), Times.Once);
    }

    [Fact]
    public void Filter_NameContainsAutoMod_AlwaysSamples()
    {
        // Arrange
        var filter = CreateFilter();
        var mockTransaction = CreateMockTransaction("service.automod.check_content");

        // Act
        var result = filter.Filter(mockTransaction.Object);

        // Assert
        result.Should().NotBeNull("any auto-moderation operations should always be sampled");
        mockTransaction.Verify(t => t.SetLabel("sampling.rate", "1.00"), Times.Once);
    }

    #endregion

    #region High Priority (50%) Tests

    [Fact]
    public void Filter_MemberJoinedEvent_UsesHighPriorityRate()
    {
        // Arrange
        var filter = CreateFilter();

        // Act - Run multiple samples to verify rate (probabilistic test)
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var mockTransaction = CreateMockTransaction(TracingConstants.Spans.DiscordEventMemberJoined);
            var result = filter.Filter(mockTransaction.Object);
            if (result != null)
            {
                sampledCount++;
            }
        }

        // Assert - Should be roughly 50% with some variance
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeInRange(0.40, 0.60,
            "member joined events should sample at ~50% (high priority rate)");
    }

    [Fact]
    public void Filter_WelcomeSendService_UsesHighPriorityRate()
    {
        // Arrange
        var filter = CreateFilter();

        // Act - Run multiple samples to verify rate
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var mockTransaction = CreateMockTransaction(TracingConstants.Spans.ServiceWelcomeSend);
            var result = filter.Filter(mockTransaction.Object);
            if (result != null)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeInRange(0.40, 0.60,
            "welcome send operations should sample at ~50% (high priority rate)");
    }

    [Fact]
    public void Filter_WelcomeChannelAttribute_UsesHighPriorityRate()
    {
        // Arrange
        var filter = CreateFilter();

        // Act - Run multiple samples
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var labels = new Dictionary<string, string>
            {
                { TracingConstants.Attributes.WelcomeChannelId, "123456789" }
            };
            var mockTransaction = CreateMockTransaction("discord.api.POST /channels/123/messages", labels: labels);
            var result = filter.Filter(mockTransaction.Object);
            if (result != null)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeInRange(0.40, 0.60,
            "operations with welcome channel should sample at ~50%");
    }

    [Fact]
    public void Filter_NameContainsMemberJoined_UsesHighPriorityRate()
    {
        // Arrange
        var filter = CreateFilter();

        // Act - Run multiple samples
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var mockTransaction = CreateMockTransaction("event.member.joined");
            var result = filter.Filter(mockTransaction.Object);
            if (result != null)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeInRange(0.40, 0.60,
            "operations with 'member.joined' should sample at ~50%");
    }

    [Fact]
    public void Filter_NameContainsWelcome_UsesHighPriorityRate()
    {
        // Arrange
        var filter = CreateFilter();

        // Act - Run multiple samples
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var mockTransaction = CreateMockTransaction("service.welcome.process");
            var result = filter.Filter(mockTransaction.Object);
            if (result != null)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeInRange(0.40, 0.60,
            "operations with 'welcome' should sample at ~50%");
    }

    [Theory]
    [InlineData("service.moderation.warn_user")]
    [InlineData("discord.command /warn")]
    [InlineData("discord.command /kick")]
    [InlineData("discord.command /ban")]
    [InlineData("discord.command /mute")]
    [InlineData("service.mod.execute_action")]
    public void Filter_ModerationOperations_UsesHighPriorityRate(string transactionName)
    {
        // Arrange
        var filter = CreateFilter();

        // Act - Run multiple samples
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var mockTransaction = CreateMockTransaction(transactionName);
            var result = filter.Filter(mockTransaction.Object);
            if (result != null)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeInRange(0.40, 0.60,
            $"moderation operation '{transactionName}' should sample at ~50%");
    }

    [Theory]
    [InlineData("service.ratwatch.record_incident")]
    [InlineData("discord.command /rat-clear")]
    [InlineData("service.rat_watch.get_stats")]
    public void Filter_RatWatchOperations_UsesHighPriorityRate(string transactionName)
    {
        // Arrange
        var filter = CreateFilter();

        // Act - Run multiple samples
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var mockTransaction = CreateMockTransaction(transactionName);
            var result = filter.Filter(mockTransaction.Object);
            if (result != null)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeInRange(0.40, 0.60,
            $"Rat Watch operation '{transactionName}' should sample at ~50%");
    }

    [Fact]
    public void Filter_ScheduledMessage_UsesHighPriorityRate()
    {
        // Arrange
        var filter = CreateFilter();

        // Act - Run multiple samples
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var mockTransaction = CreateMockTransaction("service.scheduled_message.execute");
            var result = filter.Filter(mockTransaction.Object);
            if (result != null)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeInRange(0.40, 0.60,
            "scheduled message operations should sample at ~50%");
    }

    #endregion

    #region Low Priority (1%) Tests

    [Fact]
    public void Filter_HealthCheckEndpoint_UsesLowPriorityRate()
    {
        // Arrange
        var filter = CreateFilter();

        // Act - Run multiple samples (need more samples for 1% rate)
        var sampleCount = 10000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var mockTransaction = CreateMockTransaction("GET /health");
            var result = filter.Filter(mockTransaction.Object);
            if (result != null)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeInRange(0.005, 0.015,
            "health check endpoints should sample at ~1% (low priority rate)");
    }

    [Fact]
    public void Filter_MetricsEndpoint_UsesLowPriorityRate()
    {
        // Arrange
        var filter = CreateFilter();

        // Act - Run multiple samples
        var sampleCount = 10000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var mockTransaction = CreateMockTransaction("GET /metrics");
            var result = filter.Filter(mockTransaction.Object);
            if (result != null)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeInRange(0.005, 0.015,
            "metrics endpoints should sample at ~1%");
    }

    [Theory]
    [InlineData("cache.get")]
    [InlineData("cache.set")]
    [InlineData("service.cache.get_item")]
    [InlineData("service.cache.set_item")]
    public void Filter_CacheOperations_UsesLowPriorityRate(string transactionName)
    {
        // Arrange
        var filter = CreateFilter();

        // Act - Run multiple samples
        var sampleCount = 10000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var mockTransaction = CreateMockTransaction(transactionName);
            var result = filter.Filter(mockTransaction.Object);
            if (result != null)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeInRange(0.005, 0.015,
            $"cache operation '{transactionName}' should sample at ~1%");
    }

    #endregion

    #region Default Priority Tests

    [Fact]
    public void Filter_RegularCommand_UsesDefaultRate()
    {
        // Arrange
        var filter = CreateFilter();

        // Act - Run multiple samples
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var mockTransaction = CreateMockTransaction("discord.command /ping");
            var result = filter.Filter(mockTransaction.Object);
            if (result != null)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeInRange(0.05, 0.15,
            "regular commands should sample at ~10% (default rate)");
    }

    [Fact]
    public void Filter_BackgroundServiceOperation_UsesDefaultRate()
    {
        // Arrange
        var filter = CreateFilter();

        // Act - Run multiple samples
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var mockTransaction = CreateMockTransaction("background.metrics_aggregation.execute");
            var result = filter.Filter(mockTransaction.Object);
            if (result != null)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeInRange(0.05, 0.15,
            "background service operations should sample at ~10%");
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void Constructor_InitializesWithDefaultOptions()
    {
        // Arrange & Act
        var filter = CreateFilter();

        // Assert
        filter.Should().NotBeNull("filter should initialize with default options");
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ElasticApmTransactionFilter initialized")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log initialization with configuration values");
    }

    [Fact]
    public void Constructor_UsesCustomOptions()
    {
        // Arrange
        var customOptions = new SamplingOptions
        {
            DefaultRate = 0.25,
            ErrorRate = 1.0,
            SlowThresholdMs = 3000,
            HighPriorityRate = 0.75,
            LowPriorityRate = 0.05
        };

        // Act
        var filter = CreateFilter(customOptions);

        // Run test with high priority operation to verify custom rate
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var mockTransaction = CreateMockTransaction("service.moderation.warn");
            var result = filter.Filter(mockTransaction.Object);
            if (result != null)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeInRange(0.70, 0.80,
            "high priority operations should use custom rate of 75%");
    }

    [Fact]
    public void Filter_AlwaysReturnsNullOrTransaction()
    {
        // Arrange
        var filter = CreateFilter();
        var mockTransaction = CreateMockTransaction("test.operation");

        // Act
        var result = filter.Filter(mockTransaction.Object);

        // Assert
        if (result != null)
        {
            result.Should().BeSameAs(mockTransaction.Object,
                "when sampled, should return the same transaction object");
        }
        else
        {
            // Transaction was dropped, which is valid
            result.Should().BeNull("when dropped, should return null");
        }
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public void Filter_NullTransactionName_DoesNotThrow()
    {
        // Arrange
        var filter = CreateFilter();
        var mockTransaction = CreateMockTransaction(null!);

        // Act
        var act = () => filter.Filter(mockTransaction.Object);

        // Assert
        act.Should().NotThrow("null transaction name should be handled gracefully");
    }

    [Fact]
    public void Filter_EmptyTransactionName_DoesNotThrow()
    {
        // Arrange
        var filter = CreateFilter();
        var mockTransaction = CreateMockTransaction(string.Empty);

        // Act
        var act = () => filter.Filter(mockTransaction.Object);

        // Assert
        act.Should().NotThrow("empty transaction name should be handled gracefully");
    }

    [Fact]
    public void Filter_EmptyLabels_DoesNotThrow()
    {
        // Arrange
        var filter = CreateFilter();
        var mockTransaction = CreateMockTransaction("test.operation", labels: new Dictionary<string, string>());

        // Act
        var act = () => filter.Filter(mockTransaction.Object);

        // Assert
        act.Should().NotThrow("empty labels should be handled gracefully");
    }

    [Fact]
    public void Filter_RateLimitRemainingNonZero_DoesNotAlwaysSample()
    {
        // Arrange
        var filter = CreateFilter();

        // Act - Run multiple samples
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var labels = new Dictionary<string, string>
            {
                { TracingConstants.Attributes.DiscordApiRateLimitRemaining, "5" }
            };
            var mockTransaction = CreateMockTransaction("discord.api.GET /guilds/123", labels: labels);
            var result = filter.Filter(mockTransaction.Object);
            if (result != null)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeLessThan(1.0,
            "non-zero rate limit remaining should not trigger always-sample");
    }

    [Fact]
    public void Filter_RateLimitRemainingInvalidFormat_DoesNotThrow()
    {
        // Arrange
        var filter = CreateFilter();
        var labels = new Dictionary<string, string>
        {
            { TracingConstants.Attributes.DiscordApiRateLimitRemaining, "invalid" }
        };
        var mockTransaction = CreateMockTransaction("discord.api.GET /guilds/123", labels: labels);

        // Act
        var act = () => filter.Filter(mockTransaction.Object);

        // Assert
        act.Should().NotThrow("invalid rate limit format should be handled gracefully");
    }

    [Fact]
    public void Filter_CaseInsensitiveTransactionNameMatching()
    {
        // Arrange
        var filter = CreateFilter();

        // Test with different casing
        var testCases = new[]
        {
            "SERVICE.AUTOMOD.CHECK",
            "Service.AutoMod.Check",
            "service.automod.check",
            "SERVICE.automod.CHECK"
        };

        // Act & Assert
        foreach (var transactionName in testCases)
        {
            var mockTransaction = CreateMockTransaction(transactionName);
            var result = filter.Filter(mockTransaction.Object);

            result.Should().NotBeNull(
                $"transaction name '{transactionName}' should match 'automod' case-insensitively and always be sampled");
        }
    }

    [Fact]
    public void Filter_SampledTransaction_SetsLabels()
    {
        // Arrange
        var filter = CreateFilter();
        var mockTransaction = CreateMockTransaction(TracingConstants.Spans.DiscordEventAutoModSpamDetected);

        // Act
        var result = filter.Filter(mockTransaction.Object);

        // Assert
        result.Should().NotBeNull("auto-moderation should always be sampled");
        mockTransaction.Verify(t => t.SetLabel("sampling.rate", It.IsAny<string>()), Times.Once,
            "should set sampling rate label");
        mockTransaction.Verify(t => t.SetLabel("sampling.decision", "sampled"), Times.Once,
            "should set sampling decision label");
    }

    [Fact]
    public void Filter_DroppedTransaction_DoesNotSetLabels()
    {
        // Arrange
        // Use 0% default rate to ensure transaction is always dropped
        var options = new SamplingOptions
        {
            DefaultRate = 0.0,
            HighPriorityRate = 0.0,
            LowPriorityRate = 0.0
        };
        var filter = CreateFilter(options);
        var mockTransaction = CreateMockTransaction("discord.command /ping");

        // Act
        var result = filter.Filter(mockTransaction.Object);

        // Assert
        result.Should().BeNull("transaction should be dropped with 0% sampling rate");
        mockTransaction.Verify(t => t.SetLabel(It.IsAny<string>(), It.IsAny<string>()), Times.Never,
            "should not set labels on dropped transactions");
    }

    [Fact]
    public void Filter_RateLimitRemaining_ZeroString_AlwaysSamples()
    {
        // Arrange
        var filter = CreateFilter();
        var labels = new Dictionary<string, string>
        {
            { TracingConstants.Attributes.DiscordApiRateLimitRemaining, "0" }
        };
        var mockTransaction = CreateMockTransaction("discord.api.POST /messages", labels: labels);

        // Act
        var result = filter.Filter(mockTransaction.Object);

        // Assert
        result.Should().NotBeNull("rate limit remaining '0' should always be sampled");
        mockTransaction.Verify(t => t.SetLabel("sampling.rate", "1.00"), Times.Once);
    }

    [Fact]
    public void Filter_MultipleAlwaysSampleConditions_StillAlwaysSamples()
    {
        // Arrange
        var filter = CreateFilter();
        var labels = new Dictionary<string, string>
        {
            { TracingConstants.Attributes.DiscordApiRateLimitRemaining, "0" },
            { TracingConstants.Attributes.DiscordApiErrorCode, "429" }
        };
        var mockTransaction = CreateMockTransaction("service.automod.process", labels: labels);

        // Act
        var result = filter.Filter(mockTransaction.Object);

        // Assert
        result.Should().NotBeNull("multiple always-sample conditions should still always sample");
        mockTransaction.Verify(t => t.SetLabel("sampling.rate", "1.00"), Times.Once);
    }

    [Fact]
    public void Filter_NullTransactionType_DoesNotThrow()
    {
        // Arrange
        var filter = CreateFilter();
        var mockTransaction = CreateMockTransaction("test.operation", type: null!);

        // Act
        var act = () => filter.Filter(mockTransaction.Object);

        // Assert
        act.Should().NotThrow("null transaction type should be handled gracefully");
    }

    #endregion

    #region Label Reading Tests

    [Fact]
    public void Filter_LabelsWithoutRateLimitRemaining_DoesNotAlwaysSample()
    {
        // Arrange
        var filter = CreateFilter();
        var labels = new Dictionary<string, string>
        {
            { "some.other.label", "value" }
        };

        // Act - Run multiple samples
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var mockTransaction = CreateMockTransaction("discord.api.GET /guilds/123", labels: labels);
            var result = filter.Filter(mockTransaction.Object);
            if (result != null)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeLessThan(1.0,
            "labels without rate limit should not trigger always-sample");
    }

    [Fact]
    public void Filter_WelcomeChannelIdLabel_UsesHighPriorityRate()
    {
        // Arrange
        var filter = CreateFilter();

        // Act - Run multiple samples
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var labels = new Dictionary<string, string>
            {
                { TracingConstants.Attributes.WelcomeChannelId, "999888777" }
            };
            var mockTransaction = CreateMockTransaction("api.send_message", labels: labels);
            var result = filter.Filter(mockTransaction.Object);
            if (result != null)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeInRange(0.40, 0.60,
            "welcome channel ID label should trigger high priority sampling");
    }

    #endregion
}
