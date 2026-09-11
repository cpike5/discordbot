namespace DiscordBot.Core.Models.FeatureRequests;

/// <summary>
/// Who produced a turn in the feature-request conversation.
/// </summary>
public enum FeatureRequestTurnRole
{
    /// <summary>The member describing the request.</summary>
    User = 0,

    /// <summary>The assistant gathering requirements.</summary>
    Assistant = 1
}

/// <summary>
/// A single turn of the AI-powered feature-request conversation.
/// </summary>
/// <remarks>
/// Deliberately independent of the agent engine's message contract so that
/// <see cref="FeatureRequestConversationState"/> — and with it <c>DiscordBot.Core</c> — stays
/// free of any dependency on the agent engine. Callers map a turn onto the engine's message
/// type at the point they build an agent run.
/// </remarks>
public class FeatureRequestTurn
{
    /// <summary>Who produced this turn.</summary>
    public FeatureRequestTurnRole Role { get; set; }

    /// <summary>The turn's text.</summary>
    public string Content { get; set; } = string.Empty;
}
