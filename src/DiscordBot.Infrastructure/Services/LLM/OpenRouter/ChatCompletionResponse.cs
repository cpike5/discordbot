using System.Text.Json;

namespace DiscordBot.Infrastructure.Services.LLM.OpenRouter;

/// <summary>
/// A buffered <c>/chat/completions</c> reply. Only the fields this bot reads are modelled; unknown
/// fields are ignored on deserialization.
/// </summary>
public sealed record ChatCompletionResponse
{
    public string? Id { get; init; }

    /// <summary>The model that actually served the call (informational; requests carry the ask).</summary>
    public string? Model { get; init; }

    public IReadOnlyList<ResponseChoice>? Choices { get; init; }

    public TokenUsage? Usage { get; init; }

    /// <summary>OpenRouter surfaces some failures as an error object on a 200 body.</summary>
    public ApiErrorBody? Error { get; init; }

    /// <summary>The reply message, or null when the response carries no usable choice.</summary>
    public ResponseMessage? Message => Choices is { Count: > 0 } ? Choices[0].Message : null;

    public string? FinishReason => Choices is { Count: > 0 } ? Choices[0].FinishReason : null;
}

public sealed record ResponseChoice
{
    public string? FinishReason { get; init; }

    public ResponseMessage? Message { get; init; }
}

public sealed record ResponseMessage
{
    public string? Content { get; init; }

    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }
}

/// <summary>One requested tool invocation (also replayed verbatim on assistant messages).</summary>
public sealed record ToolCall
{
    public string? Id { get; init; }

    public string? Type { get; init; }

    public FunctionCall? Function { get; init; }
}

public sealed record FunctionCall
{
    public string? Name { get; init; }

    /// <summary>The call's input as a JSON-object <em>string</em>, per the OpenAI wire shape.</summary>
    public string? Arguments { get; init; }
}

public sealed record TokenUsage
{
    public long PromptTokens { get; init; }

    public long CompletionTokens { get; init; }

    /// <summary>
    /// What OpenRouter charged for this call, in USD — the real billed figure, not an estimate from
    /// a price list, and already net of any prompt-cache discount. Null when the response carried no
    /// <c>cost</c> (a BYOK call, or a provider that doesn't report one), which is why this is
    /// nullable rather than zero: unknown is not free.
    /// </summary>
    public decimal? Cost { get; init; }

    public PromptTokensDetails? PromptTokensDetails { get; init; }

    /// <summary>Prompt-cache read tokens, zero when the response reports none.</summary>
    public long CacheReadTokens => PromptTokensDetails?.CachedTokens ?? 0;

    /// <summary>Prompt-cache write tokens, zero when the response reports none.</summary>
    public long CacheWriteTokens => PromptTokensDetails?.CacheWriteTokens ?? 0;
}

public sealed record PromptTokensDetails
{
    public long? CachedTokens { get; init; }

    public long? CacheWriteTokens { get; init; }
}

public sealed record ApiErrorBody
{
    public string? Message { get; init; }

    public int? Code { get; init; }

    /// <summary>Provider-raw error detail, when OpenRouter attaches it.</summary>
    public JsonElement? Metadata { get; init; }
}
