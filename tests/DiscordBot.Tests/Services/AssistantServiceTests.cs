using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AssistantService"/>.
/// Tests cover question processing, rate limiting, consent checking, and metrics logging.
/// </summary>
public class AssistantServiceTests
{
    private readonly Mock<ILogger<AssistantService>> _mockLogger;
    private readonly Mock<IAgentRunner> _mockAgentRunner;
    private readonly Mock<IToolRegistry> _mockToolRegistry;
    private readonly Mock<IPromptTemplate> _mockPromptTemplate;
    private readonly Mock<IConsentService> _mockConsentService;
    private readonly Mock<IAssistantGuildSettingsService> _mockGuildSettingsService;
    private readonly Mock<IAssistantUsageMetricsRepository> _mockMetricsRepository;
    private readonly Mock<IAssistantInteractionLogRepository> _mockInteractionLogRepository;
    private readonly IMemoryCache _cache;
    private readonly AssistantOptions _options;
    private readonly AssistantService _service;

    private const ulong TestGuildId = 123456789UL;
    private const ulong TestChannelId = 987654321UL;
    private const ulong TestUserId = 111222333UL;
    private const ulong TestMessageId = 444555666UL;
    private const string TestQuestion = "How do I use the soundboard?";
    private const string TestResponse = "You can use the soundboard by typing /play followed by the sound name.";

    public AssistantServiceTests()
    {
        _mockLogger = new Mock<ILogger<AssistantService>>();
        _mockAgentRunner = new Mock<IAgentRunner>();
        _mockToolRegistry = new Mock<IToolRegistry>();
        _mockPromptTemplate = new Mock<IPromptTemplate>();
        _mockConsentService = new Mock<IConsentService>();
        _mockGuildSettingsService = new Mock<IAssistantGuildSettingsService>();
        _mockMetricsRepository = new Mock<IAssistantUsageMetricsRepository>();
        _mockInteractionLogRepository = new Mock<IAssistantInteractionLogRepository>();
        _cache = new MemoryCache(new MemoryCacheOptions());

        _options = new AssistantOptions
        {
            GloballyEnabled = true,
            RequireExplicitConsent = true,
            DefaultRateLimit = 5,
            RateLimitWindowMinutes = 5,
            MaxQuestionLength = 500,
            MaxResponseLength = 1800,
            MaxTokens = 1024,
            Temperature = 0.7,
            MaxToolCallsPerQuestion = 5,
            EnableDocumentationTools = true,
            EnableCostTracking = true,
            LogInteractions = true,
            AgentPromptPath = "docs/agents/assistant-agent.md",
            TruncationSuffix = "\n\n... *(response truncated)*",
            ErrorMessage = "Oops, something went wrong."
        };

        var mockOptions = new Mock<IOptions<AssistantOptions>>();
        mockOptions.Setup(o => o.Value).Returns(_options);

        _service = new AssistantService(
            _mockLogger.Object,
            _mockAgentRunner.Object,
            _mockToolRegistry.Object,
            _mockPromptTemplate.Object,
            _mockConsentService.Object,
            _mockGuildSettingsService.Object,
            _mockMetricsRepository.Object,
            _mockInteractionLogRepository.Object,
            _cache,
            mockOptions.Object);

        // Default setup for prompt template
        _mockPromptTemplate
            .Setup(p => p.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("You are a helpful assistant.");

        _mockPromptTemplate
            .Setup(p => p.Render(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .Returns("You are a helpful assistant.");
    }

    #region AskQuestionAsync Tests

    [Fact]
    public async Task AskQuestionAsync_ReturnsSuccess_WhenAllChecksPass()
    {
        // Arrange
        SetupSuccessfulRun();

        // Act
        var result = await _service.AskQuestionAsync(
            TestGuildId, TestChannelId, TestUserId, TestMessageId, TestQuestion);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Response.Should().Be(TestResponse);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task AskQuestionAsync_ReturnsError_WhenQuestionIsEmpty()
    {
        // Act
        var result = await _service.AskQuestionAsync(
            TestGuildId, TestChannelId, TestUserId, TestMessageId, "");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task AskQuestionAsync_ReturnsError_WhenQuestionIsTooLong()
    {
        // Arrange
        var longQuestion = new string('a', _options.MaxQuestionLength + 1);

        // Act
        var result = await _service.AskQuestionAsync(
            TestGuildId, TestChannelId, TestUserId, TestMessageId, longQuestion);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("too long");
    }

    [Fact]
    public async Task AskQuestionAsync_ReturnsError_WhenAssistantNotEnabledForGuild()
    {
        // Arrange
        _mockGuildSettingsService
            .Setup(s => s.IsEnabledAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.AskQuestionAsync(
            TestGuildId, TestChannelId, TestUserId, TestMessageId, TestQuestion);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not enabled");
    }

    [Fact]
    public async Task AskQuestionAsync_ReturnsError_WhenChannelNotAllowed()
    {
        // Arrange
        _mockGuildSettingsService
            .Setup(s => s.IsEnabledAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockGuildSettingsService
            .Setup(s => s.IsChannelAllowedAsync(TestGuildId, TestChannelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.AskQuestionAsync(
            TestGuildId, TestChannelId, TestUserId, TestMessageId, TestQuestion);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not allowed in this channel");
    }

    [Fact]
    public async Task AskQuestionAsync_ReturnsError_WhenUserLacksConsent()
    {
        // Arrange
        _mockGuildSettingsService
            .Setup(s => s.IsEnabledAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockGuildSettingsService
            .Setup(s => s.IsChannelAllowedAsync(TestGuildId, TestChannelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockConsentService
            .Setup(c => c.HasConsentAsync(TestUserId, ConsentType.AssistantUsage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.AskQuestionAsync(
            TestGuildId, TestChannelId, TestUserId, TestMessageId, TestQuestion);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("grant consent");
    }

    [Fact]
    public async Task AskQuestionAsync_ReturnsError_WhenRateLimited()
    {
        // Arrange
        SetupAllChecksPass();
        _mockGuildSettingsService
            .Setup(s => s.GetRateLimitAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Setup agent runner for the first 5 successful calls
        _mockAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentRunResult
            {
                Success = true,
                Response = TestResponse,
                TotalUsage = new LlmUsage { InputTokens = 100, OutputTokens = 50 }
            });

        // Pre-fill the rate limit cache with max requests
        for (int i = 0; i < _options.DefaultRateLimit; i++)
        {
            await _service.AskQuestionAsync(
                TestGuildId, TestChannelId, TestUserId, TestMessageId, TestQuestion);
        }

        // Act - The 6th call should be rate limited
        var result = await _service.AskQuestionAsync(
            TestGuildId, TestChannelId, TestUserId, TestMessageId, TestQuestion);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("question limit");
    }

    [Fact]
    public async Task AskQuestionAsync_TracksTokenUsage_InResult()
    {
        // Arrange
        SetupAllChecksPass();
        _mockGuildSettingsService
            .Setup(s => s.GetRateLimitAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        _mockAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentRunResult
            {
                Success = true,
                Response = TestResponse,
                TotalToolCalls = 2,
                TotalUsage = new LlmUsage
                {
                    InputTokens = 100,
                    OutputTokens = 50,
                    CachedTokens = 80,
                    CacheWriteTokens = 0
                }
            });

        // Act
        var result = await _service.AskQuestionAsync(
            TestGuildId, TestChannelId, TestUserId, TestMessageId, TestQuestion);

        // Assert
        result.Success.Should().BeTrue();
        result.InputTokens.Should().Be(100);
        result.OutputTokens.Should().Be(50);
        result.CachedTokens.Should().Be(80);
        result.ToolCalls.Should().Be(2);
    }

    [Fact]
    public async Task AskQuestionAsync_LogsMetrics_WhenCostTrackingEnabled()
    {
        // Arrange
        SetupSuccessfulRun();
        _mockGuildSettingsService
            .Setup(s => s.GetRateLimitAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        // Act
        await _service.AskQuestionAsync(
            TestGuildId, TestChannelId, TestUserId, TestMessageId, TestQuestion);

        // Assert
        _mockMetricsRepository.Verify(
            r => r.IncrementMetricsAsync(
                TestGuildId,
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AskQuestionAsync_LogsInteraction_WhenLoggingEnabled()
    {
        // Arrange
        SetupSuccessfulRun();
        _mockGuildSettingsService
            .Setup(s => s.GetRateLimitAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        // Act
        await _service.AskQuestionAsync(
            TestGuildId, TestChannelId, TestUserId, TestMessageId, TestQuestion);

        // Assert
        _mockInteractionLogRepository.Verify(
            r => r.AddAsync(
                It.Is<AssistantInteractionLog>(log =>
                    log.GuildId == TestGuildId &&
                    log.UserId == TestUserId &&
                    log.Question == TestQuestion),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AskQuestionAsync_TruncatesLongResponse()
    {
        // Arrange
        SetupAllChecksPass();
        _mockGuildSettingsService
            .Setup(s => s.GetRateLimitAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        var longResponse = new string('a', _options.MaxResponseLength + 100);
        _mockAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentRunResult
            {
                Success = true,
                Response = longResponse,
                TotalUsage = new LlmUsage()
            });

        // Act
        var result = await _service.AskQuestionAsync(
            TestGuildId, TestChannelId, TestUserId, TestMessageId, TestQuestion);

        // Assert
        result.Success.Should().BeTrue();
        result.Response.Should().NotBeNull();
        result.Response!.Length.Should().BeLessThanOrEqualTo(_options.MaxResponseLength);
        result.Response.Should().EndWith(_options.TruncationSuffix);
    }

    [Fact]
    public async Task AskQuestionAsync_ReturnsAgentError_WhenAgentFails()
    {
        // Arrange
        SetupAllChecksPass();
        _mockGuildSettingsService
            .Setup(s => s.GetRateLimitAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        _mockAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentRunResult
            {
                Success = false,
                ErrorMessage = "LLM API error",
                TotalUsage = new LlmUsage()
            });

        // Act
        var result = await _service.AskQuestionAsync(
            TestGuildId, TestChannelId, TestUserId, TestMessageId, TestQuestion);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("LLM API error");
    }

    #endregion

    #region IsEnabledForGuildAsync Tests

    [Fact]
    public async Task IsEnabledForGuildAsync_ReturnsFalse_WhenGloballyDisabled()
    {
        // Arrange
        var options = new AssistantOptions { GloballyEnabled = false };
        var mockOptions = new Mock<IOptions<AssistantOptions>>();
        mockOptions.Setup(o => o.Value).Returns(options);

        var service = new AssistantService(
            _mockLogger.Object,
            _mockAgentRunner.Object,
            _mockToolRegistry.Object,
            _mockPromptTemplate.Object,
            _mockConsentService.Object,
            _mockGuildSettingsService.Object,
            _mockMetricsRepository.Object,
            _mockInteractionLogRepository.Object,
            _cache,
            mockOptions.Object);

        // Act
        var result = await service.IsEnabledForGuildAsync(TestGuildId);

        // Assert
        result.Should().BeFalse();
        _mockGuildSettingsService.Verify(
            s => s.IsEnabledAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "should not check guild settings when globally disabled");
    }

    [Fact]
    public async Task IsEnabledForGuildAsync_ReturnsTrue_WhenGloballyAndGuildEnabled()
    {
        // Arrange
        _mockGuildSettingsService
            .Setup(s => s.IsEnabledAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsEnabledForGuildAsync(TestGuildId);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region CheckRateLimitAsync Tests

    [Fact]
    public async Task CheckRateLimitAsync_ReturnsAllowed_WhenNoUsageRecorded()
    {
        // Arrange
        _mockGuildSettingsService
            .Setup(s => s.GetRateLimitAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        var result = await _service.CheckRateLimitAsync(TestGuildId, TestUserId);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.RemainingQuestions.Should().Be(5);
    }

    #endregion

    #region GetUsageMetricsAsync Tests

    [Fact]
    public async Task GetUsageMetricsAsync_ReturnsMetrics_WhenExists()
    {
        // Arrange
        var date = DateTime.UtcNow.Date;
        var metrics = new AssistantUsageMetrics
        {
            GuildId = TestGuildId,
            Date = date,
            TotalQuestions = 10,
            TotalInputTokens = 1000,
            TotalOutputTokens = 500
        };

        _mockMetricsRepository
            .Setup(r => r.GetRangeAsync(TestGuildId, date, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { metrics });

        // Act
        var result = await _service.GetUsageMetricsAsync(TestGuildId, date);

        // Assert
        result.Should().NotBeNull();
        result!.TotalQuestions.Should().Be(10);
    }

    [Fact]
    public async Task GetUsageMetricsAsync_ReturnsNull_WhenNotExists()
    {
        // Arrange
        var date = DateTime.UtcNow.Date;
        _mockMetricsRepository
            .Setup(r => r.GetRangeAsync(TestGuildId, date, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AssistantUsageMetrics>());

        // Act
        var result = await _service.GetUsageMetricsAsync(TestGuildId, date);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetRecentInteractionsAsync Tests

    [Fact]
    public async Task GetRecentInteractionsAsync_ReturnsInteractions()
    {
        // Arrange
        var interactions = new List<AssistantInteractionLog>
        {
            new AssistantInteractionLog
            {
                GuildId = TestGuildId,
                UserId = TestUserId,
                Question = "Question 1",
                Response = "Response 1"
            },
            new AssistantInteractionLog
            {
                GuildId = TestGuildId,
                UserId = TestUserId,
                Question = "Question 2",
                Response = "Response 2"
            }
        };

        _mockInteractionLogRepository
            .Setup(r => r.GetRecentByGuildAsync(TestGuildId, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(interactions);

        // Act
        var result = await _service.GetRecentInteractionsAsync(TestGuildId);

        // Assert
        var list = result.ToList();
        list.Should().HaveCount(2);
    }

    #endregion

    #region Helper Methods

    private void SetupAllChecksPass()
    {
        _mockGuildSettingsService
            .Setup(s => s.IsEnabledAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockGuildSettingsService
            .Setup(s => s.IsChannelAllowedAsync(TestGuildId, TestChannelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockConsentService
            .Setup(c => c.HasConsentAsync(TestUserId, ConsentType.AssistantUsage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private void SetupSuccessfulRun()
    {
        SetupAllChecksPass();
        _mockGuildSettingsService
            .Setup(s => s.GetRateLimitAsync(TestGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(100); // High limit to avoid rate limiting in tests

        _mockAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentRunResult
            {
                Success = true,
                Response = TestResponse,
                TotalToolCalls = 0,
                TotalUsage = new LlmUsage
                {
                    InputTokens = 100,
                    OutputTokens = 50,
                    CachedTokens = 80,
                    CacheWriteTokens = 0
                }
            });
    }

    #endregion
}
