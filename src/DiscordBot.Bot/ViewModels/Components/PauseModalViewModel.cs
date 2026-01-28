// src/DiscordBot.Bot/ViewModels/Components/PauseModalViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for the Pause Insertion Modal component (Pro mode only).
/// </summary>
/// <remarks>
/// <para>
/// This component provides a modal dialog for inserting SSML pause markers with adjustable duration.
/// Users can select pause duration via slider (100ms-3000ms) or quick preset buttons (250ms, 500ms, 1000ms).
/// A live preview shows how the pause marker will appear in the text.
/// </para>
/// <para>
/// <strong>Typical Usage:</strong>
/// <code>
/// var model = new PauseModalViewModel
/// {
///     Id = "pauseModal",
///     DefaultDuration = 500,
///     OnInsertCallback = "handlePauseInsert"
/// };
/// </code>
/// </para>
/// <para>
/// <strong>Component Rendering:</strong>
/// Include in Razor pages using the _PauseModal partial:
/// <code>
/// @await Html.PartialAsync("Components/_PauseModal", Model)
/// </code>
/// </para>
/// <para>
/// <strong>JavaScript Integration:</strong>
/// The modal must be opened programmatically via the namespaced API:
/// <code>
/// // Open the modal
/// window.pauseModal.open('pauseModal');
///
/// // Close the modal
/// window.pauseModal.close('pauseModal');
/// </code>
/// When the user confirms insertion, the specified callback receives the duration:
/// <code>
/// function handlePauseInsert(duration) {
///     console.log('Insert pause:', duration, 'ms');
///     // Insert pause marker at cursor position
/// }
/// </code>
/// </para>
/// <para>
/// <strong>Accessibility:</strong>
/// The modal implements full accessibility features:
/// <list type="bullet">
/// <item>role="dialog" and aria-modal="true" for screen reader support</item>
/// <item>Focus trap prevents tabbing outside the modal</item>
/// <item>Escape key closes the modal</item>
/// <item>Auto-focus on slider when opened</item>
/// <item>Backdrop click closes the modal</item>
/// </list>
/// </para>
/// <para>
/// <strong>Visibility:</strong>
/// This component should only be visible in Pro TTS mode.
/// The parent page is responsible for controlling visibility based on mode selection.
/// </para>
/// </remarks>
public record PauseModalViewModel
{
    /// <summary>
    /// Gets the unique identifier for the modal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This ID is used to:
    /// - Generate the modal container element ID
    /// - Scope JavaScript functions to this specific instance
    /// - Generate element IDs for slider, preview, value display, etc.
    /// </para>
    /// <para>
    /// Should be camelCase and alphanumeric only.
    /// </para>
    /// <para>
    /// Default value: "pauseModal"
    /// </para>
    /// </remarks>
    public string Id { get; init; } = "pauseModal";

    /// <summary>
    /// Gets the title of the modal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Displayed in the modal header. Used as the accessible name via aria-labelledby.
    /// </para>
    /// <para>
    /// Default value: "Insert Pause"
    /// </para>
    /// </remarks>
    public string Title { get; init; } = "Insert Pause";

    /// <summary>
    /// Gets the minimum pause duration in milliseconds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Defines the lower bound of the slider range.
    /// SSML specification recommends minimum pause duration of 100ms for audible effect.
    /// </para>
    /// <para>
    /// Default value: 100
    /// </para>
    /// </remarks>
    public int MinDuration { get; init; } = 100;

    /// <summary>
    /// Gets the maximum pause duration in milliseconds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Defines the upper bound of the slider range.
    /// Very long pauses (>3 seconds) may confuse listeners or trigger timeouts.
    /// </para>
    /// <para>
    /// Default value: 3000
    /// </para>
    /// </remarks>
    public int MaxDuration { get; init; } = 3000;

    /// <summary>
    /// Gets the step increment for the slider in milliseconds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Controls the granularity of slider adjustments.
    /// 100ms steps provide good balance between precision and ease of use.
    /// </para>
    /// <para>
    /// Default value: 100
    /// </para>
    /// </remarks>
    public int Step { get; init; } = 100;

    /// <summary>
    /// Gets the default pause duration in milliseconds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The slider is initialized to this value when the modal opens.
    /// 500ms (half second) is a common "medium" pause duration.
    /// </para>
    /// <para>
    /// Must be within the range [MinDuration, MaxDuration] and align with Step.
    /// </para>
    /// <para>
    /// Default value: 500
    /// </para>
    /// </remarks>
    public int DefaultDuration { get; init; } = 500;

    /// <summary>
    /// Gets the JavaScript callback function name to call when inserting a pause.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the user clicks "Insert Pause", this function is invoked with the selected duration:
    /// <code>
    /// function handlePauseInsert(duration) {
    ///     // duration is an integer in milliseconds (e.g., 500)
    ///     console.log('Insert pause:', duration, 'ms');
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// The callback is responsible for:
    /// <list type="bullet">
    /// <item>Inserting the pause marker at the cursor position in the textarea</item>
    /// <item>Formatting the marker appropriately (e.g., "[500ms]")</item>
    /// <item>Updating any state or preview displays</item>
    /// </list>
    /// </para>
    /// <para>
    /// Function name must be a valid JavaScript identifier (alphanumeric, underscore, dollar sign;
    /// cannot start with a number). An empty string means no callback is invoked.
    /// </para>
    /// <para>
    /// If the callback name is invalid, an ArgumentException is thrown during initialization.
    /// </para>
    /// </remarks>
    public string OnInsertCallback
    {
        get => _onInsertCallback;
        init => _onInsertCallback = ValidateCallbackName(value);
    }

    private string _onInsertCallback = string.Empty;

    /// <summary>
    /// Gets the text for the insert button.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Default value: "Insert Pause"
    /// </para>
    /// </remarks>
    public string InsertText { get; init; } = "Insert Pause";

    /// <summary>
    /// Gets the text for the cancel button.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Default value: "Cancel"
    /// </para>
    /// </remarks>
    public string CancelText { get; init; } = "Cancel";

    /// <summary>
    /// Validates that the callback name is a valid JavaScript identifier.
    /// </summary>
    /// <param name="name">The callback function name to validate</param>
    /// <returns>The validated callback name, or empty string if null/whitespace</returns>
    /// <exception cref="ArgumentException">Thrown if the callback name is not a valid JavaScript identifier</exception>
    private static string ValidateCallbackName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        // Valid JavaScript identifier: alphanumeric, underscore, dollar sign; cannot start with number
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[\$_a-zA-Z][\$_a-zA-Z0-9]*$"))
        {
            throw new ArgumentException($"Invalid callback function name '{name}'. Must be a valid JavaScript identifier.", nameof(name));
        }

        return name;
    }
}
