namespace DiscordBot.Core.DTOs.LLM;

/// <summary>Outcome of one <c>ILlmModelCatalogService.RefreshAsync</c> call.</summary>
public sealed record LlmCatalogRefreshResult
{
    /// <summary>Slugs seen for the first time and inserted.</summary>
    public int Added { get; init; }

    /// <summary>Previously-known slugs whose fields were refreshed.</summary>
    public int Updated { get; init; }

    /// <summary>
    /// Previously-available slugs this refresh no longer returned - marked <c>IsAvailable = false</c>,
    /// not deleted.
    /// </summary>
    public int Removed { get; init; }

    /// <summary>When this refresh ran (UTC).</summary>
    public DateTime FetchedAt { get; init; }
}

/// <summary>Outcome of one <c>ILlmModelCatalogService.SetEnabledAsync</c> call.</summary>
public sealed record LlmModelEnableResult
{
    public bool Success { get; init; }

    /// <summary>Set when <see cref="Success"/> is false - e.g. the slug is a mode's current default.</summary>
    public string? Error { get; init; }

    public static LlmModelEnableResult Ok() => new() { Success = true };

    public static LlmModelEnableResult Fail(string error) => new() { Success = false, Error = error };
}
