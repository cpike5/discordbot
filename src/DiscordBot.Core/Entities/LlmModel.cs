namespace DiscordBot.Core.Entities;

/// <summary>
/// One OpenRouter model as pulled into the local catalog. Rows are upserted by
/// <c>ILlmModelCatalogService.RefreshAsync</c> and never deleted, so a model that
/// disappears from OpenRouter is kept with <see cref="IsAvailable"/> set to false rather
/// than removed - an admin's decision to enable it is never silently lost.
/// </summary>
public class LlmModel
{
    /// <summary>
    /// The OpenRouter model slug (e.g. "anthropic/claude-sonnet-4.6"). Primary key.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name from the catalog.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Catalog description, truncated to 1,000 characters.</summary>
    public string? Description { get; set; }

    /// <summary>The slug prefix before the first "/", used for grouping and filtering.</summary>
    public string Vendor { get; set; } = string.Empty;

    /// <summary>Context window size in tokens.</summary>
    public int ContextLength { get; set; }

    /// <summary>Prompt (input) price per million tokens, in USD. Null when the catalog reports unknown ("-1").</summary>
    public decimal? PromptPricePerMillion { get; set; }

    /// <summary>Completion (output) price per million tokens, in USD. Null when the catalog reports unknown.</summary>
    public decimal? CompletionPricePerMillion { get; set; }

    /// <summary>Prompt-cache read price per million tokens, in USD. Null when not reported or unknown.</summary>
    public decimal? CacheReadPricePerMillion { get; set; }

    /// <summary>Prompt-cache write price per million tokens, in USD. Null when not reported or unknown.</summary>
    public decimal? CacheWritePricePerMillion { get; set; }

    /// <summary>Whether the model's <c>supported_parameters</c> include "tools" (native function calling).</summary>
    public bool SupportsTools { get; set; }

    /// <summary>Whether the model's <c>architecture.input_modalities</c> include "image".</summary>
    public bool SupportsImages { get; set; }

    /// <summary>Model release date, from the catalog's unix <c>created</c> field.</summary>
    public DateTime? ReleasedAt { get; set; }

    /// <summary>When this slug was first seen by a catalog refresh (UTC).</summary>
    public DateTime FirstSeenAt { get; set; }

    /// <summary>When this slug was last seen by a catalog refresh (UTC).</summary>
    public DateTime LastSeenAt { get; set; }

    /// <summary>
    /// Whether the most recent catalog refresh still returned this slug. Set false, never
    /// deleted, when a refresh no longer sees it.
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// The admin allowlist flag. Only ever changed by an explicit admin action (or the
    /// one-time bootstrap on the first-ever refresh) - a catalog refresh never touches it.
    /// Default false: nothing is enabled until an admin opts in.
    /// </summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>When this model was enabled, if it currently is (or was, historically).</summary>
    public DateTime? EnabledAt { get; set; }

    /// <summary>Who enabled this model - a user ID, or null for a system action (e.g. bootstrap).</summary>
    public string? EnabledBy { get; set; }
}
