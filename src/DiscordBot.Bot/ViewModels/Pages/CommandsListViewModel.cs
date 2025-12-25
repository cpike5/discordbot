using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the Commands page, displaying all registered command modules.
/// </summary>
public record CommandsListViewModel
{
    /// <summary>
    /// Gets the collection of command module view models.
    /// </summary>
    public IReadOnlyList<CommandModuleViewModel> Modules { get; init; } = Array.Empty<CommandModuleViewModel>();

    /// <summary>
    /// Gets the total number of modules.
    /// </summary>
    public int ModuleCount => Modules.Count;

    /// <summary>
    /// Gets the total number of commands across all modules.
    /// </summary>
    public int TotalCommandCount => Modules.Sum(m => m.CommandCount);

    /// <summary>
    /// Gets whether there are any modules to display.
    /// </summary>
    public bool HasModules => Modules.Count > 0;

    /// <summary>
    /// Creates a <see cref="CommandsListViewModel"/> from a collection of <see cref="CommandModuleDto"/>.
    /// </summary>
    /// <param name="dtos">The command module DTOs to map from.</param>
    /// <returns>A new <see cref="CommandsListViewModel"/> instance.</returns>
    public static CommandsListViewModel FromDtos(IEnumerable<CommandModuleDto> dtos)
    {
        return new CommandsListViewModel
        {
            Modules = dtos.Select(CommandModuleViewModel.FromDto).ToList()
        };
    }
}
