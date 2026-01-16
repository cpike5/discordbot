using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for scheduled message operations and execution management.
/// </summary>
public interface IScheduledMessageService
{
    /// <summary>
    /// Gets a scheduled message by ID.
    /// </summary>
    /// <param name="id">The unique identifier of the scheduled message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The scheduled message data, or null if not found.</returns>
    Task<ScheduledMessageDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets scheduled messages for a specific guild with pagination.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the paginated scheduled messages and the total count.</returns>
    Task<(IEnumerable<ScheduledMessageDto> Items, int TotalCount)> GetByGuildIdAsync(
        ulong guildId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new scheduled message.
    /// </summary>
    /// <param name="dto">The creation request containing the scheduled message data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created scheduled message data.</returns>
    Task<ScheduledMessageDto> CreateAsync(ScheduledMessageCreateDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing scheduled message.
    /// </summary>
    /// <param name="id">The unique identifier of the scheduled message to update.</param>
    /// <param name="dto">The update request containing the fields to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated scheduled message data, or null if not found.</returns>
    Task<ScheduledMessageDto?> UpdateAsync(Guid id, ScheduledMessageUpdateDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a scheduled message.
    /// </summary>
    /// <param name="id">The unique identifier of the scheduled message to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the message was deleted, false if not found.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the next execution time based on frequency and optional base time.
    /// </summary>
    /// <param name="frequency">The schedule frequency.</param>
    /// <param name="cronExpression">The cron expression (required when frequency is Custom).</param>
    /// <param name="baseTime">The base time to calculate from. Defaults to current UTC time if not specified.</param>
    /// <returns>The calculated next execution time, or null if calculation fails.</returns>
    Task<DateTime?> CalculateNextExecutionAsync(
        ScheduleFrequency frequency,
        string? cronExpression,
        DateTime? baseTime = null);

    /// <summary>
    /// Executes a scheduled message immediately by sending it to Discord and updating its state.
    /// Updates LastExecutedAt and NextExecutionAt. Disables the message if frequency is Once.
    /// </summary>
    /// <param name="id">The unique identifier of the scheduled message to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if execution was successful, false if the message was not found or execution failed.</returns>
    Task<bool> ExecuteScheduledMessageAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a scheduled message immediately by sending it to Discord and updating its state.
    /// Updates LastExecutedAt and NextExecutionAt. Disables the message if frequency is Once.
    /// This overload accepts a pre-loaded entity to avoid redundant database queries.
    /// </summary>
    /// <param name="message">The scheduled message entity to execute (must be a tracked entity for updates).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if execution was successful, false if execution failed.</returns>
    Task<bool> ExecuteScheduledMessageAsync(ScheduledMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a cron expression for correctness.
    /// </summary>
    /// <param name="cronExpression">The cron expression to validate.</param>
    /// <returns>A tuple indicating whether the expression is valid and an error message if not.</returns>
    Task<(bool IsValid, string? ErrorMessage)> ValidateCronExpressionAsync(string cronExpression);
}
