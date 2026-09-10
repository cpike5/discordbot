namespace DiscordBot.Infrastructure.Services.LLM.OpenRouter;

/// <summary>
/// The body of <c>GET /models</c>. Owned wire records rather than an SDK - only the fields this bot
/// reads are declared; the rest is ignored by the serializer. Property names serialize snake_case via
/// <see cref="OpenRouterJson.Options"/>.
/// </summary>
public sealed record ModelListResponse
{
    public IReadOnlyList<ModelInfo>? Data { get; init; }
}

/// <summary>One catalog entry. Alias entries (id starting with "~") are skipped by the caller.</summary>
public sealed record ModelInfo
{
    public string? Id { get; init; }

    public string? Name { get; init; }

    public string? Description { get; init; }

    /// <summary>Unix seconds. Null becomes a null <c>ReleasedAt</c>.</summary>
    public long? Created { get; init; }

    public int? ContextLength { get; init; }

    public ModelPricing? Pricing { get; init; }

    public ModelArchitecture? Architecture { get; init; }

    /// <summary>Contains "tools" for models with native function calling.</summary>
    public IReadOnlyList<string>? SupportedParameters { get; init; }
}

/// <summary>Per-token USD prices, as decimal strings. "-1" or unparseable means unknown (null).</summary>
public sealed record ModelPricing
{
    public string? Prompt { get; init; }

    public string? Completion { get; init; }

    public string? InputCacheRead { get; init; }

    public string? InputCacheWrite { get; init; }
}

public sealed record ModelArchitecture
{
    /// <summary>Contains "image" for models that accept image input.</summary>
    public IReadOnlyList<string>? InputModalities { get; init; }
}
