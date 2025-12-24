using System.Diagnostics;
using OpenTelemetry.Trace;

namespace DiscordBot.Infrastructure.Tracing;

/// <summary>
/// Provides the ActivitySource for infrastructure-level tracing (repositories, database operations).
/// </summary>
public static class InfrastructureActivitySource
{
    /// <summary>
    /// The name of the activity source for infrastructure operations.
    /// </summary>
    public const string SourceName = "DiscordBot.Infrastructure";

    /// <summary>
    /// The version of the activity source.
    /// </summary>
    public static readonly string Version = typeof(InfrastructureActivitySource).Assembly
        .GetName().Version?.ToString() ?? "1.0.0";

    /// <summary>
    /// The singleton ActivitySource instance for infrastructure tracing.
    /// </summary>
    public static readonly ActivitySource Source = new(SourceName, Version);

    /// <summary>
    /// Attribute keys for database operations.
    /// </summary>
    public static class Attributes
    {
        public const string DbSystem = "db.system";
        public const string DbOperation = "db.operation";
        public const string DbEntityType = "db.entity.type";
        public const string DbEntityId = "db.entity.id";
        public const string DbDurationMs = "db.duration.ms";
    }

    /// <summary>
    /// Starts an activity for a repository operation.
    /// </summary>
    /// <param name="operationName">The repository method name (e.g., "GetByIdAsync").</param>
    /// <param name="entityType">The entity type name.</param>
    /// <param name="dbOperation">The database operation (SELECT, INSERT, UPDATE, DELETE).</param>
    /// <param name="entityId">Optional entity ID for the operation.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartRepositoryActivity(
        string operationName,
        string entityType,
        string dbOperation,
        string? entityId = null)
    {
        var activity = Source.StartActivity(
            name: $"db.{dbOperation.ToLowerInvariant()} {entityType}",
            kind: ActivityKind.Client);

        if (activity is null)
            return null;

        activity.SetTag(Attributes.DbOperation, dbOperation);
        activity.SetTag(Attributes.DbEntityType, entityType);

        if (!string.IsNullOrEmpty(entityId))
        {
            activity.SetTag(Attributes.DbEntityId, entityId);
        }

        // Inherit correlation ID from parent activity baggage
        var correlationId = Activity.Current?.GetBaggageItem("correlation-id");
        if (!string.IsNullOrEmpty(correlationId))
        {
            activity.SetTag("correlation.id", correlationId);
        }

        return activity;
    }

    /// <summary>
    /// Records the duration and marks the activity as complete.
    /// </summary>
    /// <param name="activity">The activity to complete.</param>
    /// <param name="durationMs">The operation duration in milliseconds.</param>
    public static void CompleteActivity(Activity? activity, double durationMs)
    {
        if (activity is null)
            return;

        activity.SetTag(Attributes.DbDurationMs, durationMs);
        activity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Records an exception and marks the activity as failed.
    /// </summary>
    /// <param name="activity">The activity to record the error on.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="durationMs">The operation duration in milliseconds.</param>
    public static void RecordException(Activity? activity, Exception exception, double durationMs)
    {
        if (activity is null)
            return;

        activity.SetTag(Attributes.DbDurationMs, durationMs);
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.AddException(exception);
    }
}
