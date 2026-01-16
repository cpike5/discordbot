using System.Diagnostics;
using Elastic.Apm;
using Elastic.Apm.Api;
using OpenTelemetry.Trace;

namespace DiscordBot.Bot.Tracing;

/// <summary>
/// Represents both an OpenTelemetry Activity and Elastic APM Transaction
/// for background service operations. Implements IDisposable to ensure
/// proper cleanup of both resources.
/// </summary>
public sealed class BackgroundServiceActivityScope : IDisposable
{
    /// <summary>
    /// Gets the OpenTelemetry Activity for distributed tracing.
    /// </summary>
    public Activity? Activity { get; }

    /// <summary>
    /// Gets the Elastic APM Transaction for APM visibility.
    /// </summary>
    public ITransaction? ApmTransaction { get; }

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackgroundServiceActivityScope"/> class.
    /// </summary>
    /// <param name="activity">The OpenTelemetry activity.</param>
    /// <param name="apmTransaction">The Elastic APM transaction.</param>
    public BackgroundServiceActivityScope(Activity? activity, ITransaction? apmTransaction)
    {
        Activity = activity;
        ApmTransaction = apmTransaction;
    }

    /// <summary>
    /// Marks both the Activity and APM Transaction as successful.
    /// </summary>
    public void SetSuccess()
    {
        BotActivitySource.SetSuccess(Activity);
        if (ApmTransaction != null)
        {
            ApmTransaction.Outcome = Outcome.Success;
        }
    }

    /// <summary>
    /// Records an exception on both the Activity and APM Transaction.
    /// </summary>
    /// <param name="ex">The exception to record.</param>
    public void RecordException(Exception ex)
    {
        BotActivitySource.RecordException(Activity, ex);
        if (ApmTransaction != null)
        {
            ApmTransaction.CaptureException(ex);
            ApmTransaction.Outcome = Outcome.Failure;
        }
    }

    /// <summary>
    /// Disposes both the Activity and APM Transaction.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Activity?.Dispose();
        ApmTransaction?.End();
    }
}

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
    /// Creates a new ActivityContext for a root activity (no parent).
    /// Use this when you need to start an independent trace that should not
    /// inherit from Activity.Current.
    /// </summary>
    /// <param name="traceFlags">Optional trace flags (default: None).</param>
    /// <returns>A new ActivityContext with random trace and span IDs.</returns>
    public static ActivityContext CreateRootContext(ActivityTraceFlags traceFlags = ActivityTraceFlags.None)
    {
        return new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            traceFlags);
    }

    /// <summary>
    /// Starts an activity for a Discord slash command execution.
    /// </summary>
    /// <param name="commandName">The name of the command being executed.</param>
    /// <param name="guildId">The guild ID where the command was invoked.</param>
    /// <param name="userId">The user ID who invoked the command.</param>
    /// <param name="interactionId">The Discord interaction ID.</param>
    /// <param name="correlationId">The application correlation ID.</param>
    /// <param name="asRootSpan">Whether to create an independent root span (default: true).</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartCommandActivity(
        string commandName,
        ulong? guildId,
        ulong userId,
        ulong interactionId,
        string correlationId,
        bool asRootSpan = true)
    {
        var parentContext = asRootSpan ? CreateRootContext() : default;

        var activity = Source.StartActivity(
            $"discord.command {commandName}",
            ActivityKind.Server,
            parentContext);

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
    /// <param name="asRootSpan">Whether to create an independent root span (default: true).</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartComponentActivity(
        string componentType,
        string customId,
        ulong? guildId,
        ulong userId,
        ulong interactionId,
        string correlationId,
        bool asRootSpan = true)
    {
        var parentContext = asRootSpan ? CreateRootContext() : default;

        var activity = Source.StartActivity(
            $"discord.component {componentType}",
            ActivityKind.Server,
            parentContext);

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
    /// <param name="asRootSpan">Whether to create an independent root span (default: true).</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartGatewayActivity(
        string eventName,
        int? latency = null,
        string? connectionState = null,
        bool asRootSpan = true)
    {
        var parentContext = asRootSpan ? CreateRootContext() : default;

        var activity = Source.StartActivity(
            eventName,
            ActivityKind.Server,
            parentContext);

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
    /// <param name="asRootSpan">Whether to create an independent root span (default: true).</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartEventActivity(
        string eventName,
        ulong? guildId = null,
        ulong? channelId = null,
        ulong? userId = null,
        bool asRootSpan = true)
    {
        var parentContext = asRootSpan ? CreateRootContext() : default;

        var activity = Source.StartActivity(
            eventName,
            ActivityKind.Server,
            parentContext);

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
    /// Starts an activity for a background service execution cycle.
    /// </summary>
    /// <param name="serviceName">The name of the background service.</param>
    /// <param name="executionCycle">The current execution cycle number.</param>
    /// <param name="correlationId">Optional correlation ID for the execution.</param>
    /// <param name="asRootSpan">Whether to create an independent root span (default: true).</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartBackgroundServiceActivity(
        string serviceName,
        int executionCycle,
        string? correlationId = null,
        bool asRootSpan = true)
    {
        var spanName = string.Format(TracingConstants.Spans.BackgroundServiceExecute, serviceName);
        var parentContext = asRootSpan ? CreateRootContext() : default;

        var activity = Source.StartActivity(
            spanName,
            ActivityKind.Internal,
            parentContext);

        if (activity is null)
            return null;

        activity.SetTag(TracingConstants.Attributes.BackgroundServiceName, serviceName);
        activity.SetTag(TracingConstants.Attributes.BackgroundExecutionCycle, executionCycle);

        if (!string.IsNullOrEmpty(correlationId))
        {
            activity.SetTag(TracingConstants.Attributes.CorrelationId, correlationId);
            activity.AddBaggage(TracingConstants.Baggage.CorrelationId, correlationId);
        }

        return activity;
    }

    /// <summary>
    /// Starts an activity and APM transaction for a background service execution cycle.
    /// Returns a scope that manages both resources and should be disposed when the cycle completes.
    /// </summary>
    /// <param name="serviceName">The name of the background service.</param>
    /// <param name="executionCycle">The current execution cycle number.</param>
    /// <param name="correlationId">Optional correlation ID for the execution.</param>
    /// <param name="asRootSpan">Whether to create an independent root span (default: true).</param>
    /// <returns>A scope containing both the Activity and APM Transaction.</returns>
    public static BackgroundServiceActivityScope StartBackgroundServiceActivityWithApm(
        string serviceName,
        int executionCycle,
        string? correlationId = null,
        bool asRootSpan = true)
    {
        // Create OpenTelemetry Activity (existing logic)
        var activity = StartBackgroundServiceActivity(serviceName, executionCycle, correlationId, asRootSpan);

        // Create APM Transaction for Elastic APM visibility
        var transactionName = $"background.service.{serviceName}";
        var apmTransaction = Agent.Tracer.StartTransaction(transactionName, "background");

        // Set labels matching Activity tags for consistent filtering
        apmTransaction.SetLabel("service.name", serviceName);
        apmTransaction.SetLabel("execution.cycle", executionCycle);
        if (!string.IsNullOrEmpty(correlationId))
        {
            apmTransaction.SetLabel("correlation_id", correlationId);
        }

        return new BackgroundServiceActivityScope(activity, apmTransaction);
    }

    /// <summary>
    /// Starts an activity for a background service batch processing operation.
    /// </summary>
    /// <param name="serviceName">The name of the background service.</param>
    /// <param name="batchSize">The number of items in the batch.</param>
    /// <param name="batchType">Optional description of what is being batched.</param>
    /// <param name="asRootSpan">Whether to create an independent root span (default: true).</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartBackgroundBatchActivity(
        string serviceName,
        int batchSize,
        string? batchType = null,
        bool asRootSpan = true)
    {
        var spanName = string.Format(TracingConstants.Spans.BackgroundServiceBatch, serviceName);
        var parentContext = asRootSpan ? CreateRootContext() : default;

        var activity = Source.StartActivity(
            spanName,
            ActivityKind.Internal,
            parentContext);

        if (activity is null)
            return null;

        activity.SetTag(TracingConstants.Attributes.BackgroundServiceName, serviceName);
        activity.SetTag(TracingConstants.Attributes.BackgroundBatchSize, batchSize);

        if (!string.IsNullOrEmpty(batchType))
        {
            activity.SetTag(TracingConstants.Attributes.BackgroundItemType, batchType);
        }

        return activity;
    }

    /// <summary>
    /// Starts an activity for a background service cleanup operation.
    /// </summary>
    /// <param name="serviceName">The name of the background service.</param>
    /// <param name="targetType">The type of records being cleaned up.</param>
    /// <param name="asRootSpan">Whether to create an independent root span (default: true).</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartBackgroundCleanupActivity(
        string serviceName,
        string targetType,
        bool asRootSpan = true)
    {
        var spanName = string.Format(TracingConstants.Spans.BackgroundServiceCleanup, serviceName);
        var parentContext = asRootSpan ? CreateRootContext() : default;

        var activity = Source.StartActivity(
            spanName,
            ActivityKind.Internal,
            parentContext);

        if (activity is null)
            return null;

        activity.SetTag(TracingConstants.Attributes.BackgroundServiceName, serviceName);
        activity.SetTag(TracingConstants.Attributes.BackgroundItemType, targetType);

        return activity;
    }

    /// <summary>
    /// Starts an activity for a service layer operation.
    /// </summary>
    /// <param name="serviceName">The name of the service (e.g., "guild", "rat_watch").</param>
    /// <param name="operation">The operation being performed (e.g., "get_by_id", "create").</param>
    /// <param name="guildId">Optional guild ID for the operation.</param>
    /// <param name="userId">Optional user ID for the operation.</param>
    /// <param name="entityId">Optional entity ID being operated on.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartServiceActivity(
        string serviceName,
        string operation,
        ulong? guildId = null,
        ulong? userId = null,
        string? entityId = null)
    {
        var spanName = string.Format(TracingConstants.Spans.ServiceOperation, serviceName, operation);

        var activity = Source.StartActivity(
            name: spanName,
            kind: ActivityKind.Internal);

        if (activity is null)
            return null;

        activity.SetTag(TracingConstants.Attributes.ServiceName, serviceName);
        activity.SetTag(TracingConstants.Attributes.ServiceOperation, operation);

        if (guildId.HasValue)
        {
            activity.SetTag(TracingConstants.Attributes.GuildId, guildId.Value.ToString());
        }

        if (userId.HasValue)
        {
            activity.SetTag(TracingConstants.Attributes.UserId, userId.Value.ToString());
        }

        if (!string.IsNullOrEmpty(entityId))
        {
            activity.SetTag(TracingConstants.Attributes.ServiceEntityId, entityId);
        }

        return activity;
    }

    /// <summary>
    /// Sets the number of records returned from a service operation.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="count">The number of records returned.</param>
    public static void SetRecordsReturned(Activity? activity, int count)
    {
        activity?.SetTag(TracingConstants.Attributes.ServiceRecordsReturned, count);
    }

    /// <summary>
    /// Records the number of items processed on an activity.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="recordsProcessed">The number of records processed.</param>
    public static void SetRecordsProcessed(Activity? activity, int recordsProcessed)
    {
        activity?.SetTag(TracingConstants.Attributes.BackgroundRecordsProcessed, recordsProcessed);
    }

    /// <summary>
    /// Records the number of items deleted on an activity.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="recordsDeleted">The number of records deleted.</param>
    public static void SetRecordsDeleted(Activity? activity, int recordsDeleted)
    {
        activity?.SetTag(TracingConstants.Attributes.BackgroundRecordsDeleted, recordsDeleted);
    }

    /// <summary>
    /// Starts an activity for Azure Speech synthesis.
    /// </summary>
    /// <param name="textLength">The length of the text being synthesized.</param>
    /// <param name="voice">The voice name.</param>
    /// <param name="region">The Azure region.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartAzureSpeechActivity(
        int textLength,
        string voice,
        string region)
    {
        var activity = Source.StartActivity(
            name: TracingConstants.Spans.AzureSpeechSynthesize,
            kind: ActivityKind.Client);

        if (activity is null)
            return null;

        activity.SetTag(TracingConstants.Attributes.TtsTextLength, textLength);
        activity.SetTag(TracingConstants.Attributes.TtsVoice, voice);
        activity.SetTag(TracingConstants.Attributes.TtsRegion, region);

        return activity;
    }

    /// <summary>
    /// Starts an activity for retrieving available voices from Azure Speech.
    /// </summary>
    /// <param name="locale">The locale filter for voices.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartGetVoicesActivity(string? locale)
    {
        var activity = Source.StartActivity(
            name: TracingConstants.Spans.AzureSpeechGetVoices,
            kind: ActivityKind.Client);

        if (activity is null)
            return null;

        if (!string.IsNullOrEmpty(locale))
        {
            activity.SetTag("tts.locale", locale);
        }

        return activity;
    }

    /// <summary>
    /// Starts an activity for audio format conversion.
    /// </summary>
    /// <param name="fromFormat">Source audio format.</param>
    /// <param name="toFormat">Target audio format.</param>
    /// <param name="bytesIn">Input byte count.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartAudioConversionActivity(
        string fromFormat,
        string toFormat,
        int bytesIn)
    {
        var activity = Source.StartActivity(
            name: TracingConstants.Spans.TtsAudioConvert,
            kind: ActivityKind.Internal);

        if (activity is null)
            return null;

        activity.SetTag(TracingConstants.Attributes.AudioFormatFrom, fromFormat);
        activity.SetTag(TracingConstants.Attributes.AudioFormatTo, toFormat);
        activity.SetTag("audio.bytes_in", bytesIn);

        return activity;
    }

    /// <summary>
    /// Starts an activity for Discord audio streaming.
    /// </summary>
    /// <param name="guildId">The guild ID where audio is being streamed.</param>
    /// <param name="durationSeconds">Expected duration in seconds.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartDiscordAudioStreamActivity(
        ulong guildId,
        double durationSeconds)
    {
        var activity = Source.StartActivity(
            name: TracingConstants.Spans.DiscordAudioStream,
            kind: ActivityKind.Client);

        if (activity is null)
            return null;

        activity.SetTag(TracingConstants.Attributes.GuildId, guildId.ToString());
        activity.SetTag(TracingConstants.Attributes.AudioDurationSeconds, durationSeconds);

        return activity;
    }

    /// <summary>
    /// Starts an activity for FFmpeg transcoding.
    /// </summary>
    /// <param name="soundName">The name of the sound being transcoded.</param>
    /// <param name="filePath">The relative file path.</param>
    /// <param name="filter">The audio filter being applied.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartFfmpegTranscodeActivity(
        string soundName,
        string filePath,
        string filter)
    {
        var activity = Source.StartActivity(
            name: TracingConstants.Spans.SoundboardFfmpegTranscode,
            kind: ActivityKind.Internal);

        if (activity is null)
            return null;

        activity.SetTag(TracingConstants.Attributes.SoundName, soundName);
        activity.SetTag(TracingConstants.Attributes.SoundFilePath, filePath);
        activity.SetTag(TracingConstants.Attributes.AudioFilter, filter);

        return activity;
    }

    /// <summary>
    /// Starts an activity for soundboard audio streaming to Discord.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="soundId">The sound ID.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartSoundboardStreamActivity(
        ulong guildId,
        Guid soundId)
    {
        var activity = Source.StartActivity(
            name: TracingConstants.Spans.SoundboardAudioStream,
            kind: ActivityKind.Client);

        if (activity is null)
            return null;

        activity.SetTag(TracingConstants.Attributes.GuildId, guildId.ToString());
        activity.SetTag(TracingConstants.Attributes.SoundId, soundId.ToString());

        return activity;
    }

    /// <summary>
    /// Records audio streaming completion metrics on the activity.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="bytesWritten">Total bytes written to the stream.</param>
    /// <param name="bufferCount">Number of buffers written.</param>
    public static void RecordAudioStreamMetrics(
        Activity? activity,
        long bytesWritten,
        int bufferCount)
    {
        if (activity is null)
            return;

        activity.SetTag(TracingConstants.Attributes.AudioBytesWritten, bytesWritten);
        activity.SetTag(TracingConstants.Attributes.AudioBufferCount, bufferCount);
    }

    /// <summary>
    /// Records FFmpeg process details on the activity.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="processId">The FFmpeg process ID.</param>
    /// <param name="exitCode">The process exit code.</param>
    /// <param name="arguments">The FFmpeg arguments (sanitized).</param>
    public static void RecordFfmpegDetails(
        Activity? activity,
        int processId,
        int exitCode,
        string arguments)
    {
        if (activity is null)
            return;

        activity.SetTag(TracingConstants.Attributes.FfmpegProcessId, processId);
        activity.SetTag(TracingConstants.Attributes.FfmpegExitCode, exitCode);
        activity.SetTag(TracingConstants.Attributes.FfmpegArguments, arguments);
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

    /// <summary>
    /// Enriches the current Elastic APM transaction with activity tags.
    /// Call this for important activities to ensure APM has all context.
    /// </summary>
    /// <remarks>
    /// This method copies relevant Activity tags to APM labels, enabling correlation
    /// between OpenTelemetry traces and Elastic APM transactions. Tags prefixed with
    /// "otel." are skipped as they are OpenTelemetry-specific, and values longer than
    /// 256 characters are skipped to avoid APM label size limits.
    /// </remarks>
    /// <param name="activity">The activity whose tags should be copied to APM.</param>
    public static void EnrichCurrentApmTransaction(Activity? activity)
    {
        if (activity is null)
            return;

        var transaction = Agent.Tracer.CurrentTransaction;
        if (transaction is null)
            return;

        // Copy relevant tags to APM labels
        foreach (var tag in activity.Tags)
        {
            // Skip very long values to avoid APM label size limits
            if (tag.Value?.Length > 256)
                continue;

            // Skip internal OpenTelemetry tags
            if (tag.Key.StartsWith("otel.", StringComparison.OrdinalIgnoreCase))
                continue;

            // Set the label on the APM transaction
            transaction.SetLabel(tag.Key, tag.Value ?? string.Empty);
        }
    }

    /// <summary>
    /// Enriches the current Elastic APM span with activity tags.
    /// Call this when working within a span context.
    /// </summary>
    /// <param name="activity">The activity whose tags should be copied to the current APM span.</param>
    public static void EnrichCurrentApmSpan(Activity? activity)
    {
        if (activity is null)
            return;

        var span = Agent.Tracer.CurrentSpan;
        if (span is null)
            return;

        // Copy relevant tags to APM span labels
        foreach (var tag in activity.Tags)
        {
            // Skip very long values
            if (tag.Value?.Length > 256)
                continue;

            // Skip internal OpenTelemetry tags
            if (tag.Key.StartsWith("otel.", StringComparison.OrdinalIgnoreCase))
                continue;

            span.SetLabel(tag.Key, tag.Value ?? string.Empty);
        }
    }
}
