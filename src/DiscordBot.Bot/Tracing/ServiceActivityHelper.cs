using System.Diagnostics;

namespace DiscordBot.Bot.Tracing;

/// <summary>
/// Helper for executing service operations wrapped in a tracing activity.
/// Eliminates boilerplate try/catch/SetSuccess/RecordException patterns.
/// </summary>
public static class ServiceActivityHelper
{
    /// <summary>
    /// Executes an async operation within a service activity, automatically setting
    /// success or recording the exception on the activity.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="serviceName">The service name for tracing (e.g., "guild", "moderation").</param>
    /// <param name="operationName">The operation name for tracing (e.g., "get_by_id", "create").</param>
    /// <param name="action">The async delegate receiving the activity and returning the result.</param>
    /// <param name="guildId">Optional guild ID to tag on the activity.</param>
    /// <param name="userId">Optional user ID to tag on the activity.</param>
    /// <param name="entityId">Optional entity ID to tag on the activity.</param>
    /// <returns>The result of the action.</returns>
    public static async Task<T> ExecuteAsync<T>(
        string serviceName,
        string operationName,
        Func<Activity?, Task<T>> action,
        ulong? guildId = null,
        ulong? userId = null,
        string? entityId = null)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            serviceName,
            operationName,
            guildId: guildId,
            userId: userId,
            entityId: entityId);

        try
        {
            var result = await action(activity);
            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Executes a void async operation within a service activity, automatically setting
    /// success or recording the exception on the activity.
    /// </summary>
    /// <param name="serviceName">The service name for tracing (e.g., "guild", "moderation").</param>
    /// <param name="operationName">The operation name for tracing (e.g., "get_by_id", "create").</param>
    /// <param name="action">The async delegate receiving the activity.</param>
    /// <param name="guildId">Optional guild ID to tag on the activity.</param>
    /// <param name="userId">Optional user ID to tag on the activity.</param>
    /// <param name="entityId">Optional entity ID to tag on the activity.</param>
    public static async Task ExecuteAsync(
        string serviceName,
        string operationName,
        Func<Activity?, Task> action,
        ulong? guildId = null,
        ulong? userId = null,
        string? entityId = null)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            serviceName,
            operationName,
            guildId: guildId,
            userId: userId,
            entityId: entityId);

        try
        {
            await action(activity);
            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }
}
