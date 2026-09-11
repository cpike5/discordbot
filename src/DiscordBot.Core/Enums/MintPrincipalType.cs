namespace DiscordBot.Core.Enums;

/// <summary>
/// The kind of principal a mint authority grant names.
/// </summary>
public enum MintPrincipalType
{
    /// <summary>A single Discord user.</summary>
    User = 0,

    /// <summary>Everyone holding a Discord role.</summary>
    Role = 1,

    /// <summary>The bot itself, so background jobs can mint without a fake user.</summary>
    System = 2
}
