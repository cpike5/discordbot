using System.Diagnostics;
using OpenTelemetry.Trace;

namespace DiscordBot.Bot.Tracing;

/// <summary>
/// Provides the ActivitySource for Discord bot tracing.
/// Follows the singleton pattern to ensure consistent source naming.
/// </summary>
public static class BotActivitySource
{
    /// <summary>
    /// The name of the activity source for Discord bot operations.
    /// </summary>
    public const string SourceName = "DiscordBot.Bot";

    /// <summary>
    /// The version of the activity source.
    /// </summary>
    public static readonly string Version = typeof(BotActivitySource).Assembly
        .GetName().Version?.ToString() ?? "1.0.0";

    /// <summary>
    /// The singleton ActivitySource instance for bot tracing.
    /// </summary>
    public static readonly ActivitySource Source = new(SourceName, Version);

    /// <summary>
    /// Starts an activity for a Discord slash command execution.
    /// </summary>
    /// <param name="commandName">The name of the command being executed.</param>
    /// <param name="guildId">The guild ID where the command was invoked.</param>
    /// <param name="userId">The user ID who invoked the command.</param>
    /// <param name="interactionId">The Discord interaction ID.</param>
    /// <param name="correlationId">The application correlation ID.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartCommandActivity(
        string commandName,
        ulong? guildId,
        ulong userId,
        ulong interactionId,
        string correlationId)
    {
        var activity = Source.StartActivity(
            name: $"discord.command {commandName}",
            kind: ActivityKind.Server);

        if (activity is null)
            return null;

        activity.SetTag(TracingConstants.Attributes.CommandName, commandName);
        activity.SetTag(TracingConstants.Attributes.GuildId, guildId?.ToString() ?? "dm");
        activity.SetTag(TracingConstants.Attributes.UserId, userId.ToString());
        activity.SetTag(TracingConstants.Attributes.InteractionId, interactionId.ToString());
        activity.SetTag(TracingConstants.Attributes.CorrelationId, correlationId);

        // Add correlation ID as baggage for downstream propagation
        activity.AddBaggage(TracingConstants.Baggage.CorrelationId, correlationId);

        return activity;
    }

    /// <summary>
    /// Starts an activity for a Discord component interaction (button, select, modal).
    /// </summary>
    /// <param name="componentType">The type of component (button, select_menu, modal).</param>
    /// <param name="customId">The custom ID of the component.</param>
    /// <param name="guildId">The guild ID where the interaction occurred.</param>
    /// <param name="userId">The user ID who triggered the interaction.</param>
    /// <param name="interactionId">The Discord interaction ID.</param>
    /// <param name="correlationId">The application correlation ID.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartComponentActivity(
        string componentType,
        string customId,
        ulong? guildId,
        ulong userId,
        ulong interactionId,
        string correlationId)
    {
        var activity = Source.StartActivity(
            name: $"discord.component {componentType}",
            kind: ActivityKind.Server);

        if (activity is null)
            return null;

        activity.SetTag(TracingConstants.Attributes.ComponentType, componentType);
        activity.SetTag(TracingConstants.Attributes.ComponentId, SanitizeCustomId(customId));
        activity.SetTag(TracingConstants.Attributes.GuildId, guildId?.ToString() ?? "dm");
        activity.SetTag(TracingConstants.Attributes.UserId, userId.ToString());
        activity.SetTag(TracingConstants.Attributes.InteractionId, interactionId.ToString());
        activity.SetTag(TracingConstants.Attributes.CorrelationId, correlationId);

        // Add correlation ID as baggage for downstream propagation
        activity.AddBaggage(TracingConstants.Baggage.CorrelationId, correlationId);

        return activity;
    }

    /// <summary>
    /// Records an error on the activity and sets error status.
    /// </summary>
    /// <param name="activity">The activity to record the error on.</param>
    /// <param name="exception">The exception that occurred.</param>
    public static void RecordException(Activity? activity, Exception exception)
    {
        if (activity is null)
            return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.AddException(exception);
    }

    /// <summary>
    /// Marks the activity as successful.
    /// </summary>
    /// <param name="activity">The activity to mark as successful.</param>
    public static void SetSuccess(Activity? activity)
    {
        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Starts an activity for bot lifecycle events (startup, shutdown).
    /// </summary>
    /// <param name="stage">The lifecycle stage (e.g., "startup", "shutdown").</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartLifecycleActivity(string stage)
    {
        var spanName = stage.ToLowerInvariant() switch
        {
            "startup" => TracingConstants.Spans.BotLifecycleStart,
            "shutdown" => TracingConstants.Spans.BotLifecycleStop,
            _ => $"bot.lifecycle.{stage}"
        };

        var activity = Source.StartActivity(
            name: spanName,
            kind: ActivityKind.Internal);

        if (activity is null)
            return null;

        activity.SetTag(TracingConstants.Attributes.BotLifecycleStage, stage);

        return activity;
    }

    /// <summary>
    /// Starts an activity for Discord Gateway events (connected, disconnected, ready).
    /// </summary>
    /// <param name="eventName">The gateway event name.</param>
    /// <param name="latency">Optional latency in milliseconds.</param>
    /// <param name="connectionState">Optional connection state.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartGatewayActivity(
        string eventName,
        int? latency = null,
        string? connectionState = null)
    {
        var activity = Source.StartActivity(
            name: eventName,
            kind: ActivityKind.Server);

        if (activity is null)
            return null;

        if (latency.HasValue)
        {
            activity.SetTag(TracingConstants.Attributes.ConnectionLatencyMs, latency.Value);
        }

        if (!string.IsNullOrEmpty(connectionState))
        {
            activity.SetTag(TracingConstants.Attributes.ConnectionState, connectionState);
        }

        return activity;
    }

    /// <summary>
    /// Starts an activity for Discord events (message, member, etc.).
    /// </summary>
    /// <param name="eventName">The event span name (use TracingConstants.Spans).</param>
    /// <param name="guildId">Optional guild ID where the event occurred.</param>
    /// <param name="channelId">Optional channel ID.</param>
    /// <param name="userId">Optional user ID.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartEventActivity(
        string eventName,
        ulong? guildId = null,
        ulong? channelId = null,
        ulong? userId = null)
    {
        var activity = Source.StartActivity(
            name: eventName,
            kind: ActivityKind.Server);

        if (activity is null)
            return null;

        if (guildId.HasValue)
        {
            activity.SetTag(TracingConstants.Attributes.GuildId, guildId.Value.ToString());
        }

        if (channelId.HasValue)
        {
            activity.SetTag(TracingConstants.Attributes.ChannelId, channelId.Value.ToString());
        }

        if (userId.HasValue)
        {
            activity.SetTag(TracingConstants.Attributes.UserId, userId.Value.ToString());
        }

        return activity;
    }

    /// <summary>
    /// Sanitizes custom IDs to remove potentially sensitive data like user-specific correlation IDs.
    /// Extracts the handler:action portion for tracing.
    /// </summary>
    private static string SanitizeCustomId(string customId)
    {
        // Custom ID format: {handler}:{action}:{userId}:{correlationId}:{data}
        // We only want handler:action for the span attribute
        var parts = customId.Split(':');
        if (parts.Length >= 2)
        {
            return $"{parts[0]}:{parts[1]}";
        }
        return customId;
    }
}
