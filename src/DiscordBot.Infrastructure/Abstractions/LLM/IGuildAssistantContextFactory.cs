namespace DiscordBot.Infrastructure.Abstractions.LLM;

/// <summary>
/// Builds the guild-scoped <see cref="IAssistantContext"/> for one question, bundling the
/// prompt template, tool registry, guild lookup, and metrics/interaction-log repositories so
/// <c>AssistantService</c> only needs this one dependency to hand the pipeline a context.
/// </summary>
public interface IGuildAssistantContextFactory
{
    /// <summary>
    /// Builds the context, resolving the guild assistant's effective model slug (see
    /// <see cref="ILlmModelResolver"/>) as part of the build - hence async.
    /// </summary>
    /// <param name="callerCanMutate">
    /// Whether this caller may use tools that create or change data, decided from their Discord
    /// permissions by the host. Lands on <c>ToolContext.CanMutate</c>; defaults to false so a
    /// caller that was never assessed gets read-only access rather than the other way round.
    /// </param>
    Task<IAssistantContext> CreateAsync(
        ulong guildId,
        ulong channelId,
        ulong userId,
        ulong messageId,
        int rateLimit,
        string question,
        bool callerCanMutate = false,
        CancellationToken cancellationToken = default);
}
