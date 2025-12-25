namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for command module information.
/// </summary>
public class CommandModuleDto
{
    /// <summary>
    /// Gets or sets the module class name.
    /// </summary>
    /// <example>GeneralModule, ConsentModule</example>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the formatted display name for the module.
    /// </summary>
    /// <example>General, Consent</example>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the module description from the [Group] attribute.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether the module uses a slash command group.
    /// </summary>
    public bool IsSlashGroup { get; set; }

    /// <summary>
    /// Gets or sets the group prefix for grouped commands.
    /// </summary>
    /// <example>consent, user</example>
    public string? GroupName { get; set; }

    /// <summary>
    /// Gets or sets the list of commands in this module.
    /// </summary>
    public List<CommandInfoDto> Commands { get; set; } = new();

    /// <summary>
    /// Gets the total number of commands in this module.
    /// </summary>
    public int CommandCount => Commands.Count;
}
