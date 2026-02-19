# Recurring Patterns and Conventions

Quick reference guide for common patterns and conventions used throughout the Discord bot project. Use this when implementing new features to maintain consistency.

## Table of Contents

1. [DI Registration](#di-registration)
2. [Configuration](#configuration)
3. [Discord Commands](#discord-commands)
4. [Razor Pages](#razor-pages)
5. [Data Access](#data-access)
6. [Authorization](#authorization)
7. [Audit Logging](#audit-logging)
8. [Error Handling](#error-handling)
9. [MonitoredBackgroundService](#monitoredbackgroundservice)
10. [IMemoryReportable](#imemoryreportable)
11. [Per-Guild Locking](#per-guild-locking)

---

## DI Registration

Register services via `IServiceCollection` extension methods in `Extensions/` folder.

### Pattern Structure

```csharp
namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering [feature] services.
/// </summary>
public static class [Feature]ServiceExtensions
{
    /// <summary>
    /// Adds all [feature] services to the service collection.
    /// </summary>
    public static IServiceCollection Add[Feature](
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind and validate options
        services.Configure<[OptionsClass]>(
            configuration.GetSection([OptionsClass].SectionName));

        // Register services by lifetime
        // Singletons: shared state, caching, connection pooling
        // Scoped: per-request, user-specific data
        // Transient: stateless, thread-safe utilities

        return services;
    }
}
```

### Service Lifetime Conventions

| Lifetime | Usage | Example |
|----------|-------|---------|
| **Singleton** | Application-wide state, caches, connection pools | `IPlaybackService`, `IVoxClipLibrary`, `ISoundCacheService` |
| **Scoped** | Per-request operations, user/guild-specific data | `ISoundService`, `IVoxService`, `IBotService` |
| **Transient** | Stateless utilities, thread-safe builders | `ISsmlBuilder`, `ISsmlValidator` |
| **Hosted Service** | Background tasks, startup initialization | `VoxClipLibraryInitializer`, `VoiceAutoLeaveService` |

### Real Example: VoiceServiceExtensions.cs

```csharp
public static IServiceCollection AddVox(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Configure options from appsettings
    services.Configure<VoxOptions>(
        configuration.GetSection(VoxOptions.SectionName));

    // Singleton for shared library state
    services.AddSingleton<IVoxClipLibrary, VoxClipLibrary>();

    // Singleton for stateless audio processing
    services.AddSingleton<IVoxConcatenationService, VoxConcatenationService>();

    // Scoped for per-request orchestration
    services.AddScoped<IVoxService, VoxService>();

    // Hosted service for initialization
    services.AddHostedService<VoxClipLibraryInitializer>();

    return services;
}
```

### Pattern: Composite Registration

Combine multiple related service groups:

```csharp
public static IServiceCollection AddVoiceSupport(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddVoiceCore(configuration);
    services.AddSoundboard(configuration);
    services.AddTts(configuration);
    services.AddVox(configuration);
    return services;
}
```

---

## Configuration

Use the Options pattern (`IOptions<T>`) for all configuration.

### Creating Options Classes

```csharp
namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for [feature].
/// </summary>
public class [Feature]Options
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "[FeatureName]";

    /// <summary>
    /// Gets or sets [property description].
    /// </summary>
    public string Property { get; set; } = "default_value";
}
```

### Real Example: VoxOptions

```csharp
public class VoxOptions
{
    public const string SectionName = "Vox";

    public string BasePath { get; set; } = "./sounds";
    public int DefaultWordGapMs { get; set; } = 50;
    public int MaxMessageWords { get; set; } = 50;
    public int MaxMessageLength { get; set; } = 500;
}
```

### Using Configuration in Services

```csharp
public class VoxModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IOptions<VoxOptions> _voxOptions;

    public VoxModule(IOptions<VoxOptions> voxOptions)
    {
        _voxOptions = voxOptions;
    }

    public async Task VoxAsync(string message, int? gap = null)
    {
        // Access value via .Value
        var wordGapMs = gap ?? _voxOptions.Value.DefaultWordGapMs;
        // ...
    }
}
```

### Real Example: AudioCacheOptions

```csharp
public class AudioCacheOptions
{
    public const string SectionName = "AudioCache";

    public bool Enabled { get; set; } = true;
    public string CachePath { get; set; } = "./cache/audio";
    public long MaxCacheSizeBytes { get; set; } = 524_288_000; // 500MB
    public int MaxEntries { get; set; } = 1000;
    public int EntryTtlHours { get; set; } = 168; // 7 days
    public int MaxCacheDurationSeconds { get; set; } = 60;
    public int CleanupIntervalMinutes { get; set; } = 60;
}
```

### appsettings.json Structure

```json
{
  "Vox": {
    "BasePath": "./sounds",
    "DefaultWordGapMs": 50,
    "MaxMessageWords": 50,
    "MaxMessageLength": 500
  },
  "AudioCache": {
    "Enabled": true,
    "CachePath": "./cache/audio",
    "MaxCacheSizeBytes": 524288000,
    "MaxEntries": 1000,
    "EntryTtlHours": 168,
    "MaxCacheDurationSeconds": 60,
    "CleanupIntervalMinutes": 60
  }
}
```

---

## Discord Commands

Use `InteractionModuleBase<SocketInteractionContext>` for slash command organization.

### Module Structure

```csharp
namespace DiscordBot.Bot.Commands;

/// <summary>
/// Slash command module for [feature] commands.
/// Provides descriptions of what users can do.
/// </summary>
[RequireGuildActive]
[RequireAudioEnabled]
[RateLimit(5, 10)]  // 5 invocations per 10 seconds
public class [Feature]Module : InteractionModuleBase<SocketInteractionContext>
{
    private readonly I[Feature]Service _service;
    private readonly IOptions<[Feature]Options> _options;
    private readonly ILogger<[Feature]Module> _logger;

    public [Feature]Module(
        I[Feature]Service service,
        IOptions<[Feature]Options> options,
        ILogger<[Feature]Module> logger)
    {
        _service = service;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Slash command that does something.
    /// </summary>
    [SlashCommand("command-name", "User-friendly description")]
    [RequireVoiceChannel]
    public async Task CommandAsync(
        [Summary("param", "Parameter description")]
        [MaxLength(500)]
        string input)
    {
        var guildId = Context.Guild.Id;
        var userId = Context.User.Id;

        _logger.LogInformation(
            "Command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            userId,
            Context.Guild.Name,
            guildId);

        await DeferAsync(ephemeral: true);

        try
        {
            var result = await _service.DoSomethingAsync(guildId, input);

            if (!result.Success)
            {
                _logger.LogError("Operation failed: {Error}", result.ErrorMessage);
                await FollowupAsync(text: result.ErrorMessage, ephemeral: true);
                return;
            }

            await FollowupAsync(text: "Success!", ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in command");
            await FollowupAsync(text: "An error occurred.", ephemeral: true);
        }
    }
}
```

### Real Example: VoxModule

```csharp
[RequireGuildActive]
[RequireAudioEnabled]
[RateLimit(5, 10)]
public class VoxModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IVoxService _voxService;
    private readonly IOptions<VoxOptions> _voxOptions;
    private readonly ILogger<VoxModule> _logger;

    public VoxModule(
        IVoxService voxService,
        IOptions<VoxOptions> voxOptions,
        ILogger<VoxModule> logger)
    {
        _voxService = voxService;
        _voxOptions = voxOptions;
        _logger = logger;
    }

    [SlashCommand("vox", "Play a VOX group announcement")]
    [RequireVoiceChannel]
    public async Task VoxAsync(
        [Summary("message", "The message to play")]
        [MaxLength(500)]
        [Autocomplete(typeof(VoxClipAutocompleteHandler))]
        string message,
        [Summary("gap", "Word gap in milliseconds (20-200)")]
        [MinValue(20)]
        [MaxValue(200)]
        int? gap = null)
    {
        await PlayVoxAsync(message, VoxClipGroup.Vox, gap);
    }

    private async Task PlayVoxAsync(string message, VoxClipGroup group, int? gap)
    {
        // Implementation follows standard error handling pattern
    }
}
```

### Preconditions

Apply validation attributes to control access:

```csharp
[RequireGuildActive]      // Bot must be active for guild
[RequireAudioEnabled]     // Audio features must be enabled
[RequireVoiceChannel]     // User must be in voice channel
[RequireModerator]        // User must have moderator role
[RateLimit(5, 10)]        // 5 calls per 10 seconds
public class VoxModule : InteractionModuleBase<SocketInteractionContext>
{
    // ...
}
```

---

## Razor Pages

Organize page models with consistent structure.

### Page Model Pattern

```csharp
namespace DiscordBot.Bot.Pages.[Feature];

/// <summary>
/// Page model for the [Feature] management page.
/// Displays [what the page shows].
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class IndexModel : PageModel
{
    private readonly I[Feature]Service _service;
    private readonly IGuildService _guildService;
    private readonly IOptions<[Feature]Options> _options;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        I[Feature]Service service,
        IGuildService guildService,
        IOptions<[Feature]Options> options,
        ILogger<IndexModel> logger)
    {
        _service = service;
        _guildService = guildService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Guild ID from the route.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public ulong GuildId { get; set; }

    /// <summary>
    /// Guild name for display.
    /// </summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>
    /// Data to display on the page.
    /// </summary>
    public List<DisplayItem> Items { get; set; } = new();

    /// <summary>
    /// Success message from TempData (cross-request).
    /// </summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// Handles GET requests to display the page.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(long guildId, CancellationToken cancellationToken = default)
    {
        GuildId = (ulong)guildId;
        _logger.LogInformation("User accessing feature for guild {GuildId}", GuildId);

        try
        {
            // Fetch guild and data
            var guild = await _guildService.GetGuildByIdAsync(GuildId, cancellationToken);
            if (guild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found", GuildId);
                return NotFound();
            }

            GuildName = guild.Name;
            Items = await _service.GetItemsAsync(GuildId, cancellationToken);

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load page for guild {GuildId}", GuildId);
            ErrorMessage = "Failed to load page. Please try again.";
            return Page();
        }
    }

    /// <summary>
    /// Handles POST requests to perform an action.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing form submission for guild {GuildId}", GuildId);

        try
        {
            await _service.DoSomethingAsync(GuildId, cancellationToken);
            SuccessMessage = "Operation completed successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process form for guild {GuildId}", GuildId);
            ErrorMessage = "Operation failed. Please try again.";
        }

        return RedirectToPage("Index", new { guildId = GuildId });
    }
}
```

### Real Example: VOX Index Page

```csharp
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class IndexModel : PageModel
{
    private readonly IVoxClipLibrary _voxClipLibrary;
    private readonly IGuildService _guildService;
    private readonly IGuildAudioSettingsRepository _audioSettingsRepository;
    private readonly VoxOptions _voxOptions;
    private readonly ILogger<IndexModel> _logger;

    [BindProperty(SupportsGet = true)]
    public ulong GuildId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string GroupFilter { get; set; } = "all";

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public const int PageSize = 50;

    public List<VoxClipInfo> FilteredClips { get; set; } = new();
    public int TotalPages { get; set; }

    public async Task<IActionResult> OnGetAsync(long guildId, CancellationToken cancellationToken = default)
    {
        GuildId = (ulong)guildId;
        // ... load and filter clips
        return Page();
    }
}
```

### Pagination Pattern

```csharp
public const int PageSize = 50;

[BindProperty(SupportsGet = true)]
public int PageNumber { get; set; } = 1;

public int TotalPages { get; set; }

// In OnGetAsync:
TotalPages = (int)Math.Ceiling(TotalClipCount / (double)PageSize);

// Ensure page is within bounds
if (PageNumber < 1) PageNumber = 1;
if (PageNumber > TotalPages && TotalPages > 0) PageNumber = TotalPages;

// Apply pagination
var items = allItems
    .Skip((PageNumber - 1) * PageSize)
    .Take(PageSize)
    .ToList();
```

---

## Data Access

Use the generic `Repository<T>` base class with specialized repositories for custom queries.

### Generic Repository Pattern

```csharp
namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Generic repository implementation providing basic CRUD operations.
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly BotDbContext Context;
    protected readonly DbSet<T> DbSet;
    protected readonly ILogger<Repository<T>> Logger;

    public Repository(BotDbContext context, ILogger<Repository<T>> logger)
    {
        Context = context;
        DbSet = context.Set<T>();
        Logger = logger;
    }

    public virtual async Task<T?> GetByIdAsync(
        object id,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            "GetByIdAsync", typeof(T).Name, "SELECT", id?.ToString());

        try
        {
            return await DbSet.FindAsync(new[] { id }, cancellationToken);
        }
        catch (Exception ex)
        {
            InfrastructureActivitySource.RecordException(activity, ex);
            Logger.LogError(ex, "Repository operation failed");
            throw;
        }
    }

    public virtual async Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    public virtual async Task<T> AddAsync(
        T entity,
        CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public virtual async Task UpdateAsync(
        T entity,
        CancellationToken cancellationToken = default)
    {
        DbSet.Update(entity);
        await Context.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task DeleteAsync(
        T entity,
        CancellationToken cancellationToken = default)
    {
        DbSet.Remove(entity);
        await Context.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(predicate, cancellationToken);
    }

    public virtual async Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        return predicate == null
            ? await DbSet.CountAsync(cancellationToken)
            : await DbSet.CountAsync(predicate, cancellationToken);
    }
}
```

### Specialized Repository Pattern

```csharp
/// <summary>
/// Repository implementation for [Entity] entities with specialized querying.
/// </summary>
public class [Entity]Repository : Repository<[Entity]>, I[Entity]Repository
{
    private readonly ILogger<[Entity]Repository> _logger;

    public [Entity]Repository(
        BotDbContext context,
        ILogger<[Entity]Repository> logger,
        ILogger<Repository<[Entity]>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<[Entity]?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching [Entity] by key: {Key}", key);
        return await DbSet.FirstOrDefaultAsync(e => e.Key == key, cancellationToken);
    }

    public async Task<IReadOnlyList<[Entity]>> GetByGuildAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(e => e.GuildId == guildId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
```

### Real Example: AuditLogRepository

Specialized repository with query filtering and pagination:

```csharp
public async Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetLogsAsync(
    AuditLogQueryDto query,
    CancellationToken cancellationToken = default)
{
    var queryable = DbSet.AsNoTracking();

    // Apply filters
    if (query.Category.HasValue)
        queryable = queryable.Where(l => l.Category == query.Category.Value);
    if (query.Action.HasValue)
        queryable = queryable.Where(l => l.Action == query.Action.Value);
    if (!string.IsNullOrWhiteSpace(query.ActorId))
        queryable = queryable.Where(l => l.ActorId == query.ActorId);

    // Get total count before pagination
    var totalCount = await queryable.CountAsync(cancellationToken);

    // Apply sorting and pagination
    queryable = query.SortBy.ToLowerInvariant() switch
    {
        "category" => query.SortDescending
            ? queryable.OrderByDescending(l => l.Category)
            : queryable.OrderBy(l => l.Category),
        _ => queryable.OrderByDescending(l => l.Timestamp)
    };

    var items = await queryable
        .Skip((query.Page - 1) * query.PageSize)
        .Take(query.PageSize)
        .ToListAsync(cancellationToken);

    return (items, totalCount);
}
```

### Performance Tracing Pattern

All repository methods use OpenTelemetry activity tracing:

```csharp
public virtual async Task<T?> GetByIdAsync(
    object id,
    CancellationToken cancellationToken = default)
{
    using var activity = InfrastructureActivitySource.StartRepositoryActivity(
        operationName: "GetByIdAsync",
        entityType: _entityTypeName,
        dbOperation: "SELECT",
        entityId: id?.ToString());

    var stopwatch = Stopwatch.StartNew();

    try
    {
        var result = await DbSet.FindAsync(new[] { id }, cancellationToken);
        InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);
        return result;
    }
    catch (Exception ex)
    {
        InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);
        throw;
    }
}
```

---

## Authorization

Use policy-based authorization with role hierarchy and guild-specific checks.

### Policy Registration Pattern

```csharp
namespace DiscordBot.Bot.Extensions;

public static class IdentityServiceExtensions
{
    public static IServiceCollection AddIdentityServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ... identity configuration ...

        services.AddAuthorizationPolicies();

        // Register custom authorization handlers
        services.AddScoped<IAuthorizationHandler, GuildAccessHandler>();

        return services;
    }

    private static IServiceCollection AddAuthorizationPolicies(
        this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Hierarchical role policies
            options.AddPolicy("RequireSuperAdmin", policy =>
                policy.RequireRole("SuperAdmin"));

            options.AddPolicy("RequireAdmin", policy =>
                policy.RequireRole("SuperAdmin", "Admin"));

            options.AddPolicy("RequireModerator", policy =>
                policy.RequireRole("SuperAdmin", "Admin", "Moderator"));

            // Guild-specific authorization (requires custom handler)
            options.AddPolicy("GuildAccess", policy =>
                policy.Requirements.Add(new GuildAccessRequirement()));

            // Fallback - require authentication for all pages by default
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}
```

### Role Hierarchy

```
SuperAdmin (all access)
├── Admin (guild management, user management)
│   └── Moderator (user moderation, logs)
│       └── Viewer (read-only access)
```

### Using Policies in Pages

```csharp
[Authorize(Policy = "RequireAdmin")]      // Must be Admin or SuperAdmin
[Authorize(Policy = "GuildAccess")]       // Must have access to this guild
public class IndexModel : PageModel
{
    // ...
}
```

### Using Preconditions in Commands

```csharp
[RequireModerator]                        // User must have moderator role
[RequireGuildActive]                      // Guild must not be disabled
[RateLimit(10, 60)]                       // 10 uses per 60 seconds
public class ModerationModule : InteractionModuleBase<SocketInteractionContext>
{
    // ...
}
```

---

## Audit Logging

Use the fluent builder API via `IAuditLogService.CreateBuilder()` for audit log creation. Inject `IAuditLogService` into any service or page model that needs to record an audit entry.

### Using Audit Logs in Code

Obtain a builder from the service, chain the desired configuration, then terminate with `LogAsync()` (awaits confirmation) or `Enqueue()` (fire-and-forget via background queue).

```csharp
// Await confirmation that the entry was written
await _auditLogService.CreateBuilder()
    .ForCategory(AuditLogCategory.User)
    .WithAction(AuditLogAction.UserBanned)
    .ByUser(banningUserId)
    .OnTarget("User", bannedUserId)
    .InGuild(guildId)
    .WithDetails(new { reason = reason, username = username })
    .LogAsync();

// Fire-and-forget via background queue (high-performance path)
_auditLogService.CreateBuilder()
    .ForCategory(AuditLogCategory.Command)
    .WithAction(AuditLogAction.CommandExecuted)
    .ByUser(userId)
    .InGuild(guildId)
    .WithDetails(new { commandName = "vox", input = message })
    .Enqueue();

// System-initiated action (no human actor)
await _auditLogService.CreateBuilder()
    .ForCategory(AuditLogCategory.System)
    .WithAction(AuditLogAction.BotStarted)
    .BySystem()
    .LogAsync();

// Include IP address for web-originated actions
await _auditLogService.CreateBuilder()
    .ForCategory(AuditLogCategory.Security)
    .WithAction(AuditLogAction.Login)
    .ByUser(userId)
    .FromIpAddress(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown")
    .LogAsync();

// Group related entries with a correlation ID
var correlationId = Guid.NewGuid().ToString();
await _auditLogService.CreateBuilder()
    .ForCategory(AuditLogCategory.Configuration)
    .WithAction(AuditLogAction.SettingChanged)
    .ByUser(userId)
    .InGuild(guildId)
    .WithCorrelationId(correlationId)
    .LogAsync();
```

### Fluent Builder API Reference

| Method | Signature | Description |
|--------|-----------|-------------|
| `ForCategory` | `ForCategory(AuditLogCategory)` | Sets the log category (required) |
| `WithAction` | `WithAction(AuditLogAction)` | Sets the specific action performed (required) |
| `ByUser` | `ByUser(string userId)` | Actor is a human user; sets `ActorType = User` |
| `BySystem` | `BySystem()` | Actor is an automated process; sets `ActorType = System` |
| `ByBot` | `ByBot()` | Actor is the Discord bot; sets `ActorType = Bot` |
| `OnTarget` | `OnTarget(string targetType, string targetId)` | Entity that was affected |
| `InGuild` | `InGuild(ulong guildId)` | Guild context for the action |
| `WithDetails` | `WithDetails(object)` or `WithDetails(Dictionary<string, object?>)` | Arbitrary JSON-serialized metadata |
| `FromIpAddress` | `FromIpAddress(string)` | Originating IP (web interface actions) |
| `WithCorrelationId` | `WithCorrelationId(string)` | Groups related log entries for tracing |
| `LogAsync` | `Task LogAsync(CancellationToken)` | Writes the entry and waits for confirmation |
| `Enqueue` | `void Enqueue()` | Pushes to background queue; does not await |

### Audit Log Categories and Actions

```csharp
public enum AuditLogCategory
{
    User = 1,           // User-related actions (login, profile updates, ban, kick)
    Guild = 2,          // Guild-related actions (settings, channel management)
    Configuration = 3,  // Bot settings and feature toggles
    Security = 4,       // Permission changes, role modifications
    Command = 5,        // Slash command execution
    Message = 6,        // Message deletion and editing
    System = 7          // Bot startup, shutdown, internal errors
}

public enum AuditLogAction
{
    Created = 1,            // A new entity was created
    Updated = 2,            // An existing entity was updated
    Deleted = 3,            // An entity was deleted
    Login = 4,              // A user logged in
    Logout = 5,             // A user logged out
    PermissionChanged = 6,  // Permissions changed for a user or role
    SettingChanged = 7,     // A configuration setting was changed
    CommandExecuted = 8,    // A command was executed
    MessageDeleted = 9,     // A message was deleted
    MessageEdited = 10,     // A message was edited
    UserBanned = 11,        // A user was banned from a guild
    UserUnbanned = 12,      // A user was unbanned from a guild
    UserKicked = 13,        // A user was kicked from a guild
    RoleAssigned = 14,      // A role was assigned to a user
    RoleRemoved = 15,       // A role was removed from a user
    BotStarted = 16,        // The Discord bot started
    BotStopped = 17,        // The Discord bot stopped
    BotConnected = 18,      // The bot connected to the Discord gateway
    BotDisconnected = 19,   // The bot disconnected from the Discord gateway
    UserDataPurged = 20,    // User data purged (GDPR right to be forgotten)
    BulkDataPurged = 21,    // Bulk data purge executed
    UserDataExported = 22   // User data exported (GDPR right of access)
}

public enum AuditLogActorType
{
    User,
    Bot,
    System
}
```

---

## Error Handling

Follow consistent error handling across services, commands, and pages.

### Service Error Handling Pattern

```csharp
public class [Feature]Service : I[Feature]Service
{
    private readonly ILogger<[Feature]Service> _logger;

    public async Task<ServiceResult<T>> DoSomethingAsync(/* params */)
    {
        try
        {
            _logger.LogInformation("Starting operation");

            // Validate input
            if (/* validation fails */)
            {
                return ServiceResult<T>.Failure("Validation error message");
            }

            // Perform operation
            var result = await PerformOperationAsync();

            _logger.LogInformation("Operation completed successfully");
            return ServiceResult<T>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            // Expected business logic exceptions
            _logger.LogWarning(ex, "Operation failed due to invalid state");
            return ServiceResult<T>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            // Unexpected exceptions
            _logger.LogError(ex, "Unexpected error during operation");
            return ServiceResult<T>.Failure("An unexpected error occurred.");
        }
    }
}
```

### Command Error Handling Pattern

```csharp
public async Task CommandAsync(/* params */)
{
    var guildId = Context.Guild.Id;
    _logger.LogInformation("Command executing in guild {GuildId}", guildId);

    await DeferAsync(ephemeral: true);

    try
    {
        var result = await _service.DoSomethingAsync(guildId, /* params */);

        if (!result.Success)
        {
            _logger.LogError("Operation failed: {Error}", result.ErrorMessage);
            await FollowupAsync(
                text: result.ErrorMessage ?? "An unknown error occurred.",
                ephemeral: true);
            return;
        }

        _logger.LogInformation("Command completed successfully");
        await FollowupAsync(text: "Success!", ephemeral: true);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error in command for guild {GuildId}", guildId);
        await FollowupAsync(
            text: "An error occurred while processing your request. Please try again later.",
            ephemeral: true);
    }
}
```

### Page Model Error Handling Pattern

```csharp
public async Task<IActionResult> OnGetAsync(long guildId, CancellationToken cancellationToken = default)
{
    GuildId = (ulong)guildId;
    _logger.LogInformation("Loading page for guild {GuildId}", GuildId);

    try
    {
        var guild = await _guildService.GetGuildByIdAsync(GuildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", GuildId);
            return NotFound();
        }

        // Load data
        var data = await _service.GetDataAsync(GuildId, cancellationToken);

        return Page();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to load page for guild {GuildId}", GuildId);
        ErrorMessage = "Failed to load page. Please try again.";
        return Page();
    }
}
```

### ServiceResult Pattern

```csharp
/// <summary>
/// Result object for service operations with success/failure indication.
/// </summary>
public class ServiceResult<T>
{
    public bool Success { get; private set; }
    public T? Data { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static ServiceResult<T> Success(T data) =>
        new() { Success = true, Data = data };

    public static ServiceResult<T> Failure(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}

public class ServiceResult
{
    public bool Success { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static ServiceResult Success() =>
        new() { Success = true };

    public static ServiceResult Failure(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}
```

### Logging Levels

| Level | Usage |
|-------|-------|
| **Trace** | Very detailed diagnostic info; rate limit checks, cache hits |
| **Debug** | Detailed info for debugging; method entry/exit, variable values |
| **Information** | Notable events; command execution, successful operations |
| **Warning** | Potential issues; slow operations, rate limit violations |
| **Error** | Error conditions; failed operations, exceptions |
| **Critical** | Critical failures; system-wide issues |

---

## Common Gotchas

### Discord ID Precision in JavaScript

Always treat Discord IDs (64-bit integers) as strings in JavaScript/Razor:

```razor
<!-- WRONG - loses precision -->
window.guildId = @Model.GuildId;

<!-- CORRECT - preserves all digits -->
window.guildId = '@Model.GuildId';
```

### Defer Before Long Operations

Always defer command responses before performing operations:

```csharp
await DeferAsync(ephemeral: true);  // Deferred at start

try
{
    // Long-running operation
    var result = await _service.LongOperationAsync();

    // Use FollowupAsync, not RespondAsync
    await FollowupAsync(text: "Done!", ephemeral: true);
}
```

### Configuration Validation

Always validate options at startup using `ValidateOnStart()`:

```csharp
services.AddOptions<MyOptions>()
    .Bind(configuration.GetSection(MyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();  // Fail fast on startup
```

### AsNoTracking for Read-Only Queries

Use `AsNoTracking()` for better performance when you don't need to update:

```csharp
var items = await DbSet
    .AsNoTracking()  // Don't track for change detection
    .Where(/* filter */)
    .ToListAsync();
```

---

## MonitoredBackgroundService

All periodic and event-driven background services extend `MonitoredBackgroundService` instead of `BackgroundService` directly. The base class handles health registry integration, heartbeat tracking, status management, and structured error recording automatically.

### Class Hierarchy

```
IHostedService
  └── BackgroundService (Microsoft.Extensions.Hosting)
        └── MonitoredBackgroundService (Bot/Services)
              └── YourService
```

### What the Base Class Provides

| Member | Description |
|--------|-------------|
| `ServiceName` (abstract) | Display name used in health registry and logs |
| `ExecuteMonitoredAsync` (abstract) | Override this instead of `ExecuteAsync`; your service loop goes here |
| `UpdateHeartbeat()` | Call periodically in your loop to record the last-alive timestamp |
| `RecordError(Exception)` | Records error message and sets Status to "Error"; service keeps running |
| `RecordError(string)` | Records a string error message; service keeps running |
| `ClearError()` | Clears error state and returns Status to "Running" |
| `SetStatus(string)` | Sets a custom status string (e.g., "Syncing", "Processing") |
| `Status` | Current status string: "Initializing" → "Running" / "Error" → "Stopped" |
| `LastHeartbeat` | DateTime? of the last `UpdateHeartbeat()` call |
| `LastError` | Most recent error message, or null if no error |

### Lifecycle

`ExecuteAsync` is sealed. The base class:

1. Yields immediately on startup to avoid blocking `IHostedService.StartAsync`.
2. Lazily resolves `IBackgroundServiceHealthRegistry` and calls `Register(ServiceName, this)`.
3. Sets Status to `"Running"` and calls `ExecuteMonitoredAsync(stoppingToken)`.
4. On `OperationCanceledException`: logs graceful stop (normal shutdown path).
5. On any other exception: records the error, sets Status to `"Error"`, re-throws.
6. In `finally`: sets Status to `"Stopped"`, calls `Unregister(ServiceName)`.

### Implementation Pattern

```csharp
public class MyAggregationService : MonitoredBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public override string ServiceName => "My Aggregation Service";

    public MyAggregationService(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        ILogger<MyAggregationService> logger)
        : base(serviceProvider, logger)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                SetStatus("Processing");

                await using var scope = _scopeFactory.CreateAsyncScope();
                var repo = scope.ServiceProvider.GetRequiredService<IMyRepository>();
                await repo.AggregateAsync(stoppingToken);

                UpdateHeartbeat();
                ClearError();
                SetStatus("Running");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Aggregation failed");
                RecordError(ex);
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
```

### Registration

Register as a hosted service in an `IServiceCollection` extension method:

```csharp
services.AddHostedService<MyAggregationService>();
```

The service self-registers with `IBackgroundServiceHealthRegistry` at startup. Dashboard components read health status from the registry.

### Known Implementors

All services listed in the [Background Services section of the Service Catalog](service-catalog.md#background-services) (except `BotHostedService`, which manages bot lifecycle directly) extend `MonitoredBackgroundService`.

---

## IMemoryReportable

Services that hold significant in-memory state implement `IMemoryReportable` to expose their approximate memory footprint to the diagnostics subsystem. `MemoryDiagnosticsService` collects reports from all registered implementors and aggregates them for the admin dashboard.

### Interface Contract

```csharp
// src/DiscordBot.Core/Interfaces/IMemoryReportable.cs
public interface IMemoryReportable
{
    /// <summary>Gets the display name for this service in memory reports.</summary>
    string ServiceName { get; }

    /// <summary>Gets the estimated memory usage of this service.</summary>
    ServiceMemoryReportDto GetMemoryReport();
}
```

`ServiceMemoryReportDto` carries the service name, estimated bytes used, entry counts, and any additional metadata the service wants to surface.

### Implementation Pattern

```csharp
public class MyHistoryService : IMyHistoryService, IMemoryReportable
{
    private readonly ConcurrentQueue<MyEntry> _history = new();

    public string ServiceName => "My History Service";

    public ServiceMemoryReportDto GetMemoryReport()
    {
        var entries = _history.Count;
        // Estimate: fixed overhead per entry
        var estimatedBytes = entries * 128L;

        return new ServiceMemoryReportDto
        {
            ServiceName = ServiceName,
            EstimatedBytes = estimatedBytes,
            EntryCount = entries
        };
    }
}
```

### Registration

Register the service for both its own interface and `IMemoryReportable`:

```csharp
services.AddSingleton<MyHistoryService>();
services.AddSingleton<IMyHistoryService>(sp => sp.GetRequiredService<MyHistoryService>());
services.AddSingleton<IMemoryReportable>(sp => sp.GetRequiredService<MyHistoryService>());
```

`MemoryDiagnosticsService` resolves `IEnumerable<IMemoryReportable>` and calls `GetMemoryReport()` on each.

### Known Implementors

| Service | What it tracks |
|---------|---------------|
| `CpuHistoryService` | CPU sample ring buffer |
| `LatencyHistoryService` | Latency measurement history |
| `InteractionStateService` | Active multi-step interaction state per user |
| `RaidDetectionService` | Per-guild join event windows for raid detection |
| `SpamDetectionService` | Per-guild message frequency tracking windows |
| `DiscordClientMemoryReporter` | Discord.Net socket client cache footprint |

---

## Per-Guild Locking

Services that manage guild-specific mutable state use a `ConcurrentDictionary<ulong, SemaphoreSlim>` to provide per-guild exclusive access without blocking unrelated guilds. This avoids a single global lock becoming a bottleneck across many concurrent guilds.

### Rationale

Discord bots serve many guilds simultaneously. A global lock on shared audio or detection state would serialize all guild operations. Per-guild semaphores ensure:

- Guild A's playback does not block Guild B's playback.
- Concurrent operations within the same guild are safely serialized.
- Memory overhead is proportional to the number of active guilds.

### Pattern

```csharp
public class MyGuildService : IMyGuildService
{
    // One semaphore per guild, created on first access
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _guildLocks = new();

    private SemaphoreSlim GetGuildLock(ulong guildId)
        => _guildLocks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));

    public async Task DoExclusiveOperationAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var guildLock = GetGuildLock(guildId);
        await guildLock.WaitAsync(cancellationToken);
        try
        {
            // Guild-exclusive work here
        }
        finally
        {
            guildLock.Release();
        }
    }
}
```

### Cleanup Consideration

Semaphores for guilds the bot has left can accumulate over time. Services with long lifetimes (singletons) should remove entries when a guild disconnect event is received:

```csharp
if (_guildLocks.TryRemove(guildId, out var removedLock))
{
    removedLock.Dispose();
}
```

### Known Implementors

| Service | Protected state |
|---------|----------------|
| `AudioService` | Voice channel connection state per guild |
| `PlaybackService` | Sound playback queue and active stream per guild |
| `SpamDetectionService` | Message frequency windows per guild |
| `RaidDetectionService` | Join event detection windows per guild |

---

## See Also

- [Component API Documentation](../articles/component-api.md) - UI component library
- [Authorization Policies Documentation](../articles/authorization-policies.md) - Detailed auth setup
- [Audit Log System](../articles/audit-log-system.md) - Comprehensive audit logging guide
- [Form Implementation Standards](../articles/form-implementation-standards.md) - Form handling patterns
- [Database Schema](../articles/database-schema.md) - Entity relationships and structure
