namespace DiscordBot.Core.Enums;

/// <summary>
/// The three places the bot resolves an OpenRouter model slug for: the guild assistant, the DM
/// (owner) assistant, and the feature-request requirements-gathering conversation. Adding a mode
/// means adding a member here and a matching key in <see cref="LlmModeSettings.KeyFor"/> plus a
/// <c>SettingCategory.AiModels</c> entry in <c>SettingDefinitions</c>.
/// </summary>
public enum LlmMode
{
    GuildAssistant,
    DmAssistant,
    FeatureRequests
}

/// <summary>
/// Maps each <see cref="LlmMode"/> to the setting key that names its default model - the same key
/// the mode's options class historically read the slug from, reused as a <c>SettingDefinitions</c>
/// entry so a DB row shadows the configured value with no new plumbing.
/// </summary>
public static class LlmModeSettings
{
    /// <summary>Setting key for the guild assistant's default model.</summary>
    public const string GuildAssistantKey = "Assistant:Sampling:Model";

    /// <summary>Setting key for the DM assistant's default model.</summary>
    public const string DmAssistantKey = "DmAssistant:Model";

    /// <summary>Setting key for the feature-request requirements-gathering model.</summary>
    public const string FeatureRequestsKey = "FeatureRequests:RequirementsGatheringModel";

    /// <summary>Gets the setting key backing <paramref name="mode"/>'s default model.</summary>
    public static string KeyFor(LlmMode mode) => mode switch
    {
        LlmMode.GuildAssistant => GuildAssistantKey,
        LlmMode.DmAssistant => DmAssistantKey,
        LlmMode.FeatureRequests => FeatureRequestsKey,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown LLM mode.")
    };

    /// <summary>Human-readable label for the mode, for admin-facing UI and error messages.</summary>
    public static string LabelFor(LlmMode mode) => mode switch
    {
        LlmMode.GuildAssistant => "Guild Assistant",
        LlmMode.DmAssistant => "DM Assistant",
        LlmMode.FeatureRequests => "Feature Requests",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown LLM mode.")
    };

    /// <summary>All modes, in the stable order the admin UI and defaults endpoint display them.</summary>
    public static readonly IReadOnlyList<LlmMode> All = new[]
    {
        LlmMode.GuildAssistant,
        LlmMode.DmAssistant,
        LlmMode.FeatureRequests
    };
}
