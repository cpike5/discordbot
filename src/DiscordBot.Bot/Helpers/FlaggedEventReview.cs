using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// A review step a moderator can take on a flagged event from the list.
/// </summary>
public enum FlaggedEventReviewAction
{
    /// <summary>Mark as not needing action.</summary>
    Dismiss,

    /// <summary>Mark as seen, still open.</summary>
    Acknowledge
}

/// <summary>
/// Which review steps make sense for which status. An acknowledged event is still open, so it
/// can be dismissed or have its outcome recorded; a dismissed or actioned one is finished.
/// </summary>
public static class FlaggedEventReviewRules
{
    /// <summary>
    /// What a reviewer without a linked Discord account is told. Reviews record who made them, and
    /// that is a Discord user ID, so there is nothing honest to record for such an account.
    /// </summary>
    public const string LinkDiscordMessage = "Link your Discord account to review events.";

    /// <summary>Whether <paramref name="action"/> can be applied to an event in <paramref name="status"/>.</summary>
    public static bool CanApply(FlaggedEventReviewAction action, FlaggedEventStatus status) => action switch
    {
        FlaggedEventReviewAction.Dismiss => status is FlaggedEventStatus.Pending or FlaggedEventStatus.Acknowledged,
        FlaggedEventReviewAction.Acknowledge => status == FlaggedEventStatus.Pending,
        _ => false
    };

    /// <summary>Whether an outcome can be recorded for an event in <paramref name="status"/>.</summary>
    public static bool CanRecordOutcome(FlaggedEventStatus status) =>
        status is FlaggedEventStatus.Pending or FlaggedEventStatus.Acknowledged;
}

/// <summary>
/// What happened across a batch of review actions, and the one-line message that says so.
/// </summary>
public sealed class FlaggedEventBatchOutcome
{
    /// <summary>How the batch is reported.</summary>
    public enum ToastKind
    {
        /// <summary>Everything changed.</summary>
        Success,

        /// <summary>Some changed, some did not.</summary>
        Warning,

        /// <summary>Nothing changed.</summary>
        Error
    }

    /// <summary>Events that were updated.</summary>
    public int Done { get; set; }

    /// <summary>Events that were already past this step.</summary>
    public int Skipped { get; set; }

    /// <summary>Events that no longer exist.</summary>
    public int NotFound { get; set; }

    /// <summary>Events where the update threw.</summary>
    public int Failed { get; set; }

    /// <summary>
    /// The toast to show: all done is a success, a mix is a warning that names what was left
    /// behind and why, and nothing done is an error.
    /// </summary>
    public (ToastKind Kind, string Message) Describe(FlaggedEventReviewAction action, int total)
    {
        var past = action == FlaggedEventReviewAction.Dismiss ? "dismissed" : "acknowledged";
        var problems = Skipped + NotFound + Failed;

        if (Done > 0 && problems == 0)
        {
            return (ToastKind.Success, total == 1
                ? $"Event {past}."
                : $"{DisplayFormat.Plural(Done, "event")} {past}.");
        }

        var reasons = new List<string>();
        if (Skipped > 0) reasons.Add($"{Skipped} already reviewed");
        if (NotFound > 0) reasons.Add($"{NotFound} no longer exist{(NotFound == 1 ? "s" : "")}");
        if (Failed > 0) reasons.Add($"{Failed} could not be updated");
        var because = string.Join(", ", reasons);

        if (Done > 0)
        {
            return (ToastKind.Warning,
                $"{char.ToUpperInvariant(past[0])}{past[1..]} {Done} of {DisplayFormat.Plural(total, "event")}. {because}.");
        }

        if (because.Length == 0) return (ToastKind.Error, "There was nothing to update.");

        return (ToastKind.Error, $"No events were {past}. {char.ToUpperInvariant(because[0])}{because[1..]}.");
    }
}
