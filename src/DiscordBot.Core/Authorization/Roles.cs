namespace DiscordBot.Core.Authorization;

/// <summary>
/// Defines role names used throughout the application.
/// </summary>
public static class Roles
{
    /// <summary>System owner with full access to all features including user management.</summary>
    public const string SuperAdmin = "SuperAdmin";

    /// <summary>Guild administrator with full CRUD access to guilds and bot control.</summary>
    public const string Admin = "Admin";

    /// <summary>Limited admin with edit access but no delete permissions.</summary>
    public const string Moderator = "Moderator";

    /// <summary>Read-only access to dashboards and logs.</summary>
    public const string Viewer = "Viewer";

    /// <summary>All roles for authorization policies.</summary>
    public static readonly string[] All = { SuperAdmin, Admin, Moderator, Viewer };
}
