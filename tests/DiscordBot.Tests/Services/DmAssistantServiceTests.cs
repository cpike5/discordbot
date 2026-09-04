using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="DmAssistantService"/>, focused on the blank-response-after-success
/// handling that keeps a "successful" but empty agent run from being saved to conversation
/// history or counted in daily metrics.
/// </summary>
public class DmAssistantServiceTests
{
    private const ulong OwnerId = 111UL;

    private readonly Mock<ILogger<DmAssistantService>> _mockLogger;
    private readonly Mock<IAssistantMessagePipeline> _mockPipeline;
    private readonly Mock<IBotOwnerResolver> _mockOwnerResolver;
    private readonly Mock<IDmAssistantContextFactory> _mockContextFactory;
    private readonly Mock<IAssistantContext> _mockContext;
    private readonly DmAssistantOptions _options;
    private readonly DmAssistantService _service;

    public DmAssistantServiceTests()
    {
        _mockLogger = new Mock<ILogger<DmAssistantService>>();
        _mockPipeline = new Mock<IAssistantMessagePipeline>();
        _mockOwnerResolver = new Mock<IBotOwnerResolver>();
        _mockContextFactory = new Mock<IDmAssistantContextFactory>();
        _mockContext = new Mock<IAssistantContext>();

        _options = new DmAssistantOptions
        {
            ErrorMessage = "Oops, something went wrong.",
            PlaceholderMessage = "Not yet available."
        };

        var mockOptions = new Mock<IOptions<DmAssistantOptions>>();
        mockOptions.Setup(o => o.Value).Returns(_options);

        _mockOwnerResolver.Setup(r => r.GetOwnerIdAsync()).ReturnsAsync(OwnerId);

        _mockContext
            .Setup(c => c.FormatUserMessageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string msg, CancellationToken _) => msg);

        _mockContextFactory
            .Setup(f => f.CreateAsync(OwnerId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockContext.Object);

        _service = new DmAssistantService(
            _mockLogger.Object,
            _mockPipeline.Object,
            _mockOwnerResolver.Object,
            _mockContextFactory.Object,
            mockOptions.Object);
    }

    [Fact]
    public async Task ProcessMessageAsync_SuccessWithBlankResponse_MarksResultFailed_BeforeRecordingUsage()
    {
        // Arrange: the pipeline reports Success=true but with a blank Response — this must be
        // treated as a failure so it is never saved to conversation history / daily metrics.
        var pipelineResult = new AssistantPipelineResult
        {
            Success = true,
            Response = "   ",
            InputTokens = 10,
            OutputTokens = 5
        };

        _mockPipeline
            .Setup(p => p.RunAsync(It.IsAny<string>(), _mockContext.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipelineResult);

        AssistantPipelineResult? recordedResult = null;
        _mockContext
            .Setup(c => c.RecordUsageAsync(It.IsAny<string>(), It.IsAny<AssistantPipelineResult>(), It.IsAny<CancellationToken>()))
            .Callback<string, AssistantPipelineResult, CancellationToken>((_, result, _) => recordedResult = result)
            .Returns(Task.CompletedTask);

        // Act
        var response = await _service.ProcessMessageAsync(OwnerId, "Hello?");

        // Assert
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Be(_options.ErrorMessage);

        recordedResult.Should().NotBeNull();
        recordedResult!.Success.Should().BeFalse(
            "a blank response must be recorded as a failure so DmAssistantContext does not save it " +
            "to conversation history or count it toward daily metrics");
        recordedResult.ErrorMessage.Should().Be(_options.ErrorMessage);

        _mockContext.Verify(
            c => c.RecordUsageAsync(It.IsAny<string>(), It.IsAny<AssistantPipelineResult>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_SuccessWithBlankResponse_PreservesExistingErrorMessage()
    {
        // A pipeline error message (if one happened to be set alongside Success=true) should win
        // over the generic options error message.
        var pipelineResult = new AssistantPipelineResult
        {
            Success = true,
            Response = string.Empty,
            ErrorMessage = "specific pipeline error"
        };

        _mockPipeline
            .Setup(p => p.RunAsync(It.IsAny<string>(), _mockContext.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipelineResult);

        AssistantPipelineResult? recordedResult = null;
        _mockContext
            .Setup(c => c.RecordUsageAsync(It.IsAny<string>(), It.IsAny<AssistantPipelineResult>(), It.IsAny<CancellationToken>()))
            .Callback<string, AssistantPipelineResult, CancellationToken>((_, result, _) => recordedResult = result)
            .Returns(Task.CompletedTask);

        var response = await _service.ProcessMessageAsync(OwnerId, "Hello?");

        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Be("specific pipeline error");
        recordedResult!.Success.Should().BeFalse();
        recordedResult.ErrorMessage.Should().Be("specific pipeline error");
    }

    [Fact]
    public async Task ProcessMessageAsync_SuccessWithNonBlankResponse_RecordsSuccessfulResult()
    {
        var pipelineResult = new AssistantPipelineResult
        {
            Success = true,
            Response = "Here is the answer.",
            InputTokens = 10,
            OutputTokens = 20
        };

        _mockPipeline
            .Setup(p => p.RunAsync(It.IsAny<string>(), _mockContext.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipelineResult);

        AssistantPipelineResult? recordedResult = null;
        _mockContext
            .Setup(c => c.RecordUsageAsync(It.IsAny<string>(), It.IsAny<AssistantPipelineResult>(), It.IsAny<CancellationToken>()))
            .Callback<string, AssistantPipelineResult, CancellationToken>((_, result, _) => recordedResult = result)
            .Returns(Task.CompletedTask);

        var response = await _service.ProcessMessageAsync(OwnerId, "Hello?");

        response.Success.Should().BeTrue();
        response.Response.Should().Be("Here is the answer.");
        recordedResult!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessMessageAsync_NonOwner_ReturnsPlaceholder_WithoutInvokingPipeline()
    {
        _mockOwnerResolver.Setup(r => r.GetOwnerIdAsync()).ReturnsAsync(999UL); // not OwnerId

        var response = await _service.ProcessMessageAsync(OwnerId, "Hello?");

        response.IsOwner.Should().BeFalse();
        response.IsPlaceholder.Should().BeTrue();
        _mockPipeline.Verify(
            p => p.RunAsync(It.IsAny<string>(), It.IsAny<IAssistantContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
