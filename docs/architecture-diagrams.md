# Architecture Diagrams

Mermaid-based architectural diagrams for the Discord Bot application.

---

## 1. System Context (C4 Level 1)

High-level view of the system and its external dependencies.

```mermaid
C4Context
    title Discord Bot - System Context

    Person(discordUser, "Discord User", "Interacts via slash commands and messages")
    Person(admin, "Admin", "Manages guilds and bot configuration via web portal")

    Enterprise_Boundary(sys, "Discord Bot System") {
        System(bot, "Discord Bot", "ASP.NET Core app hosting Discord gateway client, web portal, and background services")
    }

    System_Ext(discord, "Discord API", "Gateway WebSocket and REST API")
    System_Ext(openrouter, "OpenRouter API", "OpenAI-compatible chat completions; routes to Claude/GPT/etc. for the AI assistant feature")
    System_Ext(azure, "Azure Cognitive Services", "Text-to-Speech synthesis")
    System_Ext(seq, "Seq", "Structured log aggregation")
    System_Ext(elastic, "Elastic APM / Elasticsearch", "Distributed tracing and log shipping")
    System_Ext(loki, "Grafana Loki", "Log aggregation")

    Rel(discordUser, discord, "Sends messages and commands")
    Rel(discord, bot, "Delivers events via", "WebSocket Gateway")
    Rel(bot, discord, "Sends responses via", "REST API")
    Rel(admin, bot, "Manages via", "HTTPS / Browser")
    Rel(bot, openrouter, "AI assistant queries", "HTTPS")
    Rel(bot, azure, "TTS synthesis", "HTTPS")
    Rel(bot, seq, "Ships logs to", "HTTP")
    Rel(bot, elastic, "Traces and logs", "HTTP")
    Rel(bot, loki, "Ships logs to", "HTTP")
```

---

## 2. Container Diagram (C4 Level 2)

Internal containers and layers within the system.

```mermaid
C4Container
    title Discord Bot - Container Diagram

    Person(user, "Discord User", "")
    Person(admin, "Admin", "")

    System_Boundary(botSystem, "Discord Bot Application") {
        Container(gateway, "Discord Gateway Client", "Discord.Net 3.19", "Manages WebSocket connection, receives events, dispatches slash commands")
        Container(web, "Web Portal", "ASP.NET Core, Razor Pages, Tailwind CSS", "Admin dashboard, guild management, member portal with HTMX/Alpine.js")
        Container(api, "REST API Controllers", "ASP.NET Core MVC", "30+ controllers serving AJAX/HTMX backends")
        Container(services, "Application Services", "C# / DI", "Business logic, moderation, scheduling, audio orchestration, AI assistants (guild + DM)")
        Container(bgServices, "Background Services", "IHostedService", "25+ hosted services for scheduling, aggregation, cleanup, metrics")
        Container(signalr, "SignalR Hub", "ASP.NET Core SignalR", "Real-time dashboard updates, notifications, audio status")
        ContainerDb(db, "Database", "SQLite or PostgreSQL", "All application data via EF Core with dual-provider support")
    }

    System_Ext(discord, "Discord API", "")
    System_Ext(openrouter, "OpenRouter API", "")
    System_Ext(azureTts, "Azure Speech", "")

    Rel(user, discord, "Uses Discord")
    Rel(discord, gateway, "Events", "WebSocket")
    Rel(gateway, discord, "Responses", "REST")
    Rel(admin, web, "Manages", "HTTPS")
    Rel(web, api, "Calls", "HTTP / HTMX")
    Rel(api, services, "Delegates to")
    Rel(gateway, services, "Delegates to")
    Rel(services, db, "Reads/Writes", "EF Core")
    Rel(bgServices, services, "Invokes")
    Rel(bgServices, db, "Reads/Writes", "EF Core")
    Rel(signalr, admin, "Pushes updates", "WebSocket")
    Rel(services, openrouter, "AI queries", "HTTPS")
    Rel(services, azureTts, "TTS synthesis", "HTTPS")
    Rel(services, signalr, "Broadcasts events")
```

---

## 3. Clean Architecture Layers

```mermaid
flowchart TB
    subgraph bot["DiscordBot.Bot - Application Layer"]
        direction TB
        commands["Slash Command Modules\n22+ Discord.Interactions modules"]
        handlers["Event Handlers\nMessage logging, auto-mod, welcome, voice state"]
        pages["Razor Pages\nAdmin dashboard, guild management, portal"]
        controllers["REST API Controllers\n30+ controllers"]
        hosted["Background Services\n25+ IHostedService implementations"]
        hub["DashboardHub\nSignalR real-time"]
    end

    subgraph infra["DiscordBot.Infrastructure - Data Layer"]
        direction TB
        dbctx["BotDbContext\nSqlite + PostgreSQL variants"]
        repos["37 Repositories\nBase Repository with tracing"]
        configs["EF Configurations\nFluent API entity configs"]
        llm["LLM Infrastructure\nOpenRouterLlmClient, AgentRunner, ToolRegistry"]
        voxLib["VoxClipLibrary\nAudio clip scanning and concatenation"]
    end

    subgraph core["DiscordBot.Core - Domain Layer"]
        direction TB
        entities["40+ Entities\nGuild, User, ModerationCase, Sound, etc."]
        interfaces["Service and Repository Interfaces\nIRepository, ILlmClient, IToolProvider"]
        dtos["DTOs and Enums\nConfiguration models, value objects"]
    end

    bot --> infra
    bot --> core
    infra --> core

    style core fill:#2d5a2d,color:#fff
    style infra fill:#2d4a7a,color:#fff
    style bot fill:#7a4a2d,color:#fff
```

---

## 4. Feature Domain Map

```mermaid
flowchart LR
    subgraph moderation["Moderation"]
        modCase["ModerationCase"]
        flagged["FlaggedEvent"]
        modNote["ModNote"]
        modTag["ModTag / UserModTag"]
        watchlist["Watchlist"]
        autoMod["Auto-Moderation\nSpam, Content Filter, Raid"]
    end

    subgraph audio["Audio / Voice"]
        soundboard["Soundboard\nUpload, play, queue"]
        tts["TTS\nAzure Speech, SSML"]
        vox["VOX\nHalf-Life clip synthesis"]
        playback["PlaybackService\nFFmpeg, PCM streaming"]
    end

    subgraph scheduling["Scheduling / Notifications"]
        scheduled["ScheduledMessage\nCron-based recurring"]
        reminders["Reminder\nNatural language time"]
        notifications["UserNotification\nDashboard bell"]
    end

    subgraph analytics["Analytics / Observability"]
        memberAct["Member Activity\nHourly/Daily snapshots"]
        channelAct["Channel Activity"]
        guildMetrics["Guild Metrics"]
        perfMetrics["Performance Metrics\nCPU, memory, DB, cache"]
        alerts["Alert Monitoring\nThresholds + incidents"]
    end

    subgraph ai["AI Assistant"]
        assistant["AssistantService\nRate limiting, consent"]
        agent["AgentRunner\nAgentic tool-use loop"]
        tools["Tool Providers\nDocs, RatWatch, GuildInfo"]
    end

    subgraph dmAssistant["DM Assistant"]
        dmSvc["DmAssistantService\nOwner detection, history"]
        dmHandler["DmAssistantMessageHandler"]
        dmConvMsg["DmConversationMessage\nSliding window history"]
    end

    dmSvc -.->|"shared ILlmClient"| assistant

    subgraph community["Community"]
        ratwatch["RatWatch\nAccountability + voting"]
        welcome["Welcome System\nConfigurable join messages"]
        consent["User Consent\nGDPR compliance"]
    end

    subgraph identity["Identity / Auth"]
        appUser["ApplicationUser\nASP.NET Identity"]
        oauth["Discord OAuth\nToken refresh"]
        guildAccess["Guild Access\nRole-based authorization"]
        verify["Account Verification\nDiscord-to-portal linking"]
    end
```

---

## 5. Audio Pipeline

```mermaid
flowchart TD
    subgraph triggers["Trigger Sources"]
        sbCmd["/soundboard play"]
        ttsCmd["/tts speak"]
        voxCmd["/vox, /fvox, /hgrunt"]
        portal["Portal Playback\nWeb UI trigger"]
    end

    subgraph orchestration["Orchestration"]
        sbOrch["SoundboardOrchestrationService"]
        ttsPlay["TtsPlaybackService"]
        voxSvc["VoxService"]
    end

    subgraph processing["Audio Processing"]
        cache["SoundCacheService\nFFmpeg pre-processing cache"]
        azureTts["AzureTtsService\nAzure Speech SDK"]
        voxConcat["VoxConcatenationService\nClip lookup + concatenation"]
        voxLibrary["VoxClipLibrary\nvox/ fvox/ hgrunt/ .wav/.mp3"]
    end

    subgraph playback["Playback Infrastructure"]
        playbackSvc["PlaybackService\nQueue/Replace modes, per-guild state"]
        audioSvc["AudioService\nVoice connection management, PCM streams"]
        ffmpeg["FFmpeg\nFormat conversion to PCM"]
    end

    discord["Discord Voice Channel\nOpus-encoded audio"]

    sbCmd --> sbOrch
    ttsCmd --> ttsPlay
    voxCmd --> voxSvc
    portal --> sbOrch

    sbOrch --> cache
    ttsPlay --> azureTts
    voxSvc --> voxConcat
    voxConcat --> voxLibrary

    cache --> playbackSvc
    azureTts --> playbackSvc
    voxConcat --> playbackSvc

    playbackSvc --> audioSvc
    audioSvc --> ffmpeg
    ffmpeg --> discord

    style triggers fill:#4a3a6a,color:#fff
    style orchestration fill:#3a5a6a,color:#fff
    style processing fill:#3a6a4a,color:#fff
    style playback fill:#6a4a3a,color:#fff
```

---

## 6. AI Assistant Sequence

```mermaid
sequenceDiagram
    actor User as Discord User
    participant Discord as Discord Gateway
    participant Handler as AssistantMessageHandler
    participant Svc as AssistantService
    participant Agent as AgentRunner
    participant LLM as OpenRouterLlmClient
    participant Tools as ToolRegistry
    participant API as OpenRouter API

    User ->> Discord: @bot question
    Discord ->> Handler: MessageReceived event
    Handler ->> Svc: ProcessAsync(message)

    Svc ->> Svc: Check consent + rate limit
    Svc ->> Agent: RunAsync(conversation)

    loop Agentic Tool-Use Loop
        Agent ->> LLM: SendMessageAsync(messages)
        LLM ->> API: POST /chat/completions
        API -->> LLM: Response (finish_reason: stop or tool_calls)
        LLM -->> Agent: LlmResponse

        opt Tool Use Requested
            Agent ->> Tools: ExecuteToolAsync(name, input)
            Tools -->> Agent: Tool result
            Agent ->> Agent: Append role:"tool" message (tool_call_id)
        end
    end

    Agent -->> Svc: Final response
    Svc ->> Svc: Log interaction + usage metrics
    Svc -->> Handler: Response text
    Handler ->> Discord: Reply to message
    Discord -->> User: Bot response
```

---

## 6b. DM Assistant Sequence

```mermaid
sequenceDiagram
    actor User as Discord User (DM)
    participant Discord as Discord Gateway
    participant Handler as DmAssistantMessageHandler
    participant Svc as DmAssistantService
    participant Repo as DmConversationMessageRepository
    participant LLM as ILlmClient
    participant API as OpenRouter API

    User ->> Discord: DM to bot
    Discord ->> Handler: MessageReceived (DM)
    Handler ->> Svc: ProcessMessageAsync(userId, message)

    Svc ->> Svc: IsOwnerAsync(userId)

    alt Owner
        Svc ->> Repo: GetRecentByUserAsync(userId, limit)
        Repo -->> Svc: Conversation history
        Svc ->> Svc: Build messages array (history + current)
        Svc ->> LLM: SendMessageAsync(messages)
        LLM ->> API: POST /chat/completions
        API -->> LLM: Response
        LLM -->> Svc: LlmResponse

        Svc ->> Repo: AddAsync(user message)
        Svc ->> Repo: AddAsync(assistant response)
        Svc ->> Repo: DeleteOldestByUserAsync(userId, keepCount)

        Svc ->> Svc: Log interaction + usage metrics
        Svc -->> Handler: DmAssistantResponse
    else Non-Owner
        Svc -->> Handler: Placeholder response
    end

    Handler ->> Discord: Reply to DM
    Discord -->> User: Bot response
```

---

## 7. Request Pipeline (Web Portal)

```mermaid
flowchart TD
    req([HTTP Request])
    cors["CORS Middleware"]
    auth["Authentication\nDiscord OAuth + ASP.NET Identity"]
    claims["DiscordClaimsTransformation\nEnrich with guild roles"]
    routing["Endpoint Routing"]

    subgraph endpoints["Endpoint Types"]
        direction TB
        razor["Razor Pages\nAdmin dashboard, guild pages"]
        apiCtrl["API Controllers\nAJAX/HTMX backends"]
        signalr["SignalR Hub\nDashboardHub"]
        swagger["Swagger UI\nOpenAPI docs"]
    end

    subgraph authz["Authorization"]
        viewer["RequireViewer Policy"]
        guildAccess["GuildAccessAuthorizationHandler\nValidate guild portal access"]
        memberAccess["PortalGuildMemberAuthorizationHandler\nValidate guild membership"]
    end

    subgraph services["Service Layer"]
        biz["Business Services"]
        repos["Repositories"]
    end

    db[(Database)]
    resp([HTTP Response])

    req --> cors --> auth --> claims --> routing
    routing --> endpoints
    razor --> authz
    apiCtrl --> authz
    signalr --> authz
    authz --> services
    services --> repos --> db
    db --> resp
    swagger --> resp

    style authz fill:#6a3a3a,color:#fff
```

---

## 8. Background Services Architecture

```mermaid
flowchart LR
    subgraph lifecycle["Bot Lifecycle"]
        botHost["BotHostedService\nDiscord client lifecycle"]
        voxInit["VoxClipLibraryInitializer\nStartup clip scanning"]
    end

    subgraph scheduling["Scheduling"]
        schedExec["ScheduledMessageExecutionService\n60s polling"]
        reminderExec["ReminderExecutionService\nPolling"]
        ratExec["RatWatchExecutionService\n30s polling"]
    end

    subgraph aggregation["Analytics Aggregation"]
        memberAgg["MemberActivityAggregationService\n60m/24h"]
        channelAgg["ChannelActivityAggregationService\n60m/24h"]
        guildAgg["GuildMetricsAggregationService\nHourly"]
    end

    subgraph metrics["Performance Monitoring"]
        metricsCollect["MetricsCollectionService\n60s"]
        cpuSample["CpuSamplingService"]
        alertMonitor["AlertMonitoringService"]
        metricsBroadcast["PerformanceMetricsBroadcastService\n5-30s via SignalR"]
        bizMetrics["BusinessMetricsUpdateService\n5m"]
        otelMetrics["MetricsUpdateService\n30s"]
    end

    subgraph cleanup["Retention / Cleanup"]
        msgCleanup["MessageLogCleanupService\n24h"]
        auditRetention["AuditLogRetentionService\n24h"]
        soundRetention["SoundPlayLogRetentionService\n24h"]
        analyticsRetention["AnalyticsRetentionService\n24h"]
        audioCache["AudioCacheCleanupService"]
        notifRetention["NotificationRetentionService\n24h"]
        interactionCleanup["InteractionStateCleanupService\n1m"]
        verifyCleanup["VerificationCleanupService\n5m"]
    end

    subgraph sync["Sync Services"]
        memberSync["MemberSyncService\n24h reconciliation"]
        tokenRefresh["DiscordTokenRefreshService\n30m"]
        voiceLeave["VoiceAutoLeaveService\n30s"]
    end

    monitor["MonitoredBackgroundService\nDecorator pattern"]
    health["BackgroundServiceHealthRegistry\nHealth check endpoint"]

    lifecycle --> monitor
    scheduling --> monitor
    aggregation --> monitor
    metrics --> monitor
    cleanup --> monitor
    sync --> monitor
    monitor --> health
```

---

## 9. Core Entity Relationships

```mermaid
erDiagram
    Guild ||--o{ GuildMember : "has members"
    User ||--o{ GuildMember : "belongs to guilds"
    Guild ||--o| GuildModerationConfig : "has config"
    Guild ||--o| GuildAudioSettings : "has audio config"
    Guild ||--o| GuildTtsSettings : "has TTS config"
    Guild ||--o| GuildRatWatchSettings : "has RatWatch config"
    Guild ||--o| AssistantGuildSettings : "has AI config"
    Guild ||--o| WelcomeConfiguration : "has welcome config"

    Guild ||--o{ ModerationCase : "contains"
    Guild ||--o{ FlaggedEvent : "contains"
    ModerationCase }o--o| FlaggedEvent : "relates to"
    Guild ||--o{ ModNote : "contains"
    Guild ||--o{ ModTag : "defines"
    ModTag ||--o{ UserModTag : "applied as"
    Guild ||--o{ Watchlist : "monitors"

    Guild ||--o{ Sound : "owns"
    Sound ||--o{ SoundPlayLog : "tracks plays"
    Guild ||--o{ TtsMessage : "logs TTS"

    Guild ||--o{ RatWatch : "hosts"
    RatWatch ||--o{ RatVote : "receives votes"
    RatWatch ||--o| RatRecord : "produces record"

    Guild ||--o{ ScheduledMessage : "schedules"
    Guild ||--o{ Reminder : "has reminders"

    Guild ||--o{ CommandLog : "logs commands"
    Guild ||--o{ MessageLog : "logs messages"
    User ||--o{ CommandLog : "executes"
    User ||--o{ MessageLog : "authors"

    Guild ||--o{ MemberActivitySnapshot : "aggregates"
    Guild ||--o{ ChannelActivitySnapshot : "aggregates"
    Guild ||--o{ GuildMetricsSnapshot : "snapshots"

    Guild ||--o{ AssistantInteractionLog : "logs AI"
    Guild ||--o{ AssistantUsageMetrics : "tracks AI cost"

    User ||--o{ DmConversationMessage : "has DM history"
    User ||--o{ DmAssistantInteractionLog : "logs DM interactions"
    User ||--o{ DmAssistantUsageMetrics : "tracks DM usage"

    ApplicationUser ||--o| DiscordOAuthToken : "authenticates with"
    ApplicationUser ||--o{ UserGuildAccess : "has portal access"
    ApplicationUser ||--o{ VerificationCode : "verifies via"
    ApplicationUser ||--o{ UserNotification : "receives"
```

---

## 10. Observability Stack

```mermaid
flowchart TD
    subgraph app["Application"]
        serilog["Serilog\nStructured logging"]
        otel["OpenTelemetry\nTraces + Metrics"]
        elasticApm["Elastic APM Agent\nAuto-instrumentation"]
        customMeters["Custom Meters\nBotMetrics, BusinessMetrics, ApiMetrics, SloMetrics, VoxMetrics"]
        activitySources["ActivitySources\nPer-domain tracing"]
        correlationMw["Correlation ID Middleware\nRequest threading"]
        queryInterceptor["QueryPerformanceInterceptor\nSlow query detection"]
    end

    subgraph sinks["Log Sinks"]
        console["Console"]
        file["File"]
        seqSink["Seq"]
        lokiSink["Grafana Loki"]
        esSink["Elasticsearch"]
    end

    subgraph tracing["Trace Exporters"]
        otlp["OTLP Exporter"]
        elasticExporter["Elastic APM"]
    end

    subgraph metricsExport["Metrics"]
        prometheus["Prometheus /metrics"]
        otelMetrics["OTLP Metrics"]
    end

    subgraph dashboards["Dashboards"]
        signalrDash["SignalR DashboardHub\nReal-time portal metrics"]
        portalPerf["Portal Performance Pages\nSystem health, API metrics, alerts"]
    end

    serilog --> console & file & seqSink & lokiSink & esSink
    otel --> otlp & prometheus & otelMetrics
    elasticApm --> elasticExporter
    customMeters --> otel
    activitySources --> otel
    correlationMw --> serilog
    queryInterceptor --> serilog

    signalrDash --> portalPerf

    style app fill:#2d4a7a,color:#fff
    style sinks fill:#4a3a2d,color:#fff
    style tracing fill:#3a4a2d,color:#fff
```

---

## 11. Auto-Moderation Flow

```mermaid
sequenceDiagram
    participant Discord as Discord Gateway
    participant Handler as AutoModerationHandler
    participant Spam as SpamDetectionService
    participant Filter as ContentFilterService
    participant Raid as RaidDetectionService
    participant Config as GuildModerationConfig
    participant Repo as FlaggedEventRepository
    participant Notify as DashboardHub

    Discord ->> Handler: MessageReceived
    Handler ->> Config: Get guild moderation config

    par Detection Services
        Handler ->> Spam: CheckMessage(message)
        Spam -->> Handler: SpamResult
    and
        Handler ->> Filter: CheckContent(message)
        Filter -->> Handler: FilterResult
    end

    alt Flagged
        Handler ->> Repo: Create FlaggedEvent
        Handler ->> Notify: Broadcast to moderators via SignalR
        Handler ->> Discord: Auto-action (delete, mute, warn)
    end

    Note over Raid: Raid detection runs on<br/>member join events separately
```

---

## 12. Authentication and Identity

```mermaid
flowchart TD
    subgraph discordSide["Discord"]
        discordUser["Discord User"]
        discordOAuth["Discord OAuth2"]
    end

    subgraph portal["Web Portal"]
        login["Login Page"]
        oauthCallback["OAuth Callback"]
        claimsTransform["DiscordClaimsTransformation\nEnrich with guild roles"]
        identity["ASP.NET Identity\nApplicationUser"]
    end

    subgraph linking["Account Linking"]
        verifyCmd["/verify-account\nDiscord slash command"]
        verifyCode["VerificationCode\nTime-limited code"]
        verifyPage["Portal Verification Page"]
    end

    subgraph authz["Authorization"]
        viewerPolicy["RequireViewer Policy"]
        guildHandler["GuildAccessAuthorizationHandler"]
        memberHandler["PortalGuildMemberAuthorizationHandler"]
        tokenRefresh["DiscordTokenRefreshService\n30m background refresh"]
    end

    discordUser --> login
    login --> discordOAuth
    discordOAuth --> oauthCallback
    oauthCallback --> claimsTransform --> identity

    discordUser --> verifyCmd
    verifyCmd --> verifyCode
    verifyCode --> verifyPage
    verifyPage --> identity

    identity --> viewerPolicy --> guildHandler & memberHandler
    identity --> tokenRefresh

    style discordSide fill:#5865F2,color:#fff
    style linking fill:#4a5a3a,color:#fff
```
