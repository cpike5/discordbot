# AI Assistant Agent - Implementation Plan

**Document Version**: 1.0
**Last Updated**: 2026-01-12
**Status**: Ready for Implementation

---

## 1. Overview

### Feature Summary
Implement an AI-powered assistant feature that responds to bot mentions in Discord with helpful information about bot features, commands, and usage. The assistant uses Claude API with tool-based documentation access to provide accurate, conversational help directly in Discord channels.

### Scope
This implementation adds:
- Message detection and response system for bot mentions
- Claude API integration with tool-based documentation access
- Rate limiting with per-user and per-guild controls
- Guild configuration for channel restrictions and rate limits
- Consent-based privacy protection
- Basic usage metrics and cost tracking
- Admin UI for guild configuration

### Architecture Impact
**Affected Layers:**
- **Core**: New entities, interfaces, DTOs, configuration
- **Infrastructure**: Service implementations, repositories, message handler
- **Bot**: Discord event handler, DI registration, admin UI pages

---

## 2. Context from CLAUDE.md

### Architecture Pattern
Three-layer clean architecture:
- **Domain (DiscordBot.Core)**: Entities, interfaces, DTOs, enums
- **Infrastructure (DiscordBot.Infrastructure)**: EF Core DbContext, repositories, service implementations
- **Application (DiscordBot.Bot)**: Web API controllers, Razor Pages admin UI, bot hosted service, command modules, DI composition

### Existing Patterns to Follow

#### Configuration
- Options pattern: `IOptions<AssistantOptions>` (already created at `src/DiscordBot.Core/Configuration/AssistantOptions.cs`)
- User Secrets for API keys: `dotnet user-secrets set "Claude:ApiKey" "sk-ant-..."`
- appsettings.json for non-sensitive defaults

#### Entities
- Discord IDs stored as `ulong` in C#, converted to `long` for database via EF Core value conversion
- Navigation properties for relationships
- Timestamp properties in UTC

#### Services
- Interface in Core (`IAssistantService`)
- Implementation in Infrastructure (`AssistantService`)
- Constructor injection for dependencies
- `ILogger<T>` for logging
- `CancellationToken` on async methods

#### Repositories
- Generic base interface `IRepository<T>`
- Specialized interface `IAssistantGuildSettingsRepository : IRepository<AssistantGuildSettings>`
- Implementation in Infrastructure

#### Consent System
- Existing `ConsentType` enum (add `AssistantUsage = 2`)
- Existing `UserConsent` entity and `IConsentService`
- `/consent` command for opt-in/opt-out

---

## 3. Database Design

### 3.1 Entities

#### AssistantGuildSettings
```csharp
namespace DiscordBot.Core.Entities;

/// <summary>
/// Per-guild configuration settings for the AI assistant feature.
/// </summary>
public class AssistantGuildSettings
{
    /// <summary>
    /// Discord guild snowflake ID (serves as primary key).
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Whether the assistant feature is enabled for this guild.
    /// </summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>
    /// List of channel IDs where assistant is allowed to respond.
    /// Empty list means all channels are allowed.
    /// Stored as JSON array.
    /// </summary>
    public string AllowedChannelIds { get; set; } = "[]";

    /// <summary>
    /// Guild-specific rate limit override (questions per RateLimitWindowMinutes).
    /// Null means use global default from AssistantOptions.DefaultRateLimit.
    /// </summary>
    public int? RateLimitOverride { get; set; }

    /// <summary>
    /// Timestamp when these settings were created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when these settings were last updated (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property for the guild these settings belong to.
    /// </summary>
    public Guild? Guild { get; set; }

    /// <summary>
    /// Helper to deserialize AllowedChannelIds from JSON.
    /// </summary>
    public List<ulong> GetAllowedChannelIdsList()
    {
        if (string.IsNullOrWhiteSpace(AllowedChannelIds) || AllowedChannelIds == "[]")
            return new List<ulong>();

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<ulong>>(AllowedChannelIds)
                ?? new List<ulong>();
        }
        catch
        {
            return new List<ulong>();
        }
    }

    /// <summary>
    /// Helper to serialize AllowedChannelIds to JSON.
    /// </summary>
    public void SetAllowedChannelIdsList(List<ulong> channelIds)
    {
        AllowedChannelIds = System.Text.Json.JsonSerializer.Serialize(channelIds ?? new List<ulong>());
    }
}
```

**EF Core Configuration** (GuildAudioSettings pattern):
- Primary key: `GuildId`
- Foreign key to `Guilds` with cascade delete
- Default values: `IsEnabled = false`, `AllowedChannelIds = "[]"`
- Indexes: None needed (primary key sufficient)

**SQL Schema**:
```sql
CREATE TABLE AssistantGuildSettings (
    GuildId INTEGER NOT NULL PRIMARY KEY,
    IsEnabled INTEGER NOT NULL DEFAULT 0,
    AllowedChannelIds TEXT NOT NULL DEFAULT '[]',
    RateLimitOverride INTEGER NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    FOREIGN KEY (GuildId) REFERENCES Guilds(Id) ON DELETE CASCADE
);
```

---

#### AssistantUsageMetrics
```csharp
namespace DiscordBot.Core.Entities;

/// <summary>
/// Aggregated daily usage metrics for the AI assistant feature.
/// Tracks token usage, costs, and question counts per guild.
/// </summary>
public class AssistantUsageMetrics
{
    /// <summary>
    /// Unique identifier for this metrics record.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Discord guild snowflake ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Date for which metrics are aggregated (UTC date only, no time component).
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Total number of questions asked on this date.
    /// </summary>
    public int TotalQuestions { get; set; } = 0;

    /// <summary>
    /// Total input tokens consumed (non-cached).
    /// </summary>
    public int TotalInputTokens { get; set; } = 0;

    /// <summary>
    /// Total output tokens consumed.
    /// </summary>
    public int TotalOutputTokens { get; set; } = 0;

    /// <summary>
    /// Total cached tokens served from prompt cache.
    /// </summary>
    public int TotalCachedTokens { get; set; } = 0;

    /// <summary>
    /// Total tokens written to cache (on cache miss).
    /// </summary>
    public int TotalCacheWriteTokens { get; set; } = 0;

    /// <summary>
    /// Total number of cache hits.
    /// </summary>
    public int TotalCacheHits { get; set; } = 0;

    /// <summary>
    /// Total number of cache misses.
    /// </summary>
    public int TotalCacheMisses { get; set; } = 0;

    /// <summary>
    /// Total number of tool calls executed.
    /// </summary>
    public int TotalToolCalls { get; set; } = 0;

    /// <summary>
    /// Estimated total cost in USD for this date.
    /// Calculated using token counts and pricing from AssistantOptions.
    /// </summary>
    public decimal EstimatedCostUsd { get; set; } = 0m;

    /// <summary>
    /// Number of failed requests (API errors, timeouts).
    /// </summary>
    public int FailedRequests { get; set; } = 0;

    /// <summary>
    /// Average response latency in milliseconds.
    /// </summary>
    public int AverageLatencyMs { get; set; } = 0;

    /// <summary>
    /// Timestamp when this record was last updated (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property for the guild.
    /// </summary>
    public Guild? Guild { get; set; }
}
```

**EF Core Configuration**:
- Primary key: `Id` (auto-increment long)
- Foreign key: `GuildId` → `Guilds(Id)` with cascade delete
- Composite unique index: `(GuildId, Date)` - one record per guild per day
- Index: `Date` - for retention cleanup queries

**SQL Schema**:
```sql
CREATE TABLE AssistantUsageMetrics (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GuildId INTEGER NOT NULL,
    Date TEXT NOT NULL,
    TotalQuestions INTEGER NOT NULL DEFAULT 0,
    TotalInputTokens INTEGER NOT NULL DEFAULT 0,
    TotalOutputTokens INTEGER NOT NULL DEFAULT 0,
    TotalCachedTokens INTEGER NOT NULL DEFAULT 0,
    TotalCacheWriteTokens INTEGER NOT NULL DEFAULT 0,
    TotalCacheHits INTEGER NOT NULL DEFAULT 0,
    TotalCacheMisses INTEGER NOT NULL DEFAULT 0,
    TotalToolCalls INTEGER NOT NULL DEFAULT 0,
    EstimatedCostUsd REAL NOT NULL DEFAULT 0.0,
    FailedRequests INTEGER NOT NULL DEFAULT 0,
    AverageLatencyMs INTEGER NOT NULL DEFAULT 0,
    UpdatedAt TEXT NOT NULL,
    FOREIGN KEY (GuildId) REFERENCES Guilds(Id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IX_AssistantUsageMetrics_GuildId_Date
    ON AssistantUsageMetrics (GuildId, Date);

CREATE INDEX IX_AssistantUsageMetrics_Date
    ON AssistantUsageMetrics (Date);
```

---

#### AssistantInteractionLog
```csharp
namespace DiscordBot.Core.Entities;

/// <summary>
/// Detailed log of individual assistant interactions for debugging and audit.
/// Stored in AuditLog table with custom fields in Details JSON.
/// </summary>
public class AssistantInteractionLog
{
    /// <summary>
    /// Unique identifier for this interaction.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Timestamp when the question was asked (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Discord user ID who asked the question.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Discord guild ID where question was asked.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Discord channel ID where question was asked.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Discord message ID of the user's question.
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    /// User's original question (truncated to MaxQuestionLength).
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Claude's response (may be truncated to MaxResponseLength).
    /// </summary>
    public string? Response { get; set; }

    /// <summary>
    /// Number of input tokens consumed (non-cached).
    /// </summary>
    public int InputTokens { get; set; } = 0;

    /// <summary>
    /// Number of output tokens consumed.
    /// </summary>
    public int OutputTokens { get; set; } = 0;

    /// <summary>
    /// Number of tokens served from cache.
    /// </summary>
    public int CachedTokens { get; set; } = 0;

    /// <summary>
    /// Number of tokens written to cache (on cache miss).
    /// </summary>
    public int CacheCreationTokens { get; set; } = 0;

    /// <summary>
    /// Whether the prompt cache was hit.
    /// </summary>
    public bool CacheHit { get; set; } = false;

    /// <summary>
    /// Number of tool calls executed.
    /// </summary>
    public int ToolCalls { get; set; } = 0;

    /// <summary>
    /// Total response latency in milliseconds.
    /// </summary>
    public int LatencyMs { get; set; } = 0;

    /// <summary>
    /// Whether the request succeeded.
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Error message if request failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Estimated cost in USD for this interaction.
    /// </summary>
    public decimal EstimatedCostUsd { get; set; } = 0m;

    /// <summary>
    /// Navigation property for the user.
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// Navigation property for the guild.
    /// </summary>
    public Guild? Guild { get; set; }
}
```

**EF Core Configuration**:
- Primary key: `Id` (auto-increment long)
- Foreign keys: `UserId` → `Users(Id)`, `GuildId` → `Guilds(Id)` (no cascade)
- Indexes:
  - `(GuildId, Timestamp)` - for guild-specific queries
  - `(UserId, Timestamp)` - for user-specific queries
  - `Timestamp` - for retention cleanup queries
- String properties: `Question` (MaxLength 500), `Response` (MaxLength 2000), `ErrorMessage` (MaxLength 1000)

**SQL Schema**:
```sql
CREATE TABLE AssistantInteractionLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Timestamp TEXT NOT NULL,
    UserId INTEGER NOT NULL,
    GuildId INTEGER NOT NULL,
    ChannelId INTEGER NOT NULL,
    MessageId INTEGER NOT NULL,
    Question TEXT NOT NULL,
    Response TEXT,
    InputTokens INTEGER NOT NULL DEFAULT 0,
    OutputTokens INTEGER NOT NULL DEFAULT 0,
    CachedTokens INTEGER NOT NULL DEFAULT 0,
    CacheCreationTokens INTEGER NOT NULL DEFAULT 0,
    CacheHit INTEGER NOT NULL DEFAULT 0,
    ToolCalls INTEGER NOT NULL DEFAULT 0,
    LatencyMs INTEGER NOT NULL DEFAULT 0,
    Success INTEGER NOT NULL DEFAULT 1,
    ErrorMessage TEXT,
    EstimatedCostUsd REAL NOT NULL DEFAULT 0.0,
    FOREIGN KEY (UserId) REFERENCES Users(Id),
    FOREIGN KEY (GuildId) REFERENCES Guilds(Id)
);

CREATE INDEX IX_AssistantInteractionLogs_GuildId_Timestamp
    ON AssistantInteractionLogs (GuildId, Timestamp);

CREATE INDEX IX_AssistantInteractionLogs_UserId_Timestamp
    ON AssistantInteractionLogs (UserId, Timestamp);

CREATE INDEX IX_AssistantInteractionLogs_Timestamp
    ON AssistantInteractionLogs (Timestamp);
```

---

### 3.2 Enum Updates

#### ConsentType
```csharp
namespace DiscordBot.Core.Enums;

public enum ConsentType
{
    MessageLogging = 1,
    AssistantUsage = 2  // NEW: Consent for AI assistant interactions
}
```

---

## 4. Interface Definitions

### 4.1 Service Interfaces

#### IAssistantService
```csharp
namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for AI assistant operations.
/// Handles Claude API interactions, tool execution, and response generation.
/// </summary>
public interface IAssistantService
{
    /// <summary>
    /// Processes a user question and returns Claude's response.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="channelId">Discord channel ID.</param>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="messageId">Discord message ID.</param>
    /// <param name="question">User's question.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Assistant response result containing the answer or error.</returns>
    Task<AssistantResponseResult> AskQuestionAsync(
        ulong guildId,
        ulong channelId,
        ulong userId,
        ulong messageId,
        string question,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the assistant is enabled for a specific guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if enabled, false otherwise.</returns>
    Task<bool> IsEnabledForGuildAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the assistant is allowed in a specific channel.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="channelId">Discord channel ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if allowed, false otherwise.</returns>
    Task<bool> IsAllowedInChannelAsync(
        ulong guildId,
        ulong channelId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has exceeded their rate limit.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Rate limit check result.</returns>
    Task<RateLimitCheckResult> CheckRateLimitAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets usage metrics for a guild on a specific date.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="date">Date to retrieve metrics for (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Usage metrics or null if not found.</returns>
    Task<AssistantUsageMetrics?> GetUsageMetricsAsync(
        ulong guildId,
        DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets usage metrics for a guild over a date range.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="startDate">Start date (inclusive, UTC).</param>
    /// <param name="endDate">End date (inclusive, UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of usage metrics ordered by date descending.</returns>
    Task<IEnumerable<AssistantUsageMetrics>> GetUsageMetricsRangeAsync(
        ulong guildId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent interaction logs for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="limit">Maximum number of logs to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of interaction logs ordered by timestamp descending.</returns>
    Task<IEnumerable<AssistantInteractionLog>> GetRecentInteractionsAsync(
        ulong guildId,
        int limit = 50,
        CancellationToken cancellationToken = default);
}
```

---

#### IDocumentationToolService
```csharp
namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for documentation tool implementations.
/// Provides Claude with access to bot documentation and command metadata.
/// </summary>
public interface IDocumentationToolService
{
    /// <summary>
    /// Gets documentation for a specific feature.
    /// </summary>
    /// <param name="featureName">Name of the feature (e.g., "soundboard", "rat-watch").</param>
    /// <param name="guildId">Guild ID for context (used for URL generation).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool execution result with documentation content or error.</returns>
    Task<ToolExecutionResult> GetFeatureDocumentationAsync(
        string featureName,
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches available commands by keyword.
    /// </summary>
    /// <param name="query">Search query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool execution result with matching commands or error.</returns>
    Task<ToolExecutionResult> SearchCommandsAsync(
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed information about a specific command.
    /// </summary>
    /// <param name="commandName">Name of the command (e.g., "ping", "rat-stats").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool execution result with command details or error.</returns>
    Task<ToolExecutionResult> GetCommandDetailsAsync(
        string commandName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all available bot features.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool execution result with feature list or error.</returns>
    Task<ToolExecutionResult> ListFeaturesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of cached documentation files for prompt caching.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping feature names to documentation content.</returns>
    Task<Dictionary<string, string>> GetCachedDocumentationAsync(
        CancellationToken cancellationToken = default);
}
```

---

#### IAssistantGuildSettingsService
```csharp
namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing assistant guild settings.
/// </summary>
public interface IAssistantGuildSettingsService
{
    /// <summary>
    /// Gets guild settings, creating default settings if none exist.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Guild settings (always non-null).</returns>
    Task<AssistantGuildSettings> GetOrCreateSettingsAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates guild settings.
    /// </summary>
    /// <param name="settings">Updated settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateSettingsAsync(
        AssistantGuildSettings settings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables the assistant for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnableAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Disables the assistant for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DisableAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);
}
```

---

### 4.2 Repository Interfaces

#### IAssistantGuildSettingsRepository
```csharp
namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for AssistantGuildSettings entities.
/// </summary>
public interface IAssistantGuildSettingsRepository : IRepository<AssistantGuildSettings>
{
    /// <summary>
    /// Gets settings for a specific guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Guild settings or null if not found.</returns>
    Task<AssistantGuildSettings?> GetByGuildIdAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all guilds with assistant enabled.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of guild settings where IsEnabled is true.</returns>
    Task<IEnumerable<AssistantGuildSettings>> GetEnabledGuildsAsync(
        CancellationToken cancellationToken = default);
}
```

---

#### IAssistantUsageMetricsRepository
```csharp
namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for AssistantUsageMetrics entities.
/// </summary>
public interface IAssistantUsageMetricsRepository : IRepository<AssistantUsageMetrics>
{
    /// <summary>
    /// Gets or creates metrics for a guild on a specific date.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="date">Date (UTC, date component only).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Existing or newly created metrics record.</returns>
    Task<AssistantUsageMetrics> GetOrCreateAsync(
        ulong guildId,
        DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metrics for a guild over a date range.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="startDate">Start date (inclusive, UTC).</param>
    /// <param name="endDate">End date (inclusive, UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of metrics ordered by date descending.</returns>
    Task<IEnumerable<AssistantUsageMetrics>> GetRangeAsync(
        ulong guildId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments metrics for a successful interaction.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="date">Date (UTC).</param>
    /// <param name="inputTokens">Input tokens consumed.</param>
    /// <param name="outputTokens">Output tokens consumed.</param>
    /// <param name="cachedTokens">Cached tokens served.</param>
    /// <param name="cacheWriteTokens">Tokens written to cache.</param>
    /// <param name="cacheHit">Whether cache was hit.</param>
    /// <param name="toolCalls">Number of tool calls.</param>
    /// <param name="latencyMs">Response latency.</param>
    /// <param name="cost">Estimated cost.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IncrementMetricsAsync(
        ulong guildId,
        DateTime date,
        int inputTokens,
        int outputTokens,
        int cachedTokens,
        int cacheWriteTokens,
        bool cacheHit,
        int toolCalls,
        int latencyMs,
        decimal cost,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments failed request count.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="date">Date (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IncrementFailedRequestAsync(
        ulong guildId,
        DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes metrics older than the specified date.
    /// </summary>
    /// <param name="cutoffDate">Delete metrics before this date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of records deleted.</returns>
    Task<int> DeleteOlderThanAsync(
        DateTime cutoffDate,
        CancellationToken cancellationToken = default);
}
```

---

#### IAssistantInteractionLogRepository
```csharp
namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for AssistantInteractionLog entities.
/// </summary>
public interface IAssistantInteractionLogRepository : IRepository<AssistantInteractionLog>
{
    /// <summary>
    /// Gets recent interaction logs for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="limit">Maximum number of logs to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of logs ordered by timestamp descending.</returns>
    Task<IEnumerable<AssistantInteractionLog>> GetRecentByGuildAsync(
        ulong guildId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent interaction logs for a user.
    /// </summary>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="limit">Maximum number of logs to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of logs ordered by timestamp descending.</returns>
    Task<IEnumerable<AssistantInteractionLog>> GetRecentByUserAsync(
        ulong userId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes interaction logs older than the specified date.
    /// </summary>
    /// <param name="cutoffDate">Delete logs before this date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of records deleted.</returns>
    Task<int> DeleteOlderThanAsync(
        DateTime cutoffDate,
        CancellationToken cancellationToken = default);
}
```

---

### 4.3 DTOs

#### AssistantResponseResult
```csharp
namespace DiscordBot.Core.DTOs;

/// <summary>
/// Result of an assistant question processing operation.
/// </summary>
public class AssistantResponseResult
{
    public bool Success { get; set; }
    public string? Response { get; set; }
    public string? ErrorMessage { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CachedTokens { get; set; }
    public int CacheCreationTokens { get; set; }
    public bool CacheHit { get; set; }
    public int ToolCalls { get; set; }
    public int LatencyMs { get; set; }
    public decimal EstimatedCostUsd { get; set; }
}
```

#### RateLimitCheckResult
```csharp
namespace DiscordBot.Core.DTOs;

/// <summary>
/// Result of a rate limit check.
/// </summary>
public class RateLimitCheckResult
{
    public bool IsAllowed { get; set; }
    public int RemainingQuestions { get; set; }
    public TimeSpan? RetryAfter { get; set; }
    public string? Message { get; set; }
}
```

#### ToolExecutionResult
```csharp
namespace DiscordBot.Core.DTOs;

/// <summary>
/// Result of a documentation tool execution.
/// </summary>
public class ToolExecutionResult
{
    public bool Success { get; set; }
    public string? Data { get; set; }
    public string? Error { get; set; }
}
```

---

## 5. Implementation Tasks by Phase

### Phase 1: Core Infrastructure (2-3 hours)

#### 1.1 Update ConsentType Enum
**File**: `src/DiscordBot.Core/Enums/ConsentType.cs`
- Add `AssistantUsage = 2`

#### 1.2 Create Entities
**Files to create:**
- `src/DiscordBot.Core/Entities/AssistantGuildSettings.cs`
- `src/DiscordBot.Core/Entities/AssistantUsageMetrics.cs`
- `src/DiscordBot.Core/Entities/AssistantInteractionLog.cs`

**Pattern**: Follow `GuildRatWatchSettings.cs` for guild settings entity

#### 1.3 Create DTOs
**Files to create:**
- `src/DiscordBot.Core/DTOs/AssistantResponseResult.cs`
- `src/DiscordBot.Core/DTOs/RateLimitCheckResult.cs`
- `src/DiscordBot.Core/DTOs/ToolExecutionResult.cs`

#### 1.4 Create Interfaces
**Files to create:**
- `src/DiscordBot.Core/Interfaces/IAssistantService.cs`
- `src/DiscordBot.Core/Interfaces/IDocumentationToolService.cs`
- `src/DiscordBot.Core/Interfaces/IAssistantGuildSettingsService.cs`
- `src/DiscordBot.Core/Interfaces/IAssistantGuildSettingsRepository.cs`
- `src/DiscordBot.Core/Interfaces/IAssistantUsageMetricsRepository.cs`
- `src/DiscordBot.Core/Interfaces/IAssistantInteractionLogRepository.cs`

#### 1.5 Add NuGet Package
```bash
cd src/DiscordBot.Bot
dotnet add package Anthropic.SDK
```

**Version**: Latest stable (verify supports prompt caching and tool use)

#### 1.6 Create EF Core Configuration
**Files to create:**
- `src/DiscordBot.Infrastructure/Data/Configurations/AssistantGuildSettingsConfiguration.cs`
- `src/DiscordBot.Infrastructure/Data/Configurations/AssistantUsageMetricsConfiguration.cs`
- `src/DiscordBot.Infrastructure/Data/Configurations/AssistantInteractionLogConfiguration.cs`

**Pattern**: Follow `GuildRatWatchSettingsConfiguration.cs`

#### 1.7 Update DbContext
**File**: `src/DiscordBot.Infrastructure/Data/BotDbContext.cs`

Add DbSets:
```csharp
public DbSet<AssistantGuildSettings> AssistantGuildSettings => Set<AssistantGuildSettings>();
public DbSet<AssistantUsageMetrics> AssistantUsageMetrics => Set<AssistantUsageMetrics>();
public DbSet<AssistantInteractionLog> AssistantInteractionLogs => Set<AssistantInteractionLog>();
```

#### 1.8 Create EF Core Migration
```bash
cd src/DiscordBot.Bot
dotnet ef migrations add AddAssistantFeature --project ../DiscordBot.Infrastructure --startup-project .
```

**Acceptance Criteria**:
- [ ] All entities compile without errors
- [ ] All interfaces defined with XML comments
- [ ] Migration generates correct SQL schema
- [ ] No breaking changes to existing tables

---

### Phase 2: Documentation Tools (3-4 hours)

#### 2.1 Implement DocumentationToolService
**File**: `src/DiscordBot.Infrastructure/Services/DocumentationToolService.cs`

**Dependencies**:
- `IOptions<AssistantOptions>` - for `DocumentationBasePath`, `ReadmePath`
- `IOptions<ApplicationOptions>` - for base URL
- `ILogger<DocumentationToolService>`

**Implementation**:
- Read markdown files from `docs/articles/`
- Parse README.md for command lists
- Generate guild-specific URLs using `{BASE_URL}/Portal/Soundboard/{GUILD_ID}` pattern
- Handle file not found gracefully (return ToolExecutionResult with error)
- Implement timeout via `CancellationTokenSource` (use `ToolExecutionTimeoutMs`)

**Methods**:
- `GetFeatureDocumentationAsync(featureName, guildId)` - reads `docs/articles/{featureName}.md`
- `SearchCommandsAsync(query)` - searches README.md and returns matching commands
- `GetCommandDetailsAsync(commandName)` - returns details for specific command
- `ListFeaturesAsync()` - lists all `.md` files in `docs/articles/`
- `GetCachedDocumentationAsync()` - loads files from `CachedDocumentationFiles` for prompt caching

#### 2.2 Create Repository Implementations
**Files to create:**
- `src/DiscordBot.Infrastructure/Repositories/AssistantGuildSettingsRepository.cs`
- `src/DiscordBot.Infrastructure/Repositories/AssistantUsageMetricsRepository.cs`
- `src/DiscordBot.Infrastructure/Repositories/AssistantInteractionLogRepository.cs`

**Pattern**: Inherit from `Repository<T>`, implement specialized methods

#### 2.3 Implement AssistantGuildSettingsService
**File**: `src/DiscordBot.Infrastructure/Services/AssistantGuildSettingsService.cs`

**Dependencies**:
- `IAssistantGuildSettingsRepository`
- `IOptions<AssistantOptions>` - for `EnabledByDefaultForNewGuilds`
- `ILogger<AssistantGuildSettingsService>`

**Implementation**:
- `GetOrCreateSettingsAsync()` - returns existing or creates with defaults
- `UpdateSettingsAsync()` - updates settings, sets `UpdatedAt = DateTime.UtcNow`
- `EnableAsync()` / `DisableAsync()` - convenience methods

#### 2.4 Unit Tests for DocumentationToolService
**File**: `tests/DiscordBot.Infrastructure.Tests/Services/DocumentationToolServiceTests.cs`

**Test cases**:
- Successfully reads existing feature documentation
- Returns error for non-existent feature
- Generates correct guild-specific URLs
- Handles timeout gracefully
- Parses README.md correctly
- Lists all features
- Caches common documentation

**Acceptance Criteria**:
- [ ] All tool methods implemented
- [ ] File reading works correctly
- [ ] URL generation includes guild ID
- [ ] Timeout handled properly
- [ ] Unit tests pass with >90% coverage
- [ ] Documentation files are readable

---

### Phase 3: Claude Integration (4-5 hours)

#### 3.1 Implement AssistantService
**File**: `src/DiscordBot.Infrastructure/Services/AssistantService.cs`

**Dependencies**:
- `IOptions<AssistantOptions>` - for all configuration
- `IDocumentationToolService` - for tool execution
- `IAssistantGuildSettingsService` - for guild settings
- `IConsentService` - for consent checks
- `IAssistantUsageMetricsRepository` - for metrics logging
- `IAssistantInteractionLogRepository` - for interaction logging
- `IMemoryCache` - for rate limiting (in-memory)
- `ILogger<AssistantService>`
- Anthropic SDK client (injected or created from options)

**Implementation**:

##### 3.1.1 AskQuestionAsync
1. Validate question length (reject if > `MaxQuestionLength`)
2. Check consent via `IConsentService.HasConsentAsync(userId, ConsentType.AssistantUsage)`
3. Check rate limit via in-memory cache
4. Load agent prompt from file (`AgentPromptPath`)
5. Build Claude request:
   - System prompt: Agent prompt + cached common docs (if `CacheCommonDocumentation` enabled)
   - User message: Question
   - Tools: Define 4 documentation tools
   - Model, MaxTokens, Temperature from options
   - Prompt caching: Mark system prompt with `cache_control: ephemeral`
6. Execute Claude API call with timeout (`ApiTimeoutMs`)
7. Handle tool calls:
   - Execute tools via `IDocumentationToolService`
   - Loop until Claude returns final response (max `MaxToolCallsPerQuestion`)
8. Truncate response if > `MaxResponseLength`
9. Calculate cost using token counts and pricing from options
10. Log interaction via `IAssistantInteractionLogRepository`
11. Update metrics via `IAssistantUsageMetricsRepository`
12. Return `AssistantResponseResult`

##### 3.1.2 Rate Limiting
- Cache key: `assistant_ratelimit:{guildId}:{userId}`
- Cache value: List of timestamps (questions asked within window)
- On check: Remove timestamps older than `RateLimitWindowMinutes`
- If count >= `DefaultRateLimit` (or `RateLimitOverride`): Deny
- Store in `IMemoryCache` with sliding expiration of `RateLimitWindowMinutes`
- Bypass for users with role >= `RateLimitBypassRole` (check via guild member roles)

##### 3.1.3 Prompt Caching
- If `EnablePromptCaching` is true:
  - Mark system prompt content with `cache_control: { type: "ephemeral" }`
  - Include cached docs from `GetCachedDocumentationAsync()` in system prompt
  - Cache is valid for 5 minutes (Claude's cache TTL)
  - Cache is shared globally across all requests
- If caching disabled:
  - Send system prompt normally without cache control
  - All docs fetched via tools only

##### 3.1.4 Error Handling
- API timeout: Retry up to `MaxRetryAttempts` with `RetryDelayMs` delay
- API error (4xx/5xx): Return `ErrorMessage` to user, log error
- Tool execution error: Continue with partial data, inform Claude tool failed
- Empty response: Return `ErrorMessage` to user, log error

#### 3.2 Update ConsentService
**File**: Check if `ConsentService` exists in Infrastructure, or if consent logic is elsewhere

**Changes**:
- Ensure `HasConsentAsync()` supports `ConsentType.AssistantUsage`
- No changes needed if consent service is generic

#### 3.3 Unit Tests for AssistantService
**File**: `tests/DiscordBot.Infrastructure.Tests/Services/AssistantServiceTests.cs`

**Test cases**:
- Successfully processes question and returns response
- Rejects question when user lacks consent
- Enforces rate limiting
- Bypasses rate limit for admins
- Truncates long responses
- Calculates cost correctly
- Logs interactions
- Updates metrics
- Handles API errors gracefully
- Executes tools correctly
- Implements prompt caching

**Mock dependencies**: Claude API client, repositories, cache, consent service

**Acceptance Criteria**:
- [ ] All service methods implemented
- [ ] Rate limiting works correctly
- [ ] Consent checks functional
- [ ] Claude API calls succeed
- [ ] Tool execution works
- [ ] Prompt caching implemented
- [ ] Metrics and logging functional
- [ ] Unit tests pass with >80% coverage
- [ ] Integration test with real Claude API (optional)

---

### Phase 4: Discord Integration (3-4 hours)

#### 4.1 Create Message Handler
**File**: `src/DiscordBot.Bot/Handlers/AssistantMessageHandler.cs`

**Pattern**: Similar to existing event handlers in Bot project

**Implementation**:
```csharp
public class AssistantMessageHandler
{
    private readonly IAssistantService _assistantService;
    private readonly IOptions<AssistantOptions> _options;
    private readonly ILogger<AssistantMessageHandler> _logger;
    private readonly DiscordSocketClient _client;

    public AssistantMessageHandler(
        IAssistantService assistantService,
        IOptions<AssistantOptions> options,
        ILogger<AssistantMessageHandler> logger,
        DiscordSocketClient client)
    {
        _assistantService = assistantService;
        _options = options;
        _logger = logger;
        _client = client;
    }

    public async Task HandleMessageReceivedAsync(SocketMessage message)
    {
        // Ignore bot messages
        if (message.Author.IsBot) return;

        // Ignore DMs (guild-only feature)
        if (message.Channel is not SocketGuildChannel guildChannel) return;

        // Check if bot is mentioned
        if (!message.MentionedUsers.Any(u => u.Id == _client.CurrentUser.Id)) return;

        // Check if globally enabled
        if (!_options.Value.GloballyEnabled) return;

        var guildId = guildChannel.Guild.Id;
        var channelId = message.Channel.Id;
        var userId = message.Author.Id;
        var messageId = message.Id;

        // Extract question (remove bot mention)
        var question = message.Content
            .Replace($"<@{_client.CurrentUser.Id}>", "")
            .Replace($"<@!{_client.CurrentUser.Id}>", "")
            .Trim();

        if (string.IsNullOrWhiteSpace(question)) return;

        // Check if enabled for guild
        if (!await _assistantService.IsEnabledForGuildAsync(guildId))
            return;

        // Check if allowed in channel
        if (!await _assistantService.IsAllowedInChannelAsync(guildId, channelId))
            return;

        // Check rate limit
        var rateLimitCheck = await _assistantService.CheckRateLimitAsync(guildId, userId);
        if (!rateLimitCheck.IsAllowed)
        {
            // Send ephemeral message (DM or reply)
            await message.Channel.SendMessageAsync(
                rateLimitCheck.Message ?? "You've reached your question limit. Please try again later.",
                messageReference: new MessageReference(messageId));
            return;
        }

        // Show typing indicator
        if (_options.Value.ShowTypingIndicator)
        {
            await message.Channel.TriggerTypingAsync();
        }

        try
        {
            // Process question
            var result = await _assistantService.AskQuestionAsync(
                guildId, channelId, userId, messageId, question);

            if (result.Success && !string.IsNullOrWhiteSpace(result.Response))
            {
                // Send response as reply
                await message.Channel.SendMessageAsync(
                    result.Response,
                    messageReference: new MessageReference(messageId));
            }
            else
            {
                // Send error message
                await message.Channel.SendMessageAsync(
                    _options.Value.ErrorMessage,
                    messageReference: new MessageReference(messageId));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing assistant question");
            await message.Channel.SendMessageAsync(
                _options.Value.ErrorMessage,
                messageReference: new MessageReference(messageId));
        }
    }
}
```

#### 4.2 Wire Up Event Handler
**File**: `src/DiscordBot.Bot/Services/BotHostedService.cs` (or wherever Discord events are wired)

**Changes**:
- Inject `AssistantMessageHandler` into `BotHostedService`
- Subscribe to `_client.MessageReceived += _assistantMessageHandler.HandleMessageReceivedAsync`
- Ensure handler is invoked for all messages

#### 4.3 Update DI Registration
**File**: `src/DiscordBot.Bot/Program.cs` (or `Startup.cs`)

**Add**:
```csharp
// Assistant feature
builder.Services.Configure<AssistantOptions>(
    builder.Configuration.GetSection(AssistantOptions.SectionName));

builder.Services.AddSingleton<AssistantMessageHandler>();
builder.Services.AddScoped<IAssistantService, AssistantService>();
builder.Services.AddScoped<IDocumentationToolService, DocumentationToolService>();
builder.Services.AddScoped<IAssistantGuildSettingsService, AssistantGuildSettingsService>();
builder.Services.AddScoped<IAssistantGuildSettingsRepository, AssistantGuildSettingsRepository>();
builder.Services.AddScoped<IAssistantUsageMetricsRepository, AssistantUsageMetricsRepository>();
builder.Services.AddScoped<IAssistantInteractionLogRepository, AssistantInteractionLogRepository>();

// Memory cache for rate limiting (if not already registered)
builder.Services.AddMemoryCache();
```

#### 4.4 Update ConsentModule
**File**: `src/DiscordBot.Bot/Commands/ConsentModule.cs`

**Changes**:
- Add `AssistantUsage` to consent options displayed in `/consent` command
- Update `/consent` autocomplete to include "assistant_usage"
- Update help text to mention AI assistant feature

**Example**:
```csharp
[SlashCommand("consent", "Manage your data consent preferences")]
public async Task ConsentAsync(
    [Summary("type", "Type of consent to grant/revoke")]
    [Autocomplete(typeof(ConsentTypeAutocompleteHandler))]
    string consentType,
    [Summary("action", "Grant or revoke consent")]
    [Choice("Grant", "grant")]
    [Choice("Revoke", "revoke")]
    string action)
{
    // Parse consent type
    var type = consentType.ToLower() switch
    {
        "message_logging" => ConsentType.MessageLogging,
        "assistant_usage" => ConsentType.AssistantUsage,
        _ => throw new ArgumentException("Invalid consent type")
    };

    // Grant or revoke
    // ... existing logic ...
}
```

#### 4.5 Integration Testing
**Manual tests**:
1. Mention bot with question in allowed channel → Receives response
2. Mention bot without consent → Receives consent prompt
3. Exceed rate limit → Receives rate limit message
4. Mention bot in disabled guild → No response
5. Mention bot in restricted channel → No response
6. Question too long → Truncated or rejected
7. API timeout → Error message shown
8. Bot shows typing indicator while waiting

**Acceptance Criteria**:
- [ ] Message handler detects bot mentions
- [ ] Consent checked before processing
- [ ] Rate limiting enforced
- [ ] Typing indicator shown
- [ ] Responses posted as replies
- [ ] Errors handled gracefully
- [ ] `/consent` command updated
- [ ] Manual tests pass

---

### Phase 5: Admin UI (3-4 hours)

#### 5.1 Create Guild Settings Page
**File**: `src/DiscordBot.Bot/Pages/Guilds/AssistantSettings.cshtml.cs`

**PageModel**:
```csharp
[Authorize(Policy = "RequireAdmin")]
public class AssistantSettingsModel : PageModel
{
    private readonly IAssistantGuildSettingsService _settingsService;
    private readonly IGuildService _guildService;

    [BindProperty(SupportsGet = true)]
    public ulong GuildId { get; set; }

    [BindProperty]
    public bool IsEnabled { get; set; }

    [BindProperty]
    public List<ulong> AllowedChannelIds { get; set; } = new();

    [BindProperty]
    public int? RateLimitOverride { get; set; }

    public Guild? Guild { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Guild = await _guildService.GetGuildByIdAsync(GuildId);
        if (Guild == null) return NotFound();

        var settings = await _settingsService.GetOrCreateSettingsAsync(GuildId);
        IsEnabled = settings.IsEnabled;
        AllowedChannelIds = settings.GetAllowedChannelIdsList();
        RateLimitOverride = settings.RateLimitOverride;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var settings = await _settingsService.GetOrCreateSettingsAsync(GuildId);
        settings.IsEnabled = IsEnabled;
        settings.SetAllowedChannelIdsList(AllowedChannelIds);
        settings.RateLimitOverride = RateLimitOverride;

        await _settingsService.UpdateSettingsAsync(settings);

        TempData["SuccessMessage"] = "Assistant settings updated successfully.";
        return RedirectToPage();
    }
}
```

**Razor Page** (`AssistantSettings.cshtml`):
- Enable/disable toggle
- Channel multi-select (fetch guild channels via Discord API)
- Rate limit override input (optional)
- Save button
- Link to usage metrics (Phase 5.2)

**Route**: `/Guilds/AssistantSettings/{guildId:long}`

#### 5.2 Create Usage Metrics Page
**File**: `src/DiscordBot.Bot/Pages/Guilds/AssistantMetrics.cshtml.cs`

**PageModel**:
```csharp
[Authorize(Policy = "RequireAdmin")]
public class AssistantMetricsModel : PageModel
{
    private readonly IAssistantService _assistantService;
    private readonly IGuildService _guildService;

    [BindProperty(SupportsGet = true)]
    public ulong GuildId { get; set; }

    public Guild? Guild { get; set; }
    public List<AssistantUsageMetrics> Metrics { get; set; } = new();
    public decimal TotalCost { get; set; }
    public int TotalQuestions { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Guild = await _guildService.GetGuildByIdAsync(GuildId);
        if (Guild == null) return NotFound();

        // Last 30 days
        var endDate = DateTime.UtcNow.Date;
        var startDate = endDate.AddDays(-30);

        Metrics = (await _assistantService.GetUsageMetricsRangeAsync(
            GuildId, startDate, endDate)).ToList();

        TotalCost = Metrics.Sum(m => m.EstimatedCostUsd);
        TotalQuestions = Metrics.Sum(m => m.TotalQuestions);

        return Page();
    }
}
```

**Razor Page** (`AssistantMetrics.cshtml`):
- Summary cards: Total questions, total cost, avg latency
- Table: Daily metrics (date, questions, cost, cache hit rate)
- Chart (optional): Cost over time (using Chart.js)

**Route**: `/Guilds/AssistantMetrics/{guildId:long}`

#### 5.3 Add Navigation Links
**Files to update**:
- `src/DiscordBot.Bot/Pages/Guilds/Details.cshtml` - Add "Assistant Settings" link
- `src/DiscordBot.Bot/Pages/Shared/_Layout.cshtml` - Add to guild dropdown (if applicable)

#### 5.4 Update Privacy Page
**File**: `src/DiscordBot.Bot/Pages/Account/Privacy.cshtml`

**Add section**:
```html
<h3>AI Assistant</h3>
<p>
    When you mention the bot with a question, your question is sent to Anthropic's Claude API
    for processing. Questions and responses are logged for debugging and cost monitoring.
    Logs are retained for 90 days.
</p>
<p>
    You must explicitly opt-in via <code>/consent</code> before using the assistant.
</p>
<p>
    Anthropic's privacy policy: <a href="https://www.anthropic.com/privacy">https://www.anthropic.com/privacy</a>
</p>
```

**Acceptance Criteria**:
- [ ] Guild settings page functional
- [ ] Usage metrics page displays data
- [ ] Navigation links added
- [ ] Privacy page updated
- [ ] Form validation works
- [ ] Success/error messages shown
- [ ] Admin authorization enforced

---

### Phase 6: Testing & Deployment (2-3 hours)

#### 6.1 Unit Tests
**Files to create/update**:
- `tests/DiscordBot.Infrastructure.Tests/Services/AssistantServiceTests.cs`
- `tests/DiscordBot.Infrastructure.Tests/Services/DocumentationToolServiceTests.cs`
- `tests/DiscordBot.Infrastructure.Tests/Services/AssistantGuildSettingsServiceTests.cs`
- `tests/DiscordBot.Infrastructure.Tests/Repositories/AssistantGuildSettingsRepositoryTests.cs`
- `tests/DiscordBot.Infrastructure.Tests/Repositories/AssistantUsageMetricsRepositoryTests.cs`
- `tests/DiscordBot.Infrastructure.Tests/Repositories/AssistantInteractionLogRepositoryTests.cs`

**Test coverage targets**:
- Services: >80%
- Repositories: >90%
- Entities: 100%

#### 6.2 Integration Tests
**Optional** (requires real Claude API key):
- Test end-to-end question flow with real Claude API
- Verify prompt caching works
- Verify tool execution works
- Verify cost calculations are accurate

#### 6.3 Manual Testing Checklist
- [ ] Bot responds to mentions in allowed channels
- [ ] Bot ignores mentions in restricted channels
- [ ] Bot ignores mentions when disabled
- [ ] Consent check blocks non-consented users
- [ ] Rate limiting enforces limits correctly
- [ ] Admin bypass works for rate limits
- [ ] Typing indicator shows while waiting
- [ ] Responses are correctly formatted (Discord markdown)
- [ ] Long responses are truncated with suffix
- [ ] Errors show friendly message
- [ ] Metrics are logged correctly
- [ ] Interaction logs are created
- [ ] Admin UI settings page works
- [ ] Admin UI metrics page shows data
- [ ] `/consent` command includes AssistantUsage

#### 6.4 Configuration
**appsettings.json**:
```json
{
  "Assistant": {
    "GloballyEnabled": false,
    "EnabledByDefaultForNewGuilds": false,
    "DefaultRateLimit": 5,
    "RateLimitWindowMinutes": 5,
    "RateLimitBypassRole": "Admin",
    "MaxQuestionLength": 500,
    "MaxResponseLength": 1800,
    "TruncationSuffix": "\n\n... *(response truncated)*",
    "Model": "claude-3-5-sonnet-20241022",
    "ApiTimeoutMs": 30000,
    "MaxTokens": 1024,
    "Temperature": 0.7,
    "AgentPromptPath": "docs/agents/assistant-agent.md",
    "DocumentationBasePath": "docs/articles",
    "ReadmePath": "README.md",
    "EnableDocumentationTools": true,
    "MaxToolCallsPerQuestion": 5,
    "ToolExecutionTimeoutMs": 5000,
    "ErrorMessage": "Oops, I'm having trouble thinking right now. Please try again in a moment.",
    "MaxRetryAttempts": 2,
    "RetryDelayMs": 1000,
    "EnableCostTracking": true,
    "DailyCostThresholdUsd": 5.00,
    "CostPerMillionInputTokens": 3.00,
    "CostPerMillionOutputTokens": 15.00,
    "EnablePromptCaching": true,
    "CacheCommonDocumentation": true,
    "CachedDocumentationFiles": [
      "commands-page.md",
      "soundboard.md",
      "rat-watch.md",
      "tts-support.md"
    ],
    "CostPerMillionCachedTokens": 0.30,
    "CostPerMillionCacheWriteTokens": 3.75,
    "RequireExplicitConsent": true,
    "LogInteractions": true,
    "InteractionLogRetentionDays": 90,
    "BaseUrl": null,
    "ShowTypingIndicator": true,
    "IncludeGuildContext": true
  }
}
```

**User Secrets** (development):
```bash
cd src/DiscordBot.Bot
dotnet user-secrets set "Claude:ApiKey" "sk-ant-api-key-here"
```

**Production**:
- Set `Claude:ApiKey` via environment variable or Azure Key Vault
- Set `Assistant:GloballyEnabled` to `false` initially
- Enable for test guild only via admin UI

#### 6.5 Database Migration
```bash
cd src/DiscordBot.Bot
dotnet ef database update --project ../DiscordBot.Infrastructure --startup-project .
```

#### 6.6 Deployment Steps
1. **Pre-deployment**:
   - [ ] Merge feature branch to main
   - [ ] Run all tests (`dotnet test`)
   - [ ] Review migration SQL
   - [ ] Backup production database
   - [ ] Set Claude API key in production secrets

2. **Deployment**:
   - [ ] Deploy code to production
   - [ ] Apply database migration
   - [ ] Verify migration applied correctly
   - [ ] Keep `GloballyEnabled: false`

3. **Post-deployment**:
   - [ ] Enable assistant for test guild via admin UI
   - [ ] Test with real Discord messages
   - [ ] Monitor logs for errors
   - [ ] Monitor costs via metrics page
   - [ ] Run for 1 week with test guild only
   - [ ] Review cost vs. budget ($5/day threshold)
   - [ ] If stable, gradually enable for more guilds

**Rollback plan**:
- Disable globally via appsettings (`GloballyEnabled: false`)
- Revert code deployment if critical bugs
- Restore database backup if migration fails

**Acceptance Criteria**:
- [ ] All unit tests pass
- [ ] Integration tests pass (if applicable)
- [ ] Manual testing checklist complete
- [ ] Configuration validated
- [ ] Migration applied successfully
- [ ] Deployed with feature disabled by default
- [ ] Test guild enabled and monitored
- [ ] Costs within budget after 1 week
- [ ] No critical errors in logs

---

## 6. File Manifest

### New Files to Create

#### Core Layer (`src/DiscordBot.Core`)
```
Entities/
  AssistantGuildSettings.cs
  AssistantUsageMetrics.cs
  AssistantInteractionLog.cs

DTOs/
  AssistantResponseResult.cs
  RateLimitCheckResult.cs
  ToolExecutionResult.cs

Interfaces/
  IAssistantService.cs
  IDocumentationToolService.cs
  IAssistantGuildSettingsService.cs
  IAssistantGuildSettingsRepository.cs
  IAssistantUsageMetricsRepository.cs
  IAssistantInteractionLogRepository.cs

Configuration/
  AssistantOptions.cs (ALREADY EXISTS)

Enums/
  ConsentType.cs (UPDATE EXISTING - add AssistantUsage = 2)
```

#### Infrastructure Layer (`src/DiscordBot.Infrastructure`)
```
Services/
  AssistantService.cs
  DocumentationToolService.cs
  AssistantGuildSettingsService.cs

Repositories/
  AssistantGuildSettingsRepository.cs
  AssistantUsageMetricsRepository.cs
  AssistantInteractionLogRepository.cs

Data/Configurations/
  AssistantGuildSettingsConfiguration.cs
  AssistantUsageMetricsConfiguration.cs
  AssistantInteractionLogConfiguration.cs

Data/
  BotDbContext.cs (UPDATE EXISTING - add DbSets)
```

#### Bot Layer (`src/DiscordBot.Bot`)
```
Handlers/
  AssistantMessageHandler.cs

Pages/Guilds/
  AssistantSettings.cshtml
  AssistantSettings.cshtml.cs
  AssistantMetrics.cshtml
  AssistantMetrics.cshtml.cs

Commands/
  ConsentModule.cs (UPDATE EXISTING - add AssistantUsage)

Pages/Account/
  Privacy.cshtml (UPDATE EXISTING - add AI assistant section)

Pages/Guilds/
  Details.cshtml (UPDATE EXISTING - add assistant settings link)

Program.cs (UPDATE EXISTING - add DI registrations)

Services/
  BotHostedService.cs (UPDATE EXISTING - wire up message handler)
```

#### Tests (`tests/DiscordBot.Infrastructure.Tests`)
```
Services/
  AssistantServiceTests.cs
  DocumentationToolServiceTests.cs
  AssistantGuildSettingsServiceTests.cs

Repositories/
  AssistantGuildSettingsRepositoryTests.cs
  AssistantUsageMetricsRepositoryTests.cs
  AssistantInteractionLogRepositoryTests.cs
```

#### Documentation
```
docs/requirements/
  assistant-requirements.md (ALREADY EXISTS)
  assistant-implementation-plan.md (THIS FILE)

docs/agents/
  assistant-agent.md (ALREADY EXISTS)
```

**Total**: ~35 new files, ~10 files to update

---

## 7. Configuration Updates

### 7.1 appsettings.json
Add complete `Assistant` section (see Phase 6.4 Configuration above)

### 7.2 User Secrets
```bash
dotnet user-secrets set "Claude:ApiKey" "sk-ant-..."
```

### 7.3 CLAUDE.md
Add section:
```markdown
### AI Assistant Configuration

Claude API key must be configured via User Secrets:

```bash
cd src/DiscordBot.Bot
dotnet user-secrets set "Claude:ApiKey" "your-api-key-here"
```

See [assistant-requirements.md](docs/requirements/assistant-requirements.md) for feature documentation.
```

### 7.4 README.md
Add to commands list:
```markdown
### AI Assistant
- **Mention @DiskordBott with question** - Ask questions about bot features
- `/consent` - Opt-in to AI assistant (required before use)
```

---

## 8. Testing Strategy

### 8.1 Unit Test Requirements

#### AssistantService
- Question processing with successful response
- Question processing with API error
- Rate limit enforcement
- Rate limit bypass for admins
- Consent check blocks non-consented users
- Question length validation
- Response truncation
- Cost calculation accuracy
- Metrics logging
- Interaction logging
- Tool execution loop
- Prompt caching implementation
- Retry logic on timeout

#### DocumentationToolService
- Read existing feature documentation
- Handle non-existent feature gracefully
- Search commands by keyword
- Get command details
- List all features
- Generate guild-specific URLs
- Load cached documentation
- Handle timeout

#### AssistantGuildSettingsService
- Get or create settings with defaults
- Update settings
- Enable/disable guild

#### Repositories
- CRUD operations
- Specialized query methods
- Metrics increment logic
- Retention cleanup

### 8.2 Integration Test Scenarios
1. **End-to-end question flow** (with mock Claude API):
   - User mentions bot → Consent checked → Rate limit checked → Claude called → Response posted
2. **Tool execution** (with mock Claude API):
   - Claude requests tool → Tool executed → Data returned → Claude generates response
3. **Rate limiting across multiple requests**:
   - User asks 5 questions → 6th question blocked
4. **Metrics aggregation**:
   - Multiple questions → Metrics updated correctly

### 8.3 Manual Testing
See Phase 6.3 Manual Testing Checklist

---

## 9. Deployment Checklist

### Pre-Deployment
- [ ] All unit tests pass (`dotnet test`)
- [ ] Code review completed
- [ ] Migration SQL reviewed
- [ ] Production database backed up
- [ ] Claude API key configured in production secrets
- [ ] Documentation updated (CLAUDE.md, README.md)

### Deployment
- [ ] Deploy code to production server
- [ ] Apply database migration: `dotnet ef database update`
- [ ] Verify migration success via database inspection
- [ ] Restart bot service
- [ ] Verify bot connects to Discord
- [ ] Keep `GloballyEnabled: false` in appsettings

### Post-Deployment
- [ ] Enable assistant for test guild only (via admin UI)
- [ ] Send test mention to bot
- [ ] Verify response received
- [ ] Check Serilog logs for errors
- [ ] Monitor metrics page for cost
- [ ] Monitor for 1 week
- [ ] Review daily costs vs. $5 threshold
- [ ] If stable and within budget, enable for more guilds
- [ ] Document any issues in GitHub

### Monitoring Checklist (First Week)
- [ ] Daily cost < $5/day
- [ ] No API errors in logs
- [ ] Response latency < 10 seconds average
- [ ] Cache hit rate > 50%
- [ ] No rate limit abuse
- [ ] User feedback positive

---

## 10. Risk Mitigation

### Technical Risks

#### Risk: Claude API Costs Exceed Budget
**Likelihood**: Medium
**Impact**: High
**Mitigation**:
- Default disabled (`GloballyEnabled: false`)
- Strict rate limiting (5 questions per 5 minutes)
- Daily cost threshold alert ($5/day)
- Monitor metrics daily during first week
- Disable globally if costs spike

#### Risk: Claude API Downtime/Errors
**Likelihood**: Low
**Impact**: Medium
**Mitigation**:
- Retry logic with exponential backoff
- Friendly error messages to users
- Fallback to disabled state if error rate > 25%
- Log all errors for investigation
- No impact on other bot features (async/await isolation)

#### Risk: Prompt Injection/Jailbreak
**Likelihood**: Medium
**Impact**: Medium
**Mitigation**:
- Extensive security guidelines in agent prompt
- Never expose internal data, API keys, or implementation details
- Log all interactions for audit
- Review logs weekly for suspicious activity
- Update agent prompt if vulnerabilities found

#### Risk: Rate Limiting Insufficient
**Likelihood**: Low
**Impact**: Medium
**Mitigation**:
- Start with conservative limit (5 per 5 min)
- Monitor abuse patterns in logs
- Adjust limits per guild if needed
- Admin bypass for testing

#### Risk: Documentation Files Missing/Unreadable
**Likelihood**: Low
**Impact**: Low
**Mitigation**:
- Graceful error handling in DocumentationToolService
- Return error to Claude, continue with partial data
- Unit tests verify all expected files exist
- Hardcoded fallback if agent prompt file missing

#### Risk: Database Migration Fails
**Likelihood**: Low
**Impact**: High
**Mitigation**:
- Test migration on dev/staging first
- Backup production database before migration
- Review migration SQL before applying
- Rollback plan: Restore database backup

#### Risk: Anthropic.SDK Breaking Changes
**Likelihood**: Low
**Impact**: Medium
**Mitigation**:
- Pin SDK version in project file
- Review release notes before upgrading
- Test with new SDK version before deploying
- Maintain compatibility with current SDK features (prompt caching, tool use)

#### Risk: Consent System Not Updated
**Likelihood**: Low
**Impact**: Medium
**Mitigation**:
- Update ConsentType enum first
- Update ConsentModule in same commit
- Test consent flow manually before deployment
- Document consent requirement in Privacy page

---

## 11. Success Criteria

### MVP Launch (Week 1)
- [ ] Feature deployed with no critical bugs
- [ ] Bot responds to mentions correctly
- [ ] Rate limiting prevents abuse
- [ ] Consent checks functional
- [ ] Error rate < 5%
- [ ] Average response time < 10 seconds
- [ ] Daily costs < $5/day
- [ ] Admin UI functional

### Post-Launch (30 Days)
- [ ] >50 questions answered
- [ ] Cache hit rate > 50%
- [ ] User feedback collected (informal)
- [ ] No security incidents (prompt injection, data leaks)
- [ ] Total cost < $150/month
- [ ] Identify most common questions (for FAQ optimization)
- [ ] Consider expanding to more guilds

---

## 12. Next Steps After Implementation

1. **Monitor Usage Patterns**:
   - Identify most common questions
   - Optimize cached documentation based on usage
   - Adjust rate limits based on abuse patterns

2. **Iterate on Agent Prompt**:
   - Refine based on response quality feedback
   - Add more security guidelines if needed
   - Improve response tone/formatting

3. **Performance Optimization**:
   - Measure cache hit rate, optimize caching strategy
   - Consider response caching for common questions (future feature)
   - Optimize tool execution performance

4. **Future Features** (if MVP successful):
   - Conversation history (multi-turn conversations)
   - Slash command interface (`/ask <question>`)
   - Dedicated analytics dashboard
   - Per-guild custom prompts
   - Thread-based conversations
   - Response caching

---

**End of Implementation Plan**

This plan provides a comprehensive roadmap for implementing the AI Assistant Agent feature. Follow the phases sequentially, complete acceptance criteria for each phase, and refer to existing patterns in the codebase for consistency.
