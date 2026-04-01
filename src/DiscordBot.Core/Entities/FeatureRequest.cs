using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a feature request submitted by a Discord guild member.
/// </summary>
public class FeatureRequest
{
    /// <summary>
    /// Unique identifier for this feature request.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Discord guild snowflake ID where this feature request was submitted.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Discord user snowflake ID of the member who submitted this request.
    /// </summary>
    public ulong SubmittedByUserId { get; set; }

    /// <summary>
    /// Short title derived from the description or consolidated summary.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The original description text provided by the submitter.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// JSON-serialized gathered requirements from the multi-step conversation flow.
    /// Null for direct submissions that bypassed the conversation flow.
    /// </summary>
    public string? GatheredRequirements { get; set; }

    /// <summary>
    /// Consolidated summary produced from gathered requirements.
    /// </summary>
    public string? ConsolidatedSummary { get; set; }

    /// <summary>
    /// Current lifecycle status of this feature request.
    /// </summary>
    public FeatureRequestStatus Status { get; set; }

    /// <summary>
    /// Discord user snowflake ID of the admin who reviewed this request.
    /// Null if not yet reviewed.
    /// </summary>
    public ulong? ReviewedByUserId { get; set; }

    /// <summary>
    /// Timestamp when an admin reviewed this request (UTC).
    /// Null if not yet reviewed.
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Admin notes recorded at review time.
    /// </summary>
    public string? ReviewNotes { get; set; }

    /// <summary>
    /// Git branch name created by the doc generation process.
    /// </summary>
    public string? DocBranchName { get; set; }

    /// <summary>
    /// Path to the generated documentation directory within the repository.
    /// </summary>
    public string? DocPath { get; set; }

    /// <summary>
    /// Error message captured if doc generation failed.
    /// </summary>
    public string? DocGenError { get; set; }

    /// <summary>
    /// Timestamp when this feature request was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when this feature request was last updated (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property for the guild this request belongs to.
    /// </summary>
    public Guild? Guild { get; set; }
}
