using System.Text.Json;
using Anthropic.Models.Messages;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.DTOs.LLM.Enums;
using DiscordBot.Infrastructure.Services.LLM.Anthropic;
using FluentAssertions;

namespace DiscordBot.Tests.Infrastructure.LLM;

/// <summary>
/// Unit tests for AnthropicMessageMapper.
/// Tests conversion between provider-agnostic LLM DTOs and Anthropic SDK types.
/// </summary>
public class AnthropicMessageMapperTests
{
    #region ToAnthropicMessages Tests

    [Fact]
    public void ToAnthropicMessages_WithUserMessage_ConvertsCorrectly()
    {
        // Arrange
        var request = new LlmRequest
        {
            Messages = new List<LlmMessage>
            {
                new()
                {
                    Role = LlmRole.User,
                    Content = "Hello, assistant!"
                }
            }
        };

        // Act
        var anthropicMessages = AnthropicMessageMapper.ToAnthropicMessages(request);

        // Assert
        anthropicMessages.Should().HaveCount(1);
        anthropicMessages[0].Role.ToString().Should().Contain("user");
        anthropicMessages[0].Content.Should().NotBeNull();
    }

    [Fact]
    public void ToAnthropicMessages_WithAssistantMessage_ConvertsCorrectly()
    {
        // Arrange
        var request = new LlmRequest
        {
            Messages = new List<LlmMessage>
            {
                new()
                {
                    Role = LlmRole.Assistant,
                    Content = "Hello! How can I help?"
                }
            }
        };

        // Act
        var anthropicMessages = AnthropicMessageMapper.ToAnthropicMessages(request);

        // Assert
        anthropicMessages.Should().HaveCount(1);
        anthropicMessages[0].Role.ToString().Should().Contain("assistant");
        anthropicMessages[0].Content.Should().NotBeNull();
    }

    [Fact]
    public void ToAnthropicMessages_WithToolCalls_ConvertsCorrectly()
    {
        // Arrange
        var toolInput = JsonDocument.Parse("""{"user_id": "123", "role": "admin"}""").RootElement;

        var request = new LlmRequest
        {
            Messages = new List<LlmMessage>
            {
                new()
                {
                    Role = LlmRole.Assistant,
                    Content = "I'll check the user roles.",
                    ToolCalls = new List<LlmToolCall>
                    {
                        new()
                        {
                            Id = "tool-call-1",
                            Name = "get_user_roles",
                            Input = toolInput
                        }
                    }
                }
            }
        };

        // Act
        var anthropicMessages = AnthropicMessageMapper.ToAnthropicMessages(request);

        // Assert
        anthropicMessages.Should().HaveCount(1);
        anthropicMessages[0].Role.ToString().Should().Contain("assistant");
        anthropicMessages[0].Content.Should().NotBeNull();
    }

    [Fact]
    public void ToAnthropicMessages_WithToolResults_ConvertsCorrectly()
    {
        // Arrange
        var resultContent = JsonDocument.Parse("""{"roles": ["admin", "moderator"]}""").RootElement;

        var request = new LlmRequest
        {
            Messages = new List<LlmMessage>
            {
                new()
                {
                    Role = LlmRole.User,
                    Content = "Here are the results.",
                    ToolResults = new List<LlmToolResult>
                    {
                        new()
                        {
                            ToolCallId = "tool-call-1",
                            Content = resultContent,
                            IsError = false
                        }
                    }
                }
            }
        };

        // Act
        var anthropicMessages = AnthropicMessageMapper.ToAnthropicMessages(request);

        // Assert
        anthropicMessages.Should().HaveCount(1);
        anthropicMessages[0].Role.ToString().Should().Contain("user");
        anthropicMessages[0].Content.Should().NotBeNull();
    }

    [Fact]
    public void ToAnthropicMessages_WithErrorToolResult_ConvertsCorrectly()
    {
        // Arrange
        var errorContent = JsonDocument.Parse("""{"error": "Tool not found"}""").RootElement;

        var request = new LlmRequest
        {
            Messages = new List<LlmMessage>
            {
                new()
                {
                    Role = LlmRole.User,
                    ToolResults = new List<LlmToolResult>
                    {
                        new()
                        {
                            ToolCallId = "tool-call-1",
                            Content = errorContent,
                            IsError = true
                        }
                    }
                }
            }
        };

        // Act
        var anthropicMessages = AnthropicMessageMapper.ToAnthropicMessages(request);

        // Assert
        anthropicMessages.Should().HaveCount(1);
        anthropicMessages[0].Role.ToString().Should().Contain("user");
    }

    [Fact]
    public void ToAnthropicMessages_WithSystemMessage_ThrowsArgumentException()
    {
        // Arrange
        var request = new LlmRequest
        {
            Messages = new List<LlmMessage>
            {
                new()
                {
                    Role = LlmRole.System,
                    Content = "You are helpful."
                }
            }
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => AnthropicMessageMapper.ToAnthropicMessages(request));
    }

    [Fact]
    public void ToAnthropicMessages_WithMultipleMessages_ConvertsAll()
    {
        // Arrange
        var request = new LlmRequest
        {
            Messages = new List<LlmMessage>
            {
                new() { Role = LlmRole.User, Content = "Hello" },
                new() { Role = LlmRole.Assistant, Content = "Hi there" },
                new() { Role = LlmRole.User, Content = "How are you?" }
            }
        };

        // Act
        var anthropicMessages = AnthropicMessageMapper.ToAnthropicMessages(request);

        // Assert
        anthropicMessages.Should().HaveCount(3);
        anthropicMessages[0].Role.ToString().Should().Contain("user");
        anthropicMessages[1].Role.ToString().Should().Contain("assistant");
        anthropicMessages[2].Role.ToString().Should().Contain("user");
    }

    [Fact]
    public void ToAnthropicMessages_WithEmptyContentList_ReturnsEmpty()
    {
        // Arrange
        var request = new LlmRequest { Messages = new List<LlmMessage>() };

        // Act
        var anthropicMessages = AnthropicMessageMapper.ToAnthropicMessages(request);

        // Assert
        anthropicMessages.Should().BeEmpty();
    }

    [Fact]
    public void ToAnthropicMessages_WithOnlyToolResults_ConvertsCorrectly()
    {
        // Arrange
        var resultContent = JsonDocument.Parse("""{"success": true}""").RootElement;

        var request = new LlmRequest
        {
            Messages = new List<LlmMessage>
            {
                new()
                {
                    Role = LlmRole.User,
                    ToolResults = new List<LlmToolResult>
                    {
                        new()
                        {
                            ToolCallId = "call-1",
                            Content = resultContent,
                            IsError = false
                        }
                    }
                }
            }
        };

        // Act
        var anthropicMessages = AnthropicMessageMapper.ToAnthropicMessages(request);

        // Assert
        anthropicMessages.Should().HaveCount(1);
        anthropicMessages[0].Role.ToString().Should().Contain("user");
    }

    [Fact]
    public void ToAnthropicMessages_WithEmptyContent_StillConverts()
    {
        // Arrange
        var request = new LlmRequest
        {
            Messages = new List<LlmMessage>
            {
                new()
                {
                    Role = LlmRole.User,
                    Content = "" // Empty content
                }
            }
        };

        // Act
        var anthropicMessages = AnthropicMessageMapper.ToAnthropicMessages(request);

        // Assert
        // Empty content should still create a message
        anthropicMessages.Should().HaveCount(1);
    }

    #endregion

    #region ToAnthropicTools Tests

    [Fact]
    public void ToAnthropicTools_ConvertsToolDefinitions_Correctly()
    {
        // Arrange
        var inputSchema = JsonDocument.Parse("""
        {
            "properties": {
                "user_id": {
                    "type": "string",
                    "description": "The user ID"
                },
                "role": {
                    "type": "string",
                    "description": "The role name"
                }
            },
            "required": ["user_id", "role"]
        }
        """).RootElement;

        var tools = new List<LlmToolDefinition>
        {
            new()
            {
                Name = "assign_role",
                Description = "Assign a role to a user",
                InputSchema = inputSchema
            }
        };

        // Act
        var anthropicTools = AnthropicMessageMapper.ToAnthropicTools(tools);

        // Assert
        anthropicTools.Should().HaveCount(1);
        var tool = anthropicTools[0];
        tool.Should().NotBeNull();
        var typedTool = (Tool)tool.Value;
        typedTool.Name.Should().Be("assign_role");
        typedTool.Description.Should().Be("Assign a role to a user");
    }

    [Fact]
    public void ToAnthropicTools_WithMultipleTools_ConvertsAll()
    {
        // Arrange
        var schema1 = JsonDocument.Parse("""{"properties": {}, "required": []}""").RootElement;
        var schema2 = JsonDocument.Parse("""{"properties": {}, "required": []}""").RootElement;

        var tools = new List<LlmToolDefinition>
        {
            new() { Name = "tool1", Description = "First tool", InputSchema = schema1 },
            new() { Name = "tool2", Description = "Second tool", InputSchema = schema2 }
        };

        // Act
        var anthropicTools = AnthropicMessageMapper.ToAnthropicTools(tools);

        // Assert
        anthropicTools.Should().HaveCount(2);
    }

    [Fact]
    public void ToAnthropicTools_WithNoRequiredFields_ConvertsCorrectly()
    {
        // Arrange
        var inputSchema = JsonDocument.Parse("""
        {
            "properties": {
                "optional_param": {
                    "type": "string"
                }
            }
        }
        """).RootElement;

        var tools = new List<LlmToolDefinition>
        {
            new()
            {
                Name = "optional_tool",
                Description = "Tool with optional params",
                InputSchema = inputSchema
            }
        };

        // Act
        var anthropicTools = AnthropicMessageMapper.ToAnthropicTools(tools);

        // Assert
        var tool = (Tool)anthropicTools[0].Value;
        tool.Name.Should().Be("optional_tool");
    }

    [Fact]
    public void ToAnthropicTools_WithComplexSchema_ConvertsCorrectly()
    {
        // Arrange
        var inputSchema = JsonDocument.Parse("""
        {
            "properties": {
                "filters": {
                    "type": "object",
                    "properties": {
                        "status": { "type": "string" },
                        "limit": { "type": "integer" }
                    }
                },
                "sort_by": {
                    "type": "string",
                    "enum": ["name", "date", "relevance"]
                }
            },
            "required": ["filters"]
        }
        """).RootElement;

        var tools = new List<LlmToolDefinition>
        {
            new()
            {
                Name = "search",
                Description = "Search with filters",
                InputSchema = inputSchema
            }
        };

        // Act
        var anthropicTools = AnthropicMessageMapper.ToAnthropicTools(tools);

        // Assert
        var tool = (Tool)anthropicTools[0].Value;
        tool.Name.Should().Be("search");
        tool.Description.Should().Be("Search with filters");
    }

    [Fact]
    public void ToAnthropicTools_WithEmptyToolsList_ReturnsEmpty()
    {
        // Arrange
        var tools = new List<LlmToolDefinition>();

        // Act
        var result = AnthropicMessageMapper.ToAnthropicTools(tools);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region ToLlmResponse Tests

    [Fact]
    public void ToLlmResponse_MapsTextContent_Correctly()
    {
        // Arrange
        var anthropicResponse = CreateMessage(
            stopReason: StopReason.EndTurn,
            content: new List<ContentBlock> { new TextBlock { Text = "Hello! I can help you with that.", Citations = null } },
            inputTokens: 100,
            outputTokens: 50
        );

        // Act
        var llmResponse = AnthropicMessageMapper.ToLlmResponse(anthropicResponse);

        // Assert
        llmResponse.Success.Should().BeTrue();
        llmResponse.Content.Should().Be("Hello! I can help you with that.");
        llmResponse.StopReason.Should().Be(LlmStopReason.EndTurn);
    }

    [Fact]
    public void ToLlmResponse_MapsToolUseBlocks_Correctly()
    {
        // Arrange
        var toolInput = new Dictionary<string, JsonElement>
        {
            { "user_id", JsonDocument.Parse("\"123\"").RootElement },
            { "role", JsonDocument.Parse("\"admin\"").RootElement }
        };

        var anthropicResponse = CreateMessage(
            stopReason: StopReason.ToolUse,
            content: new List<ContentBlock>
            {
                new TextBlock { Text = "I'll assign the role now.", Citations = null },
                new ToolUseBlock { ID = "tool-call-1", Name = "assign_role", Input = toolInput }
            },
            inputTokens: 100,
            outputTokens: 50
        );

        // Act
        var llmResponse = AnthropicMessageMapper.ToLlmResponse(anthropicResponse);

        // Assert
        llmResponse.Success.Should().BeTrue();
        llmResponse.StopReason.Should().Be(LlmStopReason.ToolUse);
        llmResponse.ToolCalls.Should().NotBeNull();
        llmResponse.ToolCalls.Should().HaveCount(1);
        llmResponse.ToolCalls![0].Id.Should().Be("tool-call-1");
        llmResponse.ToolCalls[0].Name.Should().Be("assign_role");
    }

    [Fact]
    public void ToLlmResponse_WithMultipleTextBlocks_ConcatenatesContent()
    {
        // Arrange
        var anthropicResponse = CreateMessage(
            stopReason: StopReason.EndTurn,
            content: new List<ContentBlock>
            {
                new TextBlock { Text = "First part. ", Citations = null },
                new TextBlock { Text = "Second part.", Citations = null }
            },
            inputTokens: 100,
            outputTokens: 50
        );

        // Act
        var llmResponse = AnthropicMessageMapper.ToLlmResponse(anthropicResponse);

        // Assert
        llmResponse.Content.Should().Contain("First part");
        llmResponse.Content.Should().Contain("Second part");
    }

    [Fact]
    public void ToLlmResponse_MapsStopReason_EndTurn()
    {
        // Arrange
        var response = CreateMessage(
            stopReason: StopReason.EndTurn,
            content: new List<ContentBlock> { new TextBlock { Text = "Done", Citations = null } },
            inputTokens: 100,
            outputTokens: 50
        );

        // Act
        var llmResponse = AnthropicMessageMapper.ToLlmResponse(response);

        // Assert
        llmResponse.StopReason.Should().Be(LlmStopReason.EndTurn);
    }

    [Fact]
    public void ToLlmResponse_MapsStopReason_ToolUse()
    {
        // Arrange
        var response = CreateMessage(
            stopReason: StopReason.ToolUse,
            content: new List<ContentBlock>
            {
                new ToolUseBlock { ID = "call-1", Name = "tool", Input = new Dictionary<string, JsonElement>() }
            },
            inputTokens: 100,
            outputTokens: 50
        );

        // Act
        var llmResponse = AnthropicMessageMapper.ToLlmResponse(response);

        // Assert
        llmResponse.StopReason.Should().Be(LlmStopReason.ToolUse);
    }

    [Fact]
    public void ToLlmResponse_MapsStopReason_MaxTokens()
    {
        // Arrange
        var response = CreateMessage(
            stopReason: StopReason.MaxTokens,
            content: new List<ContentBlock> { new TextBlock { Text = "Truncated...", Citations = null } },
            inputTokens: 100,
            outputTokens: 1024
        );

        // Act
        var llmResponse = AnthropicMessageMapper.ToLlmResponse(response);

        // Assert
        llmResponse.StopReason.Should().Be(LlmStopReason.MaxTokens);
    }

    [Fact]
    public void ToLlmResponse_MapsStopReason_StopSequence()
    {
        // Arrange
        var response = CreateMessage(
            stopReason: StopReason.StopSequence,
            content: new List<ContentBlock> { new TextBlock { Text = "Stopped", Citations = null } },
            inputTokens: 100,
            outputTokens: 50
        );

        // Act
        var llmResponse = AnthropicMessageMapper.ToLlmResponse(response);

        // Assert
        llmResponse.StopReason.Should().Be(LlmStopReason.EndTurn);
    }

    [Fact]
    public void ToLlmResponse_MapsUsage_Correctly()
    {
        // Arrange
        var response = CreateMessage(
            stopReason: StopReason.EndTurn,
            content: new List<ContentBlock> { new TextBlock { Text = "Response", Citations = null } },
            inputTokens: 250,
            outputTokens: 75,
            cacheReadInputTokens: 100,
            cacheCreationInputTokens: 50
        );

        // Act
        var llmResponse = AnthropicMessageMapper.ToLlmResponse(response);

        // Assert
        llmResponse.Usage.InputTokens.Should().Be(250);
        llmResponse.Usage.OutputTokens.Should().Be(75);
        llmResponse.Usage.CachedTokens.Should().Be(100);
        llmResponse.Usage.CacheWriteTokens.Should().Be(50);
    }

    [Fact]
    public void ToLlmResponse_WithNullCacheTokens_DefaultsToZero()
    {
        // Arrange
        var response = CreateMessage(
            stopReason: StopReason.EndTurn,
            content: new List<ContentBlock> { new TextBlock { Text = "Response", Citations = null } },
            inputTokens: 100,
            outputTokens: 50,
            cacheReadInputTokens: null,
            cacheCreationInputTokens: null
        );

        // Act
        var llmResponse = AnthropicMessageMapper.ToLlmResponse(response);

        // Assert
        llmResponse.Usage.CachedTokens.Should().Be(0);
        llmResponse.Usage.CacheWriteTokens.Should().Be(0);
    }

    [Fact]
    public void ToLlmResponse_WithEmptyContent_ReturnsNullContent()
    {
        // Arrange
        var response = CreateMessage(
            stopReason: StopReason.EndTurn,
            content: new List<ContentBlock>(),
            inputTokens: 100,
            outputTokens: 50
        );

        // Act
        var llmResponse = AnthropicMessageMapper.ToLlmResponse(response);

        // Assert
        llmResponse.Content.Should().BeNull();
    }

    [Fact]
    public void ToLlmResponse_WithMultipleToolCalls_MapsAll()
    {
        // Arrange
        var anthropicResponse = CreateMessage(
            stopReason: StopReason.ToolUse,
            content: new List<ContentBlock>
            {
                new ToolUseBlock { ID = "call-1", Name = "tool1", Input = new Dictionary<string, JsonElement>() },
                new ToolUseBlock { ID = "call-2", Name = "tool2", Input = new Dictionary<string, JsonElement>() }
            },
            inputTokens: 100,
            outputTokens: 50
        );

        // Act
        var llmResponse = AnthropicMessageMapper.ToLlmResponse(anthropicResponse);

        // Assert
        llmResponse.ToolCalls.Should().HaveCount(2);
        llmResponse.ToolCalls![0].Name.Should().Be("tool1");
        llmResponse.ToolCalls[1].Name.Should().Be("tool2");
    }

    #endregion

    #region System Message Tests

    [Fact]
    public void CreateCachedSystemMessage_SetsCacheControl()
    {
        // Arrange
        const string systemPrompt = "You are a helpful assistant.";

        // Act
        var result = AnthropicMessageMapper.CreateCachedSystemMessage(systemPrompt);

        // Assert
        result.Should().HaveCount(1);
        var message = result[0];
        message.Text.Should().Be(systemPrompt);
        message.CacheControl.Should().NotBeNull();
    }

    [Fact]
    public void CreateSystemMessage_HasNoCacheControl()
    {
        // Arrange
        const string systemPrompt = "You are a helpful assistant.";

        // Act
        var result = AnthropicMessageMapper.CreateSystemMessage(systemPrompt);

        // Assert
        result.Should().HaveCount(1);
        var message = result[0];
        message.Text.Should().Be(systemPrompt);
        message.CacheControl.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a Message object with required members set to valid defaults.
    /// </summary>
    private static Message CreateMessage(
        StopReason stopReason,
        List<ContentBlock> content,
        long inputTokens,
        long outputTokens,
        long? cacheReadInputTokens = null,
        long? cacheCreationInputTokens = null)
    {
        return new Message
        {
            ID = "msg-test",
            Model = "claude-3-5-sonnet-20241022",
            Content = content,
            StopReason = stopReason,
            StopSequence = null,
            Usage = new Usage
            {
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                CacheReadInputTokens = cacheReadInputTokens,
                CacheCreationInputTokens = cacheCreationInputTokens,
                CacheCreation = null,
                ServerToolUse = null,
                ServiceTier = null
            }
        };
    }

    #endregion
}
