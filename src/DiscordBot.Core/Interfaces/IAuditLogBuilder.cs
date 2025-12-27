using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Fluent builder interface for constructing audit log entries.
/// Provides a chainable API for creating audit logs with a more readable syntax.
/// </summary>
/// <example>
/// <code>
/// await auditLogService.CreateBuilder()
///     .ForCategory(AuditLogCategory.User)
///     .WithAction(AuditLogAction.Created)
///     .ByUser(userId)
///     .OnTarget("ScheduledMessage", messageId)
///     .InGuild(guildId)
///     .WithDetails(new { title = "Test Message", channelId = 123456 })
///     .LogAsync();
/// </code>
/// </example>
public interface IAuditLogBuilder
{
    /// <summary>
    /// Sets the category of the audit log entry.
    /// </summary>
    /// <param name="category">The audit log category.</param>
    /// <returns>The builder instance for method chaining.</returns>
    IAuditLogBuilder ForCategory(AuditLogCategory category);

    /// <summary>
    /// Sets the action that was performed.
    /// </summary>
    /// <param name="action">The audit log action.</param>
    /// <returns>The builder instance for method chaining.</returns>
    IAuditLogBuilder WithAction(AuditLogAction action);

    /// <summary>
    /// Sets the actor as a user with the specified user ID.
    /// </summary>
    /// <param name="userId">The user ID who performed the action.</param>
    /// <returns>The builder instance for method chaining.</returns>
    IAuditLogBuilder ByUser(string userId);

    /// <summary>
    /// Sets the actor as the system (automated process).
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    IAuditLogBuilder BySystem();

    /// <summary>
    /// Sets the actor as the Discord bot.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    IAuditLogBuilder ByBot();

    /// <summary>
    /// Sets the target entity that was affected by this action.
    /// </summary>
    /// <param name="targetType">The type of the target entity (e.g., "User", "ScheduledMessage").</param>
    /// <param name="targetId">The identifier of the target entity.</param>
    /// <returns>The builder instance for method chaining.</returns>
    IAuditLogBuilder OnTarget(string targetType, string targetId);

    /// <summary>
    /// Sets the Discord guild associated with this action.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <returns>The builder instance for method chaining.</returns>
    IAuditLogBuilder InGuild(ulong guildId);

    /// <summary>
    /// Sets additional contextual information as a dictionary.
    /// The dictionary will be serialized to JSON when stored.
    /// </summary>
    /// <param name="details">A dictionary containing action-specific details.</param>
    /// <returns>The builder instance for method chaining.</returns>
    IAuditLogBuilder WithDetails(Dictionary<string, object?> details);

    /// <summary>
    /// Sets additional contextual information from an anonymous object.
    /// The object will be serialized to JSON when stored.
    /// </summary>
    /// <param name="details">An object containing action-specific details.</param>
    /// <returns>The builder instance for method chaining.</returns>
    IAuditLogBuilder WithDetails(object details);

    /// <summary>
    /// Sets the IP address from which the action was performed.
    /// Primarily used for user actions through the web interface.
    /// </summary>
    /// <param name="ipAddress">The IP address.</param>
    /// <returns>The builder instance for method chaining.</returns>
    IAuditLogBuilder FromIpAddress(string ipAddress);

    /// <summary>
    /// Sets a correlation ID to group related audit log entries.
    /// Useful for tracing a series of actions that are part of the same operation.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <returns>The builder instance for method chaining.</returns>
    IAuditLogBuilder WithCorrelationId(string correlationId);

    /// <summary>
    /// Logs the audit entry asynchronously and waits for confirmation.
    /// Use this when you need to ensure the log is written before proceeding.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LogAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues the audit entry for background processing without waiting.
    /// This is a fire-and-forget operation optimized for high-performance scenarios.
    /// </summary>
    void Enqueue();
}
