namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for command precondition information.
/// </summary>
public class PreconditionDto
{
    /// <summary>
    /// Gets or sets the name of the precondition.
    /// </summary>
    /// <example>RequireAdmin, RateLimit</example>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of the precondition.
    /// </summary>
    public PreconditionType Type { get; set; }

    /// <summary>
    /// Gets or sets the configuration details for the precondition.
    /// </summary>
    /// <remarks>
    /// Contains human-readable configuration information, such as "3 per hour" for rate limits
    /// or permission names for permission-based preconditions.
    /// </remarks>
    public string? Configuration { get; set; }
}
