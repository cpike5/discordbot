namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// TTS mode for the mode switcher component.
/// </summary>
/// <remarks>
/// <para>
/// Represents the three available modes for the TTS portal interface:
/// </para>
/// <list type="table">
/// <item>
/// <term><see cref="Simple"/></term>
/// <description>
/// Simplified interface with basic voice selection and minimal controls.
/// Ideal for users who want quick, straightforward text-to-speech without advanced features.
/// </description>
/// </item>
/// <item>
/// <term><see cref="Standard"/></term>
/// <description>
/// Standard interface with voice styles, quick presets, and emphasis controls.
/// Balanced feature set for most users. (Default)
/// </description>
/// </item>
/// <item>
/// <term><see cref="Pro"/></term>
/// <description>
/// Professional interface with full SSML control, advanced formatting, and preview.
/// For users who need fine-grained control over speech synthesis.
/// </description>
/// </item>
/// </list>
/// </remarks>
public enum TtsMode
{
    /// <summary>
    /// Simple mode - basic voice selection and minimal controls.
    /// </summary>
    Simple = 0,

    /// <summary>
    /// Standard mode - voice styles, presets, and emphasis controls. (Default)
    /// </summary>
    Standard = 1,

    /// <summary>
    /// Pro mode - full SSML control and advanced features.
    /// </summary>
    Pro = 2
}

/// <summary>
/// ViewModel for the TTS mode switcher component.
/// </summary>
/// <remarks>
/// <para>
/// This component renders a segmented control (pill-style button group) that allows users
/// to switch between Simple, Standard, and Pro TTS modes. The selection is persisted in
/// localStorage and can trigger a custom JavaScript callback when changed.
/// </para>
/// <para>
/// <strong>Typical Usage:</strong>
/// <code>
/// var model = new ModeSwitcherViewModel
/// {
///     CurrentMode = TtsMode.Standard,
///     OnModeChange = "handleModeChange"
/// };
/// </code>
/// </para>
/// <para>
/// <strong>Component Rendering:</strong>
/// Include in Razor pages using the _ModeSwitcher partial:
/// <code>
/// @await Html.PartialAsync("Components/_ModeSwitcher", Model)
/// </code>
/// </para>
/// <para>
/// <strong>JavaScript Integration:</strong>
/// The component will automatically:
/// <list type="bullet">
/// <item>Save the selected mode to localStorage with key "tts_mode_preference"</item>
/// <item>Restore the mode from localStorage on page load</item>
/// <item>Call the specified callback function (if provided) when mode changes</item>
/// </list>
/// Example callback implementation:
/// <code>
/// function handleModeChange(mode) {
///     console.log('Mode changed to:', mode); // 'simple', 'standard', or 'pro'
///     // Update UI based on selected mode
/// }
/// </code>
/// </para>
/// </remarks>
public record ModeSwitcherViewModel
{
    /// <summary>
    /// Gets the currently selected TTS mode.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This determines which mode button is shown as active (highlighted with orange accent).
    /// When rendered, the active mode will have:
    /// - Orange background (<c>bg-accent-orange</c>)
    /// - White text color
    /// - <c>aria-selected="true"</c> for accessibility
    /// </para>
    /// <para>
    /// Inactive modes will have:
    /// - Tertiary background (<c>bg-bg-tertiary</c>)
    /// - Secondary text color
    /// - <c>aria-selected="false"</c>
    /// </para>
    /// <para>
    /// Default value: <see cref="TtsMode.Standard"/>
    /// </para>
    /// </remarks>
    public TtsMode CurrentMode { get; init; } = TtsMode.Standard;

    /// <summary>
    /// Gets the unique identifier for this mode switcher instance, used for generating element IDs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This ID is used to:
    /// - Generate unique button IDs: {ContainerId}-mode-{mode}
    /// - Store state in localStorage with key: tts_mode_preference
    /// - Generate the container div ID: {ContainerId}
    /// </para>
    /// <para>
    /// When using multiple mode switchers on the same page (rare), ensure each has a unique ContainerId.
    /// </para>
    /// <para>
    /// Should be camelCase and alphanumeric only.
    /// </para>
    /// <para>
    /// Default value: "modeSwitcher"
    /// </para>
    /// </remarks>
    public string ContainerId { get; init; } = "modeSwitcher";

    /// <summary>
    /// Gets the optional JavaScript callback function name to invoke when the mode changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, this function will be called whenever the user clicks a mode button.
    /// The function receives a single string parameter with the new mode: "simple", "standard", or "pro".
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// // In your ViewModel:
    /// OnModeChange = "handleModeChange"
    ///
    /// // In your JavaScript:
    /// function handleModeChange(mode) {
    ///     // Update UI visibility
    ///     document.getElementById('advancedOptions').hidden = (mode === 'simple');
    ///
    ///     // Log analytics
    ///     console.log('User switched to:', mode);
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// If not provided, mode changes will only update the UI state and localStorage,
    /// without triggering any custom logic.
    /// </para>
    /// </remarks>
    public string? OnModeChange { get; init; }
}
