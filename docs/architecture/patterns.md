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

## See Also

- [Component API Documentation](../articles/component-api.md) - UI component library
- [Authorization Policies Documentation](../articles/authorization-policies.md) - Detailed auth setup
- [Audit Log System](../articles/audit-log-system.md) - Comprehensive audit logging guide
- [Form Implementation Standards](../articles/form-implementation-standards.md) - Form handling patterns
- [Database Schema](../articles/database-schema.md) - Entity relationships and structure
