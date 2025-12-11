// src/DiscordBot.Bot/ViewModels/Components/QuickActionsCardViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for the Quick Actions card component.
/// </summary>
public record QuickActionsCardViewModel
{
    /// <summary>
    /// Gets the title of the Quick Actions card.
    /// </summary>
    public string Title { get; init; } = "Quick Actions";

    /// <summary>
    /// Gets the list of quick action items.
    /// </summary>
    public IReadOnlyList<QuickActionItemViewModel> Actions { get; init; } = Array.Empty<QuickActionItemViewModel>();

    /// <summary>
    /// Gets a value indicating whether the current user is an admin.
    /// Used to filter admin-only actions in the view.
    /// </summary>
    public bool UserIsAdmin { get; init; } = false;
}
