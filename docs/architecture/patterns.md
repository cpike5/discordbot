# Recurring Patterns and Conventions

Quick reference guide for common patterns and conventions used throughout the Discord bot project. Use this when implementing new features to maintain consistency.

## Table of Contents

1. [DI Registration](#di-registration)
2. [Configuration](#configuration)
3. [Discord Commands](#discord-commands)
4. [Razor Pages](#razor-pages)
5. [Guild Page Model Base](#guild-page-model-base)
6. [API Controller Base](#api-controller-base)
7. [Search Provider Pattern](#search-provider-pattern)
8. [Agent Tool Authoring](#agent-tool-authoring)
9. [Agent Skills](#agent-skills)
10. [Helper Extraction Pattern](#helper-extraction-pattern)
11. [Background Task Runner](#background-task-runner)
12. [Service Activity Helper](#service-activity-helper)
13. [Discord Resolver Services](#discord-resolver-services)
14. [Data Access](#data-access)
15. [Authorization](#authorization)
16. [Audit Logging](#audit-logging)
17. [Error Handling](#error-handling)
18. [MonitoredBackgroundService](#monitoredbackgroundservice)
19. [IMemoryReportable](#imemoryreportable)
20. [Per-Guild Locking](#per-guild-locking)
21. [Blazor Components](#blazor-components)
22. [Real-time event bus](#real-time-event-bus)

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

Organize page models with consistent structure. **Guild pages should use `GuildPageModelBase` instead of raw `PageModel` to provide standardized guild context setup.**

### Page Model Pattern

```csharp
namespace DiscordBot.Bot.Pages.[Feature];

/// <summary>
/// Page model for the [Feature] management page.
/// Displays [what the page shows].
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class IndexModel : GuildPageModelBase
{
    private readonly I[Feature]Service _service;
    private readonly IOptions<[Feature]Options> _options;

    public IndexModel(
        I[Feature]Service service,
        IGuildService guildService,
        IDiscordChannelResolver channelResolver,
        IOptions<[Feature]Options> options,
        ILogger<IndexModel> logger)
        : base(guildService, channelResolver, logger)
    {
        _service = service;
        _options = options.Value;
    }

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
        if (!await PopulateGuildLayout(guildId))
            return NotFound();

        _logger.LogInformation("User accessing feature for guild {GuildId}", GuildId);

        try
        {
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

## Guild Page Model Base

Guild pages inherit from `GuildPageModelBase` which provides standardized guild context setup via `PopulateGuildLayout()`. This base class handles guild validation, channel resolution, and consistent error handling across all guild-scoped pages.

### Basic Pattern

```csharp
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class IndexModel : GuildPageModelBase
{
    private readonly IGuildService _guildService;
    private readonly IDiscordChannelResolver _channelResolver;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IGuildService guildService,
        IDiscordChannelResolver channelResolver,
        ILogger<IndexModel> logger)
        : base(guildService, channelResolver, logger) { }

    public async Task<IActionResult> OnGetAsync(long guildId)
    {
        if (!await PopulateGuildLayout(guildId))
            return NotFound();

        // page-specific logic here
        return Page();
    }
}
```

### Paginated Guild Pages

For guild pages with pagination, use `PaginatedGuildPageModel` which extends `GuildPageModelBase` with automatic page/pageSize binding:

```csharp
public class IndexModel : PaginatedGuildPageModel
{
    private readonly IMyService _service;

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public List<Item> Items { get; set; } = new();
    public int TotalPages { get; set; }

    public IndexModel(
        IGuildService guildService,
        IDiscordChannelResolver channelResolver,
        IMyService service,
        ILogger<IndexModel> logger)
        : base(guildService, channelResolver, logger)
    {
        _service = service;
    }

    public async Task<IActionResult> OnGetAsync(long guildId)
    {
        if (!await PopulateGuildLayout(guildId))
            return NotFound();

        const int pageSize = 50;
        var (items, total) = await _service.GetPagedAsync(GuildId, PageNumber, pageSize);
        Items = items;
        TotalPages = (int)Math.Ceiling(total / (double)pageSize);

        return Page();
    }
}
```

### Inherited Members

| Member | Type | Description |
|--------|------|-------------|
| `GuildId` | `ulong` | Guild ID (set by `PopulateGuildLayout`) |
| `GuildName` | `string` | Guild name from Discord (set by `PopulateGuildLayout`) |
| `PopulateGuildLayout()` | `async Task<bool>` | Loads guild data; returns false if guild not found |

### Usage Count

24 guild pages use this pattern throughout the admin dashboard.

---

## API Controller Base

API controllers inherit from `ApiControllerBase` for consistent error response handling, status codes, and pagination response formatting.

### Pattern

```csharp
[ApiController]
[Route("api/[controller]")]
public class CommandLogsController : ApiControllerBase
{
    private readonly ICommandLogRepository _commandLogRepository;
    private readonly ILogger<CommandLogsController> _logger;

    public CommandLogsController(
        ICommandLogRepository commandLogRepository,
        ILogger<CommandLogsController> logger)
    {
        _commandLogRepository = commandLogRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync(int page = 1, int pageSize = 50)
    {
        try
        {
            var (logs, total) = await _commandLogRepository.GetPaginatedAsync(page, pageSize);
            return OkPaginated(logs, page, pageSize, total);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch command logs");
            return ServerError("Failed to fetch command logs");
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetByIdAsync(int id)
    {
        var log = await _commandLogRepository.GetByIdAsync(id);
        if (log == null)
            return NotFoundError("Command log not found");

        return Ok(log);
    }
}
```

### Inherited Helper Methods

| Method | Returns | Purpose |
|--------|---------|---------|
| `NotFoundError(string message)` | `NotFoundObjectResult` | Returns 404 with error payload |
| `BadRequestError(string message)` | `BadRequestObjectResult` | Returns 400 with error payload |
| `ServerError(string message)` | `ObjectResult` | Returns 500 with error payload |
| `OkPaginated(items, page, pageSize, total)` | `OkObjectResult` | Returns 200 with pagination metadata |

### Usage Count

5 controllers use this pattern.

---

## Search Provider Pattern

Search functionality is decomposed into focused `ISearchProvider` implementations, each handling a specific domain. `SearchService` orchestrates all providers and aggregates results.

### Provider Interface

```csharp
public interface ISearchProvider
{
    /// <summary>
    /// Unique category name for this provider (e.g., "Audit Logs", "Commands").
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Searches for items matching the query within this provider's domain.
    /// </summary>
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, SearchContext context);
}
```

### Search Result

```csharp
public class SearchResult
{
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Url { get; set; } = string.Empty;
    public float Score { get; set; }  // Relevance score (0-1)
    public DateTime? Timestamp { get; set; }
}
```

### Provider Registration

All providers are registered in DI and injected into `SearchService` as `IEnumerable<ISearchProvider>`:

```csharp
services.AddScoped<ISearchProvider, AuditLogSearchProvider>();
services.AddScoped<ISearchProvider, CommandLogSearchProvider>();
services.AddScoped<ISearchProvider, CommandSearchProvider>();
services.AddScoped<ISearchProvider, GuildSearchProvider>();
services.AddScoped<ISearchProvider, MessageLogSearchProvider>();
services.AddScoped<ISearchProvider, PageSearchProvider>();
services.AddScoped<ISearchProvider, ReminderSearchProvider>();
services.AddScoped<ISearchProvider, ScheduledMessageSearchProvider>();
services.AddScoped<ISearchProvider, UserSearchProvider>();
```

### Built-in Providers

Nine providers are included:

| Provider | Category | Searches |
|----------|----------|----------|
| `AuditLogSearchProvider` | Audit Logs | Audit log entries by actor, target, action |
| `CommandLogSearchProvider` | Command Logs | Command execution history |
| `CommandSearchProvider` | Commands | Registered slash commands |
| `GuildSearchProvider` | Guilds | Guild names and settings |
| `MessageLogSearchProvider` | Message Logs | Logged messages by content |
| `PageSearchProvider` | Pages | Admin dashboard pages |
| `ReminderSearchProvider` | Reminders | Reminders by title and content |
| `ScheduledMessageSearchProvider` | Scheduled Messages | Scheduled messages by content |
| `UserSearchProvider` | Users | Users by name, username, or ID |

---

## Agent Tool Authoring

A tool the AI assistant can call is **one file** in `Infrastructure/Services/LLM/Tools/` (or
`Bot/Services/LLM/Tools/` when it needs Discord.NET), plus one line in `ToolCatalog`. No provider
class, no DI registration, no static definitions file.

### The interface

```csharp
public interface IAgentTool
{
    LlmToolDefinition Definition { get; }   // what the model sees
    string? Mutation => null;               // null = read-only
    Task<ToolExecutionResult> InvokeAsync(
        JsonElement input, ToolContext context, CancellationToken cancellationToken = default);
}
```

### A complete tool

```csharp
public sealed class GetNoteTool : IAgentTool
{
    // Static: read once per run and serialized at position 0 of the request.
    private static readonly LlmToolDefinition ToolDefinition = new()
    {
        Name = "get_note",
        Description = "Retrieves a specific note by its ID. Use this when you need the full "
            + "content of a particular note.",
        InputSchema = ToolInput.ObjectSchema(
            new { note_id = ToolInput.Schema("integer", "The ID of the note to retrieve.") },
            "note_id")
    };

    private readonly IDmAssistantNoteRepository _notes;

    public GetNoteTool(IDmAssistantNoteRepository notes) => _notes = notes;

    public LlmToolDefinition Definition => ToolDefinition;

    public async Task<ToolExecutionResult> InvokeAsync(
        JsonElement input, ToolContext context, CancellationToken cancellationToken = default)
    {
        var noteId = ToolInput.GetLong(input, "note_id");
        if (noteId is null)
        {
            return ToolResults.Error("Missing required parameter: note_id");
        }

        var note = await _notes.GetByIdAsync(noteId.Value, context.UserId, cancellationToken);

        return note is null
            ? ToolResults.NotFound($"Note with ID {noteId.Value} not found.")
            : ToolResults.Json(new { id = note.Id, content = note.Content });
    }
}
```

### Three things that used to be separate rules

Each of these is now something the shape does for you rather than something to remember:

| Rule | How it is now enforced |
|------|------------------------|
| Add a `ToolCatalog` entry | The catalogue *routes* the tool. `CataloguedAgentToolProvider` keeps only the tools whose `ToolScopes` include its surface, so an uncatalogued tool is advertised nowhere — and logged as a warning saying so. |
| Check `ToolContext.CanMutate` before writing | Declare `Mutation` — a phrase that follows "isn't allowed to", e.g. `"save notes"`. `AgentToolProvider` refuses the call with `ToolPermissions.MutationForbidden` **before entering the tool**, so a write cannot half-run. |
| Report an expected failure so `ToolOutcomes.Classify` counts it | `ToolResults.Error` / `.NotFound` emit the top-level `error`/`found` keys the classifier reads. Using the helpers is the convention. |

### Arguments are untrusted

A tool argument arrives from the model, and the model's input is a Discord message from any user in
any guild where the assistant is enabled. The prompt is not a control over that — it is advice to a
component that can be talked to — so whatever an argument *selects* is validated in the tool: a file
path, a row the caller may not own, a guild that is not this one.

Refuse with the **same** payload a legitimate miss returns, and put the reason in a `Warning` log
line. A distinct "rejected" answer tells whoever wrote the message behind the call that their probe
was understood, which is the one thing a probe should not learn; the operator needs to know and the
prober does not. `get_feature_documentation` is the worked example — an allow-list on the name before
it becomes a file name, `Path.GetFullPath` containment behind it, one payload for absent and refused
alike ([tool page](../tools/get_feature_documentation.md)).

### The helpers

All in `DiscordBot.Agents`:

- **`ToolInput`** — `GetString`/`GetInt`/`GetLong`/`GetUInt64`/`GetBool`/`GetStringArray(input, key)`,
  each null when the property is missing, null, blank or the wrong kind, because those are one
  answer to a tool. `GetInt(input, key, fallback, min, max)` is the shape every `limit` wants.
  Quoted numbers are read as numbers — models do that, and snowflakes exceed JavaScript's safe
  integer range so they arrive quoted routinely. `Schema(type, description, …)` and
  `ObjectSchema(properties, required…)` build the input schema instead of a hand-written JSON string.
- **`ToolResults`** — `Json`, `Error`, `NotFound`, `Truncated`, `Forbidden`, `Failed`. Everything
  except `Failed` is a *successful* result: an expected failure has to reach the model as readable
  content, because `CreateError` prefixes `Error: ` on the wire and reads as a malfunction to retry
  around. `Failed` is for a genuine malfunction.
- **`ToolJson.Compact`** — the serializer settings: no naming policy (the names in the code are the
  names on the wire, so write payload members in `snake_case`) and nulls dropped, so an optional
  field costs nothing when absent.

### Registration

`AddAgentTools(assemblies, configuration)` scans for `IAgentTool` implementations and registers each
one scoped, ordered by full type name, through `TryAddEnumerable` so both assistant registrations
can call it. `AssistantServiceExtensions` and `DmAssistantServiceExtensions` both do, over
`AgentToolAssemblies.All` (Infrastructure and Bot).

Two adapters expose the scanned tools to the two registries, and are the only DI lines involved:

```csharp
services.AddScoped<IToolProvider, GuildAgentToolProvider>();    // AddAssistant
services.AddScoped<IDmToolProvider, DmAgentToolProvider>();     // AddDmAssistant
```

A tool that should ship dark carries `[OptInTool("Section:Enabled")]` and is registered only when
that configuration flag is true — a disabled tool then costs nothing rather than costing a class
and a runtime branch.

### What the build checks for you

`ToolContractTests` (`tests/DiscordBot.Tests/Services/LLM/`) runs over **every** tool this
application registers — the `IAgentTool`s and the hand-written providers' static definitions alike,
found by reflection so a tool added tomorrow is covered without anyone adding it to a list. It
asserts the house rules:

| Rule | Why it is a red build rather than a code review note |
|------|------------------------------------------------------|
| Name matches `^[a-z][a-z0-9_]{2,63}$` and is unique | The registry's walk is first-match; two tools answering to one name means which one runs depends on registration order. |
| Description is 40–600 characters | The floor catches the one-liner the model cannot choose by, the ceiling the essay that is paid for on every request. |
| `InputSchema` is a `type: "object"` schema, every `required` name is a declared property, every property has a `description` | The model cannot supply a property it was never shown, and guesses at one it cannot read. |
| A `ToolCatalog` entry exists — and every catalogue entry has an implementation | The catalogue routes the tool; without an entry it reaches no surface, and an entry with nothing behind it puts a dead tick box on the settings page. |
| A tool declaring `Mutation` is refused when `CanMutate` is false | Asserted through the real `AgentToolProvider`, so it is the mechanism that is checked and not a restatement of it. |
| A missing required argument comes back through `ToolResults`, classified `failed_result` | An expected failure reported any other way is invisible in the traces, and expected failures are most of what is worth seeing there. |
| `Definition` is readable without the constructor having run | It is serialized at position 0 of every request; one built from injected state can differ between two runs, and a differing schema is a cold prompt cache. |

`SkillContractTests` does the same for the skill files that ship: every tool a skill names must exist
**and** be advertised on that skill's own surface. At runtime a name the surface does not have is
dropped with a `Debug` line, because on the guild surface an allow-list excluding one is routine —
which means a typo and a correct exclusion look identical in production. Here they do not.

### Specify it in `docs/tools/`

One page per tool: purpose, dependencies, the exact model-facing description, an input table, and
**every** result shape — success and each expected failure, each marked `failed_result` where
`ToolOutcomes.Classify` counts it as one. The template is in
[`docs/tools/README.md`](../tools/README.md).

Write the page when you touch the tool, not in one sitting: a page written alongside a change is
accurate for the same reason the change is, and 29 pages written in an afternoon are 29 pages nobody
keeps true. New tools get one; the rest are backfilled as they are next changed.

### What a tool costs

Every tool's schema is serialized at position 0 of every request on its surface, needed or not.
`PromptSurface.Measure` puts a number on that, and two places report it:

- **At startup**, one line per surface — tools advertised, schema characters, estimated tokens, and
  the three largest tools by name.
- **On a guild's Assistant Metrics page**, a per-tool table with each tool's share of the prefix,
  marking the ones this guild has turned off and the ones a skill is holding back.

Both count what a surface *advertises* — the skill-aware set from `SkillToolSet.Compose` over the
run's registry — not everything `IToolRegistry.GetEnabledTools()` holds. A tool behind an unloaded
skill is registered, is callable once the skill loads, and is not in the prefix now.

Read them together with the Tool Usage table above them: a tool that is 15% of every request and was
called twice last month belongs behind a skill or turned off, and that is a decision with a number
attached rather than an argument.

### When to write an `IToolProvider` instead

`IToolProvider` is still the contract the registry speaks, and still the right shape when several
tools share expensive state — a cached Discord lookup, a compiled index — that is worth constructing
once for the group. It is the wrong shape for the common case of one capability over one service,
which is what this pattern replaces. The eleven existing providers are converted opportunistically,
when they are next being touched anyway, not in one sitting.

**When converting one, delete its DI registration in the same change.** A registry advertises every
registered provider's tools without de-duplicating, so a provider left registered beside its own
converted tools puts each name in the array twice — and the duplicate schema is paid for on every
request in the run.

---

## Agent Skills

A **skill** is how a tool stops costing its schema on every request. It is a markdown file naming
some tools and carrying the instructions for using them; the model sees only a one-line summary
until it decides a request needs the skill, and pays for the rest then.

Skill files live in `docs/agents/skills/<surface>/` — `dm/` and `guild/`, one directory per
assistant, configured by `DmAssistant:SkillsPath` and `Assistant:Tools:SkillsPath`. The format and
the authoring advice are in [`docs/agents/skills/README.md`](../agents/skills/README.md); this
section is the mechanism.

### The rule

> A tool named by **any** available skill is hidden until one of the skills naming it is loaded.

Everything else the registry holds is advertised as it always was. So a surface keeps its common
tools always-on and puts only the rare, heavy ones behind a skill — putting a frequently needed tool
in a skill file makes every request that needs it cost an extra round.

### The pieces

| Piece | Where | Does |
|-------|-------|------|
| `AgentSkill` | `Agents/Contracts/` | Key, summary, tool names, instructions. |
| `SkillFile` | `Agents/` | Parses one markdown file. `summary` is the only required key. |
| `SkillLibrary` (`ISkillLibrary`) | `Agents/` | Reads a directory, through `IPromptTemplate` so a skill is cached and hot-reloaded exactly like a prompt. |
| `SkillSession` (`ISkillActivationState`) | `Agents/` | One run's available and activated skills. |
| `SkillToolSet.Compose` | `Agents/` | Applies the rule above. `AgentRunner` calls it once at the start and again after any round that activated a skill. |
| `SkillRoster.Append` | `Agents/` | The prompt block: one line per loadable skill, plus the full instructions of anything pre-activated. |
| `SkillSessionFactory` (`ISkillSessionFactory`) | `Infrastructure/Services/LLM/` | Builds the run's session and **narrows** each skill's tools to what the run's registry advertises. |
| `LoadSkillTool` | `Infrastructure/Services/LLM/Tools/` | The `load_skill` tool. An ordinary `IAgentTool` with an ordinary `ToolCatalog` entry. |
| `DmSkillActivationStore` (`IDmSkillActivationStore`) | `Infrastructure/Services/LLM/` | Remembers a DM user's activations between turns, in `IMemoryCache`. |

The session is reachable from both ends of the mechanism: the loop reads it off
`AgentContext.Skills`, and the tool reads the same instance out of `ToolContext.Items` via
`SkillToolContextExtensions`. A context factory puts it in both places.

### Two things that are not negotiable

**A skill can never widen reach.** `SkillToolSet.Compose` builds the advertised set from
`IToolRegistry.GetEnabledTools()` and only ever subtracts from it, and `SkillSessionFactory` narrows
each skill's tool list to the same registry first. On the guild surface that registry is a
`FilteredToolRegistry` over the guild's allow-list, so a skill file naming a tool a guild has turned
off cannot turn it back on — the name is dropped, and the model is never told it existed.

**Loading costs a prompt-cache write.** The tool array serializes at position 0 of the request, so
re-composing it invalidates the cached prefix for the rest of that run. That is one cache write on a
loading turn, and it is the reason the loop re-composes only when the activated set actually changed.
Expect a cache-miss spike after a `load_skill` on the metrics page; it is the mechanism working.

### Stickiness differs by surface, and that is the whole design

- **DM assistant** — multi-turn. `DmAssistantContextFactory` replays the previous turn's activations
  into the session, so from turn 2 the skill's tools are advertised on the first call and its
  instructions are already in the prompt (the tool result that carried them the first time is not in
  the sliding-window history, which is why `SkillRoster` re-renders them). A skill is paid for once.
- **Guild assistant** — single-turn by design: `ConversationHistory` is always empty, so there is no
  previous turn to replay. A skill there costs a round **every** time it is used. That is still the
  right trade for a rare, heavy tool group and the wrong one for anything else, which is why
  `docs/agents/skills/guild/` ships empty.

### Adding a skill

One file in the right directory, and nothing else — no catalogue entry, no DI, no code. It is picked
up within the prompt cache's five minutes. The tools it names must already exist and be advertised on
that surface; a name that is not is dropped with a `Debug` line rather than an error, because on the
guild surface an allow-list excluding one is routine.

---

## Helper Extraction Pattern

Reusable logic extracted from command modules and services into static or injectable helpers. This reduces duplication and improves testability.

### Overview

| Helper | Purpose | Injected as | Used By |
|--------|---------|-------------|---------|
| `EmbedHelper` | Standardized embed factory (Error, Success, Info, Confirmation, EmptyState) | `IEmbedHelper` | 12 command modules |
| `PaginationHelper` | Page calculation and navigation button generation | `IPaginationHelper` | Command modules with lists |
| `VoiceChannelHelper` | Voice channel validation and state checks | `IVoiceChannelHelper` | Voice command modules |
| `SearchDisplayHelper` | Search result formatting and truncation | `ISearchDisplayHelper` | SearchService |
| `SearchScoringHelper` | Search result relevance scoring and ranking | `ISearchScoringHelper` | Search providers |

### Example: EmbedHelper

```csharp
public interface IEmbedHelper
{
    EmbedBuilder CreateErrorEmbed(string message);
    EmbedBuilder CreateSuccessEmbed(string message);
    EmbedBuilder CreateInfoEmbed(string title, string description);
    EmbedBuilder CreateConfirmationEmbed(string message);
    EmbedBuilder CreateEmptyStateEmbed(string message);
}

// Usage in command module
var embed = _embedHelper.CreateSuccessEmbed("Operation completed!");
await RespondAsync(embed: embed.Build(), ephemeral: true);
```

---

## Background Task Runner

Fire-and-forget tasks use `IBackgroundTaskRunner` instead of raw `Task.Run()` or `_ = MethodAsync()` expressions. This provides centralized logging, error handling, and proper cancellation token support.

### Pattern

```csharp
public class MyService
{
    private readonly IBackgroundTaskRunner _backgroundTaskRunner;

    public MyService(IBackgroundTaskRunner backgroundTaskRunner)
    {
        _backgroundTaskRunner = backgroundTaskRunner;
    }

    public async Task ProcessAsync(long guildId)
    {
        // Do synchronous work immediately
        var result = await SomeLongRunningWorkAsync(guildId);

        // Queue followup work without blocking
        _backgroundTaskRunner.Enqueue(
            async ct => await _notifier.NotifyAsync(guildId, ct),
            "notify-guild-update");
    }
}
```

### Interface

```csharp
public interface IBackgroundTaskRunner
{
    /// <summary>
    /// Enqueues a task to run in the background with proper logging and error handling.
    /// </summary>
    /// <param name="task">The async task to execute</param>
    /// <param name="taskName">Human-readable name for logging</param>
    void Enqueue(Func<CancellationToken, Task> task, string taskName);
}
```

### Benefits

- Centralizes error handling for background work
- Logs task start/completion/failure with correlation IDs
- Supports graceful shutdown via `CancellationToken`
- Prevents silent failures from dangling tasks

### Usage Count

13 call sites migrated from raw `Task.Run` and `_ = MethodAsync()` patterns.

---

## Service Activity Helper

Standardized tracing boilerplate via `ServiceActivityHelper` reduces ~757 lines of manual `Activity` creation, tagging, and exception recording across services.

### Pattern

```csharp
using var activity = ServiceActivityHelper.StartActivity("MyService.DoSomethingAsync");

try
{
    // Service logic
    var result = await DoWorkAsync();

    ServiceActivityHelper.SetSuccess(activity);
    return result;
}
catch (Exception ex)
{
    ServiceActivityHelper.RecordException(activity, ex);
    throw;
}
```

### Methods

| Method | Purpose |
|--------|---------|
| `StartActivity(operationName)` | Creates and starts an `Activity` with standard tags |
| `SetSuccess(activity)` | Sets `activity.StatusDescription = "Success"` |
| `RecordException(activity, exception)` | Records exception details to activity tags |
| `SetTag(activity, key, value)` | Safely sets a tag with type conversion |

### Benefits

- Consistent naming convention for tracing
- Automatic exception recording
- Reduced boilerplate across 10+ services

---

## Discord Resolver Services

Two specialized services provide channel and user resolution with caching, used throughout pages and services for Discord metadata lookups.

### IDiscordChannelResolver

Resolves channel names, types, and existence from the Discord client with in-memory caching:

```csharp
public interface IDiscordChannelResolver
{
    /// <summary>
    /// Gets the name of a channel by ID, or null if not found.
    /// Results are cached.
    /// </summary>
    Task<string?> GetChannelNameAsync(ulong channelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the channel type (Text, Voice, etc.) by ID.
    /// </summary>
    Task<ChannelType?> GetChannelTypeAsync(ulong channelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a channel exists.
    /// </summary>
    Task<bool> ChannelExistsAsync(ulong channelId, CancellationToken cancellationToken = default);
}
```

### IDiscordUserResolver

Resolves Discord user information with `IMemoryCache` for faster repeated lookups:

```csharp
public interface IDiscordUserResolver
{
    /// <summary>
    /// Gets user information (username, discriminator, avatar URL) by Discord ID.
    /// Results are cached for 1 hour.
    /// </summary>
    Task<DiscordUserInfo?> GetUserAsync(ulong userId, CancellationToken cancellationToken = default);
}

public class DiscordUserInfo
{
    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Discriminator { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}
```

### Usage

**IDiscordChannelResolver:**
- 8 guild pages (for displaying channel names in dropdowns/tables)
- 3 services (ModerationService, WarningService, WatchlistService)

**IDiscordUserResolver:**
- ModerationService (for ban/kick reason logging)
- WatchlistService (for user watch history)

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

## Blazor Components

The web UI is being ported from Razor Pages to Blazor, one page/cluster at a time (see
`docs/plans/blazor-port-plan.md`). Both stacks coexist under `src/DiscordBot.Bot/` until the
port finishes: `Pages/` (Razor Pages, legacy, being ported) and `Blazor/` (new UI). New UI work
goes in `Blazor/`; do not add new Razor Pages.

### Hosting model

**Blazor Web App, Interactive Server only** - no WebAssembly, no Auto. Page/component models
inject `DiscordSocketClient`, repositories and ~60 service interfaces directly, the same as
Razor Pages do today; server render keeps that possible without standing up an API for each one.

**Interactivity is per-page, not global.** `Blazor/Routes.razor` stays static SSR (no
`@rendermode` on `<Routes />`); each page opts in individually with `@rendermode
InteractiveServer` at the top of its `.razor` file. Shell chrome (sidebar, navbar) will be
static SSR with small interactive islands (notification bell, toast host) once the layouts land
in Phase 3 - don't put a render mode on a whole layout.

Registration lives in `Extensions/BlazorServiceExtensions.cs` (`AddBlazorWeb(IServiceCollection,
IWebHostEnvironment)`, called from `Program.cs` next to `AddWebServices()`) and follows the same
DI-registration-by-extension-method pattern as everything else - see
[DI Registration](#di-registration). `Program.cs` maps `MapRazorComponents<Blazor.App>()
.AddInteractiveServerRenderMode()` next to `MapRazorPages()`, and calls `app.UseAntiforgery()`
immediately after `app.UseAuthorization()` (required for `EditForm`/`<AntiforgeryToken />` on
static SSR Blazor pages; it validates the same ASP.NET Core antiforgery token Razor Pages'
`[ValidateAntiForgeryToken]`/`asp-antiforgery` already use, so the two don't double-validate).

### Where things live

| Folder | Contents |
| --- | --- |
| `Blazor/App.razor` | Static root document: `<!DOCTYPE html>`/`<head>` (`<base href="/">`, fonts, `app.css`, pre-paint theme script, sidebar FOUC guard - copied verbatim from `Pages/Shared/_Layout.cshtml`), `<HeadOutlet />`, `<Routes />`, an absolute-path `blazor.web.js` `<script>`. The `<base>` tag and the absolute script `src` both matter: without either, `blazor.web.js` resolves `_blazor/initializers` (and its own script URL) relative to the *current route* instead of the app root, 404ing and leaving the circuit dead on any nested page (e.g. `/admin/blazor-smoke`) - this is the known .NET 10 regression `tests/DiscordBot.E2E`'s nested-route test guards. |
| `Blazor/Routes.razor` | `Router` + `AuthorizeRouteView` (`DefaultLayout="typeof(MainLayout)"`, `NotFoundPage="typeof(NotFound)"`) + `RedirectToLogin` + `FocusOnNavigate`. |
| `Blazor/Layout/` | `EmptyLayout` (no chrome; an opt-in layout for pages that declare `@layout EmptyLayout`, e.g. the error pages - `Routes.razor`'s `DefaultLayout` is `MainLayout`), `MainLayout`/`MainSidebar`/`MainNavbar`/`MobileSearchOverlay`/`ShellNavigation` (Phase 3 - see below), and later `GuildLayout`/`PortalLayout`/`LandingLayout`. |
| `Blazor/Shared/` | The design-system component library (Phase 2 - Button, Card, Modal, etc., one `bUnit` test each). |
| `Blazor/Pages/` | Routable pages, mirroring today's `Pages/` tree as it's ported. |
| `Blazor/Interop/` | Thin C# wrappers around the interop JS modules (Phase 1+ - `charts.js`/`audio.js`/`browser.js`/`theme.js`). |
| `Blazor/Services/` | Blazor-specific services: the revalidating auth state provider, circuit observability, and the event bus/toast/loading services. |

### The static shell + islands pattern (`MainLayout`)

`MainLayout` (plan §4.7/§5 Phase 3) ports `Pages/Shared/_Layout.cshtml` + `_Navbar.cshtml` +
`_Sidebar.cshtml` + `_MobileSearchOverlay.cshtml` + the root `_ToastContainer.cshtml` into one
static-SSR layout, composed from `MainSidebar`, `MainNavbar` and `MobileSearchOverlay` (also
static SSR - same ids/classes as the partials they replace, so `site.css`/`app.css` apply
unchanged). `Routes.razor`'s `DefaultLayout` is `MainLayout` itself, so a routed page gets the
admin shell unless it opts out with `@layout EmptyLayout` (or another named layout, e.g.
`LandingLayout`/`GuildLayout`/`PortalLayout`). `<AuthorizeView Policy="...">` replaces the
legacy `<authorize policy="...">` tag helper one for one in `MainSidebar`; active-link state
(the `active` class, `aria-current="page"`) comes from `Blazor/Layout/ShellNavigation.cs`, a
small pure-string helper (`IsActive(currentPath, exact:, prefixes:)`) matching
`NavigationManager`'s current URL path against the same exact/prefix rules `_Sidebar.cshtml`
used to compute from a Razor Pages route value it no longer has.

Live chrome inside the static shell is small interactive islands: `NotificationBell` (in
`MainNavbar`) and `ToastHost`/`LoadingOverlay` (in `MainLayout` itself) each carry their own
`@rendermode InteractiveServer` rather than putting a render mode on the layout - see
"Interactivity is per-page" above. Islands inside a static parent may only receive serializable
parameters; all three here take none, since their state is entirely scoped-service-driven
(`IDashboardNotificationQueryService`/the event bus, `IToastService`, `ILoadingState`).

**`data-shell-action` delegation.** `MainLayout`/`MainSidebar`/`MainNavbar` never render
interactively, so they have no `IJSRuntime` to call and no server-side click handler to bind to
for sidebar collapse, the mobile drawer, the user menu, or the mobile search overlay. Those
elements instead carry a `data-shell-action="..."` attribute (`toggle-mobile-sidebar`,
`toggle-sidebar-collapse`, `toggle-user-menu`, `toggle-mobile-search`, `close-mobile-search`,
`dismiss-error-ui`) with no inline `onclick=` (Phase 6 adds a CSP that would block it anyway).
`wwwroot/js/blazor/shell.js`, a classic script (not a module - loaded from `App.razor` after
`blazor.web.js`) attaches one delegated listener per event type at `document` level, keyed off
that attribute, once on load. Because the listeners live on `document` rather than the elements
themselves, they survive Blazor's enhanced navigation swapping the sidebar/navbar markup back
out from under them - nothing needs to re-register after a client-side route change. The same
module also restores the persisted `sidebarCollapsed` localStorage state (the key `App.razor`'s
inline pre-paint FOUC-guard script also reads) and resyncs it on Blazor's `enhancedload` event.

**`ErrorBoundary` only catches static/prerender exceptions.** `MainLayout` wraps `@Body` in an
`<ErrorBoundary>` with an `ErrorContent` built from the `Alert` component
(`Variant="AlertVariant.Error"`), matching the design system's alert styling rather than a raw
stack trace. This only catches exceptions thrown while the static shell (or a static-SSR page
inside it) renders. An exception inside an `@rendermode InteractiveServer` island's own circuit
is a **circuit** failure, not something this component-tree `ErrorBoundary` can see - it
surfaces through Blazor's own reconnect UI instead (`#blazor-error-ui` in `App.razor`, styled
with design tokens rather than the framework template's default inline colors, dismissed via
the same `data-shell-action` dispatch as everything else in this section).

### Auth in components

**`HttpContext` is only available during prerendering, never once the Interactive Server circuit
is live.** Static SSR pages (Identity/account pages, per §4.2 of the port plan) can inject
`HttpContext` freely; any `@rendermode InteractiveServer` component cannot - use
`[CascadingParameter] Task<AuthenticationState>` or the injected `AuthenticationStateProvider`
instead, and never inject `IHttpContextAccessor` into an interactive component (it throws or
returns null once the circuit is running).

A circuit outlives the auth cookie that created it, and today's per-request
`DiscordClaimsTransformation` does not run again inside a circuit. `RevalidatingIdentityAuthenticationStateProvider`
(`Blazor/Services/`, registered scoped as `AuthenticationStateProvider`) closes that gap: every
30 minutes it re-checks, via a fresh `UserManager<ApplicationUser>` scope, that the user still
exists, isn't locked out, and (when the store supports it) that the security-stamp claim on the
circuit's principal still matches. A failed check ends the circuit; the next navigation forces a
real sign-in.

Because `HttpContext` disappears once a circuit is running, anything a component would have read
off it for audit logging - caller IP, user agent - has to be captured once, when the circuit
opens. `BlazorCircuitHandler.OnCircuitOpenedAsync` reads `IHttpContextAccessor.HttpContext` (it
*is* available at that one moment) and populates the scoped `CircuitClientInfoService`;
components read IP/UA from that service instead of `HttpContext`.

### Static-SSR account pages

Sign-in/sign-out (`docs/plans/blazor-port-plan.md` Phase 4 cluster 4c) is the model for every
account page that stays static SSR (`Login`, `Profile`, `Lockout`, `AccessDenied`, `Privacy`,
`LinkDiscord`): no `@rendermode`, `HttpContext` cascaded freely per "Auth in components" above,
and the sign-in/OAuth logic itself pulled into a plain scoped service (`IPasswordSignInService`,
`IExternalLoginHandler`, both in `Services/Account/`) so it is unit-testable without bUnit or a
running host - the component is left with only query/form wiring and turning a returned outcome
into a `NavigationManager.NavigateTo` or a rendered error.

**`FormName` IS required on a static SSR `EditForm`, unlike an interactive one.** This inverts the
interactive `EditForm` rule (deviation (b) in the port plan's Phase 4a section): an interactive
`EditForm` always posts back through its own circuit regardless of `FormName`, so that page's rule
is "don't set it unless a static no-JS fallback is deliberately implemented end to end." A page
that never goes `@rendermode InteractiveServer` has no circuit to post back through - the browser's
own POST is the only mechanism - so `FormName` (`Login.razor`'s `FormName="login"`,
`Profile.razor`'s `FormName="profile-theme"`) is how ASP.NET Core's form-value binder
(`[SupplyParameterFromForm]`) tells one page's form apart from another's on the same route.
`EditForm` still emits its own `<AntiforgeryToken />` hidden input automatically; a plain
`<form>` that isn't an `EditForm` (the Discord challenge button, the logout button) needs an
explicit `<AntiforgeryToken />` instead.

**The Discord challenge and sign-out live behind minimal-API endpoints, not the page itself.**
`Extensions/AccountEndpointExtensions.cs`'s `MapAccountEndpoints()` (mapped from `Program.cs` next
to `MapRazorPages()`) owns `POST /Account/Logout`, `POST /Account/PerformExternalLogin` (the
Discord `Results.Challenge`), and `GET /Account/ExternalLogin/Callback` - a static SSR page has no
"page handler" the way a Razor Page did, so a plain `<form method="post" action="...">` posting to
one of these routes replaces `asp-page-handler`. A minimal API endpoint that binds a parameter with
`[FromForm]` gets the same antiforgery validation `[ValidateAntiForgeryToken]`/`asp-antiforgery`
gave a Razor Pages handler automatically, once `app.UseAntiforgery()` is in the pipeline (already
true here) - no explicit `[ValidateAntiForgeryToken]`/`DisableAntiforgery()` call needed on either
endpoint (proven by `AccountEndpointExtensionsTests.MapAccountEndpoints_PostEndpointsRequireAntiforgery_GetEndpointsDoNot`,
which builds the real endpoint data via `MapAccountEndpoints` on a `WebApplication` and asserts
`IAntiforgeryMetadata` directly - the handler-level tests in that same file call the handlers as
plain delegates and never construct an endpoint, so none of them exercise this). Route strings live
as `public const` fields on `Extensions/AccountRoutes` (`Login`, `Logout`, `PerformExternalLogin`,
`ExternalLoginCallback`, `Lockout`, `AccessDenied`, `LinkDiscord`, `Privacy`) rather than being
retyped at each call site, since more than one file needs the exact same literal: the cookie
config's `LoginPath`/`LogoutPath`/`AccessDeniedPath` (kept as literal strings there, not a
reference to this class - `IdentityConfigOptions` lives in `DiscordBot.Core`, which cannot
reference `DiscordBot.Bot`), the OAuth `OnRemoteFailure` redirect (`IdentityServiceExtensions`),
`RedirectToLogin.razor`, `MainNavbar.razor`, the legacy `Pages/Shared/_Navbar.cshtml`,
`AccessDenied.razor`'s "Sign Out" form, and - for `PerformExternalLogin`, `LinkDiscord` and
`Privacy` specifically - `LinkDiscord.razor`/`LinkDiscord.razor.cs`/`Privacy.razor.cs`'s own
Discord-linking form and self-redirects.

### Circuit observability

`BlazorCircuitHandler : CircuitHandler` (`Blazor/Services/`, registered scoped - one instance per
circuit) logs "Blazor circuit opened"/"closed" at Information with the user ID, circuit ID and a
correlation ID (reused from the opening request via `HttpContextExtensions.GetCorrelationId()` if
present, otherwise generated the same way `CorrelationIdMiddleware` does), and records
`blazor.circuits.opened_total` / `blazor.circuits.active` via `Metrics/BlazorMetrics.cs` (same
`IMeterFactory` pattern as `BotMetrics`/`ApiMetrics`, registered in `OpenTelemetryExtensions`).
Circuit interactions travel over the SignalR hub, never an HTTP request, so they never pass
through `ApiMetricsMiddleware` or `UseSerilogRequestLogging` - see the circuit notes in
`docs/articles/metrics.md` and `docs/articles/tracing.md`.

### GuildContext

`Blazor/Guilds/` (namespace `DiscordBot.Bot.Blazor.Guilds`) replaces the ~27 independent
`IGuildService.GetGuildByIdAsync` + breadcrumb/header/nav builds `Pages/Guilds/GuildPageModelBase.cs`
repeats today with one resolve-once-per-route pipeline (`docs/plans/blazor-port-plan.md` §4.7,
Phase 3):

- **`IGuildContextProvider`** (`GuildContextProvider`, registered scoped in `AddBlazorUiServices()`)
  loads the guild (`GuildContextStatus.NotFound` on a miss), authorizes the user against the
  consolidated `GuildAccess` policy via `IAuthorizationService.AuthorizeAsync(user, guildId,
  "GuildAccess")` - passing the guild id as the `resource` argument, which `GuildAccessHandler`
  checks before any route/query value, specifically for this non-HTTP caller
  (`GuildContextStatus.Forbidden` on a deny) - then computes `CanEdit`
  (`IsGuildAdmin || IsAppAdmin`, the same rule `Pages/Guilds/Details.cshtml.cs` uses), the
  `AudioEnabled`/`RatWatchEnabled` feature flags, and takes the tab list from
  `GuildNavigationConfig.GetTabs()`. The result is a `GuildContext` record - plain, JSON-serializable,
  no `ClaimsPrincipal` - wrapped in a `GuildContextResult` (`NotFound | Forbidden | Ok`).
- **It's scoped and memoised** because one HTTP request's prerender and one circuit's lifetime are
  each exactly one DI scope, and a guild page plus the `GuildLayout` wrapping it both resolve the
  same guild id within that one scope - the provider caches its result per `guildId` (a `Dictionary`
  keyed by guild id, not by user, since one scope belongs to one user) so the second caller costs
  nothing. This is why it's registered `AddScoped`, not `AddSingleton` - a singleton's cache would
  leak across users and never expire.
- **The static-layout-vs-page resolution rule:** during prerender, the static `GuildLayout` and the
  interactive page it wraps share the request's DI scope, so whichever resolves first populates the
  memoisation cache for the other. Once the circuit takes over, the page gets a *new* scope (a fresh
  `IGuildContextProvider`, cache empty again) - `GuildLayout` obtains the guild id itself via
  `GuildRoutes.TryGetGuildId(NavigationManager.Uri, out var guildId)` (it has no route-parameter
  binding of its own) and calls the same provider a page derived from `GuildPageBase` calls, so
  the two agree without a cascading parameter carrying it across the static/interactive boundary
  (cascading values don't cross that boundary at all - see "Auth in components" above).
- **`GuildPageBase : ComponentBase`** is what a routable guild page derives from: declares
  `[Parameter] public long GuildId { get; set; }` (routes are `{guildId:long}` - no `ulong`
  route-constraint type exists), resolves via `IGuildContextProvider` in a sealed
  `OnInitializedAsync`/`OnParametersSetAsync` pair (re-resolving only when `GuildId` changes),
  and persists the `GuildContextResult` with `PersistentComponentState.RegisterOnPersisting`/
  `TryTakeFromJson` so the provider - and everything it calls - runs once per page load, not once
  during prerender and again when the circuit reconnects. A derived page overrides the virtual
  `OnGuildContextReadyAsync()` hook for its own data loading instead of the lifecycle methods
  directly, which are sealed so that bookkeeping can't be bypassed by accident. Exposes `Result`,
  `Guild` (shorthand for `Result?.Context`), and `IsLoading` as `protected`. Also carries the
  local-time-scan bookkeeping every page that renders `<LocalTime>` and can re-render its data
  needs: a protected `RequestLocalTimeScan()` a page calls at the end of every successful
  load/reload, and a virtual `OnAfterRenderAsync` override (not sealed, since a page with its own
  post-render work - `ScheduledMessages/Edit.razor.cs` detecting the viewer's timezone - overrides
  it too and calls `base.OnAfterRenderAsync(firstRender)` alongside its own logic) that runs the
  scan when requested. `localtime.js`'s document-level scan only fires once, on the very first
  render, so a page whose rows can change after that (paging, a filter, an action that reloads the
  list) without this would show the raw UTC fallback for any row rendered afterward - this was
  originally per-page boilerplate (see `Blazor/Pages/Admin/Users/Index.razor.cs`, still its own
  copy since it isn't a guild page) before being lifted into `GuildPageBase` so a guild list/detail
  page gets it for free.
- **`GuildContextGate.razor`** renders the four states a `GuildContextResult` can be in - still
  loading, not found, forbidden, or the page via a `RenderFragment<GuildContext> ChildContent` -
  with `NotFoundContent`/`ForbiddenContent`/`LoadingContent` parameters to override any of the
  default `EmptyState` fragments. A guild page's markup is typically just
  `<GuildContextGate Result="Result"><ChildContent Context="guild"> ... </ChildContent></GuildContextGate>`.
- **`GuildLayout` reuses the same provider** rather than a second lookup path: it resolves the
  guild id from the URI via `GuildRoutes.TryGetGuildId`, the active tab via
  `GuildRoutes.ResolveActiveTabId`, and calls `IGuildContextProvider.GetAsync` itself to render the
  breadcrumb/header/nav chrome - memoisation is what keeps that from being a second guild load when
  the page underneath it also resolves the same guild id.

### Portal three-state gate

The Portal's anonymous-landing / authenticated-non-member-forbidden / member-portal gate
(`Pages/Portal/PortalPageModelBase.CheckPortalAuthorizationAsync`) is implemented once in
`IPortalAccessService`/`PortalAccessService` (`Bot/Services/Portal/`, scoped) rather than inline in
the page model, so a future Blazor `PortalLayout` can reuse the exact same ordering: guild lookup,
Discord client lookup, auth state, Discord-link check, Admin/SuperAdmin bypass, then a
cache-then-REST guild membership check. `ResolveAsync` returns a `PortalAccessResult`
(`GuildNotFound | ShowLanding | NotGuildMember | Authorized`, each carrying the login URL except
`GuildNotFound`) with a `PortalContext` payload scoped to what `_PortalHeader`/`_PortalLanding`
need (guild DTO, name, icon, bot-online flag) - deliberately narrower than
`PortalPageModelBase.PortalAuthContext`, which still carries the Discord.Net `SocketGuild` the three
Portal Index pages (Soundboard/TTS/VOX) read directly for voice-channel listing.
`PortalPageModelBase.CheckPortalAuthorizationAsync` now delegates to this service and rebuilds that
`SocketGuild` from the id `IPortalAccessService` already confirmed exists (an in-memory gateway-cache
read, not a network call). Its constructor's parameter list is unchanged - `IPortalAccessService` is
resolved from `HttpContext.RequestServices` inside the method, the one service-locator exception in
this codebase, because changing the constructor would mean touching every derived Portal page model
for a refactor scoped to the base class alone.

### GuildLayout / PortalLayout

`Blazor/Layout/GuildLayout.razor` and `Blazor/Layout/PortalLayout.razor` (plan §4.7/§5 Phase 3)
are the two chrome layouts built on top of "GuildContext" and "Portal three-state gate" above.
Both are static SSR - no `@rendermode` - and neither has a route-parameter binding of its own
(a layout wraps whatever page routed), so both read the guild id straight off
`NavigationManager.Uri` (`GuildRoutes.TryGetGuildId`/`Blazor/Portal/PortalRoutes.TryGetGuildId`,
the latter handling both Portal URL shapes - `/Portal/{Feature}/{guildId}` for the three real
pages, `/Portal/{guildId}/{page}` for the probe) and call the same memoised provider a page under
them calls, so the two agree without a cascading parameter crossing the static/interactive
boundary - see "The static-layout-vs-page resolution rule" above; a page's own
`@rendermode InteractiveServer` never makes its ancestor layout interactive, so both layouts run
their resolution exactly once, during the same static render pass a page's own prerender runs in,
and are never re-rendered once a circuit takes over.

- **A page opts in** with `@layout GuildLayout` or `@layout PortalLayout` at the top of its
  `.razor` file, the same as any other layout. `GuildLayout` itself carries `@layout MainLayout`,
  so a guild page gets the full admin shell (sidebar/navbar/toast/loading) plus the guild
  breadcrumb/header/tab chrome layered on top; `PortalLayout` is **not** nested under
  `MainLayout` - the Portal is member-facing, reached via a signed link, with no admin sidebar,
  and the legacy `_PortalLayout.cshtml` was never a child of the admin shell either.
- **`GuildLayout` renders per `GuildContextResult.Status`.** `Ok`: `Breadcrumb`
  (`GuildContext.Breadcrumb(pageName)` - `null` for the Overview tab or a route matching no tab,
  reproducing `BuildBasicBreadcrumb`; the active tab's `Label` otherwise, reproducing
  `BuildPageBreadcrumb`), `GuildHeader` (`PageTitle` is that same active-tab-label-or-guild-name -
  no per-page title mechanism reaches this layout; a page sets Blazor's own `<PageTitle>`, the
  browser tab title, separately and unrelated to this), then the tab nav: `TabGroup
  Mode="TabGroupMode.Navigation" StyleVariant="TabStyleVariant.Pills"` for desktop
  (`.hidden sm:block`, matching `GuildNavBarHelper`'s existing `_TabPanel` config) plus a native
  `<select data-shell-action="navigate-select">` for mobile (`.sm:hidden`) - chosen over a
  details/summary or a re-implemented dropdown menu because a native select is keyboard- and
  screen-reader-accessible for free and this layout has no `IJSRuntime` to drive anything more
  custom; `wwwroot/js/blazor/shell.js` gained one delegated `change` listener for it.
  `NotFound`/`Forbidden`: the breadcrumb/header/nav are omitted entirely and `@Body` renders
  unchanged - the page's own `GuildContextGate` (a second, independent call into the same
  memoised provider - see "GuildContext") is what shows the 404/403 content, not this layout.
- **`PortalLayout` renders per `PortalAccessOutcome`.** Adds `portal.css` via `<HeadContent>`
  (`app.css`/`tab-panel.css` are already global in `App.razor`). `GuildNotFound`: the
  design-system `EmptyState`, same "doesn't exist or has been removed" copy
  `GuildContextGate`'s own not-found fragment uses for the guild case - the closest available
  match to "the same copy as today's 404", since the legacy `GuildNotFound` path actually returns
  a plain `NotFound()` re-executed against the generic, non-Portal `/Error/404`, not any
  Portal-specific copy. `ShowLanding`/`NotGuildMember`: straight ports of
  `_PortalLanding.cshtml`/`_PortalUnauthorized.cshtml`. `Authorized`: the ported `_PortalHeader`
  chrome (icon/name/online-offline badge/`TabGroup StyleVariant="TabStyleVariant.Portal"`
  Soundboard-TTS-VOX nav) + `@Body`. A `<ToastHost @rendermode="InteractiveServer" />` island
  renders unconditionally, matching the legacy layout always loading `toast.js`.
- **`IPortalContextProvider`** (`Blazor/Portal/`, scoped, registered in `AddBlazorUiServices()`)
  is a thin memoising wrapper over the existing `IPortalAccessService`, added for the same reason
  `IGuildContextProvider` wraps guild resolution: `PortalAccessService.ResolveAsync` does a real
  database read plus, once signed in, a `UserManager` lookup and a cache-then-REST guild
  membership check, and `PortalLayout` plus a `PortalPageBase`-derived page both resolve the same
  guild id within one scope. `PortalPageBase` (`Blazor/Portal/`) mirrors `GuildPageBase` exactly -
  same sealed `OnInitializedAsync`/`OnParametersSetAsync` pair, same `PersistentComponentState`
  round trip across the prerender-to-circuit boundary, same virtual `OnPortalContextReadyAsync()`
  hook a derived page overrides instead.
- **Portal script bundle.** `shared/keyboard-shortcuts.js` and `user-preferences.js` are both
  classified **B** (thin-interop-shim-survives) in `blazor-port-inventory.md` Part 4 and are
  loaded here as classic scripts (both are self-initializing IIFEs, the same shape `shell.js`
  already loads this way) for every non-`GuildNotFound` state, matching the legacy layout's
  "always loaded regardless of state" behavior. Neither is wired to anything yet - the
  `RegisterShortcut` interop call and `UserPreferences.init(guildId)` both need a real page
  component to drive them, and no Portal page has been ported yet (the probe is not a real
  consumer); that wiring is Phase 4f's job. `api-client.js`/`toast.js` are not loaded - both are
  superseded outright (in-circuit service calls; `ToastHost`/`IToastService`).
- **Temporary probes.** `Blazor/Pages/Guilds/GuildProbe.razor` (`/Guilds/{guildId:long}/blazor-probe`,
  `RequireAdmin`, `@inherits GuildPageBase`) and `Blazor/Pages/Portal/PortalProbe.razor`
  (`/Portal/{guildId:long}/blazor-probe`, `[AllowAnonymous]` - a Portal page branches on outcome
  rather than gating the route, `@inherits PortalPageBase`) prove both layouts end to end on real,
  interactive, nested routes, the same role `BlazorProbe.razor` played for Phase 1. Retained until
  Phase 4b/4f replace them with real ported pages - see "Blazor Routes (Phase 3...)" in
  `ui-inventory.md`.

### Paged list pages

Standardised on `Blazor/Common/PagedQuery.cs` (plan §5 Phase 4, cluster 4b) rather than each guild
list page inventing its own page/size fields - all four paged guild lists (`FeatureRequests/Index`,
`Reminders/Index`, `RatWatch/Index`, `AudioModerationLog/Index`) share it, the latter two ported
onto it after initially shipping hand-rolled component-state paging (a one-time `_seeded` flag,
callback-mode `Pagination`) that changed the URL without the URL ever being the source of truth,
so browser back/forward across a page change did nothing. A `record` of `PageNumber` (clamped ≥ 1),
`PageSize` (clamped to [1, 100], default per page), `SortBy`/`SortDescending`, built once per load
via `PagedQuery.FromQuery(pageNumber, pageSize, sortBy, sortDescending, defaultPageSize,
legacyPage)` and rendered with `Blazor/Shared/Navigation/Pagination.razor` in link mode
(`BaseUrl="@PageUrl" PageParameterName="pageNumber"` - `PageSizeParameterName` defaults to
`pageSize`). A page binds the matching `[SupplyParameterFromQuery]` names (`pageNumber`,
`pageSize`, `sortBy`, `sortDescending`; a filter like a status enum keeps its own query name,
e.g. `status`) and, since a query-string-only change doesn't re-trigger `GuildPageBase`'s sealed
`OnParametersSetAsync` (see "GuildContext" above), reloads via an `OnParametersSet()` override -
still synchronous, since that lifecycle method can't be awaited and the async pair is sealed.
`legacyPage` lets `FromQuery` fall back to an old `?page=` value when `pageNumber` is absent, so
bookmarks and existing plain-`href` widget links (e.g. `Guilds/Details`) built before this
standardisation keep resolving without an edit on their end.

That synchronous hook still has to kick off an async reload somehow, and two mistakes are easy to
make there: a bare `_ = ReloadAsync()` leaves the task's exceptions unobserved (a failed query
becomes a silently-stuck loading spinner, or - worse - an exception that surfaces somewhere
unrelated later), and two query-string changes landing in quick succession can race, with the
first load's slower response overwriting the second, newer one's result. `Blazor/Pages/Guilds/FeatureRequests/Index.razor.cs`
is the reference example for avoiding both: the hook dispatches through `InvokeAsync(ReloadAndRerenderAsync)`
rather than a bare fire-and-forget, so the reload runs on the renderer's synchronization context
like any other UI-driven call; `LoadAsync` itself increments a private `_loadGeneration` counter at
the top, captures that value, and checks it again after every `await` before writing to any
`protected` state - a load whose generation no longer matches the field (a newer load started
while this one was in flight) discards its result instead of applying it; and the whole body after
the counter increment is wrapped in `try`/`catch`, logging at Error, toasting, and setting a
`LoadFailed` flag the markup renders as an `Alert` in place of the list - never a plain empty
state, which would read as "there's nothing here" rather than "the query failed". A handler that
reloads directly (a cancel, an approve/reject) can just `await LoadAsync()` - the generation counter
still protects it against a slower `OnParametersSet`-triggered reload finishing after it.

### Per-operation scopes

Services and `BotDbContext` are scoped, and a Blazor Server circuit is one DI scope for its whole
life (plan §4.1 "Data access in components") - so a page that injects a service with `[Inject]`
the ordinary way holds the *same* instance, and the same `DbContext` with everything it has ever
tracked, for as long as the circuit stays open. That is fine for a read: `AsNoTracking()` queries
don't accumulate anything the tracker cares about. It is not fine for a mutation: `DbSet.Update()`/
`DbSet.Remove()` attach the whole reachable graph as tracked and never detach it after
`SaveChangesAsync`, so a second fetch-mutate-save of the same entity later in the same circuit
fetches a fresh, different instance for a key the first call left tracked, and EF's identity map
throws "already tracked" - see `docs/lessons-learned/scheduled-message-repeated-update-tracking.md`
for the full failure and why the fix is not in `Repository<T>` itself.

**Rule: every mutation, and the reload that follows it, resolves its service through a fresh scope
via `Blazor/Common/ScopedOperations.cs` instead of the page's injected instance.** `ScopedOperations`
is a set of `IServiceScopeFactory` extension methods - `RunAsync<TService>(Func<TService, Task>)`,
`RunAsync<TService, TResult>(Func<TService, Task<TResult>>)`, and two-service overloads for a
handler that needs two services from the same operation (e.g. a fetch-mutate-save that must stay on
one `DbContext`) - each doing `await using var scope = scopeFactory.CreateAsyncScope();` then
resolving with `GetRequiredService` and disposing the scope (and its `DbContext`) the moment the
call returns. A page injects `IServiceScopeFactory` alongside its normal `[Inject]` services and
writes `await ScopeFactory.RunAsync<IScheduledMessageService>(s => s.UpdateAsync(id, dto))` in place
of `await ScheduledMessageService.UpdateAsync(id, dto)`, then reloads through another `RunAsync`
call rather than calling the page's own `LoadAsync()` directly (a page whose load method is shared
between the initial load and a post-mutation reload takes the service as a parameter -
`LoadAsync(IScheduledMessageService service)` - so the initial call passes the injected instance and
the reload passes `ScopeFactory.RunAsync<IScheduledMessageService>(LoadAsync)`, method-group-converted
straight into the `Func<TService, Task>` the two share). The initial load in
`OnGuildContextReadyAsync`/`OnInitializedAsync` may keep using the page's injected, circuit-scoped
service - it's read-only, so there's nothing for a later call in the same circuit to collide with.

Every interactive page ported in Phase 4 clusters 4a/4b follows this: `Admin/Users/{Index,Create,Edit}`,
`Guilds/{Edit,Welcome,AssistantSettings}`, `RatWatch/Index`, `FeatureRequests/{Index,Details}`,
`Reminders/Index`, `ScheduledMessages/{Index,Create,Edit}`. A bUnit test that registers its mocks
as singletons (the norm in this codebase's component tests) needs no change for this: a child scope
still resolves the same singleton instance, only a *scoped* mock would behave differently, and none
of these tests register one that way.

`Repository<T>.UpdateAsync`/`DeleteAsync` themselves are back to their original, unconditional
`DbSet.Update(entity)`/`DbSet.Remove(entity)` bodies - an earlier attempt fixed the "already tracked"
crash generically there instead, by walking the incoming entity's reachable graph and detaching any
stale tracked entry for the same key before attaching. That was reverted: it can silently detach (and
so lose) another concurrent caller's still-pending edit to the same entity, turning a loud exception
into a quiet dropped update, and it reads keys via reflection, which breaks for an entity with a
shadow key. Per-operation scopes fix the actual root cause - the circuit-scoped `DbContext` - instead
of papering over its symptom in the generic repository.

**Known pre-existing behaviour, not changed here.** `Repository<T>.UpdateAsync` calls
`DbSet.Update(entity)`, which marks every `Include`d navigation on the entity as `Modified` too, not
just the entity itself - so saving a `ScheduledMessage` (whose `ScheduledMessageRepository.GetByIdAsync`
includes `Guild`) also rewrites the `Guilds` row it was fetched with, even though nothing about the
guild changed. Per-operation scopes don't make this better or worse (a fresh `DbContext` still walks
the same graph); it's tracked as a follow-up for its own PR, not addressed here.

### Gotchas carried over from CLAUDE.md

- **Discord snowflakes are strings** in any component `[Parameter]`, `@bind` target, or JS
  interop call - the same rule as `'@Model.GuildId'` in Razor Pages. A `ulong` id is fine inside
  C# logic; the moment it crosses into markup or `IJSRuntime.InvokeAsync`, convert it to `string`
  first, or the last digits round off silently in JavaScript.
- **`Discord:Enabled=false` (web-only mode)** works the same for Blazor pages as for Razor
  Pages - it's how the host runs for UI testing without a bot token or gateway connection. See
  "Running it locally" in `CLAUDE.md` and `docs/articles/configuration-guide.md`.

## Real-time event bus

`IDashboardEventBus` (`Bot/Services/Realtime/DashboardEventBus.cs`) is an in-process pub/sub bus
that every SignalR dashboard broadcaster dual-publishes to alongside its
`IHubContext<DashboardHub>` send, so a Blazor Server component gets the same real-time data a
browser SignalR client gets, without a client connection. Full detail, event catalog, and the
dual-publish rule for new broadcasters live in `docs/articles/signalr-realtime.md`, "In-process
event bus" — this section is the component-authoring side of that same pattern.

### Subscribing and rendering

```csharp
public partial class VoiceChannelPanel : ComponentBase, IDisposable
{
    [Parameter] public ulong GuildId { get; set; }

    [Inject] private IDashboardEventBus EventBus { get; set; } = default!;

    private IDisposable? _subscription;
    private readonly Debouncer _debouncer = new();
    private QueueUpdatedDto? _queue;

    protected override void OnInitialized()
    {
        // Guild-scoped overload: only this guild's events reach the handler, one line, no
        // `if (evt.GuildId != GuildId) return;` boilerplate.
        _subscription = EventBus.Subscribe<QueueUpdatedEvent>(GuildId, (evt, _) =>
        {
            _debouncer.Debounce(TimeSpan.FromSeconds(1), async ct =>
            {
                _queue = evt.Queue;
                await InvokeAsync(StateHasChanged);
            });
            return Task.CompletedTask;
        });
        base.OnInitialized();
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _debouncer.Dispose();
    }
}
```

### Rules

1. **Filter by guild** with the guild-scoped `Subscribe` overload (`Subscribe<TEvent>(guildId, handler)`)
   for any event deriving from `GuildScopedEvent`, instead of subscribing broadly and filtering
   by hand.
2. **Debounce or coalesce to ≤1 Hz re-render.** High-frequency events (playback progress, a burst
   of guild activity) must not drive `StateHasChanged` faster than about once a second; use
   `Blazor/Common/Debouncer.cs`.
3. **Unsubscribe in `Dispose`.** Failing to dispose the handle `Subscribe` returns leaks a
   delegate that closes over the component; the bus catches a handler that throws (logged at
   Warning) so a torn-down component can't fault the publisher, but a leaked subscription still
   does pointless work forever.
4. **Call `InvokeAsync(StateHasChanged)`.** A published event is delivered on whichever thread
   called `PublishAsync` — a background service's timer thread, a request thread handling a hub
   call, another circuit entirely — never automatically on this component's synchronization
   context.

### Toast, loading, and debounce services

`Bot/Blazor/Services/IToastService` and `ILoadingState` are the scoped (per-circuit) UI-state
counterparts to today's `wwwroot/js/toast.js` and the loading-overlay JS: inject them into a
component, call `Toast.Success(...)` / `Loading.Begin(...)`, and subscribe to their `Changed`
event the same way as the bus (`InvokeAsync(StateHasChanged)`). `Blazor/Common/Debouncer.cs` is
the general-purpose trailing-edge debounce used above and anywhere else a component coalesces
bursty input (a search box, a filter change) into one action.

---

## See Also

- [Component API Documentation](../articles/component-api.md) - UI component library
- [Authorization Policies Documentation](../articles/authorization-policies.md) - Detailed auth setup
- [Audit Log System](../articles/audit-log-system.md) - Comprehensive audit logging guide
- [Form Implementation Standards](../articles/form-implementation-standards.md) - Form handling patterns
- [Database Schema](../articles/database-schema.md) - Entity relationships and structure
