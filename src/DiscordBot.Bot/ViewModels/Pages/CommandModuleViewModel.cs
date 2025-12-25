using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for a command module, containing commands and module metadata.
/// </summary>
public record CommandModuleViewModel
{
    /// <summary>
    /// Gets the module class name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the formatted display name for the module.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the module description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets whether the module uses a slash command group.
    /// </summary>
    public bool IsSlashGroup { get; init; }

    /// <summary>
    /// Gets the group prefix for grouped commands.
    /// </summary>
    public string? GroupName { get; init; }

    /// <summary>
    /// Gets the collection of command view models in this module.
    /// </summary>
    public IReadOnlyList<CommandItemViewModel> Commands { get; init; } = Array.Empty<CommandItemViewModel>();

    /// <summary>
    /// Gets the total number of commands in this module.
    /// </summary>
    public int CommandCount => Commands.Count;

    /// <summary>
    /// Gets a unique identifier for this module (used for collapse/expand).
    /// </summary>
    public string ModuleId => Name.ToLowerInvariant().Replace(" ", "-");

    /// <summary>
    /// Creates a <see cref="CommandModuleViewModel"/> from a <see cref="CommandModuleDto"/>.
    /// </summary>
    /// <param name="dto">The command module DTO to map from.</param>
    /// <returns>A new <see cref="CommandModuleViewModel"/> instance.</returns>
    public static CommandModuleViewModel FromDto(CommandModuleDto dto)
    {
        return new CommandModuleViewModel
        {
            Name = dto.Name,
            DisplayName = dto.DisplayName,
            Description = dto.Description,
            IsSlashGroup = dto.IsSlashGroup,
            GroupName = dto.GroupName,
            Commands = dto.Commands.Select(CommandItemViewModel.FromDto).ToList()
        };
    }
}
