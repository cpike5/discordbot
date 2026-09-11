using DiscordBot.Core.Interfaces.LLM;

namespace DiscordBot.Core.DTOs.Llm.Reporting;

/// <summary>Where <see cref="ILlmModelResolver.ResolveAsync"/>'s slug came from.</summary>
public enum LlmModelResolutionSource
{
    /// <summary>A DB setting row (an admin override saved through the AI Models tab) shadows configuration.</summary>
    Database,

    /// <summary>The bound options value from appsettings/environment - no DB override present.</summary>
    Configuration,

    /// <summary>Neither a DB row nor a configured value was present; <c>OpenRouter:DefaultModel</c> was used.</summary>
    Fallback
}

/// <summary>
/// Catalog pricing for a resolved model, per million tokens in USD. Only populated when the
/// catalog has a row for the slug with at least one non-null price; null fields mean the catalog
/// did not report that price (the caller's configured fallback rate should be used for it).
/// </summary>
public sealed record LlmCatalogPricing
{
    public decimal? PromptPricePerMillion { get; init; }
    public decimal? CompletionPricePerMillion { get; init; }
    public decimal? CacheReadPricePerMillion { get; init; }
    public decimal? CacheWritePricePerMillion { get; init; }
}

/// <summary>
/// The effective OpenRouter model slug for one <see cref="Enums.LlmMode"/>, as resolved by
/// <see cref="ILlmModelResolver"/>.
/// </summary>
public sealed record LlmResolvedModel
{
    public required string Slug { get; init; }

    public required LlmModelResolutionSource Source { get; init; }

    /// <summary>
    /// The slug that would be used absent any DB override: the bound configuration value, or
    /// <c>OpenRouter:DefaultModel</c> when nothing is configured for this mode. Populated
    /// regardless of whether <see cref="Slug"/> itself came from a DB override, so a caller can
    /// show what "the configured value" resolves to even while a DB override shadows it (e.g. the
    /// AI Models tab's "Use configured value (&lt;slug&gt;)" option).
    /// </summary>
    public required string ConfiguredSlug { get; init; }

    /// <summary>
    /// Whether the slug is a known catalog row with <c>IsEnabled == true</c>. Null when the slug
    /// has never been seen by a catalog refresh at all.
    /// </summary>
    public bool? IsEnabled { get; init; }

    /// <summary>
    /// Whether the slug is a known catalog row with <c>IsAvailable == true</c>. Null when the slug
    /// has never been seen by a catalog refresh at all.
    /// </summary>
    public bool? IsAvailable { get; init; }

    /// <summary>Catalog pricing for this slug, when known. Null when there is no catalog row for it.</summary>
    public LlmCatalogPricing? Pricing { get; init; }
}
