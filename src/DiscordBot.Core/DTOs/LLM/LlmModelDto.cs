using System.Text.Json.Serialization;

namespace DiscordBot.Core.DTOs.LLM;

/// <summary>
/// One <see cref="Entities.LlmModel"/> row shaped for the admin portal's catalog table
/// (<c>LlmModelsController</c>).
/// </summary>
public sealed record LlmModelDto
{
    public required string Slug { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required string Vendor { get; init; }

    public int ContextLength { get; init; }

    public decimal? PromptPricePerMillion { get; init; }

    public decimal? CompletionPricePerMillion { get; init; }

    public decimal? CacheReadPricePerMillion { get; init; }

    public decimal? CacheWritePricePerMillion { get; init; }

    public bool SupportsTools { get; init; }

    public bool SupportsImages { get; init; }

    public DateTime? ReleasedAt { get; init; }

    public bool IsAvailable { get; init; }

    public bool IsEnabled { get; init; }

    public DateTime? EnabledAt { get; init; }
}

/// <summary>Envelope for <c>GET api/admin/llm-models</c>: the filtered/sorted catalog plus context the UI needs.</summary>
public sealed record LlmModelListResponseDto
{
    public required IReadOnlyList<LlmModelDto> Models { get; init; }

    /// <summary>Distinct vendors across the full catalog (not just the filtered page), for the vendor picker.</summary>
    public required IReadOnlyList<string> Vendors { get; init; }

    public DateTime? LastRefreshAt { get; init; }
}

/// <summary>
/// Body for <c>PUT api/admin/llm-models/enabled</c>. The slug travels in the body rather than the
/// route because OpenRouter slugs contain "/" and do not round-trip through a route segment.
/// </summary>
public sealed record LlmModelSetSlugEnabledDto
{
    public required string Slug { get; init; }

    public bool Enabled { get; init; }
}

/// <summary>Where an effective per-mode model slug came from, for <see cref="LlmModeDefaultDto"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LlmModelDefaultSource
{
    /// <summary>A DB setting row (an admin override saved through the Settings page) shadows configuration.</summary>
    Db,

    /// <summary>The bound options value from appsettings/environment - no DB override present.</summary>
    Config,
}

/// <summary>One mode's effective default model, for the read-only defaults panel on the AI Models tab.</summary>
public sealed record LlmModeDefaultDto
{
    /// <summary>Mode key: "GuildAssistant", "DmAssistant", or "FeatureRequests".</summary>
    public required string Mode { get; init; }

    /// <summary>Human-readable label for the mode.</summary>
    public required string Label { get; init; }

    /// <summary>The setting key backing this mode's default (e.g. "Assistant:Sampling:Model").</summary>
    public required string SettingKey { get; init; }

    public required string Slug { get; init; }

    public required LlmModelDefaultSource Source { get; init; }

    /// <summary>True when the slug is a known catalog row with <c>IsEnabled == true</c>.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>True when the slug is a known catalog row with <c>IsAvailable == true</c>.</summary>
    public bool IsAvailable { get; init; }

    /// <summary>False when the slug was never seen by a catalog refresh at all.</summary>
    public bool IsKnown { get; init; }
}

/// <summary>Envelope for <c>GET api/admin/llm-models/defaults</c>.</summary>
public sealed record LlmModelDefaultsResponseDto
{
    public required IReadOnlyList<LlmModeDefaultDto> Modes { get; init; }
}
