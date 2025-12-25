namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for command parameter information.
/// </summary>
public class CommandParameterDto
{
    /// <summary>
    /// Gets or sets the name of the parameter.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the parameter.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the friendly type name of the parameter.
    /// </summary>
    /// <example>String, Integer, User, Channel</example>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the parameter is required.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets the default value for the parameter, if any.
    /// </summary>
    /// <remarks>
    /// The default value is serialized as a string for display purposes.
    /// </remarks>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets the available choices for the parameter.
    /// </summary>
    /// <remarks>
    /// Populated for enum parameters or parameters with explicit choice attributes.
    /// </remarks>
    public List<string>? Choices { get; set; }
}
