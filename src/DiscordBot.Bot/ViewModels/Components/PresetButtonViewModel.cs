namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// Represents a single voice preset button in the preset bar.
/// </summary>
/// <remarks>
/// <para>
/// Encapsulates all the configuration for a quick-access voice preset, including:
/// </para>
/// <list type="bullet">
/// <item>Visual presentation (name, icon, description)</item>
/// <item>Azure TTS parameters (voice name, style, speed, pitch)</item>
/// <item>State management (active indicator)</item>
/// </list>
/// <para>
/// <strong>Typical Usage:</strong>
/// <code>
/// var preset = new PresetButtonViewModel
/// {
///     Id = "excited",
///     Name = "Excited",
///     Icon = "sparkles",
///     Description = "High energy, cheerful tone",
///     VoiceName = "en-US-JennyNeural",
///     Style = "cheerful",
///     Speed = 1.2m,
///     Pitch = 1.1m,
///     IsActive = false
/// };
/// </code>
/// </para>
/// </remarks>
public record PresetButtonViewModel
{
    /// <summary>
    /// Gets the unique identifier for this preset.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used for:
    /// - Generating button element IDs: {ContainerId}-preset-{Id}
    /// - Identifying the preset in JavaScript callbacks
    /// - Determining active state
    /// </para>
    /// <para>
    /// Should be lowercase, alphanumeric, and hyphen-separated (e.g., "excited", "robot", "narrator").
    /// </para>
    /// </remarks>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display name shown in the preset button.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Short, user-friendly label displayed below the icon.
    /// Should be 1-2 words maximum for optimal button layout.
    /// </para>
    /// <para>
    /// Examples: "Excited", "Robot", "Narrator", "Whisper"
    /// </para>
    /// </remarks>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Heroicon name for the preset's visual icon.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must be a valid Heroicon (outline style) name. The component will render the corresponding SVG path.
    /// </para>
    /// <para>
    /// Supported icons:
    /// - sparkles (Excited)
    /// - megaphone (Announcer)
    /// - computer-desktop (Robot)
    /// - face-smile (Friendly)
    /// - fire (Angry)
    /// - microphone (Narrator)
    /// - speaker-x-mark (Whisper)
    /// - speaker-wave (Shouting)
    /// </para>
    /// </remarks>
    public string Icon { get; init; } = string.Empty;

    /// <summary>
    /// Gets the tooltip description shown on hover.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Optional. Provides additional context about the preset's characteristics.
    /// Should be a brief phrase (5-10 words) describing the tone or use case.
    /// </para>
    /// <para>
    /// Examples: "High energy, cheerful tone", "Deep voice for announcements", "Robotic, monotone delivery"
    /// </para>
    /// </remarks>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Azure TTS voice name (e.g., "en-US-JennyNeural", "en-US-GuyNeural").
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must be a valid Azure Neural TTS voice identifier.
    /// See: https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support
    /// </para>
    /// <para>
    /// Common voices:
    /// - en-US-JennyNeural (female, versatile)
    /// - en-US-GuyNeural (male, versatile)
    /// - en-US-AriaNeural (female, expressive)
    /// - en-US-DavisNeural (male, narration)
    /// </para>
    /// </remarks>
    public string VoiceName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional Azure TTS speaking style (e.g., "cheerful", "angry", "narration-professional").
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only applicable to voices that support styles. Set to null or empty for neutral delivery.
    /// </para>
    /// <para>
    /// Available styles vary by voice. Common styles:
    /// - cheerful, excited, friendly (JennyNeural)
    /// - angry, sad, shouting (GuyNeural)
    /// - newscast, narration-professional (DavisNeural)
    /// - whispering (multiple voices)
    /// </para>
    /// </remarks>
    public string? Style { get; init; }

    /// <summary>
    /// Gets the speech rate multiplier (0.5 to 2.0).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Controls how fast the voice speaks. Values:
    /// - &lt; 1.0: Slower speech (e.g., 0.8 for deliberate narration)
    /// - 1.0: Normal speed
    /// - &gt; 1.0: Faster speech (e.g., 1.2 for energetic delivery)
    /// </para>
    /// <para>
    /// Azure TTS supports 0.5x to 2.0x range. Default: 1.0
    /// </para>
    /// </remarks>
    public decimal Speed { get; init; } = 1.0m;

    /// <summary>
    /// Gets the pitch adjustment multiplier (0.5 to 2.0).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Controls the voice pitch. Values:
    /// - &lt; 1.0: Lower pitch (e.g., 0.7 for robotic effect)
    /// - 1.0: Natural pitch
    /// - &gt; 1.0: Higher pitch (e.g., 1.3 for excited/shouting)
    /// </para>
    /// <para>
    /// Azure TTS supports 0.5x to 2.0x range. Default: 1.0
    /// </para>
    /// </remarks>
    public decimal Pitch { get; init; } = 1.0m;

    /// <summary>
    /// Gets whether this preset is currently active (selected).
    /// </summary>
    /// <remarks>
    /// <para>
    /// When true, the button will display with:
    /// - Orange accent border and glow
    /// - "Active" badge indicator
    /// - <c>aria-pressed="true"</c> for accessibility
    /// </para>
    /// <para>
    /// Only one preset should be active at a time in the preset bar.
    /// </para>
    /// </remarks>
    public bool IsActive { get; init; }
}
