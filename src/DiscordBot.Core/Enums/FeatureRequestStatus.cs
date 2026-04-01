namespace DiscordBot.Core.Enums;

/// <summary>
/// Represents the lifecycle status of a feature request submission.
/// </summary>
public enum FeatureRequestStatus
{
    Submitted,
    GeneratingDocs,
    DocsGenerated,
    DocGenFailed,
    Approved,
    Rejected
}
