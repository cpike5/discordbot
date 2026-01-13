namespace DiscordBot.Bot.Tracing;

/// <summary>
/// Constants for distributed tracing span names, attributes, and baggage keys.
/// </summary>
public static class TracingConstants
{
    /// <summary>
    /// Activity source names.
    /// </summary>
    public static class Sources
    {
        public const string Bot = "DiscordBot.Bot";
        public const string Infrastructure = "DiscordBot.Infrastructure";
    }

    /// <summary>
    /// Span attribute keys following OpenTelemetry semantic conventions.
    /// </summary>
    public static class Attributes
    {
        // Discord-specific attributes
        public const string CommandName = "discord.command.name";
        public const string GuildId = "discord.guild.id";
        public const string UserId = "discord.user.id";
        public const string InteractionId = "discord.interaction.id";
        public const string ComponentType = "discord.component.type";
        public const string ComponentId = "discord.component.id";
        public const string ChannelId = "discord.channel.id";
        public const string MessageId = "discord.message.id";
        public const string ConnectionLatencyMs = "discord.connection.latency_ms";
        public const string ConnectionState = "discord.connection.state";
        public const string MemberIsBot = "discord.member.is_bot";
        public const string MemberAccountAgeDays = "discord.member.account_age_days";
        public const string MemberUpdateType = "discord.member.update.type";
        public const string MemberRoleId = "discord.member.role.id";
        public const string GuildsCount = "discord.guilds.count";
        public const string EventType = "discord.event.type";

        // Auto-moderation attributes
        public const string AutoModRuleType = "automod.rule.type";
        public const string AutoModRuleId = "automod.rule.id";
        public const string AutoModSeverity = "automod.severity";
        public const string AutoModActionType = "automod.action.type";
        public const string AutoModDetectionConfidence = "automod.detection.confidence";

        // Bot lifecycle attributes
        public const string BotLifecycleStage = "bot.lifecycle.stage";
        public const string BotShardId = "bot.shard.id";

        // Welcome message attributes
        public const string WelcomeChannelId = "welcome.channel.id";
        public const string WelcomeType = "welcome.type";
        public const string WelcomeMessageTemplateUsed = "welcome.message.template_used";
        public const string WelcomeDeliverySuccess = "welcome.delivery.success";

        // Message attributes
        public const string MessageHasAttachments = "message.has_attachments";
        public const string MessageHasEmbeds = "message.has_embeds";

        // Database attributes (following OTel semantic conventions)
        public const string DbSystem = "db.system";
        public const string DbOperation = "db.operation";
        public const string DbEntityType = "db.entity.type";
        public const string DbEntityId = "db.entity.id";
        public const string DbDurationMs = "db.duration.ms";

        // Application-specific
        public const string CorrelationId = "correlation.id";
        public const string ErrorMessage = "error.message";

        // Background service attributes
        public const string BackgroundServiceName = "background.service.name";
        public const string BackgroundExecutionCycle = "background.execution.cycle";
        public const string BackgroundBatchSize = "background.batch.size";
        public const string BackgroundRecordsProcessed = "background.records.processed";
        public const string BackgroundRecordsDeleted = "background.records.deleted";
        public const string BackgroundDurationMs = "background.duration.ms";
        public const string BackgroundInterval = "background.interval";
        public const string BackgroundItemId = "background.item.id";
        public const string BackgroundItemType = "background.item.type";

        // Service layer attributes
        public const string ServiceName = "service.name";
        public const string ServiceOperation = "service.operation";
        public const string ServiceEntityType = "service.entity.type";
        public const string ServiceEntityId = "service.entity.id";
        public const string ServiceRecordsReturned = "service.records.returned";
        public const string ServiceOperationSuccess = "service.operation.success";

        // Discord API attributes
        public const string DiscordApiEndpoint = "discord.api.endpoint";
        public const string DiscordApiMethod = "discord.api.method";
        public const string DiscordApiResponseStatus = "discord.api.response.status";
        public const string DiscordApiErrorCode = "discord.api.error.code";
        public const string DiscordApiErrorMessage = "discord.api.error.message";

        // Discord API rate limit attributes
        public const string DiscordApiRateLimitLimit = "discord.api.rate_limit.limit";
        public const string DiscordApiRateLimitRemaining = "discord.api.rate_limit.remaining";
        public const string DiscordApiRateLimitReset = "discord.api.rate_limit.reset";
        public const string DiscordApiRateLimitResetAfter = "discord.api.rate_limit.reset_after";
        public const string DiscordApiRateLimitBucket = "discord.api.rate_limit.bucket";
        public const string DiscordApiRateLimitGlobal = "discord.api.rate_limit.global";

        // Discord API retry attributes
        public const string DiscordApiRetryAttempt = "discord.api.retry.attempt";
        public const string DiscordApiRetryBackoffMs = "discord.api.retry.backoff_ms";

        // Azure TTS attributes
        public const string TtsTextLength = "tts.text_length";
        public const string TtsVoice = "tts.voice";
        public const string TtsRegion = "tts.region";
        public const string TtsAudioSizeBytes = "tts.audio_size_bytes";
        public const string TtsSpeed = "tts.speed";
        public const string TtsPitch = "tts.pitch";
        public const string TtsVolume = "tts.volume";
        public const string TtsSynthesisResult = "tts.synthesis_result";
        public const string TtsCancellationReason = "tts.cancellation_reason";

        // Audio conversion and streaming attributes
        public const string AudioFormatFrom = "audio.format_from";
        public const string AudioFormatTo = "audio.format_to";
        public const string AudioDurationSeconds = "audio.duration_seconds";
        public const string AudioBytesWritten = "audio.bytes_written";
        public const string AudioBytesStreamed = "audio.bytes_streamed";
        public const string AudioBufferCount = "audio.buffer_count";
        public const string AudioFilter = "audio.filter";

        // FFmpeg attributes
        public const string FfmpegProcessId = "ffmpeg.process_id";
        public const string FfmpegExitCode = "ffmpeg.exit_code";
        public const string FfmpegArguments = "ffmpeg.arguments";

        // Sound attributes
        public const string SoundId = "sound.id";
        public const string SoundName = "sound.name";
        public const string SoundFilePath = "sound.file_path";
        public const string SoundFileSizeBytes = "sound.file_size_bytes";
        public const string SoundDurationSeconds = "sound.duration_seconds";

        // Voice channel attributes
        public const string VoiceChannelId = "voice.channel_id";
        public const string VoiceChannelName = "voice.channel_name";
        public const string VoiceConnectedAt = "voice.connected_at";
        public const string VoiceConnectionDurationSeconds = "voice.connection_duration_seconds";
    }

    /// <summary>
    /// Span names for distributed tracing operations.
    /// </summary>
    public static class Spans
    {
        // Bot lifecycle
        public const string BotLifecycleStart = "bot.lifecycle.start";
        public const string BotLifecycleStop = "bot.lifecycle.stop";

        // Discord Gateway events
        public const string DiscordGatewayConnected = "discord.gateway.connected";
        public const string DiscordGatewayDisconnected = "discord.gateway.disconnected";
        public const string DiscordGatewayReady = "discord.gateway.ready";

        // Message events
        public const string DiscordEventMessageReceived = "discord.event.message.received";
        public const string DiscordEventMessageUpdated = "discord.event.message.updated";
        public const string DiscordEventMessageDeleted = "discord.event.message.deleted";

        // Member events
        public const string DiscordEventMemberJoined = "discord.event.member.joined";
        public const string DiscordEventMemberLeft = "discord.event.member.left";
        public const string DiscordEventMemberUpdated = "discord.event.member.updated";

        // Auto-moderation events
        public const string DiscordEventAutoModSpamDetected = "discord.event.automod.spam_detected";
        public const string DiscordEventAutoModRaidDetected = "discord.event.automod.raid_detected";
        public const string DiscordEventAutoModContentFiltered = "discord.event.automod.content_filtered";

        // Service operations
        public const string ServiceWelcomeSend = "service.welcome.send";
        public const string ServiceAutoModExecuteAction = "service.automod.execute_action";

        // Background service spans
        public const string BackgroundServiceExecute = "background.{0}.execute";
        public const string BackgroundServiceBatch = "background.{0}.batch";
        public const string BackgroundServiceItem = "background.{0}.item";
        public const string BackgroundServiceCleanup = "background.{0}.cleanup";
        public const string BackgroundServiceAggregation = "background.{0}.aggregation";

        // Service operation span template
        // Format: service.{service_name}.{operation} e.g. service.guild.get_by_id
        public const string ServiceOperation = "service.{0}.{1}";

        // Discord API spans
        public const string DiscordApiRequest = "discord.api.{0} {1}";
        public const string DiscordApiRetry = "discord.api.retry";

        // Azure TTS spans
        public const string AzureSpeechSynthesize = "azure_speech.synthesize";
        public const string AzureSpeechGetVoices = "azure_speech.get_voices";

        // TTS audio processing spans
        public const string TtsAudioConvert = "tts.audio_convert";
        public const string DiscordAudioStream = "discord.audio_stream";

        // Soundboard spans
        public const string SoundboardFfmpegTranscode = "soundboard.ffmpeg_transcode";
        public const string SoundboardAudioStream = "soundboard.audio_stream";

        // Voice channel spans
        public const string DiscordVoiceJoin = "discord.voice_join";
        public const string DiscordVoiceLeave = "discord.voice_leave";
    }

    /// <summary>
    /// Baggage keys for context propagation.
    /// </summary>
    public static class Baggage
    {
        public const string CorrelationId = "correlation-id";
    }

    /// <summary>
    /// Database operation names.
    /// </summary>
    public static class DbOperations
    {
        public const string Select = "SELECT";
        public const string Insert = "INSERT";
        public const string Update = "UPDATE";
        public const string Delete = "DELETE";
        public const string Count = "COUNT";
        public const string Exists = "EXISTS";
    }
}
