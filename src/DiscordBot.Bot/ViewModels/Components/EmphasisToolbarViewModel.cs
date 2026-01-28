namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for the Emphasis Toolbar component (Pro mode only).
/// </summary>
/// <remarks>
/// <para>
/// This component renders a floating toolbar that appears when text is selected in the message textarea.
/// It provides buttons to apply SSML emphasis formatting, insert pauses, and clear formatting.
/// </para>
/// <para>
/// <strong>Typical Usage:</strong>
/// <code>
/// var model = new EmphasisToolbarViewModel
/// {
///     TargetTextareaId = "messageInput",
///     ContainerId = "emphasisToolbar",
///     OnFormatChange = "handleFormatChange"
/// };
/// </code>
/// </para>
/// <para>
/// <strong>Component Rendering:</strong>
/// Include in Razor pages using the _EmphasisToolbar partial:
/// <code>
/// @await Html.PartialAsync("Components/_EmphasisToolbar", Model)
/// </code>
/// </para>
/// <para>
/// <strong>JavaScript Integration:</strong>
/// The component will automatically:
/// <list type="bullet">
/// <item>Show/hide toolbar based on text selection in target textarea</item>
/// <item>Position toolbar above the selection (centered, 10px gap)</item>
/// <item>Track formatting markers in JavaScript state</item>
/// <item>Apply visual indicators (orange/blue underlines, pause markers)</item>
/// <item>Call the specified callback function when formatting changes</item>
/// </list>
/// Example callback implementation:
/// <code>
/// function handleFormatChange(formattedText) {
///     console.log('Formatted text:', formattedText);
///     // Update hidden input or trigger other actions
/// }
/// </code>
/// </para>
/// <para>
/// <strong>Visibility:</strong>
/// This component should only be visible in Pro TTS mode.
/// The parent page is responsible for controlling visibility based on mode selection.
/// </para>
/// </remarks>
public record EmphasisToolbarViewModel
{
    /// <summary>
    /// Gets the ID of the target textarea element to attach the toolbar to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The toolbar monitors this textarea for text selection events and positions itself accordingly.
    /// Must match the ID of an existing textarea element on the page.
    /// </para>
    /// <para>
    /// Example: "messageInput"
    /// </para>
    /// </remarks>
    public string TargetTextareaId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the unique identifier for this toolbar instance, used for generating element IDs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This ID is used to:
    /// - Generate the toolbar container div ID: {ContainerId}
    /// - Scope the JavaScript functions to this specific instance
    /// </para>
    /// <para>
    /// When using multiple toolbars on the same page (rare), ensure each has a unique ContainerId.
    /// </para>
    /// <para>
    /// Should be camelCase and alphanumeric only.
    /// </para>
    /// <para>
    /// Default value: "emphasisToolbar"
    /// </para>
    /// </remarks>
    public string ContainerId { get; init; } = "emphasisToolbar";

    /// <summary>
    /// Gets the optional JavaScript callback function name to invoke when formatting changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, this function will be called whenever the user applies or clears formatting.
    /// The function receives an object with the formatted text state:
    /// </para>
    /// <code>
    /// function handleFormatChange(formattedText) {
    ///     console.log('Plain text:', formattedText.plain);
    ///     console.log('Markers:', formattedText.markers);
    ///     // Update form or trigger other actions
    /// }
    /// </code>
    /// <para>
    /// The formattedText object structure:
    /// <code>
    /// {
    ///     plain: "This is an urgent announcement",
    ///     markers: [
    ///         { start: 11, end: 17, type: "emphasis", level: "strong" },
    ///         { start: 25, end: 35, type: "pause", duration: 500 }
    ///     ]
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// If not provided, formatting changes will only update the visual state,
    /// without triggering any custom logic.
    /// </para>
    /// <para>
    /// Function name must be a valid JavaScript identifier (alphanumeric, underscore, dollar sign;
    /// cannot start with a number). An empty string or null is treated as no callback.
    /// </para>
    /// </remarks>
    public string? OnFormatChange
    {
        get => _onFormatChange;
        init => _onFormatChange = ValidateCallbackName(value);
    }

    private string? _onFormatChange;

    /// <summary>
    /// Validates that the callback name is a valid JavaScript identifier.
    /// </summary>
    /// <param name="name">The callback function name to validate</param>
    /// <returns>The validated callback name, or null if invalid</returns>
    private static string? ValidateCallbackName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        // Valid JavaScript identifier: alphanumeric, underscore, dollar sign; cannot start with number
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[\$_a-zA-Z][\$_a-zA-Z0-9]*$"))
        {
            throw new ArgumentException($"Invalid callback function name '{name}'. Must be a valid JavaScript identifier.", nameof(name));
        }

        return name;
    }

    /// <summary>
    /// Gets whether to show keyboard shortcuts in button tooltips.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When true, tooltips will include keyboard shortcut hints:
    /// - Strong Emphasis: "Ctrl+B"
    /// - Moderate Emphasis: "Ctrl+E"
    /// </para>
    /// <para>
    /// Default value: true
    /// </para>
    /// </remarks>
    public bool ShowKeyboardShortcuts { get; init; } = true;
}
