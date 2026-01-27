namespace DiscordBot.Core.Models;

/// <summary>
/// Predefined combination of voice and style for quick access.
/// </summary>
public class StylePreset
{
    /// <summary>
    /// Unique preset identifier (e.g., "jenny-cheerful", "guy-angry").
    /// </summary>
    public required string PresetId { get; init; }

    /// <summary>
    /// Display name for UI (e.g., "Cheerful Jenny", "Angry Guy").
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Voice short name.
    /// </summary>
    public required string VoiceName { get; init; }

    /// <summary>
    /// Style name.
    /// </summary>
    public required string Style { get; init; }

    /// <summary>
    /// Style degree (0.01-2.0).
    /// </summary>
    public double StyleDegree { get; init; } = 1.0;

    /// <summary>
    /// Optional prosody overrides.
    /// </summary>
    public TtsOptions? ProsodyOptions { get; init; }

    /// <summary>
    /// Description/use case for this preset.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Category for grouping (e.g., "Emotional", "Professional", "Character").
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Whether this is a featured/popular preset.
    /// </summary>
    public bool IsFeatured { get; init; }
}
