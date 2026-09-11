namespace DiscordBot.Core.Enums;

/// <summary>
/// Which assistant surfaces a tool is registered on. A tool can appear on more than one - the
/// documentation tools are advertised to both the guild assistant and the DM assistant - so this
/// is a flags enum rather than a single choice.
/// </summary>
[Flags]
public enum ToolScopes
{
    /// <summary>Not advertised anywhere.</summary>
    None = 0,

    /// <summary>Advertised to the guild assistant, and therefore governed by the per-guild allow-list.</summary>
    Guild = 1,

    /// <summary>Advertised to the owner-only DM assistant.</summary>
    Dm = 2,

    /// <summary>Used by the feature-request conversation, which builds its own single-provider registry.</summary>
    FeatureRequests = 4
}
