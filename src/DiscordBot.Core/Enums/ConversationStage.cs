namespace DiscordBot.Core.Enums;

/// <summary>
/// Tracks which stage of the multi-step feature request conversation a user is currently in.
/// </summary>
public enum ConversationStage
{
    AwaitingProblem,
    AwaitingSuccessCriteria,
    AwaitingPriority,
    AwaitingConfirmation
}
