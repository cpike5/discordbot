# Elastic APM Tracing Plan

## Executive Summary

This document outlines a comprehensive Application Performance Monitoring (APM) tracing strategy for integrating Elastic APM into the Discord Bot Management System. The plan identifies all foundational services, critical operations, and recommended instrumentation points to achieve full observability across the three-layer architecture (Domain, Infrastructure, Application).

**Key Integration Benefits:**
- End-to-end tracing of Discord interactions from gateway to database
- Performance profiling of command execution and API requests
- Distributed tracing across Web API and Discord bot hosted service
- Correlation between logs, traces, and metrics
- Real-time performance dashboards and alerting
- Production debugging capabilities with trace context

**Elastic APM Agent:** `Elastic.Apm.NetCoreAll` NuGet package (recommended for ASP.NET Core applications)

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Transaction Boundaries](#transaction-boundaries)
3. [Service-by-Service Instrumentation](#service-by-service-instrumentation)
4. [Span Hierarchy](#span-hierarchy)
5. [Labels and Tags Strategy](#labels-and-tags-strategy)
6. [Custom Metrics](#custom-metrics)
7. [Integration with Serilog](#integration-with-serilog)
8. [Implementation Roadmap](#implementation-roadmap)
9. [Performance Considerations](#performance-considerations)
10. [Elastic APM Configuration](#elastic-apm-configuration)

---

## Architecture Overview

### Current Architecture Layers

```
┌─────────────────────────────────────────────────────────────────┐
│                     Application Layer                            │
│  DiscordBot.Bot - Web API + Bot Hosted Service                  │
│                                                                   │
│  ├── Controllers (BotController, GuildsController, etc.)        │
│  ├── Command Modules (AdminModule, GeneralModule, etc.)         │
│  ├── Services (BotService, GuildService, CommandLogService)     │
│  ├── Handlers (InteractionHandler)                              │
│  └── Hosted Services (BotHostedService)                         │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                   Infrastructure Layer                           │
│  DiscordBot.Infrastructure - Data Access + Configuration        │
│                                                                   │
│  ├── DbContext (BotDbContext)                                   │
│  ├── Repositories (GuildRepository, UserRepository, etc.)       │
│  └── Logging Configuration (Serilog)                            │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                        Domain Layer                              │
│  DiscordBot.Core - Entities, Interfaces, DTOs                   │
│                                                                   │
│  ├── Entities (Guild, User, CommandLog)                         │
│  ├── Interfaces (IRepository<T>, IGuildService, etc.)           │
│  └── DTOs (BotStatusDto, GuildDto, etc.)                        │
└─────────────────────────────────────────────────────────────────┘
```

### External Integrations

```
Discord Gateway ←→ DiscordSocketClient (Singleton)
SQLite Database ←→ EF Core (BotDbContext)
HTTP Clients    ←→ Web API Controllers
```

---

## Transaction Boundaries

Transactions represent complete units of work from a user/system perspective. Elastic APM automatically creates transactions for:
- **HTTP Requests** to Web API controllers
- **Background jobs** (can be manually instrumented)

We need to manually create transactions for:
- **Discord Interactions** (slash commands, button clicks, select menus)
- **Discord Gateway Events** (guild join, message events, etc.)
- **Bot Lifecycle Events** (startup, shutdown, reconnection)

### Primary Transaction Types

| Transaction Type | Source | Example | Priority |
|------------------|--------|---------|----------|
| `discord-interaction` | Discord slash commands, components | `/status`, `/shutdown`, button clicks | **Critical** |
| `http-request` | ASP.NET Core (auto-instrumented) | `GET /api/bot/status` | **Critical** |
| `discord-event` | Discord gateway events | Guild join, message received | High |
| `bot-lifecycle` | Bot startup/shutdown | Bot ready, reconnection | Medium |
| `background-job` | Periodic cleanup tasks | State cleanup service | Low |

---

## Service-by-Service Instrumentation

### 1. BotHostedService

**File:** `src/DiscordBot.Bot/Services/BotHostedService.cs`

**Purpose:** Manages Discord bot lifecycle (startup, login, shutdown)

**Instrumentation Points:**

#### 1.1 StartAsync (Bot Startup)
```csharp
public async Task StartAsync(CancellationToken cancellationToken)
{
    // Transaction: bot-lifecycle
    var transaction = Agent.Tracer.StartTransaction("BotHostedService.StartAsync", "bot-lifecycle");
    try
    {
        _logger.LogInformation("Starting Discord bot hosted service");

        // Span: initialize-interaction-handler
        var initSpan = transaction.StartSpan("InitializeInteractionHandler", "initialization");
        await _interactionHandler.InitializeAsync();
        initSpan.End();

        // Span: discord-login
        var loginSpan = transaction.StartSpan("DiscordLogin", "discord-gateway");
        loginSpan.SetLabel("token_configured", !string.IsNullOrWhiteSpace(_config.Token));
        await _client.LoginAsync(TokenType.Bot, _config.Token);
        loginSpan.End();

        // Span: discord-connect
        var connectSpan = transaction.StartSpan("DiscordConnect", "discord-gateway");
        await _client.StartAsync();
        connectSpan.SetLabel("connection_state", _client.ConnectionState.ToString());
        connectSpan.End();

        transaction.Result = "success";
    }
    catch (Exception ex)
    {
        transaction.CaptureException(ex);
        transaction.Result = "failure";
        throw;
    }
    finally
    {
        transaction.End();
    }
}
```

**Recommended Labels:**
- `bot.username` - Discord bot username
- `bot.token_configured` - Boolean indicating if token is set
- `discord.connection_state` - Connection state after startup
- `environment` - Development, Staging, Production

**Metrics:**
- `bot.startup_duration_ms` - Time to complete startup
- `bot.startup_failures` - Counter of startup failures

#### 1.2 StopAsync (Bot Shutdown)
```csharp
public async Task StopAsync(CancellationToken cancellationToken)
{
    // Transaction: bot-lifecycle
    var transaction = Agent.Tracer.StartTransaction("BotHostedService.StopAsync", "bot-lifecycle");
    try
    {
        // Span: discord-disconnect
        var disconnectSpan = transaction.StartSpan("DiscordDisconnect", "discord-gateway");
        await _client.StopAsync();
        disconnectSpan.End();

        // Span: discord-logout
        var logoutSpan = transaction.StartSpan("DiscordLogout", "discord-gateway");
        await _client.LogoutAsync();
        logoutSpan.End();

        transaction.Result = "success";
    }
    catch (Exception ex)
    {
        transaction.CaptureException(ex);
        transaction.Result = "failure";
    }
    finally
    {
        transaction.End();
    }
}
```

**Recommended Labels:**
- `shutdown.initiated_by` - "api", "signal", or "internal"
- `shutdown.graceful` - Boolean

---

### 2. InteractionHandler

**File:** `src/DiscordBot.Bot/Handlers/InteractionHandler.cs`

**Purpose:** Handles Discord interaction events and command discovery/registration

**Instrumentation Points:**

#### 2.1 OnInteractionCreatedAsync (Command Execution)
```csharp
private async Task OnInteractionCreatedAsync(SocketInteraction interaction)
{
    var correlationId = Guid.NewGuid().ToString("N")[..16];

    // Transaction: discord-interaction
    var transaction = Agent.Tracer.StartTransaction(
        $"Discord.Interaction.{interaction.Type}",
        "discord-interaction");

    transaction.SetLabel("correlation_id", correlationId);
    transaction.SetLabel("interaction.id", interaction.Id.ToString());
    transaction.SetLabel("interaction.type", interaction.Type.ToString());
    transaction.SetLabel("user.id", interaction.User.Id.ToString());
    transaction.SetLabel("user.username", interaction.User.Username);
    transaction.SetLabel("guild.id", interaction.GuildId?.ToString() ?? "dm");

    if (interaction is SocketSlashCommand slashCommand)
    {
        transaction.Name = $"SlashCommand/{slashCommand.CommandName}";
        transaction.SetLabel("command.name", slashCommand.CommandName);
    }
    else if (interaction is SocketMessageComponent component)
    {
        transaction.Name = $"Component/{component.Data.CustomId}";
        transaction.SetLabel("component.custom_id", component.Data.CustomId);
        transaction.SetLabel("component.type", component.Data.Type.ToString());
    }

    try
    {
        var context = new SocketInteractionContext(_client, interaction);

        // Span: execute-command
        var executeSpan = transaction.StartSpan("ExecuteCommand", "discord-command");
        await _interactionService.ExecuteCommandAsync(context, _serviceProvider);
        executeSpan.End();

        transaction.Result = "success";
    }
    catch (Exception ex)
    {
        transaction.CaptureException(ex);
        transaction.Result = "error";
    }
    finally
    {
        transaction.End();
    }
}
```

**Recommended Labels:**
- `correlation_id` - Unique ID for request tracing
- `interaction.id` - Discord interaction ID
- `interaction.type` - ApplicationCommand, MessageComponent, etc.
- `command.name` - Slash command name
- `component.custom_id` - Component custom ID
- `user.id` - Discord user ID
- `user.username` - Discord username
- `guild.id` - Guild ID or "dm"
- `guild.name` - Guild name

**Metrics:**
- `discord.interactions.total` - Counter by command/component
- `discord.interactions.duration_ms` - Histogram
- `discord.interactions.errors` - Counter by error type

#### 2.2 OnReadyAsync (Command Registration)
```csharp
private async Task OnReadyAsync()
{
    var transaction = Agent.Tracer.StartTransaction("Discord.Ready", "discord-event");

    transaction.SetLabel("bot.username", _client.CurrentUser.Username);
    transaction.SetLabel("bot.id", _client.CurrentUser.Id.ToString());
    transaction.SetLabel("guild_count", _client.Guilds.Count);

    try
    {
        // Span: register-commands
        var registerSpan = transaction.StartSpan("RegisterCommands", "discord-setup");
        registerSpan.SetLabel("test_guild_id", _config.TestGuildId?.ToString() ?? "global");

        if (_config.TestGuildId.HasValue)
        {
            await _interactionService.RegisterCommandsToGuildAsync(_config.TestGuildId.Value);
        }
        else
        {
            await _interactionService.RegisterCommandsGloballyAsync();
        }

        registerSpan.SetLabel("command_count", _interactionService.Modules.Count());
        registerSpan.End();

        transaction.Result = "success";
    }
    catch (Exception ex)
    {
        transaction.CaptureException(ex);
        transaction.Result = "failure";
    }
    finally
    {
        transaction.End();
    }
}
```

**Recommended Labels:**
- `bot.username` - Bot username
- `bot.id` - Bot ID
- `guild_count` - Number of guilds
- `test_guild_id` - Test guild ID or "global"
- `command_count` - Number of registered commands

---

### 3. Command Modules

**Files:** `src/DiscordBot.Bot/Commands/*.cs` (AdminModule, GeneralModule, etc.)

**Purpose:** Discord slash command implementations

**Instrumentation Points:**

#### 3.1 AdminModule.StatusAsync
```csharp
[SlashCommand("status", "Display bot status and health information")]
public async Task StatusAsync()
{
    // Current span is already created by InteractionHandler transaction
    var currentSpan = Agent.Tracer.CurrentSpan;

    // Span: fetch-bot-status
    var fetchSpan = currentSpan.StartSpan("FetchBotStatus", "app");
    fetchSpan.SetLabel("guild_count", _client.Guilds.Count);
    fetchSpan.SetLabel("latency_ms", _client.Latency);
    fetchSpan.SetLabel("connection_state", _client.ConnectionState.ToString());
    fetchSpan.End();

    // Span: build-embed
    var buildSpan = currentSpan.StartSpan("BuildStatusEmbed", "rendering");
    // Build embed
    buildSpan.End();

    // Span: send-response
    var responseSpan = currentSpan.StartSpan("SendResponse", "discord-api");
    await RespondAsync(embed: embed, ephemeral: true);
    responseSpan.End();
}
```

**Recommended Labels (per command):**
- `command.type` - "admin", "general", "utility"
- `command.ephemeral` - Boolean
- `response.type` - "embed", "text", "modal"

#### 3.2 AdminModule.ShutdownAsync
```csharp
[SlashCommand("shutdown", "Gracefully shut down the bot")]
public async Task ShutdownAsync()
{
    var currentSpan = Agent.Tracer.CurrentSpan;

    // Span: create-interaction-state
    var stateSpan = currentSpan.StartSpan("CreateInteractionState", "state-management");
    var correlationId = _stateService.CreateState(Context.User.Id, state);
    stateSpan.SetLabel("correlation_id", correlationId);
    stateSpan.End();

    // Span: build-confirmation
    var buildSpan = currentSpan.StartSpan("BuildConfirmationUI", "rendering");
    // Build components
    buildSpan.End();

    // Span: send-response
    var responseSpan = currentSpan.StartSpan("SendResponse", "discord-api");
    await RespondAsync(embed: embed, components: components, ephemeral: true);
    responseSpan.End();
}
```

**Metrics:**
- `commands.{command_name}.executions` - Counter
- `commands.{command_name}.duration_ms` - Histogram
- `commands.{command_name}.errors` - Counter

---

### 4. Web API Controllers

**Files:** `src/DiscordBot.Bot/Controllers/*.cs`

**Purpose:** REST API endpoints for bot management

**Instrumentation Points:**

Elastic APM **automatically instruments** ASP.NET Core controllers, creating transactions for each HTTP request. However, we should add custom spans and labels for:

#### 4.1 BotController.GetStatus
```csharp
[HttpGet("status")]
public ActionResult<BotStatusDto> GetStatus()
{
    var currentTransaction = Agent.Tracer.CurrentTransaction;

    // Add custom labels
    currentTransaction?.SetLabel("endpoint", "bot.status");
    currentTransaction?.SetLabel("operation", "read");

    // Span: fetch-bot-status
    var fetchSpan = Agent.Tracer.CurrentTransaction?.StartSpan("FetchBotStatus", "service");
    var status = _botService.GetStatus();
    fetchSpan?.SetLabel("guild_count", status.GuildCount);
    fetchSpan?.SetLabel("latency_ms", status.LatencyMs);
    fetchSpan?.SetLabel("connection_state", status.ConnectionState);
    fetchSpan?.End();

    return Ok(status);
}
```

**Recommended Labels:**
- `endpoint` - API endpoint identifier
- `operation` - "read", "write", "delete"
- `http.method` - Already captured by auto-instrumentation
- `http.status_code` - Already captured by auto-instrumentation

#### 4.2 GuildsController.GetAllGuilds
```csharp
[HttpGet]
public async Task<ActionResult<IReadOnlyList<GuildDto>>> GetAllGuilds(CancellationToken cancellationToken)
{
    var currentTransaction = Agent.Tracer.CurrentTransaction;
    currentTransaction?.SetLabel("endpoint", "guilds.list");

    // Span: fetch-guilds-from-service
    var fetchSpan = currentTransaction?.StartSpan("GuildService.GetAllGuilds", "service");
    var guilds = await _guildService.GetAllGuildsAsync(cancellationToken);
    fetchSpan?.SetLabel("guild_count", guilds.Count);
    fetchSpan?.End();

    return Ok(guilds);
}
```

#### 4.3 GuildsController.UpdateGuild
```csharp
[HttpPut("{id}")]
public async Task<ActionResult<GuildDto>> UpdateGuild(
    ulong id,
    [FromBody] GuildUpdateRequestDto request,
    CancellationToken cancellationToken)
{
    var currentTransaction = Agent.Tracer.CurrentTransaction;
    currentTransaction?.SetLabel("endpoint", "guilds.update");
    currentTransaction?.SetLabel("guild_id", id.ToString());

    // Span: update-guild-service
    var updateSpan = currentTransaction?.StartSpan("GuildService.UpdateGuild", "service");
    var guild = await _guildService.UpdateGuildAsync(id, request, cancellationToken);
    updateSpan?.End();

    if (guild == null)
    {
        currentTransaction?.SetLabel("result", "not_found");
        return NotFound(...);
    }

    currentTransaction?.SetLabel("result", "success");
    return Ok(guild);
}
```

**Metrics:**
- `api.requests.total` - Counter by endpoint
- `api.requests.duration_ms` - Histogram by endpoint
- `api.requests.errors` - Counter by endpoint and status code

---

### 5. Service Layer

**Files:** `src/DiscordBot.Bot/Services/*.cs`

**Purpose:** Business logic encapsulation

**Instrumentation Points:**

#### 5.1 BotService.GetStatus
```csharp
public BotStatusDto GetStatus()
{
    var span = Agent.Tracer.CurrentSpan?.StartSpan("BotService.GetStatus", "service");

    try
    {
        var status = new BotStatusDto
        {
            Uptime = DateTime.UtcNow - _startTime,
            GuildCount = _client.Guilds.Count,
            LatencyMs = _client.Latency,
            // ...
        };

        span?.SetLabel("guild_count", status.GuildCount);
        span?.SetLabel("latency_ms", status.LatencyMs);
        span?.SetLabel("connection_state", status.ConnectionState);

        return status;
    }
    finally
    {
        span?.End();
    }
}
```

#### 5.2 GuildService.GetAllGuildsAsync
```csharp
public async Task<IReadOnlyList<GuildDto>> GetAllGuildsAsync(CancellationToken cancellationToken = default)
{
    var span = Agent.Tracer.CurrentSpan?.StartSpan("GuildService.GetAllGuilds", "service");

    try
    {
        // Span: fetch-from-repository
        var repoSpan = span?.StartSpan("GuildRepository.GetAll", "db-query");
        var dbGuilds = await _guildRepository.GetAllAsync(cancellationToken);
        repoSpan?.SetLabel("record_count", dbGuilds.Count);
        repoSpan?.End();

        // Span: merge-discord-data
        var mergeSpan = span?.StartSpan("MergeDiscordData", "processing");
        var guilds = new List<GuildDto>();
        foreach (var dbGuild in dbGuilds)
        {
            var discordGuild = _client.GetGuild(dbGuild.Id);
            guilds.Add(MapToDto(dbGuild, discordGuild));
        }
        mergeSpan?.SetLabel("merged_count", guilds.Count);
        mergeSpan?.End();

        return guilds.AsReadOnly();
    }
    finally
    {
        span?.End();
    }
}
```

#### 5.3 GuildService.UpdateGuildAsync
```csharp
public async Task<GuildDto?> UpdateGuildAsync(
    ulong guildId,
    GuildUpdateRequestDto request,
    CancellationToken cancellationToken = default)
{
    var span = Agent.Tracer.CurrentSpan?.StartSpan("GuildService.UpdateGuild", "service");
    span?.SetLabel("guild_id", guildId.ToString());

    try
    {
        // Span: fetch-guild
        var fetchSpan = span?.StartSpan("GuildRepository.GetByDiscordId", "db-query");
        var dbGuild = await _guildRepository.GetByDiscordIdAsync(guildId, cancellationToken);
        fetchSpan?.End();

        if (dbGuild == null)
        {
            span?.SetLabel("result", "not_found");
            return null;
        }

        // Apply updates
        if (request.Prefix != null) dbGuild.Prefix = request.Prefix;
        if (request.Settings != null) dbGuild.Settings = request.Settings;
        if (request.IsActive.HasValue) dbGuild.IsActive = request.IsActive.Value;

        // Span: update-repository
        var updateSpan = span?.StartSpan("GuildRepository.Update", "db-query");
        await _guildRepository.UpdateAsync(dbGuild, cancellationToken);
        updateSpan?.End();

        span?.SetLabel("result", "success");

        var discordGuild = _client.GetGuild(guildId);
        return MapToDto(dbGuild, discordGuild);
    }
    finally
    {
        span?.End();
    }
}
```

#### 5.4 CommandExecutionLogger.LogCommandExecutionAsync
```csharp
public async Task LogCommandExecutionAsync(
    IInteractionContext context,
    string commandName,
    string? parameters,
    int executionTimeMs,
    bool success,
    string? errorMessage = null,
    string? correlationId = null,
    CancellationToken cancellationToken = default)
{
    var span = Agent.Tracer.CurrentSpan?.StartSpan("CommandExecutionLogger.Log", "logging");
    span?.SetLabel("command_name", commandName);
    span?.SetLabel("success", success);
    span?.SetLabel("execution_time_ms", executionTimeMs);
    span?.SetLabel("correlation_id", correlationId ?? "none");

    try
    {
        using var scope = _scopeFactory.CreateScope();
        var commandLogRepository = scope.ServiceProvider.GetRequiredService<ICommandLogRepository>();

        // Span: write-to-database
        var writeSpan = span?.StartSpan("CommandLogRepository.LogCommand", "db-write");
        await commandLogRepository.LogCommandAsync(
            context.Guild?.Id,
            context.User.Id,
            commandName,
            parameters,
            executionTimeMs,
            success,
            errorMessage,
            correlationId,
            cancellationToken);
        writeSpan?.End();
    }
    catch (Exception ex)
    {
        span?.CaptureException(ex);
    }
    finally
    {
        span?.End();
    }
}
```

#### 5.5 InteractionStateService.CreateState
```csharp
public string CreateState<T>(ulong userId, T data, TimeSpan? expiry = null)
{
    var span = Agent.Tracer.CurrentSpan?.StartSpan("InteractionStateService.CreateState", "state-management");
    span?.SetLabel("user_id", userId.ToString());

    try
    {
        var correlationId = GenerateCorrelationId();
        var expiryDuration = expiry ?? DefaultExpiry;

        var state = new InteractionState<T>
        {
            CorrelationId = correlationId,
            UserId = userId,
            Data = data,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(expiryDuration)
        };

        if (_states.TryAdd(correlationId, state))
        {
            span?.SetLabel("correlation_id", correlationId);
            span?.SetLabel("expires_in_seconds", (int)expiryDuration.TotalSeconds);
            return correlationId;
        }

        throw new InvalidOperationException($"Failed to create state with correlation ID {correlationId}");
    }
    finally
    {
        span?.End();
    }
}
```

**Metrics:**
- `services.{service_name}.operations` - Counter by operation
- `services.{service_name}.duration_ms` - Histogram

---

### 6. Repository Layer

**Files:** `src/DiscordBot.Infrastructure/Data/Repositories/*.cs`

**Purpose:** Data access abstraction

**Instrumentation Points:**

Elastic APM **automatically instruments** Entity Framework Core queries. However, we should add custom labels for context:

#### 6.1 Repository<T>.GetAllAsync
```csharp
public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
{
    var span = Agent.Tracer.CurrentSpan?.StartSpan($"Repository<{typeof(T).Name}>.GetAll", "db");
    span?.SetLabel("entity_type", typeof(T).Name);

    try
    {
        var results = await DbSet.ToListAsync(cancellationToken);
        span?.SetLabel("record_count", results.Count);
        return results;
    }
    finally
    {
        span?.End();
    }
}
```

#### 6.2 Repository<T>.UpdateAsync
```csharp
public virtual async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
{
    var span = Agent.Tracer.CurrentSpan?.StartSpan($"Repository<{typeof(T).Name}>.Update", "db");
    span?.SetLabel("entity_type", typeof(T).Name);
    span?.SetLabel("operation", "update");

    try
    {
        DbSet.Update(entity);
        await Context.SaveChangesAsync(cancellationToken);
    }
    finally
    {
        span?.End();
    }
}
```

#### 6.3 CommandLogRepository.LogCommandAsync
```csharp
public async Task LogCommandAsync(
    ulong? guildId,
    ulong userId,
    string commandName,
    string? parameters,
    int executionTimeMs,
    bool success,
    string? errorMessage = null,
    string? correlationId = null,
    CancellationToken cancellationToken = default)
{
    var span = Agent.Tracer.CurrentSpan?.StartSpan("CommandLogRepository.LogCommand", "db-write");
    span?.SetLabel("command_name", commandName);
    span?.SetLabel("success", success);
    span?.SetLabel("correlation_id", correlationId ?? "none");

    try
    {
        var commandLog = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = guildId,
            UserId = userId,
            CommandName = commandName,
            Parameters = parameters,
            ExecutedAt = DateTime.UtcNow,
            ResponseTimeMs = executionTimeMs,
            Success = success,
            ErrorMessage = errorMessage,
            CorrelationId = correlationId
        };

        await AddAsync(commandLog, cancellationToken);
    }
    finally
    {
        span?.End();
    }
}
```

**Metrics:**
- `repository.{entity_type}.queries` - Counter by operation type
- `repository.{entity_type}.duration_ms` - Histogram

---

### 7. DbContext (Entity Framework Core)

**File:** `src/DiscordBot.Infrastructure/Data/BotDbContext.cs`

**Purpose:** Database context

**Instrumentation:**

Elastic APM **automatically instruments** Entity Framework Core:
- Database queries
- Connection management
- Transaction boundaries

**Auto-captured Information:**
- SQL statements
- Query duration
- Connection pool metrics
- Database type (SQLite)

**Additional Manual Labels (optional):**

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    base.OnConfiguring(optionsBuilder);

    // Add APM labels to all DB operations
    optionsBuilder.AddInterceptors(new ApmDbCommandInterceptor());
}

// Custom interceptor to add labels
public class ApmDbCommandInterceptor : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        var span = Agent.Tracer.CurrentSpan;
        span?.SetLabel("db.type", "sqlite");
        span?.SetLabel("db.statement", command.CommandText);

        return base.ReaderExecuting(command, eventData, result);
    }
}
```

---

## Span Hierarchy

### Example: Slash Command Execution Flow

```
Transaction: SlashCommand/status (discord-interaction)
├── Span: ExecuteCommand (discord-command)
│   ├── Span: FetchBotStatus (app)
│   ├── Span: BuildStatusEmbed (rendering)
│   └── Span: SendResponse (discord-api)
└── Span: CommandExecutionLogger.Log (logging)
    └── Span: CommandLogRepository.LogCommand (db-write)
        └── Span: SQL INSERT (db) [auto-instrumented by EF Core]
```

### Example: HTTP API Request Flow

```
Transaction: GET /api/guilds (http-request) [auto-instrumented]
└── Span: GuildService.GetAllGuilds (service)
    ├── Span: GuildRepository.GetAll (db-query)
    │   └── Span: SQL SELECT (db) [auto-instrumented]
    └── Span: MergeDiscordData (processing)
```

### Example: Bot Startup Flow

```
Transaction: BotHostedService.StartAsync (bot-lifecycle)
├── Span: InitializeInteractionHandler (initialization)
│   └── Span: DiscoverModules (discovery)
├── Span: DiscordLogin (discord-gateway)
└── Span: DiscordConnect (discord-gateway)
    └── Span: Discord.Ready (discord-event) [separate transaction]
        └── Span: RegisterCommands (discord-setup)
```

---

## Labels and Tags Strategy

### Global Labels (Applied to All Transactions)

Configure in `appsettings.json`:

```json
{
  "ElasticApm": {
    "GlobalLabels": {
      "service_name": "discord-bot",
      "environment": "production",
      "version": "1.0.0",
      "region": "us-east-1"
    }
  }
}
```

### Transaction-Level Labels

| Label | Type | When to Use | Example |
|-------|------|-------------|---------|
| `correlation_id` | string | All Discord interactions | "a3f2c1b8" |
| `interaction.type` | string | Discord interactions | "ApplicationCommand", "MessageComponent" |
| `command.name` | string | Slash commands | "status", "shutdown" |
| `component.custom_id` | string | Component interactions | "shutdown:confirm:123:a3f2c1b8" |
| `user.id` | string | All user-initiated operations | "123456789012345678" |
| `user.username` | string | All user-initiated operations | "john_doe" |
| `guild.id` | string | Guild-specific operations | "987654321098765432" |
| `guild.name` | string | Guild-specific operations | "My Discord Server" |
| `endpoint` | string | API requests | "bot.status", "guilds.list" |
| `operation` | string | API requests | "read", "write", "delete" |
| `result` | string | All transactions | "success", "error", "not_found" |

### Span-Level Labels

| Label | Type | When to Use | Example |
|-------|------|-------------|---------|
| `entity_type` | string | Repository operations | "Guild", "User", "CommandLog" |
| `record_count` | int | Query operations | 42 |
| `execution_time_ms` | int | Command logging | 250 |
| `connection_state` | string | Discord operations | "Connected", "Disconnected" |
| `latency_ms` | int | Discord status | 45 |
| `guild_count` | int | Bot status | 15 |
| `db.statement` | string | Database queries | "SELECT * FROM Guilds" |
| `db.type` | string | Database operations | "sqlite", "postgresql" |

### Custom Context (User Context)

Elastic APM supports user context for tracking who performed an action:

```csharp
var transaction = Agent.Tracer.CurrentTransaction;
transaction?.SetUser(
    id: context.User.Id.ToString(),
    username: context.User.Username,
    email: null // Not available from Discord
);
```

---

## Custom Metrics

Elastic APM supports custom metrics for business-critical data.

### Bot Health Metrics

```csharp
// Gauge: Active guild count
Agent.Metrics.Set("bot.guilds.active", _client.Guilds.Count);

// Gauge: Gateway latency
Agent.Metrics.Set("bot.latency_ms", _client.Latency);

// Gauge: Connection state (convert to numeric)
var connectionStateValue = _client.ConnectionState == ConnectionState.Connected ? 1 : 0;
Agent.Metrics.Set("bot.connected", connectionStateValue);

// Gauge: Interaction state count
Agent.Metrics.Set("bot.interaction_states.active", _stateService.ActiveStateCount);
```

### Command Execution Metrics

```csharp
// Counter: Command executions
Agent.Metrics.IncrementCounter($"commands.{commandName}.total");

// Counter: Command failures
if (!success)
{
    Agent.Metrics.IncrementCounter($"commands.{commandName}.errors");
}

// Histogram: Command execution time (captured by APM automatically via transactions)
// No manual instrumentation needed if using transaction.Duration
```

### Database Metrics

```csharp
// Counter: Total queries
Agent.Metrics.IncrementCounter($"repository.{entityType}.queries");

// Gauge: Active database connections (if available)
// SQLite doesn't expose connection pool metrics, but other providers might
```

### API Metrics

```csharp
// Counter: API requests by endpoint
Agent.Metrics.IncrementCounter($"api.{endpoint}.requests");

// Counter: API errors by status code
if (statusCode >= 400)
{
    Agent.Metrics.IncrementCounter($"api.errors.{statusCode}");
}
```

### Periodic Metric Collection

Create a background service to collect metrics periodically:

```csharp
public class MetricsCollectionService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly IInteractionStateService _stateService;
    private readonly ILogger<MetricsCollectionService> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Collect bot metrics
                Agent.Metrics.Set("bot.guilds.active", _client.Guilds.Count);
                Agent.Metrics.Set("bot.latency_ms", _client.Latency);
                Agent.Metrics.Set("bot.connected", _client.ConnectionState == ConnectionState.Connected ? 1 : 0);

                // Collect state metrics
                Agent.Metrics.Set("bot.interaction_states.active", _stateService.ActiveStateCount);

                // Memory metrics
                var process = Process.GetCurrentProcess();
                Agent.Metrics.Set("process.memory.working_set_mb", process.WorkingSet64 / 1024 / 1024);
                Agent.Metrics.Set("process.cpu.usage_percent", process.TotalProcessorTime.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting metrics");
            }

            // Collect every 30 seconds
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
```

---

## Integration with Serilog

Elastic APM integrates seamlessly with Serilog to correlate logs with traces.

### Step 1: Configure Serilog with Elastic APM Enricher

**NuGet Package:** `Elastic.Apm.SerilogEnricher`

**Configuration in `Program.cs`:**

```csharp
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithElasticApmCorrelationInfo() // Add APM correlation info
    .WriteTo.Console(new JsonFormatter()) // Structured JSON output
    .WriteTo.File(new JsonFormatter(), "logs/discordbot-.log", rollingInterval: RollingInterval.Day));
```

### Step 2: Automatic Correlation

The `WithElasticApmCorrelationInfo()` enricher automatically adds these fields to log events:
- `transaction.id` - APM transaction ID
- `trace.id` - APM trace ID
- `span.id` - APM span ID
- `service.name` - Service name
- `service.version` - Service version
- `service.environment` - Environment name

### Step 3: Query Logs by Trace ID

In Kibana, you can:
1. View a trace in APM
2. Click "View Logs" to see all logs associated with that trace
3. Navigate from logs to traces bidirectionally

### Example Log Output

```json
{
  "timestamp": "2024-12-09T12:34:56.789Z",
  "level": "Information",
  "message": "Slash command 'status' executed successfully",
  "correlation_id": "a3f2c1b8",
  "command.name": "status",
  "user.username": "john_doe",
  "guild.id": "123456789",
  "execution_time_ms": 245,
  "trace.id": "0af7651916cd43dd8448eb211c80319c",
  "transaction.id": "49f72d5b-3920-41be-a04f-e1a1c6a5c8d3",
  "span.id": "24f6a9c3d4e2f1b0",
  "service.name": "discord-bot",
  "service.environment": "production"
}
```

### Step 4: Enhanced Logging in Services

Use the existing Serilog logging patterns, and APM correlation is automatic:

```csharp
_logger.LogInformation(
    "Guild {GuildId} updated successfully by user {UserId}, CorrelationId: {CorrelationId}",
    guildId,
    userId,
    correlationId);

// APM automatically adds:
// - trace.id
// - transaction.id
// - span.id
// These can be used to link logs to traces in Kibana
```

---

## Implementation Roadmap

### Phase 1: Foundation (Week 1)

**Objective:** Set up Elastic APM agent and basic auto-instrumentation

**Tasks:**
1. Add `Elastic.Apm.NetCoreAll` NuGet package to `DiscordBot.Bot`
2. Add `Elastic.Apm.SerilogEnricher` NuGet package
3. Configure Elastic APM in `appsettings.json` and `Program.cs`
4. Configure Serilog enricher for APM correlation
5. Test auto-instrumentation on HTTP endpoints
6. Verify data appears in Elastic APM UI

**Deliverables:**
- Elastic APM agent installed and configured
- HTTP requests automatically traced
- EF Core queries automatically traced
- Logs correlated with traces

**Acceptance Criteria:**
- [ ] API requests appear in APM UI
- [ ] Database queries appear as spans
- [ ] Logs include `trace.id` and `transaction.id`
- [ ] No performance degradation

### Phase 2: Discord Interactions (Week 2)

**Objective:** Instrument Discord interaction handling

**Tasks:**
1. Add transaction creation in `InteractionHandler.OnInteractionCreatedAsync`
2. Add labels for interaction type, command name, user info
3. Create spans for command execution phases
4. Test with various slash commands
5. Verify correlation IDs match between APM and logs

**Deliverables:**
- Discord interactions appear as transactions in APM
- Command execution fully traced
- User and guild context captured

**Acceptance Criteria:**
- [ ] Slash commands appear as transactions
- [ ] Component interactions appear as transactions
- [ ] User and guild labels populated
- [ ] Correlation IDs match in logs and APM

### Phase 3: Service Layer (Week 3)

**Objective:** Add spans to service layer operations

**Tasks:**
1. Instrument `BotService` methods
2. Instrument `GuildService` methods
3. Instrument `CommandLogService` methods
4. Instrument `InteractionStateService` methods
5. Add meaningful labels for business context

**Deliverables:**
- Service layer operations appear as spans
- Business context captured in labels
- Clear span hierarchy visible in APM

**Acceptance Criteria:**
- [ ] Service operations appear as child spans
- [ ] Labels include business-relevant data
- [ ] Span duration accurately reflects performance

### Phase 4: Bot Lifecycle (Week 4)

**Objective:** Instrument bot lifecycle events

**Tasks:**
1. Add transaction for `BotHostedService.StartAsync`
2. Add transaction for `BotHostedService.StopAsync`
3. Add transaction for `OnReadyAsync` event
4. Add spans for command registration
5. Test startup and shutdown scenarios

**Deliverables:**
- Bot startup/shutdown traced
- Command registration traced
- Discord Ready event traced

**Acceptance Criteria:**
- [ ] Startup appears as transaction
- [ ] Shutdown appears as transaction
- [ ] Command registration duration measured

### Phase 5: Custom Metrics (Week 5)

**Objective:** Add custom business metrics

**Tasks:**
1. Create `MetricsCollectionService` background service
2. Collect bot health metrics (guild count, latency, connection state)
3. Collect state management metrics (active states)
4. Add command execution counters
5. Configure metric dashboards in Kibana

**Deliverables:**
- Custom metrics collection service
- Bot health metrics in APM
- Command execution metrics in APM
- Kibana dashboards for metrics

**Acceptance Criteria:**
- [ ] Metrics appear in APM Metrics tab
- [ ] Metrics update every 30 seconds
- [ ] Dashboards visualize key metrics
- [ ] Alerting rules configured

### Phase 6: Optimization & Production (Week 6)

**Objective:** Optimize performance and prepare for production

**Tasks:**
1. Review and optimize sampling rates
2. Configure APM filters to reduce noise
3. Set up error tracking and alerting
4. Create SLO dashboards
5. Document APM usage for team
6. Load test and verify overhead is acceptable

**Deliverables:**
- Production-ready APM configuration
- Alerting rules configured
- Team documentation
- Performance benchmarks

**Acceptance Criteria:**
- [ ] APM overhead < 5% CPU/memory
- [ ] Sampling configured appropriately
- [ ] Critical alerts configured
- [ ] Team trained on APM usage

---

## Performance Considerations

### APM Overhead

Elastic APM is designed for production use with minimal overhead:
- **CPU:** < 1-2% overhead in most scenarios
- **Memory:** ~50-100MB for agent
- **Network:** Batched reporting to APM Server (configurable)

### Sampling Strategies

**Development:**
```json
{
  "ElasticApm": {
    "TransactionSampleRate": 1.0  // 100% sampling
  }
}
```

**Production (High Volume):**
```json
{
  "ElasticApm": {
    "TransactionSampleRate": 0.1,  // 10% sampling
    "TransactionMaxSpans": 500,     // Limit spans per transaction
    "StackTraceLimit": 50           // Limit stack trace depth
  }
}
```

**Adaptive Sampling:**

Elastic APM supports adaptive sampling based on transaction duration:
- Always sample slow transactions (e.g., > 1s)
- Sample fast transactions at lower rate

```json
{
  "ElasticApm": {
    "TransactionSampleRate": 0.1,
    "SpanCompressionEnabled": true,
    "SpanCompressionExactMatchMaxDuration": "50ms",
    "SpanCompressionSameKindMaxDuration": "0ms"
  }
}
```

### Span Compression

Elastic APM can compress consecutive similar spans (e.g., multiple SQL queries) to reduce overhead:

```json
{
  "ElasticApm": {
    "SpanCompressionEnabled": true,
    "SpanCompressionExactMatchMaxDuration": "50ms",
    "SpanCompressionSameKindMaxDuration": "0ms"
  }
}
```

### Filtering and Sanitization

**Exclude sensitive data from traces:**

```json
{
  "ElasticApm": {
    "SanitizeFieldNames": [
      "*password*",
      "*token*",
      "*secret*",
      "*key*",
      "*authorization*"
    ]
  }
}
```

**Ignore specific transactions:**

```csharp
// In Program.cs
Agent.AddFilter(new CustomTransactionFilter());

public class CustomTransactionFilter : IFilter
{
    public ITransaction Filter(ITransaction transaction)
    {
        // Ignore health check endpoints
        if (transaction.Name.Contains("/health"))
        {
            transaction.Drop();
        }
        return transaction;
    }

    public ISpan Filter(ISpan span) => span;
    public IError Filter(IError error) => error;
}
```

### Async Operations

All APM operations are async-safe and thread-safe:
- Use `Agent.Tracer.CurrentTransaction` to access current transaction
- Use `Agent.Tracer.CurrentSpan` to access current span
- APM context flows across async boundaries automatically

### Best Practices

1. **Avoid creating too many spans** - Limit to meaningful operations only
2. **Use span compression** - Enable for repetitive operations
3. **Sample appropriately** - 100% in dev, 10-50% in prod
4. **Filter health checks** - Exclude noisy endpoints
5. **Sanitize sensitive data** - Configure field name filters
6. **Monitor APM overhead** - Track agent CPU/memory usage
7. **Use labels wisely** - Avoid high-cardinality labels (e.g., unique IDs as label values)

---

## Elastic APM Configuration

### appsettings.json

```json
{
  "ElasticApm": {
    "ServerUrl": "https://your-apm-server.example.com:8200",
    "SecretToken": "${ELASTIC_APM_SECRET_TOKEN}",
    "ServiceName": "discord-bot",
    "ServiceVersion": "1.0.0",
    "Environment": "production",
    "TransactionSampleRate": 0.1,
    "CaptureBody": "errors",
    "CaptureHeaders": true,
    "LogLevel": "Information",
    "StackTraceLimit": 50,
    "SpanFramesMinDuration": "5ms",
    "TransactionMaxSpans": 500,
    "SpanCompressionEnabled": true,
    "SpanCompressionExactMatchMaxDuration": "50ms",
    "SpanCompressionSameKindMaxDuration": "0ms",
    "SanitizeFieldNames": [
      "*password*",
      "*token*",
      "*secret*",
      "*key*",
      "*authorization*",
      "*discord*token*"
    ],
    "GlobalLabels": {
      "region": "us-east-1",
      "datacenter": "aws",
      "team": "platform"
    }
  }
}
```

### Program.cs

```csharp
using Elastic.Apm.NetCoreAll;
using Elastic.Apm.SerilogEnricher;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog with APM enrichment
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithElasticApmCorrelationInfo() // APM correlation
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.File(new JsonFormatter(), "logs/discordbot-.log", rollingInterval: RollingInterval.Day));

// Add Discord bot services
builder.Services.AddDiscordBot(builder.Configuration);

// Add Infrastructure services
builder.Services.AddInfrastructure(builder.Configuration);

// Add application services
builder.Services.AddScoped<IBotService, BotService>();
builder.Services.AddScoped<IGuildService, GuildService>();
builder.Services.AddScoped<ICommandLogService, CommandLogService>();

// Add metrics collection service
builder.Services.AddHostedService<MetricsCollectionService>();

// Add Web API services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Enable Elastic APM for all supported features
// This auto-instruments:
// - ASP.NET Core (HTTP requests)
// - Entity Framework Core (database queries)
// - HttpClient (outbound HTTP calls)
app.UseAllElasticApm(app.Configuration);

app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
```

### Environment Variables (for sensitive configuration)

```bash
# .env file or container environment
ELASTIC_APM_SERVER_URL=https://your-apm-server.example.com:8200
ELASTIC_APM_SECRET_TOKEN=your-secret-token
ELASTIC_APM_SERVICE_NAME=discord-bot
ELASTIC_APM_ENVIRONMENT=production
```

### Docker Configuration (if using containers)

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# Install Elastic APM agent
RUN apt-get update && apt-get install -y curl

# Set environment variables
ENV ELASTIC_APM_SERVER_URL=https://your-apm-server.example.com:8200
ENV ELASTIC_APM_SERVICE_NAME=discord-bot
ENV ELASTIC_APM_ENVIRONMENT=production

COPY . /app
WORKDIR /app

ENTRYPOINT ["dotnet", "DiscordBot.Bot.dll"]
```

---

## Summary

This comprehensive APM tracing plan provides:

1. **Transaction Boundaries** - Clear definition of what constitutes a transaction (Discord interactions, API requests, bot lifecycle events)

2. **Service-by-Service Instrumentation** - Detailed instrumentation points for:
   - BotHostedService (startup/shutdown)
   - InteractionHandler (command execution)
   - Command Modules (slash commands)
   - Web API Controllers (HTTP endpoints)
   - Service Layer (business logic)
   - Repository Layer (data access)
   - DbContext (EF Core)

3. **Span Hierarchy** - Clear parent-child relationships between operations

4. **Labels and Tags** - Comprehensive labeling strategy for filtering and analysis

5. **Custom Metrics** - Business-critical metrics for bot health, commands, and state

6. **Serilog Integration** - Automatic correlation between logs and traces

7. **Implementation Roadmap** - 6-week phased rollout plan

8. **Performance Considerations** - Sampling, compression, and overhead management

9. **Production-Ready Configuration** - Complete Elastic APM setup

### Next Steps

1. **Install Elastic APM NuGet packages**
2. **Configure Elastic APM in appsettings.json**
3. **Add APM enricher to Serilog**
4. **Start with Phase 1** (auto-instrumentation)
5. **Incrementally add custom instrumentation** following the roadmap
6. **Monitor overhead** and adjust sampling as needed
7. **Create Kibana dashboards** for key metrics
8. **Set up alerts** for critical thresholds

---

**Document Version:** 1.0
**Created:** December 2024
**Last Updated:** December 2024
**Status:** Planning Document
