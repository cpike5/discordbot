namespace DiscordBot.Core.Entities;

/// <summary>
/// Defines role-based access restrictions for specific soundboard commands.
/// </summary>
public class CommandRoleRestriction
{
    /// <summary>
    /// Primary key for EF Core tracking.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Discord guild snowflake ID where this restriction applies.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Name of the command this restriction applies to (e.g., "play", "join", "leave").
    /// </summary>
    public string CommandName { get; set; } = string.Empty;

    /// <summary>
    /// List of Discord role snowflake IDs allowed to use this command.
    /// If empty, the command is available to everyone.
    /// </summary>
    public List<ulong> AllowedRoleIds { get; set; } = new();

    /// <summary>
    /// Navigation property for the guild audio settings this restriction belongs to.
    /// </summary>
    public GuildAudioSettings? GuildAudioSettings { get; set; }
}
