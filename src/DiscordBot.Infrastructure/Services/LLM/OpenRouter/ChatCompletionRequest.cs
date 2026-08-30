using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiscordBot.Infrastructure.Services.LLM.OpenRouter;

/// <summary>
/// One <c>POST /chat/completions</c> request, as this bot speaks it. Owned wire records rather than
/// an SDK: OpenRouter's OpenAI-compatible surface plus the few extensions used here
/// (<c>cache_control</c>, <c>provider</c>) is the whole abstraction the LLM layer builds on.
/// Property names serialize snake_case via <see cref="OpenRouterJson.Options"/>.
/// </summary>
public sealed record ChatCompletionRequest
{
    public required string Model { get; init; }

    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    public int? MaxTokens { get; init; }

    public double? Temperature { get; init; }

    public IReadOnlyList<ToolDefinition>? Tools { get; init; }

    /// <summary>
    /// Provider routing controls. Set whenever <see cref="Tools"/> is non-empty so a multi-provider
    /// slug only routes somewhere that actually supports function calling.
    /// </summary>
    public ProviderPreferences? Provider { get; init; }
}

/// <summary>
/// One conversation message. <see cref="Content"/> is a plain string or a list of
/// <see cref="ContentPart"/> (the wire field is polymorphic); tool-result messages carry
/// <see cref="ToolCallId"/> with role <c>"tool"</c>, and assistant messages replaying a tool round
/// carry <see cref="ToolCalls"/>.
/// </summary>
public sealed record ChatMessage
{
    public required string Role { get; init; }

    public object? Content { get; init; }

    public string? ToolCallId { get; init; }

    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }
}

/// <summary>One block of a multipart message. Only text parts are produced today.</summary>
public sealed record ContentPart
{
    public required string Type { get; init; }

    public string? Text { get; init; }

    /// <summary>
    /// Anthropic prompt-cache breakpoint, passed through by OpenRouter for Claude-family models and
    /// ignored by everything else.
    /// </summary>
    public CacheControl? CacheControl { get; init; }

    public static ContentPart TextPart(string text, CacheControl? cacheControl = null) =>
        new() { Type = "text", Text = text, CacheControl = cacheControl };
}

/// <summary>A cache breakpoint: <c>{"type":"ephemeral"}</c> is the 5-minute marker.</summary>
public sealed record CacheControl
{
    public string Type { get; init; } = "ephemeral";

    public string? Ttl { get; init; }

    /// <summary>The default 5-minute ephemeral breakpoint.</summary>
    public static CacheControl Ephemeral { get; } = new();
}

/// <summary>One entry in the request's <c>tools</c> array, in the OpenAI function envelope.</summary>
public sealed record ToolDefinition
{
    public string Type { get; init; } = "function";

    public FunctionDefinition? Function { get; init; }
}

public sealed record FunctionDefinition
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>The tool's input contract as one JSON-Schema object, passed through verbatim.</summary>
    public JsonElement Parameters { get; init; }
}

/// <summary>
/// Provider routing controls. <see cref="RequireParameters"/> is the live one: set on every request
/// that carries tools, so a multi-provider slug only routes to a provider that supports the
/// request's parameters. Without it a provider with no native function-calling support can be
/// picked, and the model then has nothing but its own text to fake a tool call with — which surfaces
/// as a raw tool-call-shaped string in the user-visible reply.
/// </summary>
public sealed record ProviderPreferences
{
    public bool? RequireParameters { get; init; }
}

/// <summary>The one serializer configuration every OpenRouter (de)serialization uses.</summary>
public static class OpenRouterJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
