namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Builds the DM-scoped <see cref="IAssistantContext"/> for one message, bundling the DM tool
/// providers, prompt template, conversation history, and metrics/interaction-log repositories
/// so <c>DmAssistantService</c> only needs this one dependency to hand the pipeline a context.
/// </summary>
public interface IDmAssistantContextFactory
{
    Task<IAssistantContext> CreateAsync(ulong userId, ulong? activeGuildId, CancellationToken cancellationToken);

    /// <summary>
    /// Logs a non-owner placeholder interaction. Lighter weight than <see cref="CreateAsync"/> —
    /// non-owner messages never reach the agent, so no tool registry or conversation history is built.
    /// </summary>
    Task LogPlaceholderInteractionAsync(
        ulong userId, string message, string placeholderResponse, int latencyMs, CancellationToken cancellationToken);
}
