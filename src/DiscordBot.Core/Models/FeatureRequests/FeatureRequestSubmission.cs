namespace DiscordBot.Core.Models.FeatureRequests;

/// <summary>
/// Input model passed to <c>IFeatureRequestService.SubmitAsync</c> to create a new feature request.
/// </summary>
public class FeatureRequestSubmission
{
    public ulong GuildId { get; set; }
    public ulong SubmittedByUserId { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// JSON-serialized <see cref="GatheredRequirements"/> produced by the conversation flow.
    /// Null for direct (single-message) submissions.
    /// </summary>
    public string? GatheredRequirementsJson { get; set; }

    public string? ConsolidatedSummary { get; set; }
}
