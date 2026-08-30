using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.DTOs.LLM.Enums;
using DiscordBot.Infrastructure.Services.LLM.OpenRouter;
using FluentAssertions;

namespace DiscordBot.Tests.Infrastructure.LLM;

/// <summary>
/// Unit tests for OpenRouterMessageMapper.
/// Tests conversion between provider-agnostic LLM DTOs and OpenRouter's OpenAI-compatible wire records.
/// </summary>
public class OpenRouterMessageMapperTests
{
    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    #region ToOpenRouterMessages Tests

    [Fact]
    public void ToOpenRouterMessages_WithUserMessage_ConvertsCorrectly()
    {
        var request = new LlmRequest
        {
            Messages = new List<LlmMessage>
            {
                new() { Role = LlmRole.User, Content = "Hello, assistant!" }
            }
        };

        var messages = OpenRouterMessageMapper.ToOpenRouterMessages(request, enablePromptCaching: false);

        messages.Should().HaveCount(1);
        messages[0].Role.Should().Be("user");
        messages[0].Content.Should().Be("Hello, assistant!");
    }

    [Fact]
    public void ToOpenRouterMessages_WithAssistantMessage_ConvertsCorrectly()
    {
        var request = new LlmRequest
        {
            Messages = new List<LlmMessage>
            {
                new() { Role = LlmRole.Assistant, Content = "How can I help?" }
            }
        };

        var messages = OpenRouterMessageMapper.ToOpenRouterMessages(request, enablePromptCaching: false);

        messages.Should().HaveCount(1);
        messages[0].Role.Should().Be("assistant");
        messages[0].Content.Should().Be("How can I help?");
        messages[0].ToolCalls.Should().BeNull();
    }

    [Fact]
    public void ToOpenRouterMessages_WithSystemPrompt_PlacesSystemMessageFirst()
    {
        var request = new LlmRequest
        {
            SystemPrompt = "You are a helpful bot.",
            Messages = new List<LlmMessage>
            {
                new() { Role = LlmRole.User, Content = "Hi" }
            }
        };

        var messages = OpenRouterMessageMapper.ToOpenRouterMessages(request, enablePromptCaching: false);

        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be("system");
        messages[0].Content.Should().Be("You are a helpful bot.");
        messages[1].Role.Should().Be("user");
    }

    [Fact]
    public void ToOpenRouterMessages_WithoutSystemPrompt_EmitsNoSystemMessage()
    {
        var request = new LlmRequest
        {
            Messages = new List<LlmMessage>
            {
                new() { Role = LlmRole.User, Content = "Hi" }
            }
        };

        var messages = OpenRouterMessageMapper.ToOpenRouterMessages(request, enablePromptCaching: false);

        messages.Should().ContainSingle();
        messages[0].Role.Should().Be("user");
    }

    [Fact]
    public void ToOpenRouterMessages_WithCachingEnabled_MarksSystemMessageAsCacheBreakpoint()
    {
        var request = new LlmRequest
        {
            SystemPrompt = "You are a helpful bot.",
            Messages = new List<LlmMessage>()
        };

        var messages = OpenRouterMessageMapper.ToOpenRouterMessages(request, enablePromptCaching: true);

        var parts = messages[0].Content.Should().BeOfType<List<ContentPart>>().Subject;
        parts.Should().ContainSingle();
        parts[0].Type.Should().Be("text");
        parts[0].Text.Should().Be("You are a helpful bot.");
        parts[0].CacheControl.Should().NotBeNull();
        parts[0].CacheControl!.Type.Should().Be("ephemeral");
    }

    [Fact]
    public void ToOpenRouterMessages_WithCachingDisabled_UsesPlainStringSystemContent()
    {
        var request = new LlmRequest
        {
            SystemPrompt = "You are a helpful bot.",
            Messages = new List<LlmMessage>()
        };

        var messages = OpenRouterMessageMapper.ToOpenRouterMessages(request, enablePromptCaching: false);

        messages[0].Content.Should().BeOfType<string>();
    }

    /// <summary>
    /// The Anthropic mapper threw on a System role inside Messages. On this wire shape a system turn
    /// is just another message, so the throw is gone.
    /// </summary>
    [Fact]
    public void ToOpenRouterMessages_WithSystemRoleInMessages_MapsItRatherThanThrowing()
    {
        var request = new LlmRequest
        {
            Messages = new List<LlmMessage>
            {
                new() { Role = LlmRole.System, Content = "Stay on topic." },
                new() { Role = LlmRole.User, Content = "Hi" }
            }
        };

        var messages = OpenRouterMessageMapper.ToOpenRouterMessages(request, enablePromptCaching: false);

        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be("system");
        messages[0].Content.Should().Be("Stay on topic.");
    }

    [Fact]
    public void ToOpenRouterMessages_WithAssistantToolCalls_EmitsOpenAiToolCallShape()
    {
        var request = new LlmRequest
        {
            Messages = new List<LlmMessage>
            {
                new()
                {
                    Role = LlmRole.Assistant,
                    Content = string.Empty,
                    ToolCalls = new List<LlmToolCall>
                    {
                        new()
                        {
                            Id = "call_1",
                            Name = "get_user_roles",
                            Input = Json("""{"user_id":"42"}""")
                        }
                    }
                }
            }
        };

        var messages = OpenRouterMessageMapper.ToOpenRouterMessages(request, enablePromptCaching: false);

        messages.Should().ContainSingle();
        messages[0].Role.Should().Be("assistant");
        // An assistant turn carrying tool calls has null content on this wire shape.
        messages[0].Content.Should().BeNull();
        messages[0].ToolCalls.Should().ContainSingle();
        messages[0].ToolCalls![0].Id.Should().Be("call_1");
        messages[0].ToolCalls![0].Type.Should().Be("function");
        messages[0].ToolCalls![0].Function!.Name.Should().Be("get_user_roles");
        // Arguments cross the wire as a JSON string, not an object.
        messages[0].ToolCalls![0].Function!.Arguments.Should().Be("""{"user_id":"42"}""");
    }

    [Fact]
    public void ToOpenRouterMessages_WithAssistantTextAndToolCalls_KeepsBoth()
    {
        var request = new LlmRequest
        {
            Messages = new List<LlmMessage>
            {
                new()
                {
                    Role = LlmRole.Assistant,
                    Content = "Let me look that up.",
                    ToolCalls = new List<LlmToolCall>
                    {
                        new() { Id = "call_1", Name = "search", Input = Json("{}") }
                    }
                }
            }
        };

        var messages = OpenRouterMessageMapper.ToOpenRouterMessages(request, enablePromptCaching: false);

        messages[0].Content.Should().Be("Let me look that up.");
        messages[0].ToolCalls.Should().ContainSingle();
    }

    /// <summary>
    /// The AgentRunner appends tool results on a single User-role message (the Anthropic
    /// convention). Each result has to become its own "tool"-role message here.
    /// </summary>
    [Fact]
    public void ToOpenRouterMessages_WithToolResults_FansOutToToolRoleMessages()
    {
        var request = new LlmRequest
        {
            Messages = new List<LlmMessage>
            {
                new()
                {
                    Role = LlmRole.User,
                    ToolResults = new List<LlmToolResult>
                    {
                        new() { ToolCallId = "call_1", Content = Json("""{"roles":["admin"]}""") },
                        new() { ToolCallId = "call_2", Content = Json("""{"count":3}""") }
                    }
                }
            }
        };

        var messages = OpenRouterMessageMapper.ToOpenRouterMessages(request, enablePromptCaching: false);

        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be("tool");
        messages[0].ToolCallId.Should().Be("call_1");
        messages[0].Content.Should().Be("""{"roles":["admin"]}""");
        messages[1].Role.Should().Be("tool");
        messages[1].ToolCallId.Should().Be("call_2");
    }

    /// <summary>There is no is_error flag on this wire shape, so an error has to read as text.</summary>
    [Fact]
    public void ToOpenRouterMessages_WithErrorToolResult_PrefixesContentWithError()
    {
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
                            ToolCallId = "call_1",
                            Content = Json("""{"error":"not found"}"""),
                            IsError = true
                        }
                    }
                }
            }
        };

        var messages = OpenRouterMessageMapper.ToOpenRouterMessages(request, enablePromptCaching: false);

        messages.Should().ContainSingle();
        messages[0].Content.Should().Be("""Error: {"error":"not found"}""");
    }

    /// <summary>
    /// Tool results must directly follow the assistant turn that requested them, so they precede any
    /// user text that shares the same DTO message.
    /// </summary>
    [Fact]
    public void ToOpenRouterMessages_WithToolResultsAndText_EmitsToolMessagesFirst()
    {
        var request = new LlmRequest
        {
            Messages = new List<LlmMessage>
            {
                new()
                {
                    Role = LlmRole.User,
                    Content = "And now summarise it.",
                    ToolResults = new List<LlmToolResult>
                    {
                        new() { ToolCallId = "call_1", Content = Json("{}") }
                    }
                }
            }
        };

        var messages = OpenRouterMessageMapper.ToOpenRouterMessages(request, enablePromptCaching: false);

        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be("tool");
        messages[1].Role.Should().Be("user");
        messages[1].Content.Should().Be("And now summarise it.");
    }

    [Fact]
    public void ToOpenRouterMessages_WithFullToolRoundTrip_PreservesOrder()
    {
        var request = new LlmRequest
        {
            SystemPrompt = "System",
            Messages = new List<LlmMessage>
            {
                new() { Role = LlmRole.User, Content = "What roles do I have?" },
                new()
                {
                    Role = LlmRole.Assistant,
                    Content = string.Empty,
                    ToolCalls = new List<LlmToolCall>
                    {
                        new() { Id = "call_1", Name = "get_roles", Input = Json("{}") }
                    }
                },
                new()
                {
                    Role = LlmRole.User,
                    ToolResults = new List<LlmToolResult>
                    {
                        new() { ToolCallId = "call_1", Content = Json("""{"roles":[]}""") }
                    }
                }
            }
        };

        var messages = OpenRouterMessageMapper.ToOpenRouterMessages(request, enablePromptCaching: false);

        messages.Select(m => m.Role).Should().Equal("system", "user", "assistant", "tool");
    }

    #endregion

    #region ToOpenRouterTools Tests

    /// <summary>
    /// The Anthropic mapper decomposed the schema into properties/required, dropping any other
    /// keyword. Here the schema crosses over verbatim.
    /// </summary>
    [Fact]
    public void ToOpenRouterTools_PassesInputSchemaThroughVerbatim()
    {
        var schema = """
            {"type":"object","properties":{"limit":{"type":"integer","minimum":1,"default":10}},"required":["limit"]}
            """;
        var tools = new List<LlmToolDefinition>
        {
            new() { Name = "search", Description = "Searches things", InputSchema = Json(schema) }
        };

        var wireTools = OpenRouterMessageMapper.ToOpenRouterTools(tools);

        wireTools.Should().ContainSingle();
        wireTools[0].Type.Should().Be("function");
        wireTools[0].Function!.Name.Should().Be("search");
        wireTools[0].Function!.Description.Should().Be("Searches things");

        var parameters = wireTools[0].Function!.Parameters;
        parameters.GetProperty("type").GetString().Should().Be("object");
        // The keywords the Anthropic mapping used to drop survive here.
        var limit = parameters.GetProperty("properties").GetProperty("limit");
        limit.GetProperty("minimum").GetInt32().Should().Be(1);
        limit.GetProperty("default").GetInt32().Should().Be(10);
        parameters.GetProperty("required").EnumerateArray().Select(e => e.GetString())
            .Should().Equal("limit");
    }

    [Fact]
    public void ToOpenRouterTools_WithUninitializedSchema_UsesEmptyObject()
    {
        var tools = new List<LlmToolDefinition>
        {
            new() { Name = "ping", Description = "Pings" }
        };

        var wireTools = OpenRouterMessageMapper.ToOpenRouterTools(tools);

        wireTools[0].Function!.Parameters.ValueKind.Should().Be(JsonValueKind.Object);
    }

    #endregion

    #region ToLlmResponse Tests

    private static ChatCompletionResponse Response(
        string? content = null,
        string? finishReason = "stop",
        IReadOnlyList<ToolCall>? toolCalls = null,
        TokenUsage? usage = null) => new()
        {
            Choices = new List<ResponseChoice>
            {
                new()
                {
                    FinishReason = finishReason,
                    Message = new ResponseMessage { Content = content, ToolCalls = toolCalls }
                }
            },
            Usage = usage
        };

    [Fact]
    public void ToLlmResponse_WithTextReply_MapsContentAndEndTurn()
    {
        var result = OpenRouterMessageMapper.ToLlmResponse(Response("Hello there"));

        result.Success.Should().BeTrue();
        result.Content.Should().Be("Hello there");
        result.StopReason.Should().Be(LlmStopReason.EndTurn);
        result.ToolCalls.Should().BeNull();
    }

    [Fact]
    public void ToLlmResponse_WithToolCalls_MapsToolUseAndParsesArguments()
    {
        var response = Response(
            content: null,
            finishReason: "tool_calls",
            toolCalls: new List<ToolCall>
            {
                new()
                {
                    Id = "call_1",
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = "get_roles",
                        Arguments = """{"user_id":"42"}"""
                    }
                }
            });

        var result = OpenRouterMessageMapper.ToLlmResponse(response);

        result.StopReason.Should().Be(LlmStopReason.ToolUse);
        result.ToolCalls.Should().ContainSingle();
        result.ToolCalls![0].Id.Should().Be("call_1");
        result.ToolCalls[0].Name.Should().Be("get_roles");
        // The JSON string becomes the parsed element the tool registry expects.
        result.ToolCalls[0].Input.GetProperty("user_id").GetString().Should().Be("42");
    }

    /// <summary>
    /// Some providers report "stop" on a turn that nonetheless carries tool calls. Honouring the
    /// reason over the payload would strand the agent loop, so the calls win.
    /// </summary>
    [Fact]
    public void ToLlmResponse_WithToolCallsButStopFinishReason_StillMapsToToolUse()
    {
        var response = Response(
            finishReason: "stop",
            toolCalls: new List<ToolCall>
            {
                new() { Id = "c1", Function = new FunctionCall { Name = "t", Arguments = "{}" } }
            });

        OpenRouterMessageMapper.ToLlmResponse(response).StopReason.Should().Be(LlmStopReason.ToolUse);
    }

    [Fact]
    public void ToLlmResponse_WithLengthFinishReason_MapsToMaxTokens()
    {
        OpenRouterMessageMapper.ToLlmResponse(Response("partial", "length"))
            .StopReason.Should().Be(LlmStopReason.MaxTokens);
    }

    [Theory]
    [InlineData("content_filter")]
    [InlineData("something_new")]
    [InlineData(null)]
    public void ToLlmResponse_WithUnrecognizedFinishReason_MapsToEndTurn(string? finishReason)
    {
        OpenRouterMessageMapper.ToLlmResponse(Response("text", finishReason))
            .StopReason.Should().Be(LlmStopReason.EndTurn);
    }

    [Fact]
    public void ToLlmResponse_WithMalformedToolArguments_DegradesToEmptyObject()
    {
        var response = Response(
            finishReason: "tool_calls",
            toolCalls: new List<ToolCall>
            {
                new() { Id = "c1", Function = new FunctionCall { Name = "t", Arguments = "{not json" } }
            });

        var result = OpenRouterMessageMapper.ToLlmResponse(response);

        result.ToolCalls.Should().ContainSingle();
        result.ToolCalls![0].Input.ValueKind.Should().Be(JsonValueKind.Object);
        result.ToolCalls[0].Input.EnumerateObject().Should().BeEmpty();
    }

    [Fact]
    public void ToLlmResponse_WithNullToolArguments_DegradesToEmptyObject()
    {
        var response = Response(
            finishReason: "tool_calls",
            toolCalls: new List<ToolCall>
            {
                new() { Id = "c1", Function = new FunctionCall { Name = "t", Arguments = null } }
            });

        var result = OpenRouterMessageMapper.ToLlmResponse(response);

        result.ToolCalls![0].Input.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public void ToLlmResponse_WithNamelessToolCall_DropsIt()
    {
        var response = Response(
            content: "text",
            finishReason: "stop",
            toolCalls: new List<ToolCall>
            {
                new() { Id = "c1", Function = new FunctionCall { Name = "  ", Arguments = "{}" } }
            });

        var result = OpenRouterMessageMapper.ToLlmResponse(response);

        result.ToolCalls.Should().BeNull();
        result.StopReason.Should().Be(LlmStopReason.EndTurn);
    }

    [Fact]
    public void ToLlmResponse_MapsUsageIncludingCacheAndCost()
    {
        var response = Response(
            content: "hi",
            usage: new TokenUsage
            {
                PromptTokens = 1200,
                CompletionTokens = 340,
                Cost = 0.00123m,
                PromptTokensDetails = new PromptTokensDetails
                {
                    CachedTokens = 1000,
                    CacheWriteTokens = 200
                }
            });

        var usage = OpenRouterMessageMapper.ToLlmResponse(response).Usage;

        usage.InputTokens.Should().Be(1200);
        usage.OutputTokens.Should().Be(340);
        usage.CachedTokens.Should().Be(1000);
        usage.CacheWriteTokens.Should().Be(200);
        usage.EstimatedCost.Should().Be(0.00123m);
    }

    /// <summary>A missing cost is unknown, not free — it must stay null rather than become zero.</summary>
    [Fact]
    public void ToLlmResponse_WithoutCost_LeavesEstimatedCostNull()
    {
        var response = Response("hi", usage: new TokenUsage { PromptTokens = 10, CompletionTokens = 5 });

        OpenRouterMessageMapper.ToLlmResponse(response).Usage.EstimatedCost.Should().BeNull();
    }

    [Fact]
    public void ToLlmResponse_WithoutUsage_ReportsZeroTokens()
    {
        var usage = OpenRouterMessageMapper.ToLlmResponse(Response("hi")).Usage;

        usage.InputTokens.Should().Be(0);
        usage.OutputTokens.Should().Be(0);
        usage.EstimatedCost.Should().BeNull();
    }

    [Fact]
    public void ToLlmResponse_WithEmptyContent_ReturnsNullContent()
    {
        OpenRouterMessageMapper.ToLlmResponse(Response(string.Empty)).Content.Should().BeNull();
    }

    #endregion
}
