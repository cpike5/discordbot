using System.Diagnostics;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OpenTelemetry.Trace;

namespace DiscordBot.Tests.Tracing;

/// <summary>
/// Unit tests for <see cref="PrioritySampler"/>.
/// Tests intelligent sampling decisions based on operation priority, error status, and latency.
/// </summary>
public class PrioritySamplerTests
{
    private readonly Mock<ILogger<PrioritySampler>> _mockLogger;
    private readonly SamplingOptions _defaultOptions;

    public PrioritySamplerTests()
    {
        _mockLogger = new Mock<ILogger<PrioritySampler>>();
        _defaultOptions = new SamplingOptions
        {
            DefaultRate = 0.1,
            ErrorRate = 1.0,
            SlowThresholdMs = 5000,
            HighPriorityRate = 0.5,
            LowPriorityRate = 0.01
        };
    }

    private PrioritySampler CreateSampler(SamplingOptions? options = null)
    {
        var samplingOptions = options ?? _defaultOptions;
        var optionsMock = new Mock<IOptions<SamplingOptions>>();
        optionsMock.Setup(o => o.Value).Returns(samplingOptions);
        return new PrioritySampler(optionsMock.Object, _mockLogger.Object);
    }

    #region Always Sample (100%) Tests

    [Fact]
    public void ShouldSample_RateLimitHit_AlwaysSamples()
    {
        // Arrange
        var sampler = CreateSampler();
        var tags = new List<KeyValuePair<string, object?>>
        {
            new(TracingConstants.Attributes.DiscordApiRateLimitRemaining, "0")
        };

        var samplingParameters = new SamplingParameters(
            parentContext: default,
            traceId: ActivityTraceId.CreateRandom(),
            name: "discord.api.POST /channels/123/messages",
            kind: ActivityKind.Client,
            tags: tags,
            links: null
        );

        // Act
        var result = sampler.ShouldSample(samplingParameters);

        // Assert
        result.Decision.Should().Be(SamplingDecision.RecordAndSample,
            "rate limit hits should always be sampled for debugging");
    }

    [Fact]
    public void ShouldSample_DiscordApiError_AlwaysSamples()
    {
        // Arrange
        var sampler = CreateSampler();
        var tags = new List<KeyValuePair<string, object?>>
        {
            new(TracingConstants.Attributes.DiscordApiErrorCode, "50001"),
            new(TracingConstants.Attributes.DiscordApiErrorMessage, "Missing Access")
        };

        var samplingParameters = new SamplingParameters(
            parentContext: default,
            traceId: ActivityTraceId.CreateRandom(),
            name: "discord.api.GET /guilds/123",
            kind: ActivityKind.Client,
            tags: tags,
            links: null
        );

        // Act
        var result = sampler.ShouldSample(samplingParameters);

        // Assert
        result.Decision.Should().Be(SamplingDecision.RecordAndSample,
            "Discord API errors should always be sampled for diagnostics");
    }

    [Fact]
    public void ShouldSample_AutoModSpamDetected_AlwaysSamples()
    {
        // Arrange
        var sampler = CreateSampler();
        var samplingParameters = new SamplingParameters(
            parentContext: default,
            traceId: ActivityTraceId.CreateRandom(),
            name: TracingConstants.Spans.DiscordEventAutoModSpamDetected,
            kind: ActivityKind.Internal,
            tags: null,
            links: null
        );

        // Act
        var result = sampler.ShouldSample(samplingParameters);

        // Assert
        result.Decision.Should().Be(SamplingDecision.RecordAndSample,
            "auto-moderation spam detection should always be sampled for security");
    }

    [Fact]
    public void ShouldSample_AutoModRaidDetected_AlwaysSamples()
    {
        // Arrange
        var sampler = CreateSampler();
        var samplingParameters = new SamplingParameters(
            parentContext: default,
            traceId: ActivityTraceId.CreateRandom(),
            name: TracingConstants.Spans.DiscordEventAutoModRaidDetected,
            kind: ActivityKind.Internal,
            tags: null,
            links: null
        );

        // Act
        var result = sampler.ShouldSample(samplingParameters);

        // Assert
        result.Decision.Should().Be(SamplingDecision.RecordAndSample,
            "auto-moderation raid detection should always be sampled for security");
    }

    [Fact]
    public void ShouldSample_AutoModContentFiltered_AlwaysSamples()
    {
        // Arrange
        var sampler = CreateSampler();
        var samplingParameters = new SamplingParameters(
            parentContext: default,
            traceId: ActivityTraceId.CreateRandom(),
            name: TracingConstants.Spans.DiscordEventAutoModContentFiltered,
            kind: ActivityKind.Internal,
            tags: null,
            links: null
        );

        // Act
        var result = sampler.ShouldSample(samplingParameters);

        // Assert
        result.Decision.Should().Be(SamplingDecision.RecordAndSample,
            "auto-moderation content filtering should always be sampled for security");
    }

    [Fact]
    public void ShouldSample_SpanNameContainsAutoMod_AlwaysSamples()
    {
        // Arrange
        var sampler = CreateSampler();
        var samplingParameters = new SamplingParameters(
            parentContext: default,
            traceId: ActivityTraceId.CreateRandom(),
            name: "service.automod.check_content",
            kind: ActivityKind.Internal,
            tags: null,
            links: null
        );

        // Act
        var result = sampler.ShouldSample(samplingParameters);

        // Assert
        result.Decision.Should().Be(SamplingDecision.RecordAndSample,
            "any auto-moderation operations should always be sampled");
    }

    #endregion

    #region High Priority (50%) Tests

    [Fact]
    public void ShouldSample_MemberJoinedEvent_UsesHighPriorityRate()
    {
        // Arrange
        var sampler = CreateSampler();
        var samplingParameters = new SamplingParameters(
            parentContext: default,
            traceId: ActivityTraceId.CreateRandom(),
            name: TracingConstants.Spans.DiscordEventMemberJoined,
            kind: ActivityKind.Internal,
            tags: null,
            links: null
        );

        // Act - Run multiple samples to verify rate (probabilistic test)
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var params2 = new SamplingParameters(
                parentContext: default,
                traceId: ActivityTraceId.CreateRandom(),
                name: TracingConstants.Spans.DiscordEventMemberJoined,
                kind: ActivityKind.Internal,
                tags: null,
                links: null
            );

            var result = sampler.ShouldSample(params2);
            if (result.Decision == SamplingDecision.RecordAndSample)
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
    public void ShouldSample_WelcomeSendService_UsesHighPriorityRate()
    {
        // Arrange
        var sampler = CreateSampler();
        var samplingParameters = new SamplingParameters(
            parentContext: default,
            traceId: ActivityTraceId.CreateRandom(),
            name: TracingConstants.Spans.ServiceWelcomeSend,
            kind: ActivityKind.Internal,
            tags: null,
            links: null
        );

        // Act - Run multiple samples to verify rate
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var params2 = new SamplingParameters(
                parentContext: default,
                traceId: ActivityTraceId.CreateRandom(),
                name: TracingConstants.Spans.ServiceWelcomeSend,
                kind: ActivityKind.Internal,
                tags: null,
                links: null
            );

            var result = sampler.ShouldSample(params2);
            if (result.Decision == SamplingDecision.RecordAndSample)
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
    public void ShouldSample_WelcomeChannelAttribute_UsesHighPriorityRate()
    {
        // Arrange
        var sampler = CreateSampler();
        var tags = new List<KeyValuePair<string, object?>>
        {
            new(TracingConstants.Attributes.WelcomeChannelId, "123456789")
        };

        var samplingParameters = new SamplingParameters(
            parentContext: default,
            traceId: ActivityTraceId.CreateRandom(),
            name: "discord.api.POST /channels/123/messages",
            kind: ActivityKind.Client,
            tags: tags,
            links: null
        );

        // Act - Run multiple samples
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var params2 = new SamplingParameters(
                parentContext: default,
                traceId: ActivityTraceId.CreateRandom(),
                name: "discord.api.POST /channels/123/messages",
                kind: ActivityKind.Client,
                tags: tags,
                links: null
            );

            var result = sampler.ShouldSample(params2);
            if (result.Decision == SamplingDecision.RecordAndSample)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeInRange(0.40, 0.60,
            "operations with welcome channel should sample at ~50%");
    }

    [Theory]
    [InlineData("service.moderation.warn_user")]
    [InlineData("discord.command /warn")]
    [InlineData("discord.command /kick")]
    [InlineData("discord.command /ban")]
    [InlineData("discord.command /mute")]
    [InlineData("service.mod.execute_action")]
    public void ShouldSample_ModerationOperations_UsesHighPriorityRate(string spanName)
    {
        // Arrange
        var sampler = CreateSampler();

        // Act - Run multiple samples
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var params2 = new SamplingParameters(
                parentContext: default,
                traceId: ActivityTraceId.CreateRandom(),
                name: spanName,
                kind: ActivityKind.Internal,
                tags: null,
                links: null
            );

            var result = sampler.ShouldSample(params2);
            if (result.Decision == SamplingDecision.RecordAndSample)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeInRange(0.40, 0.60,
            $"moderation operation '{spanName}' should sample at ~50%");
    }

    [Theory]
    [InlineData("service.ratwatch.record_incident")]
    [InlineData("discord.command /rat-clear")]
    [InlineData("service.rat_watch.get_stats")]
    public void ShouldSample_RatWatchOperations_UsesHighPriorityRate(string spanName)
    {
        // Arrange
        var sampler = CreateSampler();

        // Act - Run multiple samples
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var params2 = new SamplingParameters(
                parentContext: default,
                traceId: ActivityTraceId.CreateRandom(),
                name: spanName,
                kind: ActivityKind.Internal,
                tags: null,
                links: null
            );

            var result = sampler.ShouldSample(params2);
            if (result.Decision == SamplingDecision.RecordAndSample)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeInRange(0.40, 0.60,
            $"Rat Watch operation '{spanName}' should sample at ~50%");
    }

    [Fact]
    public void ShouldSample_ScheduledMessage_UsesHighPriorityRate()
    {
        // Arrange
        var sampler = CreateSampler();

        // Act - Run multiple samples
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var params2 = new SamplingParameters(
                parentContext: default,
                traceId: ActivityTraceId.CreateRandom(),
                name: "service.scheduled_message.execute",
                kind: ActivityKind.Internal,
                tags: null,
                links: null
            );

            var result = sampler.ShouldSample(params2);
            if (result.Decision == SamplingDecision.RecordAndSample)
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
    public void ShouldSample_HealthCheckEndpoint_UsesLowPriorityRate()
    {
        // Arrange
        var sampler = CreateSampler();

        // Act - Run multiple samples
        var sampleCount = 10000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var params2 = new SamplingParameters(
                parentContext: default,
                traceId: ActivityTraceId.CreateRandom(),
                name: "GET /health",
                kind: ActivityKind.Server,
                tags: null,
                links: null
            );

            var result = sampler.ShouldSample(params2);
            if (result.Decision == SamplingDecision.RecordAndSample)
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
    public void ShouldSample_MetricsEndpoint_UsesLowPriorityRate()
    {
        // Arrange
        var sampler = CreateSampler();

        // Act - Run multiple samples
        var sampleCount = 10000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var params2 = new SamplingParameters(
                parentContext: default,
                traceId: ActivityTraceId.CreateRandom(),
                name: "GET /metrics",
                kind: ActivityKind.Server,
                tags: null,
                links: null
            );

            var result = sampler.ShouldSample(params2);
            if (result.Decision == SamplingDecision.RecordAndSample)
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
    public void ShouldSample_CacheOperations_UsesLowPriorityRate(string spanName)
    {
        // Arrange
        var sampler = CreateSampler();

        // Act - Run multiple samples
        var sampleCount = 10000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var params2 = new SamplingParameters(
                parentContext: default,
                traceId: ActivityTraceId.CreateRandom(),
                name: spanName,
                kind: ActivityKind.Internal,
                tags: null,
                links: null
            );

            var result = sampler.ShouldSample(params2);
            if (result.Decision == SamplingDecision.RecordAndSample)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeInRange(0.005, 0.015,
            $"cache operation '{spanName}' should sample at ~1%");
    }

    #endregion

    #region Default Priority Tests

    [Fact]
    public void ShouldSample_RegularCommand_UsesDefaultRate()
    {
        // Arrange
        var sampler = CreateSampler();

        // Act - Run multiple samples
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var params2 = new SamplingParameters(
                parentContext: default,
                traceId: ActivityTraceId.CreateRandom(),
                name: "discord.command /ping",
                kind: ActivityKind.Server,
                tags: null,
                links: null
            );

            var result = sampler.ShouldSample(params2);
            if (result.Decision == SamplingDecision.RecordAndSample)
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
    public void ShouldSample_BackgroundServiceOperation_UsesDefaultRate()
    {
        // Arrange
        var sampler = CreateSampler();

        // Act - Run multiple samples
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var params2 = new SamplingParameters(
                parentContext: default,
                traceId: ActivityTraceId.CreateRandom(),
                name: "background.metrics_aggregation.execute",
                kind: ActivityKind.Internal,
                tags: null,
                links: null
            );

            var result = sampler.ShouldSample(params2);
            if (result.Decision == SamplingDecision.RecordAndSample)
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

    #region Parent-Child Sampling Tests

    [Fact]
    public void ShouldSample_ParentSampled_AlwaysSamplesChild()
    {
        // Arrange
        var sampler = CreateSampler();
        var parentContext = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded, // Parent was sampled
            traceState: null,
            isRemote: true
        );

        var samplingParameters = new SamplingParameters(
            parentContext: parentContext,
            traceId: parentContext.TraceId,
            name: "child.operation",
            kind: ActivityKind.Internal,
            tags: null,
            links: null
        );

        // Act
        var result = sampler.ShouldSample(samplingParameters);

        // Assert
        result.Decision.Should().Be(SamplingDecision.RecordAndSample,
            "child spans should always be sampled when parent was sampled");
    }

    [Fact]
    public void ShouldSample_ParentNotSampled_UsesOwnSamplingLogic()
    {
        // Arrange
        var sampler = CreateSampler();
        var parentContext = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.None, // Parent was not sampled
            traceState: null,
            isRemote: true
        );

        // Use an always-sample operation to verify it overrides parent decision
        var tags = new List<KeyValuePair<string, object?>>
        {
            new(TracingConstants.Attributes.DiscordApiErrorCode, "50001")
        };

        var samplingParameters = new SamplingParameters(
            parentContext: parentContext,
            traceId: parentContext.TraceId,
            name: "discord.api.GET /guilds/123",
            kind: ActivityKind.Client,
            tags: tags,
            links: null
        );

        // Act
        var result = sampler.ShouldSample(samplingParameters);

        // Assert
        result.Decision.Should().Be(SamplingDecision.RecordAndSample,
            "child spans can be sampled based on their own priority even if parent wasn't sampled");
    }

    [Fact]
    public void ShouldSample_LocalParentContext_UsesOwnSamplingLogic()
    {
        // Arrange
        var sampler = CreateSampler();
        var parentContext = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded,
            traceState: null,
            isRemote: false // Local parent, not remote
        );

        // Use low priority operation
        var samplingParameters = new SamplingParameters(
            parentContext: parentContext,
            traceId: parentContext.TraceId,
            name: "GET /health",
            kind: ActivityKind.Server,
            tags: null,
            links: null
        );

        // Act - Run multiple samples to verify it's using low priority rate
        var sampleCount = 10000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var params2 = new SamplingParameters(
                parentContext: parentContext,
                traceId: parentContext.TraceId,
                name: "GET /health",
                kind: ActivityKind.Server,
                tags: null,
                links: null
            );

            var result = sampler.ShouldSample(params2);
            if (result.Decision == SamplingDecision.RecordAndSample)
            {
                sampledCount++;
            }
        }

        // Assert
        var actualRate = (double)sampledCount / sampleCount;
        actualRate.Should().BeInRange(0.005, 0.015,
            "local parent context should not force sampling, should use own priority");
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void Constructor_InitializesWithDefaultOptions()
    {
        // Arrange & Act
        var sampler = CreateSampler();

        // Assert
        sampler.Should().NotBeNull("sampler should initialize with default options");
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("PrioritySampler initialized")),
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
        var sampler = CreateSampler(customOptions);

        // Run test with high priority operation to verify custom rate
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var params2 = new SamplingParameters(
                parentContext: default,
                traceId: ActivityTraceId.CreateRandom(),
                name: "service.moderation.warn",
                kind: ActivityKind.Internal,
                tags: null,
                links: null
            );

            var result = sampler.ShouldSample(params2);
            if (result.Decision == SamplingDecision.RecordAndSample)
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
    public void ShouldSample_AlwaysReturnsValidDecision()
    {
        // Arrange
        var sampler = CreateSampler();
        var samplingParameters = new SamplingParameters(
            parentContext: default,
            traceId: ActivityTraceId.CreateRandom(),
            name: "test.operation",
            kind: ActivityKind.Internal,
            tags: null,
            links: null
        );

        // Act
        var result = sampler.ShouldSample(samplingParameters);

        // Assert
        result.Should().NotBeNull("ShouldSample should always return a result");
        result.Decision.Should().BeOneOf(SamplingDecision.Drop, SamplingDecision.RecordAndSample);
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public void ShouldSample_NullTags_DoesNotThrow()
    {
        // Arrange
        var sampler = CreateSampler();
        var samplingParameters = new SamplingParameters(
            parentContext: default,
            traceId: ActivityTraceId.CreateRandom(),
            name: "test.operation",
            kind: ActivityKind.Internal,
            tags: null,
            links: null
        );

        // Act
        var act = () => sampler.ShouldSample(samplingParameters);

        // Assert
        act.Should().NotThrow("null tags should be handled gracefully");
    }

    [Fact]
    public void ShouldSample_EmptyTags_DoesNotThrow()
    {
        // Arrange
        var sampler = CreateSampler();
        var samplingParameters = new SamplingParameters(
            parentContext: default,
            traceId: ActivityTraceId.CreateRandom(),
            name: "test.operation",
            kind: ActivityKind.Internal,
            tags: new List<KeyValuePair<string, object?>>(),
            links: null
        );

        // Act
        var act = () => sampler.ShouldSample(samplingParameters);

        // Assert
        act.Should().NotThrow("empty tags should be handled gracefully");
    }

    [Fact]
    public void ShouldSample_TagsWithNullValues_DoesNotThrow()
    {
        // Arrange
        var sampler = CreateSampler();
        var tags = new List<KeyValuePair<string, object?>>
        {
            new("key1", null),
            new("key2", "value2"),
            new("key3", null)
        };

        var samplingParameters = new SamplingParameters(
            parentContext: default,
            traceId: ActivityTraceId.CreateRandom(),
            name: "test.operation",
            kind: ActivityKind.Internal,
            tags: tags,
            links: null
        );

        // Act
        var act = () => sampler.ShouldSample(samplingParameters);

        // Assert
        act.Should().NotThrow("null tag values should be handled gracefully");
    }

    [Fact]
    public void ShouldSample_RateLimitRemainingNonZero_DoesNotAlwaysSample()
    {
        // Arrange
        var sampler = CreateSampler();
        var tags = new List<KeyValuePair<string, object?>>
        {
            new(TracingConstants.Attributes.DiscordApiRateLimitRemaining, "5")
        };

        // Act - Run multiple samples
        var sampleCount = 1000;
        var sampledCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var params2 = new SamplingParameters(
                parentContext: default,
                traceId: ActivityTraceId.CreateRandom(),
                name: "discord.api.GET /guilds/123",
                kind: ActivityKind.Client,
                tags: tags,
                links: null
            );

            var result = sampler.ShouldSample(params2);
            if (result.Decision == SamplingDecision.RecordAndSample)
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
    public void ShouldSample_RateLimitRemainingInvalidFormat_DoesNotThrow()
    {
        // Arrange
        var sampler = CreateSampler();
        var tags = new List<KeyValuePair<string, object?>>
        {
            new(TracingConstants.Attributes.DiscordApiRateLimitRemaining, "invalid")
        };

        var samplingParameters = new SamplingParameters(
            parentContext: default,
            traceId: ActivityTraceId.CreateRandom(),
            name: "discord.api.GET /guilds/123",
            kind: ActivityKind.Client,
            tags: tags,
            links: null
        );

        // Act
        var act = () => sampler.ShouldSample(samplingParameters);

        // Assert
        act.Should().NotThrow("invalid rate limit format should be handled gracefully");
    }

    [Fact]
    public void ShouldSample_CaseInsensitiveSpanNameMatching()
    {
        // Arrange
        var sampler = CreateSampler();

        // Test with different casing
        var testCases = new[]
        {
            "SERVICE.AUTOMOD.CHECK",
            "Service.AutoMod.Check",
            "service.automod.check",
            "SERVICE.automod.CHECK"
        };

        // Act & Assert
        foreach (var spanName in testCases)
        {
            var samplingParameters = new SamplingParameters(
                parentContext: default,
                traceId: ActivityTraceId.CreateRandom(),
                name: spanName,
                kind: ActivityKind.Internal,
                tags: null,
                links: null
            );

            var result = sampler.ShouldSample(samplingParameters);

            result.Decision.Should().Be(SamplingDecision.RecordAndSample,
                $"span name '{spanName}' should match 'automod' case-insensitively");
        }
    }

    #endregion
}
