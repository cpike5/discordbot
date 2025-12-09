// src/DiscordBot.Bot/ViewModels/Components/EmptyStateViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record EmptyStateViewModel
{
    public EmptyStateType Type { get; init; } = EmptyStateType.NoData;
    public string Title { get; init; } = "No Data";
    public string Description { get; init; } = "There are no items to display.";
    public string? IconSvgPath { get; init; }  // Custom SVG path override
    public string? PrimaryActionText { get; init; }
    public string? PrimaryActionUrl { get; init; }
    public string? PrimaryActionOnClick { get; init; }
    public string? SecondaryActionText { get; init; }
    public string? SecondaryActionUrl { get; init; }
    public EmptyStateSize Size { get; init; } = EmptyStateSize.Default;
}

public enum EmptyStateType
{
    NoData,         // Folder icon - generic empty
    NoResults,      // Search icon with X - no search results
    FirstTime,      // Rocket/stars icon - onboarding
    Error,          // Warning icon - error loading
    NoPermission,   // Lock icon - access restricted
    Offline         // Wifi-off icon - no connection
}

public enum EmptyStateSize
{
    Compact,    // Smaller padding, icon, text
    Default,    // Standard size
    Large       // For full-page empty states
}
