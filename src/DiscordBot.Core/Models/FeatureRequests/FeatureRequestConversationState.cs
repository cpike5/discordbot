namespace DiscordBot.Core.Models.FeatureRequests;

/// <summary>
/// Transient conversation state stored in <c>IInteractionStateService</c> for the duration
/// of the AI-powered DM feature-request flow. Keyed by the correlation ID returned from
/// <c>CreateState&lt;T&gt;()</c>.
/// </summary>
public class FeatureRequestConversationState
{
    public ulong GuildId { get; set; }
    public string InitialDescription { get; set; } = string.Empty;

    /// <summary>
    /// Full conversation history for multi-turn requirements gathering, in engine-agnostic form.
    /// </summary>
    public List<FeatureRequestTurn> ConversationHistory { get; set; } = new();

    /// <summary>
    /// Whether the feature request has been submitted (agent called submit tool).
    /// </summary>
    public bool IsComplete { get; set; }
}
