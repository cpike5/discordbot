namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for the TTS style selector component.
/// </summary>
/// <remarks>
/// <para>
/// This component renders a dropdown for selecting speaking styles and a slider for adjusting style intensity.
/// The available styles are dynamically filtered based on the selected voice's capabilities.
/// </para>
/// <para>
/// <strong>Typical Usage:</strong>
/// <code>
/// var model = new StyleSelectorViewModel
/// {
///     SelectedVoice = "en-US-JennyNeural",
///     SelectedStyle = "cheerful",
///     StyleIntensity = 1.2m,
///     AvailableStyles = GetDefaultStyles(),
///     ContainerId = "styleSelector",
///     OnStyleChange = "handleStyleChange",
///     OnIntensityChange = "handleIntensityChange"
/// };
/// </code>
/// </para>
/// <para>
/// <strong>Component Rendering:</strong>
/// Include in Razor pages using the _StyleSelector partial:
/// <code>
/// @await Html.PartialAsync("Components/_StyleSelector", Model)
/// </code>
/// </para>
/// <para>
/// <strong>JavaScript Integration:</strong>
/// The component will automatically:
/// <list type="bullet">
/// <item>Fetch voice capabilities from API when voice changes</item>
/// <item>Filter dropdown options to show only supported styles</item>
/// <item>Disable styles not supported by the current voice</item>
/// <item>Reset to "(None)" if current style becomes unsupported</item>
/// <item>Disable intensity slider when style is "(None)"</item>
/// <item>Call the specified callback functions when values change</item>
/// </list>
/// Example callback implementation:
/// <code>
/// function handleStyleChange(style) {
///     console.log('Style changed to:', style);
///     document.getElementById('styleInput').value = style;
/// }
///
/// function handleIntensityChange(intensity) {
///     console.log('Intensity changed to:', intensity);
///     document.getElementById('intensityInput').value = intensity;
/// }
/// </code>
/// </para>
/// <para>
/// <strong>Visibility:</strong>
/// This component should only be visible in Standard and Pro TTS modes.
/// Hide it in Simple mode by checking the current mode before rendering.
/// </para>
/// </remarks>
public record StyleSelectorViewModel
{
    /// <summary>
    /// Gets the currently selected voice name (e.g., "en-US-JennyNeural").
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used to fetch voice capabilities from the API endpoint:
    /// <c>GET /api/portal/tts/voices/{voiceName}/capabilities</c>
    /// </para>
    /// <para>
    /// When this value changes, the component automatically:
    /// <list type="number">
    /// <item>Calls the API to get supported styles for the new voice</item>
    /// <item>Updates dropdown options to disable unsupported styles</item>
    /// <item>Resets to "(None)" if current style is not supported</item>
    /// </list>
    /// </para>
    /// </remarks>
    public string SelectedVoice { get; init; } = string.Empty;

    /// <summary>
    /// Gets the currently selected style value (e.g., "cheerful", "angry", or "" for none).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Corresponds to the <see cref="StyleOption.Value"/> of the selected style.
    /// Empty string indicates "(None)" - natural speech without style modification.
    /// </para>
    /// <para>
    /// When set to empty string, the intensity slider is automatically disabled.
    /// </para>
    /// </remarks>
    public string SelectedStyle { get; init; } = string.Empty;

    /// <summary>
    /// Gets the style intensity multiplier (0.5 to 2.0, default 1.0).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Controls how strongly the style is applied. Values:
    /// - 0.5-0.8: Subtle application of the style
    /// - 0.9-1.1: Moderate application (default range)
    /// - 1.2-2.0: Intense application of the style
    /// </para>
    /// <para>
    /// The slider displays three labeled markers:
    /// - Left (0.5): "Subtle"
    /// - Center (1.25): "Moderate"
    /// - Right (2.0): "Intense"
    /// </para>
    /// <para>
    /// This control is disabled when <see cref="SelectedStyle"/> is empty string (None).
    /// </para>
    /// </remarks>
    public decimal StyleIntensity { get; init; } = 1.0m;

    /// <summary>
    /// Gets the collection of available style options.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Typically includes:
    /// - (None) - Natural speech
    /// - cheerful - Happy, energetic
    /// - excited - Very enthusiastic
    /// - friendly - Warm, approachable
    /// - sad - Sorrowful
    /// - angry - Frustrated
    /// - whispering - Quiet, intimate
    /// - shouting - Loud, urgent
    /// - newscast - Professional
    /// </para>
    /// <para>
    /// The <see cref="StyleOption.IsDisabled"/> flag is dynamically updated based on
    /// the selected voice's capabilities via API call.
    /// </para>
    /// </remarks>
    public IReadOnlyList<StyleOption> AvailableStyles { get; init; } = Array.Empty<StyleOption>();

    /// <summary>
    /// Gets the unique identifier for this style selector instance, used for generating element IDs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This ID is used to:
    /// - Generate unique element IDs: {ContainerId}-select, {ContainerId}-slider
    /// - Generate the container div ID: {ContainerId}
    /// - Scope the JavaScript functions: styleSelector_loadStyles(containerId, voiceName)
    /// </para>
    /// <para>
    /// When using multiple style selectors on the same page (rare), ensure each has a unique ContainerId.
    /// </para>
    /// <para>
    /// Should be camelCase and alphanumeric only.
    /// </para>
    /// <para>
    /// Default value: "styleSelector"
    /// </para>
    /// </remarks>
    public string ContainerId { get; init; } = "styleSelector";

    /// <summary>
    /// Gets the optional JavaScript callback function name to invoke when the style changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, this function will be called whenever the user selects a different style.
    /// The function receives a single string parameter with the style value:
    /// </para>
    /// <code>
    /// function handleStyleChange(style) {
    ///     console.log('Style changed to:', style);
    ///     // Update form or trigger other actions
    /// }
    /// </code>
    /// <para>
    /// If not provided, style changes will only update the visual state,
    /// without triggering any custom logic.
    /// </para>
    /// </remarks>
    public string? OnStyleChange { get; init; }

    /// <summary>
    /// Gets the optional JavaScript callback function name to invoke when the intensity changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, this function will be called whenever the user adjusts the intensity slider.
    /// The function receives a single numeric parameter with the intensity value:
    /// </para>
    /// <code>
    /// function handleIntensityChange(intensity) {
    ///     console.log('Intensity changed to:', intensity);
    ///     // Update form or trigger other actions
    /// }
    /// </code>
    /// <para>
    /// If not provided, intensity changes will only update the visual state,
    /// without triggering any custom logic.
    /// </para>
    /// </remarks>
    public string? OnIntensityChange { get; init; }
}
