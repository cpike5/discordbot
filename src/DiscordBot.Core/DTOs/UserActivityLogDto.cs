using DiscordBot.Core.Entities;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for user activity log entries.
/// </summary>
public class UserActivityLogDto
{
    public Guid Id { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorEmail { get; set; } = string.Empty;
    public string? TargetUserId { get; set; }
    public string? TargetEmail { get; set; }
    public UserActivityAction Action { get; set; }
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
    public string? IpAddress { get; set; }
}
