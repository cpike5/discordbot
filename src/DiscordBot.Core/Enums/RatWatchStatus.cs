using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Core.Enums;

/// <summary>
/// Status states for a Rat Watch accountability tracker.
/// </summary>
public enum RatWatchStatus
{
    /// <summary>
    /// Waiting for scheduled time.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Accused checked in before scheduled time.
    /// </summary>
    [Display(Name = "Cleared early")]
    ClearedEarly = 1,

    /// <summary>
    /// Voting in progress.
    /// </summary>
    Voting = 2,

    /// <summary>
    /// Voting complete - guilty verdict.
    /// </summary>
    Guilty = 3,

    /// <summary>
    /// Voting complete - not guilty verdict.
    /// </summary>
    [Display(Name = "Not guilty")]
    NotGuilty = 4,

    /// <summary>
    /// Bot was offline, watch expired without voting.
    /// </summary>
    Expired = 5,

    /// <summary>
    /// Admin cancelled the watch.
    /// </summary>
    Cancelled = 6
}
