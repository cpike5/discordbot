namespace DiscordBot.Core.Models.FeatureRequests;

/// <summary>
/// Structured requirements gathered from the multi-step DM conversation flow.
/// Serialized to JSON and stored in <c>FeatureRequest.GatheredRequirements</c>.
/// </summary>
public class GatheredRequirements
{
    public string ProblemStatement { get; set; } = string.Empty;
    public string SuccessCriteria { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
}
