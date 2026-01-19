// src/DiscordBot.Bot/ViewModels/Components/CommandBreadcrumbViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for Commands page breadcrumb navigation component.
/// Generates breadcrumb path: Home / Commands / [Active Tab].
/// </summary>
public record CommandBreadcrumbViewModel
{
    /// <summary>
    /// Currently active tab identifier (command-list, execution-logs, analytics).
    /// </summary>
    public string ActiveTab { get; init; } = "command-list";

    /// <summary>
    /// Gets the display name for the current active tab.
    /// </summary>
    /// <returns>Human-readable tab name for breadcrumb display.</returns>
    public string GetTabDisplayName() => ActiveTab switch
    {
        "command-list" => "Command List",
        "execution-logs" => "Execution Logs",
        "analytics" => "Analytics",
        _ => "Command List"
    };
}
