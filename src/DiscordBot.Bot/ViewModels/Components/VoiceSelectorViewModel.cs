namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for the voice selector component used in TTS features.
/// </summary>
/// <remarks>
/// <para>
/// This component renders a searchable dropdown for selecting text-to-speech voices.
/// Voices are organized by locale and gender, providing an intuitive browsing experience.
/// </para>
/// <para>
/// <strong>Typical Usage:</strong>
/// <code>
/// var model = new VoiceSelectorViewModel
/// {
///     Voices = GetAvailableVoices(),
///     SelectedVoice = "en-US-JennyNeural",
///     ContainerId = "voiceSelector",
///     OnVoiceChange = "handleVoiceChange",
///     Placeholder = "Select a voice",
///     SearchPlaceholder = "Search voices..."
/// };
/// </code>
/// </para>
/// <para>
/// <strong>Component Rendering:</strong>
/// Include in Razor pages using the _VoiceSelector partial:
/// <code>
/// @await Html.PartialAsync("Components/_VoiceSelector", Model)
/// </code>
/// </para>
/// <para>
/// <strong>JavaScript Integration:</strong>
/// The component provides two JavaScript API methods:
/// <list type="bullet">
/// <item><c>voiceSelector_getValue(containerId)</c> - Returns the currently selected voice short name</item>
/// <item><c>voiceSelector_setValue(containerId, voiceName)</c> - Sets the selected voice programmatically</item>
/// </list>
/// Example usage:
/// <code>
/// // Get current selection
/// const currentVoice = voiceSelector_getValue('voiceSelector');
/// console.log('Current voice:', currentVoice);
///
/// // Set voice programmatically
/// voiceSelector_setValue('voiceSelector', 'en-US-GuyNeural');
///
/// // Custom callback when user changes selection
/// function handleVoiceChange(voiceName) {
///     console.log('Voice changed to:', voiceName);
///     document.getElementById('voiceInput').value = voiceName;
/// }
/// </code>
/// </para>
/// <para>
/// <strong>Voice Search:</strong>
/// The search field supports filtering by:
/// - Voice display name (e.g., "Jenny", "Guy")
/// - Locale display name (e.g., "English (US)")
/// - Gender (e.g., "Female", "Male")
/// </para>
/// </remarks>
public record VoiceSelectorViewModel
{
    /// <summary>
    /// Gets the flat list of all available voice options.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Contains all TTS voices organized as options with display names, gender, and locale information.
    /// Each option includes the voice short name (e.g., "en-US-JennyNeural") as its value.
    /// </para>
    /// <para>
    /// The list is rendered in the dropdown, and all options are searchable by display name,
    /// locale, or gender.
    /// </para>
    /// </remarks>
    public IReadOnlyList<VoiceSelectorVoiceOption> Voices { get; init; } = Array.Empty<VoiceSelectorVoiceOption>();

    /// <summary>
    /// Gets the currently selected voice short name (e.g., "en-US-JennyNeural").
    /// </summary>
    /// <remarks>
    /// <para>
    /// This value corresponds to the <see cref="VoiceSelectorVoiceOption.Value"/> of the selected voice.
    /// Empty string indicates no voice is selected.
    /// </para>
    /// <para>
    /// When changed, the <see cref="OnVoiceChange"/> callback is invoked if provided.
    /// </para>
    /// </remarks>
    public string SelectedVoice { get; init; } = string.Empty;

    /// <summary>
    /// Gets the unique identifier for this voice selector instance, used for generating element IDs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This ID is used to:
    /// - Generate unique element IDs: {ContainerId}-select, {ContainerId}-search
    /// - Generate the container div ID: {ContainerId}
    /// - Scope the JavaScript functions: voiceSelector_getValue(containerId), voiceSelector_setValue(containerId, voiceName)
    /// </para>
    /// <para>
    /// When using multiple voice selectors on the same page, ensure each has a unique ContainerId.
    /// </para>
    /// <para>
    /// Should be camelCase and alphanumeric only.
    /// </para>
    /// <para>
    /// Default value: "voiceSelector"
    /// </para>
    /// </remarks>
    public string ContainerId { get; init; } = "voiceSelector";

    /// <summary>
    /// Gets the optional JavaScript callback function name to invoke when the voice changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, this function will be called whenever the user selects a different voice.
    /// The function receives a single string parameter with the voice short name:
    /// </para>
    /// <code>
    /// function handleVoiceChange(voiceName) {
    ///     console.log('Voice changed to:', voiceName);
    ///     // Update form or trigger other actions
    ///     document.getElementById('voiceInput').value = voiceName;
    /// }
    /// </code>
    /// <para>
    /// If not provided, voice changes will only update the visual state,
    /// without triggering any custom logic.
    /// </para>
    /// </remarks>
    public string? OnVoiceChange { get; init; }

    /// <summary>
    /// Gets the placeholder text displayed in the voice selector trigger button.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shown when no voice is selected, guiding users to open the dropdown and make a selection.
    /// </para>
    /// <para>
    /// Default value: "Select a voice"
    /// </para>
    /// </remarks>
    public string Placeholder { get; init; } = "Select a voice";

    /// <summary>
    /// Gets the placeholder text displayed in the search input field.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides guidance for searching and filtering voices by name, locale, or gender.
    /// </para>
    /// <para>
    /// Default value: "Search voices..."
    /// </para>
    /// </remarks>
    public string SearchPlaceholder { get; init; } = "Search voices...";
}

/// <summary>
/// Represents a single voice option in the voice selector.
/// </summary>
/// <remarks>
/// <para>
/// Encapsulates the metadata for a TTS voice, including:
/// </para>
/// <list type="bullet">
/// <item>Voice identifier (short name, e.g., "en-US-JennyNeural")</item>
/// <item>Display information (display name, gender, locale)</item>
/// <item>User guidance (locale display name for context)</item>
/// </list>
/// <para>
/// <strong>Typical Usage:</strong>
/// <code>
/// var voiceOption = new VoiceSelectorVoiceOption
/// {
///     Value = "en-US-JennyNeural",
///     DisplayName = "Jenny",
///     Gender = "Female",
///     Locale = "en-US",
///     LocaleDisplayName = "English (US)"
/// };
/// </code>
/// </para>
/// </remarks>
public record VoiceSelectorVoiceOption
{
    /// <summary>
    /// Gets the voice short name used by Azure TTS (e.g., "en-US-JennyNeural").
    /// </summary>
    /// <remarks>
    /// <para>
    /// This value is passed directly to the Azure Speech Service for text-to-speech synthesis.
    /// Format follows Azure's naming convention: {locale}-{voiceName}
    /// </para>
    /// <para>
    /// Examples: "en-US-JennyNeural", "en-GB-LibbyNeural", "fr-FR-DeniseNeural"
    /// </para>
    /// </remarks>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display name shown in the voice selector dropdown (e.g., "Jenny").
    /// </summary>
    /// <remarks>
    /// <para>
    /// Short, user-friendly name. Should be 1-2 words maximum for optimal layout.
    /// </para>
    /// <para>
    /// Examples: "Jenny", "Guy", "Libby", "Denise"
    /// </para>
    /// </remarks>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the voice gender ("Female" or "Male").
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used for filtering and organizing voices in the UI.
    /// Valid values are "Female" or "Male".
    /// </para>
    /// </remarks>
    public string Gender { get; init; } = string.Empty;

    /// <summary>
    /// Gets the locale code (e.g., "en-US").
    /// </summary>
    /// <remarks>
    /// <para>
    /// The language and region identifier in BCP-47 format.
    /// Used for organizing voices by language/region.
    /// </para>
    /// <para>
    /// Examples: "en-US", "en-GB", "fr-FR", "de-DE"
    /// </para>
    /// </remarks>
    public string Locale { get; init; } = string.Empty;

    /// <summary>
    /// Gets the human-readable locale display name (e.g., "English (US)").
    /// </summary>
    /// <remarks>
    /// <para>
    /// Displayed alongside the voice name in the dropdown to provide context
    /// about the language and region. Makes it clear which English variant or
    /// French dialect the voice uses.
    /// </para>
    /// <para>
    /// Examples: "English (US)", "English (UK)", "French (France)", "German (Germany)"
    /// </para>
    /// </remarks>
    public string LocaleDisplayName { get; init; } = string.Empty;
}
