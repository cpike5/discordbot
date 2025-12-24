namespace DiscordBot.Core.Enums;

/// <summary>
/// Defines the types of user consent for data processing and privacy compliance.
/// </summary>
public enum ConsentType
{
    /// <summary>
    /// Consent for logging user messages and interactions.
    /// </summary>
    MessageLogging = 1

    // Future consent types can be added here:
    // Analytics = 2,
    // LLMInteraction = 3,
    // etc.
}
