namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for requesting a welcome message preview.
/// </summary>
public class WelcomePreviewRequestDto
{
    /// <summary>
    /// Gets or sets the Discord user snowflake ID to use for the preview.
    /// This user's information will be used to populate template variables.
    /// </summary>
    public ulong PreviewUserId { get; set; }
}
