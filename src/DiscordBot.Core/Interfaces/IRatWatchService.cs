namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing Rat Watch accountability trackers.
/// Handles watch creation, voting, execution, and statistics.
/// Composed of <see cref="IRatWatchReader"/> (queries), <see cref="IRatWatchLifecycle"/> (user actions),
/// and <see cref="IRatWatchExecution"/> (background service execution and settings).
/// </summary>
public interface IRatWatchService : IRatWatchReader, IRatWatchLifecycle, IRatWatchExecution
{
}
