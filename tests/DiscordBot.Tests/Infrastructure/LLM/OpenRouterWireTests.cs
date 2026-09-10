using System.Text.Json;
using DiscordBot.Infrastructure.Services.LLM.OpenRouter;
using FluentAssertions;

namespace DiscordBot.Tests.Infrastructure.LLM;

/// <summary>
/// Pins the OpenRouter wire contract: how the owned records serialize and deserialize. These are the
/// assertions that catch a rename or a naming-policy change silently altering what goes on the wire.
/// </summary>
public class OpenRouterWireTests
{
    private static JsonElement Serialize(object value) =>
        JsonDocument.Parse(JsonSerializer.Serialize(value, OpenRouterJson.Options)).RootElement;

    [Fact]
    public void Request_serializes_snake_case_with_tools_and_cache_control()
    {
        var request = new ChatCompletionRequest
        {
            Model = "anthropic/claude-sonnet-4",
            MaxTokens = 512,
            Temperature = 0.7,
            Messages = new List<ChatMessage>
            {
                new()
                {
                    Role = "system",
                    Content = new List<ContentPart>
                    {
                        ContentPart.TextPart("You are a bot.", CacheControl.Ephemeral)
                    }
                },
                new() { Role = "user", Content = "Hi" }
            },
            Tools = new List<ToolDefinition>
            {
                new()
                {
                    Function = new FunctionDefinition
                    {
                        Name = "get_roles",
                        Description = "Gets roles",
                        Parameters = JsonDocument.Parse("""{"type":"object"}""").RootElement.Clone()
                    }
                }
            },
            Provider = new ProviderPreferences { RequireParameters = true }
        };

        var json = Serialize(request);

        json.GetProperty("model").GetString().Should().Be("anthropic/claude-sonnet-4");
        json.GetProperty("max_tokens").GetInt32().Should().Be(512);
        json.GetProperty("temperature").GetDouble().Should().Be(0.7);

        var systemPart = json.GetProperty("messages")[0].GetProperty("content")[0];
        systemPart.GetProperty("type").GetString().Should().Be("text");
        systemPart.GetProperty("text").GetString().Should().Be("You are a bot.");
        systemPart.GetProperty("cache_control").GetProperty("type").GetString().Should().Be("ephemeral");
        // No ttl was set, so the null must not be written.
        systemPart.GetProperty("cache_control").TryGetProperty("ttl", out _).Should().BeFalse();

        // A plain string content stays a string, not an object.
        json.GetProperty("messages")[1].GetProperty("content").ValueKind.Should().Be(JsonValueKind.String);

        var tool = json.GetProperty("tools")[0];
        tool.GetProperty("type").GetString().Should().Be("function");
        tool.GetProperty("function").GetProperty("name").GetString().Should().Be("get_roles");
        tool.GetProperty("function").GetProperty("parameters").GetProperty("type").GetString()
            .Should().Be("object");

        json.GetProperty("provider").GetProperty("require_parameters").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void Request_omits_null_properties()
    {
        var request = new ChatCompletionRequest
        {
            Model = "openai/gpt-4o",
            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hi" } }
        };

        var json = Serialize(request);

        json.TryGetProperty("tools", out _).Should().BeFalse();
        json.TryGetProperty("provider", out _).Should().BeFalse();
        json.TryGetProperty("max_tokens", out _).Should().BeFalse();
        json.TryGetProperty("temperature", out _).Should().BeFalse();
        json.GetProperty("messages")[0].TryGetProperty("tool_calls", out _).Should().BeFalse();
        json.GetProperty("messages")[0].TryGetProperty("tool_call_id", out _).Should().BeFalse();
    }

    [Fact]
    public void Tool_result_and_assistant_replay_serialize_the_openai_shapes()
    {
        var messages = new List<ChatMessage>
        {
            new()
            {
                Role = "assistant",
                ToolCalls = new List<ToolCall>
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
                }
            },
            new() { Role = "tool", ToolCallId = "call_1", Content = """{"roles":[]}""" }
        };

        var json = Serialize(messages);

        var assistant = json[0];
        assistant.GetProperty("role").GetString().Should().Be("assistant");
        var call = assistant.GetProperty("tool_calls")[0];
        call.GetProperty("id").GetString().Should().Be("call_1");
        call.GetProperty("function").GetProperty("name").GetString().Should().Be("get_roles");
        // Arguments stay a JSON string on the wire.
        call.GetProperty("function").GetProperty("arguments").ValueKind.Should().Be(JsonValueKind.String);

        var toolMessage = json[1];
        toolMessage.GetProperty("role").GetString().Should().Be("tool");
        toolMessage.GetProperty("tool_call_id").GetString().Should().Be("call_1");
    }

    [Fact]
    public void Response_reads_content_tool_calls_and_usage()
    {
        const string body = """
            {
              "id": "gen-123",
              "model": "anthropic/claude-sonnet-4",
              "choices": [{
                "finish_reason": "tool_calls",
                "message": {
                  "content": "Looking it up",
                  "tool_calls": [{
                    "id": "call_1",
                    "type": "function",
                    "function": {"name": "get_roles", "arguments": "{\"user_id\":\"42\"}"}
                  }]
                }
              }],
              "usage": {
                "prompt_tokens": 1200,
                "completion_tokens": 340,
                "cost": 0.00123,
                "prompt_tokens_details": {"cached_tokens": 1000, "cache_write_tokens": 200}
              }
            }
            """;

        var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(body, OpenRouterJson.Options)!;

        parsed.Id.Should().Be("gen-123");
        parsed.FinishReason.Should().Be("tool_calls");
        parsed.Message!.Content.Should().Be("Looking it up");
        parsed.Message.ToolCalls![0].Function!.Name.Should().Be("get_roles");
        parsed.Message.ToolCalls[0].Function!.Arguments.Should().Be("""{"user_id":"42"}""");
        parsed.Usage!.PromptTokens.Should().Be(1200);
        parsed.Usage.CompletionTokens.Should().Be(340);
        parsed.Usage.Cost.Should().Be(0.00123m);
        parsed.Usage.CacheReadTokens.Should().Be(1000);
        parsed.Usage.CacheWriteTokens.Should().Be(200);
    }

    [Fact]
    public void Usage_without_cache_details_reads_as_zero_and_unknown_cost()
    {
        const string body = """
            {"choices":[{"finish_reason":"stop","message":{"content":"hi"}}],
             "usage":{"prompt_tokens":10,"completion_tokens":5}}
            """;

        var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(body, OpenRouterJson.Options)!;

        parsed.Usage!.CacheReadTokens.Should().Be(0);
        parsed.Usage.CacheWriteTokens.Should().Be(0);
        parsed.Usage.Cost.Should().BeNull();
    }

    [Fact]
    public void Error_body_reads_message_and_code()
    {
        const string body = """{"error":{"message":"No endpoints found","code":404}}""";

        var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(body, OpenRouterJson.Options)!;

        parsed.Error!.Message.Should().Be("No endpoints found");
        parsed.Error.Code.Should().Be(404);
        parsed.Message.Should().BeNull();
    }

    [Fact]
    public void Response_with_no_choices_exposes_null_message()
    {
        var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(
            """{"id":"gen-1","choices":[]}""", OpenRouterJson.Options)!;

        parsed.Message.Should().BeNull();
        parsed.FinishReason.Should().BeNull();
    }

    [Fact]
    public void Response_ignores_unknown_fields()
    {
        const string body = """
            {"id":"gen-1","some_future_field":{"nested":true},
             "choices":[{"finish_reason":"stop","message":{"content":"hi","reasoning":"..."}}]}
            """;

        var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(body, OpenRouterJson.Options)!;

        parsed.Message!.Content.Should().Be("hi");
    }
}
