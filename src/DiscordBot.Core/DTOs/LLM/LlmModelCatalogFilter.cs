namespace DiscordBot.Core.DTOs.LLM;

/// <summary>Sort key for <see cref="LlmModelCatalogFilter"/>.</summary>
public enum LlmModelSortBy
{
    Name,
    Vendor,
    PromptPrice,
    CompletionPrice,
    ContextLength,
    ReleasedAt,
}

/// <summary>
/// Server-side filter and sort options for <c>ILlmModelCatalogService.GetCatalogAsync</c>, so the
/// controller consuming this in PR 2 stays thin.
/// </summary>
public sealed record LlmModelCatalogFilter
{
    /// <summary>Case-insensitive substring match against slug and name. Null/empty means no filter.</summary>
    public string? SearchText { get; init; }

    /// <summary>Exact vendor match (the slug prefix before "/"). Null means all vendors.</summary>
    public string? Vendor { get; init; }

    /// <summary>When true, only rows with <c>IsEnabled == true</c>.</summary>
    public bool EnabledOnly { get; init; }

    /// <summary>When true, only rows with <c>IsAvailable == true</c>.</summary>
    public bool AvailableOnly { get; init; }

    /// <summary>When true, only rows with <c>SupportsTools == true</c>.</summary>
    public bool ToolsOnly { get; init; }

    public LlmModelSortBy SortBy { get; init; } = LlmModelSortBy.Name;

    public bool Descending { get; init; }
}
