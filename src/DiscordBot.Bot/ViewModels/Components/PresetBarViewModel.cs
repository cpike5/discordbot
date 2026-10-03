namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for the TTS preset bar component.
/// </summary>
/// <remarks>
/// <para>
/// This component renders a collection of quick-access voice preset buttons arranged in a grid (desktop)
/// or horizontal scrollable carousel (mobile). Each preset applies a predefined combination of voice,
/// style, speed, and pitch settings to the TTS form.
/// </para>
/// <para>
/// <strong>Typical Usage:</strong>
/// <code>
/// var model = new PresetBarViewModel
/// {
///     Presets = new[]
///     {
///         new PresetButtonViewModel
///         {
///             Id = "excited",
///             Name = "Excited",
///             Icon = "sparkles",
///             VoiceName = "en-US-JennyNeural",
///             Style = "cheerful",
///             Speed = 1.2m,
///             Pitch = 1.1m
///         },
///         // ... more presets
///     },
///     ContainerId = "presetBar",
///     OnPresetApply = "handlePresetApply"
/// };
/// </code>
/// </para>
/// <para>
/// <strong>Component Rendering:</strong>
/// Include in Razor pages using the _PresetBar partial:
/// <code>
/// @await Html.PartialAsync("Components/_PresetBar", Model)
/// </code>
/// </para>
/// <para>
/// <strong>JavaScript Integration:</strong>
/// The component will automatically:
/// <list type="bullet">
/// <item>Apply a 200ms highlight pulse animation when a preset is clicked</item>
/// <item>Update the active state indicator on the selected preset</item>
/// <item>Call the specified callback function (if provided) with the preset data</item>
/// <item>The parent page is responsible for updating form controls and showing toast notifications</item>
/// </list>
/// Example callback implementation:
/// <code>
/// function handlePresetApply(presetData) {
///     console.log('Applying preset:', presetData.id);
///     // presetData contains: { id, voice, style, speed, pitch }
///
///     // Update form controls
///     document.getElementById('voiceSelect').value = presetData.voice;
///     document.getElementById('styleSelect').value = presetData.style || '';
///     document.getElementById('speedInput').value = presetData.speed;
///     document.getElementById('pitchInput').value = presetData.pitch;
///
///     // Show toast notification
///     showToast(`Applied "${presetData.name}" preset`);
/// }
/// </code>
/// </para>
/// </remarks>
public record PresetBarViewModel
{
    /// <summary>
    /// Gets the collection of preset buttons to display.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Typically 8 default presets are provided:
    /// - Excited (sparkles)
    /// - Announcer (megaphone)
    /// - Robot (computer-desktop)
    /// - Friendly (face-smile)
    /// - Angry (fire)
    /// - Narrator (microphone)
    /// - Whisper (speaker-x-mark)
    /// - Shouting (speaker-wave)
    /// </para>
    /// <para>
    /// The order of presets in this collection determines their display order.
    /// On desktop, they are arranged in a 4-column grid (2 rows).
    /// On mobile, they appear in a horizontal scrollable row.
    /// </para>
    /// </remarks>
    public IReadOnlyList<PresetButtonViewModel> Presets { get; init; } = Array.Empty<PresetButtonViewModel>();

    /// <summary>
    /// Gets the unique identifier for this preset bar instance, used for generating element IDs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This ID is used to:
    /// - Generate unique button IDs: {ContainerId}-preset-{presetId}
    /// - Generate the container div ID: {ContainerId}
    /// - Scope the JavaScript functions: presetBar_applyPreset(containerId, presetId)
    /// </para>
    /// <para>
    /// When using multiple preset bars on the same page (rare), ensure each has a unique ContainerId.
    /// </para>
    /// <para>
    /// Should be camelCase and alphanumeric only.
    /// </para>
    /// <para>
    /// Default value: "presetBar"
    /// </para>
    /// </remarks>
    public string ContainerId { get; init; } = "presetBar";

    /// <summary>
    /// Gets the optional JavaScript callback function name to invoke when a preset is applied.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, this function will be called whenever the user clicks a preset button.
    /// The function receives a single object parameter with the preset data:
    /// </para>
    /// <code>
    /// {
    ///     id: "excited",
    ///     name: "Excited",
    ///     voice: "en-US-JennyNeural",
    ///     style: "cheerful",
    ///     speed: 1.2,
    ///     pitch: 1.1
    /// }
    /// </code>
    /// <para>
    /// Example usage:
    /// </para>
    /// <code>
    /// // In your ViewModel:
    /// OnPresetApply = "handlePresetApply"
    ///
    /// // In your JavaScript:
    /// function handlePresetApply(presetData) {
    ///     // Update form controls with preset values
    ///     applyPresetToForm(presetData);
    ///
    ///     // Show confirmation toast
    ///     showToast(`Applied "${presetData.name}" preset`, 'success');
    /// }
    /// </code>
    /// <para>
    /// If not provided, preset clicks will only update the visual active state,
    /// without triggering any custom logic.
    /// </para>
    /// </remarks>
    public string? OnPresetApply { get; init; }

    /// <summary>
    /// Gets the address of this page's custom-presets endpoint (GET lists, POST saves, DELETE
    /// <c>{url}/{id}</c> removes), or null for the member portal's own
    /// <c>/api/portal/tts/{guildId}/presets/custom</c>.
    /// </summary>
    /// <remarks>
    /// The portal endpoint requires the guild's member portal to be on, so the admin Text-to-Speech
    /// page passes <c>/api/guilds/{guildId}/tts/presets/custom</c>, which sits behind the admin policies.
    /// </remarks>
    public string? CustomPresetsUrl { get; init; }

    /// <summary>
    /// Gets the id of the plain input or select that holds the voice, or null for the member portal's
    /// <c>VoiceSelector</c> widget (<c>portalVoiceSelector</c>).
    /// </summary>
    /// <remarks>
    /// "Save as preset" reads the voice from here. The admin Text-to-Speech page passes
    /// <c>voiceSelect</c>. Rendered as <c>data-voice-input</c> and read by <c>preset-bar.js</c>.
    /// </remarks>
    public string? VoiceInputId { get; init; }

    /// <summary>
    /// Gets the id of the select that holds the style, or null for the portal's
    /// <c>portalStyleSelector-select</c>. The admin page passes <c>styleSelector-select</c>.
    /// Speed and pitch are read from <c>speedSlider</c> and <c>pitchSlider</c> on both pages.
    /// </summary>
    public string? StyleInputId { get; init; }
}
