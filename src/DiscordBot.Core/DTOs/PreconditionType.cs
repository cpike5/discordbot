namespace DiscordBot.Core.DTOs;

/// <summary>
/// Enumeration of precondition types for command validation.
/// </summary>
public enum PreconditionType
{
    /// <summary>
    /// Requires admin role (RequireAdminAttribute).
    /// </summary>
    Admin,

    /// <summary>
    /// Requires owner role (RequireOwnerAttribute).
    /// </summary>
    Owner,

    /// <summary>
    /// Rate limiting precondition (RateLimitAttribute).
    /// </summary>
    RateLimit,

    /// <summary>
    /// Requires bot to have specific permissions (RequireBotPermission).
    /// </summary>
    BotPermission,

    /// <summary>
    /// Requires user to have specific permissions (RequireUserPermission).
    /// </summary>
    UserPermission,

    /// <summary>
    /// Requires specific context (RequireContext).
    /// </summary>
    Context,

    /// <summary>
    /// Custom precondition not covered by standard types.
    /// </summary>
    Custom
}
