namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// Represents a single speaking style option in the style selector.
/// </summary>
/// <remarks>
/// <para>
/// Encapsulates the metadata for a TTS speaking style, including:
/// </para>
/// <list type="bullet">
/// <item>Visual presentation (label, icon, description)</item>
/// <item>Azure TTS parameter (style value)</item>
/// <item>User guidance (example phrase)</item>
/// <item>State management (disabled for unsupported styles)</item>
/// </list>
/// <para>
/// <strong>Typical Usage:</strong>
/// <code>
/// var styleOption = new StyleOption
/// {
///     Value = "cheerful",
///     Label = "Cheerful",
///     Icon = "face-smile",
///     Description = "Happy, energetic",
///     Example = "I'm so excited to share this news!",
///     IsDisabled = false
/// };
/// </code>
/// </para>
/// </remarks>
public record StyleOption
{
    /// <summary>
    /// Gets the Azure TTS style value (e.g., "cheerful", "angry", "newscast").
    /// </summary>
    /// <remarks>
    /// <para>
    /// This value is passed directly to the Azure Speech Service. Set to empty string for "(None)" option.
    /// </para>
    /// <para>
    /// Available styles vary by voice. Common styles:
    /// - cheerful, excited, friendly (JennyNeural)
    /// - angry, sad, shouting (GuyNeural)
    /// - newscast, whispering
    /// </para>
    /// </remarks>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display label shown in the dropdown.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Short, user-friendly label. Should be 1-2 words maximum for optimal layout.
    /// </para>
    /// <para>
    /// Examples: "Cheerful", "Angry", "Newscast", "(None)"
    /// </para>
    /// </remarks>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Heroicon name for the style's visual icon.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must be a valid Heroicon (outline style) name. The component will render the corresponding SVG path.
    /// </para>
    /// <para>
    /// Supported icons:
    /// - face-smile (Cheerful)
    /// - sparkles (Excited)
    /// - hand-raised (Friendly)
    /// - face-frown (Sad)
    /// - fire (Angry)
    /// - speaker-x-mark (Whispering)
    /// - speaker-wave (Shouting)
    /// - newspaper (Newscast)
    /// </para>
    /// <para>
    /// Set to empty string for "(None)" option (no icon displayed).
    /// </para>
    /// </remarks>
    public string Icon { get; init; } = string.Empty;

    /// <summary>
    /// Gets the brief description of the style's characteristics.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Short phrase (3-5 words) describing the tone or emotional quality.
    /// Displayed as helper text in the dropdown.
    /// </para>
    /// <para>
    /// Examples: "Happy, energetic", "Frustrated", "Professional", "Natural speech"
    /// </para>
    /// </remarks>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the example phrase demonstrating the style's characteristics.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sample sentence that illustrates how the style sounds. Shown in tooltip on hover.
    /// Should be a complete sentence demonstrating the emotional tone.
    /// </para>
    /// <para>
    /// Examples:
    /// - "I'm so happy to share this exciting news!" (Cheerful)
    /// - "This is absolutely unacceptable!" (Angry)
    /// - "In today's news, we report on..." (Newscast)
    /// </para>
    /// </remarks>
    public string Example { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether this style is disabled (not supported by the current voice).
    /// </summary>
    /// <remarks>
    /// <para>
    /// When true, the style option appears grayed out in the dropdown and cannot be selected.
    /// Automatically determined based on the voice's capabilities from the API.
    /// </para>
    /// <para>
    /// Only styles supported by the selected voice should have IsDisabled = false.
    /// </para>
    /// </remarks>
    public bool IsDisabled { get; init; } = false;
}
