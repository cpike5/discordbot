namespace DiscordBot.Core.DTOs.Llm.Reporting;

/// <summary>
/// One model as fetched and parsed from OpenRouter's <c>GET /models</c> by
/// <c>IOpenRouterModelCatalogClient</c> - already stripped of aliases, with pricing converted to
/// per-million USD and tool/image support resolved from the raw catalog fields. Consumed by
/// <c>ILlmModelCatalogService.RefreshAsync</c> to upsert <see cref="Entities.LlmModel"/> rows.
/// </summary>
public sealed record LlmCatalogModel
{
    /// <summary>The OpenRouter model slug, e.g. "anthropic/claude-sonnet-4.6".</summary>
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Truncated to 1,000 characters.</summary>
    public string? Description { get; init; }

    /// <summary>The slug prefix before the first "/".</summary>
    public required string Vendor { get; init; }

    public int ContextLength { get; init; }

    /// <summary>Per-million-token USD price. Null when the catalog reports "-1" or an unparseable value.</summary>
    public decimal? PromptPricePerMillion { get; init; }

    public decimal? CompletionPricePerMillion { get; init; }

    public decimal? CacheReadPricePerMillion { get; init; }

    public decimal? CacheWritePricePerMillion { get; init; }

    /// <summary>Whether <c>supported_parameters</c> includes "tools".</summary>
    public bool SupportsTools { get; init; }

    /// <summary>Whether <c>architecture.input_modalities</c> includes "image".</summary>
    public bool SupportsImages { get; init; }

    public DateTime? ReleasedAt { get; init; }
}
