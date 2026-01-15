namespace DiscordBot.Core.Enums;

/// <summary>
/// Defines the types of user consent for data processing and privacy compliance.
/// </summary>
public enum ConsentType
{
    /// <summary>
    /// Consent for logging user messages and interactions.
    /// </summary>
    MessageLogging = 1,

    /// <summary>
    /// Consent for using the AI assistant feature.
    /// Required before users can interact with the bot's AI capabilities.
    /// </summary>
    AssistantUsage = 2
}
