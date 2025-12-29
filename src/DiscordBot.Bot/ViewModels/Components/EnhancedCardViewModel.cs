// src/DiscordBot.Bot/ViewModels/Components/EnhancedCardViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for enhanced card component with modern styling and accent colors.
/// This is an evolution of the standard CardViewModel with additional design features
/// including gradient top borders, hover lift effects, and better visual hierarchy.
/// </summary>
public record EnhancedCardViewModel
{
    /// <summary>
    /// Card title displayed in the header.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Optional subtitle displayed below the title.
    /// </summary>
    public string? Subtitle { get; init; }

    /// <summary>
    /// Custom HTML content for the card header (overrides Title/Subtitle if provided).
    /// </summary>
    public string? HeaderContent { get; init; }

    /// <summary>
    /// Action buttons or controls displayed in the header (right side).
    /// </summary>
    public string? HeaderActions { get; init; }

    /// <summary>
    /// Main card body content as HTML.
    /// </summary>
    public string? BodyContent { get; init; }

    /// <summary>
    /// Footer content displayed at the bottom of the card.
    /// </summary>
    public string? FooterContent { get; init; }

    /// <summary>
    /// Accent color for the gradient top border.
    /// </summary>
    public CardAccent AccentColor { get; init; } = CardAccent.None;

    /// <summary>
    /// Whether to show the gradient top border (3px colored line).
    /// When true and AccentColor is set, displays a gradient border at the top.
    /// </summary>
    public bool ShowGradientTop { get; init; } = true;

    /// <summary>
    /// Enable hover lift effect (card raises slightly on hover).
    /// </summary>
    public bool HoverLift { get; init; } = true;

    /// <summary>
    /// Makes the card clickable with cursor pointer.
    /// </summary>
    public bool IsInteractive { get; init; } = false;

    /// <summary>
    /// JavaScript onclick handler (e.g., "window.location.href='/page'").
    /// </summary>
    public string? OnClick { get; init; }

    /// <summary>
    /// Enables collapse/expand functionality with a toggle button in the header.
    /// </summary>
    public bool IsCollapsible { get; init; } = false;

    /// <summary>
    /// Initial expanded state for collapsible cards.
    /// </summary>
    public bool IsExpanded { get; init; } = true;

    /// <summary>
    /// Additional CSS classes to apply to the card container.
    /// </summary>
    public string? CssClass { get; init; }

    /// <summary>
    /// Applies minimal padding (useful when body contains its own layout structure).
    /// </summary>
    public bool CompactPadding { get; init; } = false;

    /// <summary>
    /// ID attribute for the card element (useful for testing or JavaScript targeting).
    /// </summary>
    public string? Id { get; init; }
}
