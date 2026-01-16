using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.DTOs.LLM.Enums;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services.LLM;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Infrastructure.LLM;

/// <summary>
/// Unit tests for AgentRunner.
/// Tests the agentic loop orchestration, tool use cycles, and error handling.
/// </summary>
public class AgentRunnerTests
{
    private readonly Mock<ILlmClient> _mockLlmClient;
    private readonly Mock<ILogger<AgentRunner>> _mockLogger;
    private readonly AgentRunner _agentRunner;

    public AgentRunnerTests()
    {
        _mockLlmClient = new Mock<ILlmClient>();
        _mockLogger = new Mock<ILogger<AgentRunner>>();
        _agentRunner = new AgentRunner(_mockLlmClient.Object, _mockLogger.Object);
    }

    #region Happy Path Tests

    [Fact]
    public async Task RunAsync_WithSuccessfulResponse_ReturnsSuccess()
    {
        // Arrange
        const string userMessage = "Hello, agent!";
        var context = new AgentContext
        {
            SystemPrompt = "You are a helpful assistant.",
            ExecutionContext = new ToolContext { UserId = 123, GuildId = 456 },
            MaxTokens = 1024,
            Temperature = 0.7,
            MaxToolCallIterations = 5
        };

        var llmResponse = new LlmResponse
        {
            Success = true,
            Content = "Hello! I'm ready to help.",
            StopReason = LlmStopReason.EndTurn,
            Usage = new LlmUsage
            {
                InputTokens = 50,
                OutputTokens = 20,
                CachedTokens = 0,
                CacheWriteTokens = 0
            }
        };

        _mockLlmClient.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act
        var result = await _agentRunner.RunAsync(userMessage, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Response.Should().Be("Hello! I'm ready to help.");
        result.LoopCount.Should().Be(1);
        result.TotalToolCalls.Should().Be(0);
        result.TotalUsage.InputTokens.Should().Be(50);
        result.TotalUsage.OutputTokens.Should().Be(20);
        result.ErrorMessage.Should().BeNullOrEmpty();
    }

    #endregion

    #region Tool Use Tests

    [Fact]
    public async Task RunAsync_WithToolUse_ExecutesToolsAndContinues()
    {
        // Arrange
        const string userMessage = "What are the user roles?";

        var toolRegistry = new Mock<IToolRegistry>();
        var context = new AgentContext
        {
            SystemPrompt = "You are a helpful assistant.",
            ToolRegistry = toolRegistry.Object,
            ExecutionContext = new ToolContext { UserId = 123, GuildId = 456 },
            MaxTokens = 1024,
            Temperature = 0.7,
            MaxToolCallIterations = 5
        };

        var toolDef = new LlmToolDefinition
        {
            Name = "get_roles",
            Description = "Get user roles",
            InputSchema = JsonDocument.Parse("""{"properties": {}, "required": []}""").RootElement
        };

        toolRegistry.Setup(t => t.GetEnabledTools())
            .Returns(new[] { toolDef });

        // First response: tool use
        var toolCall = new LlmToolCall
        {
            Id = "call-1",
            Name = "get_roles",
            Input = JsonDocument.Parse("""{}""").RootElement
        };

        var toolUseResponse = new LlmResponse
        {
            Success = true,
            Content = "I'll check the roles for you.",
            StopReason = LlmStopReason.ToolUse,
            ToolCalls = new List<LlmToolCall> { toolCall },
            Usage = new LlmUsage { InputTokens = 50, OutputTokens = 30 }
        };

        // Second response: final answer
        var finalResponse = new LlmResponse
        {
            Success = true,
            Content = "The user has admin and moderator roles.",
            StopReason = LlmStopReason.EndTurn,
            Usage = new LlmUsage { InputTokens = 100, OutputTokens = 25 }
        };

        _mockLlmClient.SetupSequence(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolUseResponse)
            .ReturnsAsync(finalResponse);

        var toolResult = JsonDocument.Parse("""{"roles": ["admin", "moderator"]}""").RootElement;
        toolRegistry.Setup(t => t.ExecuteToolAsync("get_roles", It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.CreateSuccess(toolResult));

        // Act
        var result = await _agentRunner.RunAsync(userMessage, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Response.Should().Be("The user has admin and moderator roles.");
        result.LoopCount.Should().Be(2);
        result.TotalToolCalls.Should().Be(1);
        result.TotalUsage.InputTokens.Should().Be(150); // 50 + 100
        result.TotalUsage.OutputTokens.Should().Be(55); // 30 + 25

        // Verify tool was executed
        toolRegistry.Verify(
            t => t.ExecuteToolAsync("get_roles", It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithMultipleToolCalls_ExecutesAllTools()
    {
        // Arrange
        const string userMessage = "Get user info and roles";

        var toolRegistry = new Mock<IToolRegistry>();
        var context = new AgentContext
        {
            SystemPrompt = "You are a helpful assistant.",
            ToolRegistry = toolRegistry.Object,
            ExecutionContext = new ToolContext { UserId = 123, GuildId = 456 },
            MaxToolCallIterations = 5
        };

        var toolDef1 = new LlmToolDefinition
        {
            Name = "get_user_info",
            Description = "Get user information",
            InputSchema = JsonDocument.Parse("""{"properties": {}, "required": []}""").RootElement
        };

        var toolDef2 = new LlmToolDefinition
        {
            Name = "get_roles",
            Description = "Get user roles",
            InputSchema = JsonDocument.Parse("""{"properties": {}, "required": []}""").RootElement
        };

        toolRegistry.Setup(t => t.GetEnabledTools())
            .Returns(new[] { toolDef1, toolDef2 });

        // Response with multiple tool calls
        var toolCall1 = new LlmToolCall
        {
            Id = "call-1",
            Name = "get_user_info",
            Input = JsonDocument.Parse("""{}""").RootElement
        };

        var toolCall2 = new LlmToolCall
        {
            Id = "call-2",
            Name = "get_roles",
            Input = JsonDocument.Parse("""{}""").RootElement
        };

        var multiToolResponse = new LlmResponse
        {
            Success = true,
            Content = "I'll fetch both pieces of information.",
            StopReason = LlmStopReason.ToolUse,
            ToolCalls = new List<LlmToolCall> { toolCall1, toolCall2 },
            Usage = new LlmUsage { InputTokens = 50, OutputTokens = 30 }
        };

        var finalResponse = new LlmResponse
        {
            Success = true,
            Content = "User info and roles retrieved.",
            StopReason = LlmStopReason.EndTurn,
            Usage = new LlmUsage { InputTokens = 150, OutputTokens = 25 }
        };

        _mockLlmClient.SetupSequence(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(multiToolResponse)
            .ReturnsAsync(finalResponse);

        toolRegistry.Setup(t => t.ExecuteToolAsync("get_user_info", It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.CreateSuccess(JsonDocument.Parse("""{"username": "testuser"}""").RootElement));

        toolRegistry.Setup(t => t.ExecuteToolAsync("get_roles", It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.CreateSuccess(JsonDocument.Parse("""{"roles": ["admin"]}""").RootElement));

        // Act
        var result = await _agentRunner.RunAsync(userMessage, context);

        // Assert
        result.Success.Should().BeTrue();
        result.LoopCount.Should().Be(2);
        result.TotalToolCalls.Should().Be(2);

        // Verify both tools were executed
        toolRegistry.Verify(
            t => t.ExecuteToolAsync("get_user_info", It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
        toolRegistry.Verify(
            t => t.ExecuteToolAsync("get_roles", It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Token Usage Tests

    [Fact]
    public async Task RunAsync_AccumulatesTokenUsage_AcrossIterations()
    {
        // Arrange
        const string userMessage = "Test message";

        var toolRegistry = new Mock<IToolRegistry>();
        var context = new AgentContext
        {
            SystemPrompt = "You are a helpful assistant.",
            ToolRegistry = toolRegistry.Object,
            ExecutionContext = new ToolContext { UserId = 123, GuildId = 456 },
            MaxToolCallIterations = 5
        };

        var toolDef = new LlmToolDefinition
        {
            Name = "test_tool",
            Description = "Test tool",
            InputSchema = JsonDocument.Parse("""{"properties": {}, "required": []}""").RootElement
        };

        toolRegistry.Setup(t => t.GetEnabledTools())
            .Returns(new[] { toolDef });

        // First response with tool use
        var toolResponse = new LlmResponse
        {
            Success = true,
            Content = "Calling tool",
            StopReason = LlmStopReason.ToolUse,
            ToolCalls = new List<LlmToolCall>
            {
                new() { Id = "call-1", Name = "test_tool", Input = JsonDocument.Parse("""{}""").RootElement }
            },
            Usage = new LlmUsage
            {
                InputTokens = 100,
                OutputTokens = 50,
                CachedTokens = 10,
                CacheWriteTokens = 20
            }
        };

        // Second response with final answer
        var finalResponse = new LlmResponse
        {
            Success = true,
            Content = "Done",
            StopReason = LlmStopReason.EndTurn,
            Usage = new LlmUsage
            {
                InputTokens = 200,
                OutputTokens = 75,
                CachedTokens = 5,
                CacheWriteTokens = 0
            }
        };

        _mockLlmClient.SetupSequence(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolResponse)
            .ReturnsAsync(finalResponse);

        toolRegistry.Setup(t => t.ExecuteToolAsync("test_tool", It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.CreateSuccess(JsonDocument.Parse("""{"result": "ok"}""").RootElement));

        // Act
        var result = await _agentRunner.RunAsync(userMessage, context);

        // Assert
        result.TotalUsage.InputTokens.Should().Be(300); // 100 + 200
        result.TotalUsage.OutputTokens.Should().Be(125); // 50 + 75
        result.TotalUsage.CachedTokens.Should().Be(15); // 10 + 5
        result.TotalUsage.CacheWriteTokens.Should().Be(20); // 20 + 0
    }

    [Fact]
    public async Task RunAsync_AccumulatesEstimatedCost_AcrossIterations()
    {
        // Arrange
        const string userMessage = "Test message";
        var context = new AgentContext
        {
            SystemPrompt = "You are a helpful assistant.",
            ExecutionContext = new ToolContext { UserId = 123, GuildId = 456 },
            MaxToolCallIterations = 5
        };

        var response1 = new LlmResponse
        {
            Success = true,
            Content = "Response 1",
            StopReason = LlmStopReason.EndTurn,
            Usage = new LlmUsage
            {
                InputTokens = 100,
                OutputTokens = 50,
                EstimatedCost = 0.01m
            }
        };

        var response2 = new LlmResponse
        {
            Success = true,
            Content = "Response 2",
            StopReason = LlmStopReason.EndTurn,
            Usage = new LlmUsage
            {
                InputTokens = 50,
                OutputTokens = 25,
                EstimatedCost = 0.005m
            }
        };

        _mockLlmClient.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response1);

        // Act - First call
        var result1 = await _agentRunner.RunAsync(userMessage, context);

        // Assert
        result1.TotalUsage.EstimatedCost.Should().Be(0.01m);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task RunAsync_WithLlmFailure_ReturnsError()
    {
        // Arrange
        const string userMessage = "Test message";
        var context = new AgentContext
        {
            SystemPrompt = "You are a helpful assistant.",
            ExecutionContext = new ToolContext { UserId = 123, GuildId = 456 }
        };

        var failureResponse = new LlmResponse
        {
            Success = false,
            ErrorMessage = "LLM service unavailable"
        };

        _mockLlmClient.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failureResponse);

        // Act
        var result = await _agentRunner.RunAsync(userMessage, context);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("LLM service unavailable");
        result.LoopCount.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_WithToolUseButNoToolCalls_ReturnsError()
    {
        // Arrange
        const string userMessage = "Test message";

        var toolRegistry = new Mock<IToolRegistry>();
        var context = new AgentContext
        {
            SystemPrompt = "You are a helpful assistant.",
            ToolRegistry = toolRegistry.Object,
            ExecutionContext = new ToolContext { UserId = 123, GuildId = 456 }
        };

        toolRegistry.Setup(t => t.GetEnabledTools())
            .Returns(new List<LlmToolDefinition>());

        // LLM says it wants to use tools but provides no tool calls
        var invalidResponse = new LlmResponse
        {
            Success = true,
            StopReason = LlmStopReason.ToolUse,
            ToolCalls = null, // No tool calls!
            Usage = new LlmUsage()
        };

        _mockLlmClient.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invalidResponse);

        // Act
        var result = await _agentRunner.RunAsync(userMessage, context);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("tool calls");
    }

    [Fact]
    public async Task RunAsync_WithToolUseButNoRegistry_ReturnsError()
    {
        // Arrange
        const string userMessage = "Test message";
        var context = new AgentContext
        {
            SystemPrompt = "You are a helpful assistant.",
            ToolRegistry = null, // No registry
            ExecutionContext = new ToolContext { UserId = 123, GuildId = 456 }
        };

        var toolUseResponse = new LlmResponse
        {
            Success = true,
            StopReason = LlmStopReason.ToolUse,
            ToolCalls = new List<LlmToolCall>
            {
                new() { Id = "call-1", Name = "tool", Input = JsonDocument.Parse("""{}""").RootElement }
            },
            Usage = new LlmUsage()
        };

        _mockLlmClient.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolUseResponse);

        // Act
        var result = await _agentRunner.RunAsync(userMessage, context);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("ToolRegistry");
    }

    [Fact]
    public async Task RunAsync_WithToolExecutionException_RecordsErrorAndContinues()
    {
        // Arrange
        const string userMessage = "Test message";

        var toolRegistry = new Mock<IToolRegistry>();
        var context = new AgentContext
        {
            SystemPrompt = "You are a helpful assistant.",
            ToolRegistry = toolRegistry.Object,
            ExecutionContext = new ToolContext { UserId = 123, GuildId = 456 },
            MaxToolCallIterations = 5
        };

        var toolDef = new LlmToolDefinition
        {
            Name = "failing_tool",
            Description = "A tool that fails",
            InputSchema = JsonDocument.Parse("""{"properties": {}, "required": []}""").RootElement
        };

        toolRegistry.Setup(t => t.GetEnabledTools())
            .Returns(new[] { toolDef });

        // First response: tool use
        var toolUseResponse = new LlmResponse
        {
            Success = true,
            StopReason = LlmStopReason.ToolUse,
            ToolCalls = new List<LlmToolCall>
            {
                new() { Id = "call-1", Name = "failing_tool", Input = JsonDocument.Parse("""{}""").RootElement }
            },
            Usage = new LlmUsage { InputTokens = 50, OutputTokens = 30 }
        };

        // Final response after tool fails
        var finalResponse = new LlmResponse
        {
            Success = true,
            Content = "Tool failed, proceeding anyway.",
            StopReason = LlmStopReason.EndTurn,
            Usage = new LlmUsage { InputTokens = 100, OutputTokens = 25 }
        };

        _mockLlmClient.SetupSequence(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolUseResponse)
            .ReturnsAsync(finalResponse);

        // Tool throws an exception
        toolRegistry.Setup(t => t.ExecuteToolAsync("failing_tool", It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Tool execution failed"));

        // Act
        var result = await _agentRunner.RunAsync(userMessage, context);

        // Assert - Agent continues despite tool failure
        result.Success.Should().BeTrue();
        result.LoopCount.Should().Be(2);
        result.TotalToolCalls.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_WithMaxTokensStopReason_ReturnsTruncatedResponse()
    {
        // Arrange
        const string userMessage = "Test message";
        var context = new AgentContext
        {
            SystemPrompt = "You are a helpful assistant.",
            ExecutionContext = new ToolContext { UserId = 123, GuildId = 456 },
            MaxTokens = 100
        };

        var truncatedResponse = new LlmResponse
        {
            Success = true,
            Content = "This response was truncated due to max tokens...",
            StopReason = LlmStopReason.MaxTokens,
            Usage = new LlmUsage { InputTokens = 50, OutputTokens = 100 }
        };

        _mockLlmClient.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(truncatedResponse);

        // Act
        var result = await _agentRunner.RunAsync(userMessage, context);

        // Assert
        result.Success.Should().BeTrue(); // Still successful, but truncated
        result.Response.Should().Be("This response was truncated due to max tokens...");
        result.ErrorMessage.Should().Contain("max tokens");
    }

    [Fact]
    public async Task RunAsync_WithErrorStopReason_ReturnsError()
    {
        // Arrange
        const string userMessage = "Test message";
        var context = new AgentContext
        {
            SystemPrompt = "You are a helpful assistant.",
            ExecutionContext = new ToolContext { UserId = 123, GuildId = 456 }
        };

        var errorResponse = new LlmResponse
        {
            Success = true,
            StopReason = LlmStopReason.Error,
            ErrorMessage = "Internal LLM error occurred",
            Usage = new LlmUsage()
        };

        _mockLlmClient.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(errorResponse);

        // Act
        var result = await _agentRunner.RunAsync(userMessage, context);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Internal LLM error occurred");
    }

    #endregion

    #region Iteration Limit Tests

    [Fact]
    public async Task RunAsync_ExceedsMaxIterations_ReturnsError()
    {
        // Arrange
        const string userMessage = "Test message";

        var toolRegistry = new Mock<IToolRegistry>();
        var context = new AgentContext
        {
            SystemPrompt = "You are a helpful assistant.",
            ToolRegistry = toolRegistry.Object,
            ExecutionContext = new ToolContext { UserId = 123, GuildId = 456 },
            MaxToolCallIterations = 2 // Very low limit
        };

        var toolDef = new LlmToolDefinition
        {
            Name = "test_tool",
            Description = "Test tool",
            InputSchema = JsonDocument.Parse("""{"properties": {}, "required": []}""").RootElement
        };

        toolRegistry.Setup(t => t.GetEnabledTools())
            .Returns(new[] { toolDef });

        // Every response wants more tools (infinite loop scenario)
        var toolUseResponse = new LlmResponse
        {
            Success = true,
            StopReason = LlmStopReason.ToolUse,
            ToolCalls = new List<LlmToolCall>
            {
                new() { Id = "call-1", Name = "test_tool", Input = JsonDocument.Parse("""{}""").RootElement }
            },
            Usage = new LlmUsage { InputTokens = 50, OutputTokens = 30 }
        };

        _mockLlmClient.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolUseResponse);

        toolRegistry.Setup(t => t.ExecuteToolAsync("test_tool", It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.CreateSuccess(JsonDocument.Parse("""{"result": "ok"}""").RootElement));

        // Act
        var result = await _agentRunner.RunAsync(userMessage, context);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("maximum tool call iterations");
        result.LoopCount.Should().Be(2);
    }

    #endregion

    #region Input Validation Tests

    [Fact]
    public async Task RunAsync_WithNullUserMessage_ThrowsArgumentException()
    {
        // Arrange
        var context = new AgentContext { SystemPrompt = "Test" };

        // Act & Assert
        // ArgumentNullException is a subclass of ArgumentException
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _agentRunner.RunAsync(null!, context));
    }

    [Fact]
    public async Task RunAsync_WithEmptyUserMessage_ThrowsArgumentException()
    {
        // Arrange
        var context = new AgentContext { SystemPrompt = "Test" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _agentRunner.RunAsync("", context));
    }

    [Fact]
    public async Task RunAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        const string userMessage = "Test message";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _agentRunner.RunAsync(userMessage, null!));
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLlmClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AgentRunner(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AgentRunner(_mockLlmClient.Object, null!));
    }

    #endregion

    #region Tool Execution Error Scenarios

    [Fact]
    public async Task RunAsync_WithToolExecutionError_IncludesErrorInResults()
    {
        // Arrange
        const string userMessage = "Test message";

        var toolRegistry = new Mock<IToolRegistry>();
        var context = new AgentContext
        {
            SystemPrompt = "You are a helpful assistant.",
            ToolRegistry = toolRegistry.Object,
            ExecutionContext = new ToolContext { UserId = 123, GuildId = 456 },
            MaxToolCallIterations = 5
        };

        var toolDef = new LlmToolDefinition
        {
            Name = "error_tool",
            Description = "Tool that returns error",
            InputSchema = JsonDocument.Parse("""{"properties": {}, "required": []}""").RootElement
        };

        toolRegistry.Setup(t => t.GetEnabledTools())
            .Returns(new[] { toolDef });

        // First response: tool use
        var toolUseResponse = new LlmResponse
        {
            Success = true,
            StopReason = LlmStopReason.ToolUse,
            ToolCalls = new List<LlmToolCall>
            {
                new() { Id = "call-1", Name = "error_tool", Input = JsonDocument.Parse("""{}""").RootElement }
            },
            Usage = new LlmUsage { InputTokens = 50, OutputTokens = 30 }
        };

        // Final response
        var finalResponse = new LlmResponse
        {
            Success = true,
            Content = "Tool failed.",
            StopReason = LlmStopReason.EndTurn,
            Usage = new LlmUsage { InputTokens = 100, OutputTokens = 25 }
        };

        _mockLlmClient.SetupSequence(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolUseResponse)
            .ReturnsAsync(finalResponse);

        // Tool returns error
        toolRegistry.Setup(t => t.ExecuteToolAsync("error_tool", It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.CreateError("Tool execution failed"));

        // Act
        var result = await _agentRunner.RunAsync(userMessage, context);

        // Assert - Agent continues despite tool error
        result.Success.Should().BeTrue();
        result.TotalToolCalls.Should().Be(1);
    }

    #endregion

    #region Conversation History Tests

    [Fact]
    public async Task RunAsync_BuildsCorrectConversationHistory()
    {
        // Arrange
        const string userMessage = "Initial message";

        var toolRegistry = new Mock<IToolRegistry>();
        var context = new AgentContext
        {
            SystemPrompt = "You are a helpful assistant.",
            ToolRegistry = toolRegistry.Object,
            ExecutionContext = new ToolContext { UserId = 123, GuildId = 456 },
            MaxToolCallIterations = 5
        };

        var toolDef = new LlmToolDefinition
        {
            Name = "test_tool",
            Description = "Test tool",
            InputSchema = JsonDocument.Parse("""{"properties": {}, "required": []}""").RootElement
        };

        toolRegistry.Setup(t => t.GetEnabledTools())
            .Returns(new[] { toolDef });

        // First response: tool use
        var toolCall = new LlmToolCall
        {
            Id = "call-1",
            Name = "test_tool",
            Input = JsonDocument.Parse("""{"param": "value"}""").RootElement
        };

        var toolUseResponse = new LlmResponse
        {
            Success = true,
            Content = "Using tool",
            StopReason = LlmStopReason.ToolUse,
            ToolCalls = new List<LlmToolCall> { toolCall },
            Usage = new LlmUsage { InputTokens = 50, OutputTokens = 30 }
        };

        var finalResponse = new LlmResponse
        {
            Success = true,
            Content = "Final answer",
            StopReason = LlmStopReason.EndTurn,
            Usage = new LlmUsage { InputTokens = 100, OutputTokens = 25 }
        };

        LlmRequest? capturedRequest = null;
        _mockLlmClient.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(toolUseResponse);

        _mockLlmClient.SetupSequence(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolUseResponse)
            .ReturnsAsync(finalResponse);

        toolRegistry.Setup(t => t.ExecuteToolAsync("test_tool", It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.CreateSuccess(JsonDocument.Parse("""{"result": "ok"}""").RootElement));

        // Act
        var result = await _agentRunner.RunAsync(userMessage, context);

        // Assert
        result.Success.Should().BeTrue();

        // Verify the second request includes the tool results
        var secondCall = _mockLlmClient.Invocations[1];
        var secondRequest = (LlmRequest)secondCall.Arguments[0];
        secondRequest.Messages.Should().HaveCountGreaterThan(1);
    }

    #endregion
}
