using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Models.FeatureRequests;

/// <summary>
/// Transient conversation state stored in <c>IInteractionStateService</c> for the duration
/// of the multi-step DM feature-request flow. Keyed by the correlation ID returned from
/// <c>CreateState&lt;T&gt;()</c>.
/// </summary>
public class FeatureRequestConversationState
{
    public ConversationStage Stage { get; set; }
    public ulong GuildId { get; set; }
    public string InitialDescription { get; set; } = string.Empty;
    public string? ProblemStatement { get; set; }
    public string? SuccessCriteria { get; set; }
    public string? Priority { get; set; }
}
