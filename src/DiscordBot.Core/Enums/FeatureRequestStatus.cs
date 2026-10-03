using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Core.Enums;

/// <summary>
/// Represents the lifecycle status of a feature request submission.
/// </summary>
public enum FeatureRequestStatus
{
    Submitted,
    [Display(Name = "Writing documentation")]
    GeneratingDocs,
    [Display(Name = "Documentation ready")]
    DocsGenerated,
    [Display(Name = "Documentation failed")]
    DocGenFailed,
    Approved,
    Rejected
}
