namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for individual command information.
/// </summary>
public class CommandInfoDto
{
    /// <summary>
    /// Gets or sets the command name.
    /// </summary>
    /// <example>ping, help</example>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full command name including group prefix.
    /// </summary>
    /// <example>consent grant, user info</example>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the command description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of command parameters.
    /// </summary>
    public List<CommandParameterDto> Parameters { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of preconditions applied to the command.
    /// </summary>
    public List<PreconditionDto> Preconditions { get; set; } = new();

    /// <summary>
    /// Gets or sets the name of the module containing this command.
    /// </summary>
    public string ModuleName { get; set; } = string.Empty;
}
