// src/DiscordBot.Bot/ViewModels/Components/CommandHeaderViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for Commands page header component with dynamic subtitle based on active tab.
/// </summary>
public record CommandHeaderViewModel
{
    /// <summary>
    /// Main page title. Defaults to "Commands".
    /// </summary>
    public string Title { get; init; } = "Commands";

    /// <summary>
    /// Optional subtitle text displayed below the title. Dynamic based on active tab.
    /// </summary>
    public string Subtitle { get; init; } = string.Empty;

    /// <summary>
    /// Currently active tab identifier (command-list, execution-logs, analytics).
    /// </summary>
    public string ActiveTab { get; init; } = "command-list";
}
