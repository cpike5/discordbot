namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for the SSML Preview component (Pro mode only).
/// </summary>
/// <remarks>
/// <para>
/// This component renders a collapsible, read-only preview of the generated SSML markup.
/// It provides syntax highlighting, character count comparison, and a copy-to-clipboard button.
/// The preview updates automatically as the user makes changes via the Pro mode controls.
/// </para>
/// <para>
/// <strong>Typical Usage:</strong>
/// <code>
/// var model = new SsmlPreviewViewModel
/// {
///     ContainerId = "ssmlPreview",
///     InitialSsml = "&lt;speak version=\"1.0\"&gt;...&lt;/speak&gt;",
///     StartCollapsed = true,
///     OnCopy = "handleSsmlCopy",
///     ShowCharacterCount = true
/// };
/// </code>
/// </para>
/// <para>
/// <strong>Component Rendering:</strong>
/// Include in Razor pages using the _SsmlPreview partial:
/// <code>
/// @await Html.PartialAsync("Components/_SsmlPreview", Model)
/// </code>
/// </para>
/// <para>
/// <strong>JavaScript Integration:</strong>
/// The component will automatically:
/// <list type="bullet">
/// <item>Apply syntax highlighting to XML/SSML content</item>
/// <item>Update when ssmlPreview_update(containerId, ssml, plainTextLength) is called</item>
/// <item>Display character count comparison (SSML length vs plain text length)</item>
/// <item>Copy SSML to clipboard when copy button is clicked</item>
/// <item>Show toast notification on successful copy</item>
/// <item>Animate expand/collapse transitions (200ms)</item>
/// <item>Call the specified callback function when copy button is clicked</item>
/// </list>
/// Example callback implementation:
/// <code>
/// function handleSsmlCopy() {
///     console.log('SSML copied to clipboard');
///     // Trigger analytics or other actions
/// }
/// </code>
/// </para>
/// <para>
/// <strong>Syntax Highlighting Colors:</strong>
/// Uses CSS custom properties from the design system:
/// - Tags: var(--color-accent-blue)
/// - Attributes: var(--color-accent-orange)
/// - Values: var(--color-success)
/// - Text content: var(--color-text-primary)
/// </para>
/// <para>
/// <strong>Visibility:</strong>
/// This component should only be visible in Pro TTS mode.
/// The parent page is responsible for controlling visibility based on mode selection.
/// </para>
/// </remarks>
public record SsmlPreviewViewModel
{
    /// <summary>
    /// Gets the unique identifier for this preview instance, used for generating element IDs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This ID is used to:
    /// - Generate the preview container div ID: {ContainerId}
    /// - Scope the JavaScript functions to this specific instance
    /// - Generate element IDs: {ContainerId}-content, {ContainerId}-copyBtn
    /// </para>
    /// <para>
    /// When using multiple previews on the same page (rare), ensure each has a unique ContainerId.
    /// </para>
    /// <para>
    /// Should be camelCase and alphanumeric only.
    /// </para>
    /// <para>
    /// Default value: "ssmlPreview"
    /// </para>
    /// </remarks>
    public string ContainerId { get; init; } = "ssmlPreview";

    /// <summary>
    /// Gets the optional initial SSML content to display in the preview.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If provided, this SSML will be displayed when the component first loads.
    /// If not provided (null or empty), the preview will be empty until updated via JavaScript.
    /// </para>
    /// <para>
    /// The content should be valid SSML markup, typically starting with:
    /// <code>&lt;speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="en-US"&gt;</code>
    /// </para>
    /// <para>
    /// HTML entities will be automatically escaped by Razor.
    /// </para>
    /// </remarks>
    public string? InitialSsml { get; init; }

    /// <summary>
    /// Gets whether the preview should start in a collapsed state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When true (default), the preview content is hidden and only the header is visible.
    /// Users can click the header or chevron icon to expand and view the SSML.
    /// </para>
    /// <para>
    /// When false, the preview starts expanded with full SSML content visible.
    /// </para>
    /// <para>
    /// The collapsed state is controlled via CSS transitions for smooth animation (200ms).
    /// </para>
    /// <para>
    /// Default value: true
    /// </para>
    /// </remarks>
    public bool StartCollapsed { get; init; } = true;

    /// <summary>
    /// Gets the optional JavaScript callback function name to invoke when the copy button is clicked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, this function will be called after successfully copying SSML to the clipboard.
    /// The function receives no parameters.
    /// </para>
    /// <code>
    /// function handleSsmlCopy() {
    ///     console.log('SSML copied to clipboard');
    ///     // Track analytics, update UI, etc.
    /// }
    /// </code>
    /// <para>
    /// If not provided, the copy operation will still work, but no custom callback will be triggered.
    /// </para>
    /// <para>
    /// Function name must be a valid JavaScript identifier (alphanumeric, underscore, dollar sign;
    /// cannot start with a number). An empty string or null is treated as no callback.
    /// </para>
    /// </remarks>
    public string? OnCopy { get; init; }

    /// <summary>
    /// Gets whether to show character count comparison (SSML length vs plain text length).
    /// </summary>
    /// <remarks>
    /// <para>
    /// When true (default), displays a character count badge showing:
    /// - SSML length (total characters in SSML markup)
    /// - Plain text length (user's input without markup)
    /// - Overhead percentage ((SSML - plain) / plain * 100)
    /// </para>
    /// <para>
    /// Example display: "234 chars (124 plain text, +89% markup)"
    /// </para>
    /// <para>
    /// This helps users understand how much overhead the SSML markup adds.
    /// </para>
    /// <para>
    /// Default value: true
    /// </para>
    /// </remarks>
    public bool ShowCharacterCount { get; init; } = true;
}
