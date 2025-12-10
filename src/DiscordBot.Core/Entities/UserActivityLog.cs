namespace DiscordBot.Core.Entities;

/// <summary>
/// Audit log entry for user management actions.
/// </summary>
public class UserActivityLog
{
    public Guid Id { get; set; }

    /// <summary>
    /// The user who performed the action.
    /// </summary>
    public string ActorUserId { get; set; } = string.Empty;
    public ApplicationUser Actor { get; set; } = null!;

    /// <summary>
    /// The user affected by the action (null for non-user-specific actions).
    /// </summary>
    public string? TargetUserId { get; set; }
    public ApplicationUser? Target { get; set; }

    /// <summary>
    /// The type of action performed.
    /// </summary>
    public UserActivityAction Action { get; set; }

    /// <summary>
    /// Additional details about the action (JSON or descriptive text).
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// When the action occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// IP address of the actor (for security auditing).
    /// </summary>
    public string? IpAddress { get; set; }
}

/// <summary>
/// Types of user management actions that are logged.
/// </summary>
public enum UserActivityAction
{
    UserCreated,
    UserUpdated,
    UserDeleted,
    UserEnabled,
    UserDisabled,
    RoleAssigned,
    RoleRemoved,
    PasswordReset,
    DiscordLinked,
    DiscordUnlinked,
    AccountLocked,
    AccountUnlocked,
    LoginSuccess,
    LoginFailed
}
