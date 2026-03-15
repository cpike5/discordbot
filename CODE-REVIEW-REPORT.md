# .NET Codebase Deep Dive Review

**Date:** 2026-03-15
**Scope:** Full codebase analysis across all layers (~1,090 C# files in `src/`)
**Focus:** Code duplication, non-standard patterns, simplification opportunities

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Architecture Overview](#architecture-overview)
3. [Critical Findings](#critical-findings)
   - [Service Layer Duplication](#1-service-layer-duplication)
   - [Discord Command Handler Duplication](#2-discord-command-handler-duplication)
   - [Data Access Layer Issues](#3-data-access-layer-issues)
   - [Web Layer Duplication](#4-web-layer-duplication)
4. [Detailed Findings by Category](#detailed-findings-by-category)
5. [Recommended Abstractions](#recommended-abstractions)
6. [Prioritized Refactoring Roadmap](#prioritized-refactoring-roadmap)

---

## Executive Summary

The codebase follows clean architecture principles (Core → Infrastructure → Bot) and uses consistent patterns overall. However, the analysis reveals **significant code duplication** concentrated in a few key areas, totaling an estimated **3,000–4,000 lines** that could be eliminated through targeted abstractions. The most impactful opportunities are:

| Area | Estimated Duplicated LOC | Priority |
|------|------------------------:|----------|
| Activity/tracing boilerplate across services | ~650+ | CRITICAL |
| Embed building in command modules | ~1,200+ | CRITICAL |
| Guild page layout initialization | ~1,000+ | HIGH |
| Pagination validation (services + pages + controllers) | ~300+ | HIGH |
| Discord user/channel resolution | ~200+ | HIGH |
| Repository query patterns | ~400+ | MEDIUM |
| Controller error responses | ~200+ | MEDIUM |

---

## Architecture Overview

```
┌──────────────────────────────────────────────┐
│            DiscordBot.Bot (~740 files)        │
│  Razor Pages · REST API · Discord Commands   │
│  SignalR Hubs · 80+ Services · Middleware     │
├──────────────────────────────────────────────┤
│       DiscordBot.Infrastructure (~200 files)  │
│  EF Core · Repositories · Migrations         │
│  LLM/Vox Services · Tracing                  │
├──────────────────────────────────────────────┤
│          DiscordBot.Core (~150 files)         │
│  Entities · DTOs · Interfaces · Enums         │
│  Configuration · ViewModels · Utilities       │
└──────────────────────────────────────────────┘
```

**Tech Stack:** .NET 8, Discord.Net 3.19.0-fork, EF Core 8 (SQLite + PostgreSQL), Anthropic SDK, Azure Speech, Serilog, OpenTelemetry, Elastic APM, Tailwind CSS

---

## Critical Findings

### 1. Service Layer Duplication

#### 1.1 Activity/Tracing Boilerplate (CRITICAL — ~50+ methods)

Every service method wraps logic in an identical 13-line tracing pattern:

```csharp
using var activity = BotActivitySource.StartServiceActivity("service_name", "method_name", guildId: guildId);
try
{
    _logger.LogDebug("...");
    // actual logic
    BotActivitySource.SetSuccess(activity);
    return result;
}
catch (Exception ex)
{
    BotActivitySource.RecordException(activity, ex);
    throw;
}
```

**Affected files (partial list):**
- `Services/Audit/AuditLogService.cs` — lines 51–96, 101–134, 141–169
- `Services/Guild/GuildService.cs` — lines 50–96, 103–201
- `Services/Moderation/ModerationService.cs` — lines 34–82, 87–114, 119–146
- `Services/ReminderService.cs` — lines 47–79
- `Services/WatchlistService.cs` — all public methods
- `Services/ConsentService.cs` — all public methods
- `Services/NotificationService.cs` — all public methods

**Recommendation:** Introduce an interceptor/decorator pattern or a helper that wraps a `Func<Task<T>>` with activity tracking, reducing each method by ~10 lines.

---

#### 1.2 Pagination Validation (HIGH — 6+ services)

Identical validation logic repeated across services:

```csharp
if (query.Page < 1) query.Page = 1;
if (query.PageSize < 1 || query.PageSize > 100) query.PageSize = 20;
```

**Files:**
- `Services/Audit/AuditLogService.cs` — lines 64–72
- `Services/Commands/CommandLogService.cs` — lines 44–53
- `Services/Guild/GuildService.cs`
- `Services/Moderation/ModerationService.cs`
- `Services/NotificationService.cs`

**Recommendation:** Create a `PaginationQuery.Normalize()` extension method.

---

#### 1.3 Fire-and-Forget Task Pattern (HIGH — 50+ instances)

Inconsistent fire-and-forget usage with no unified error handling:

```csharp
_ = SomeTaskAsync(params, CancellationToken.None);
_ = Task.Run(async () => { ... });
```

**Files:**
- `Services/BotHostedService.cs` — lines 150, 345, 348, 366, 370, 534, 556, 561, 564, 610, 615
- `Services/NotificationService.cs` — lines 81, 152, 154, 227, 229
- `Services/PlaybackService.cs` — lines 125, 141, 148, 200, 201, 938, 870
- `Services/AlertMonitoringService.cs` — multiple instances

**Recommendation:** Create `IBackgroundTaskRunner` with centralized error logging/telemetry.

---

#### 1.4 Entity-to-DTO Batch Mapping (MEDIUM — 8+ methods)

Repeated async mapping loops:

```csharp
var dtos = new List<T>();
foreach (var entity in entities)
    dtos.Add(await MapToDtoAsync(entity, ct));
```

**Files:**
- `Services/Moderation/ModerationService.cs` — lines 172–176, 246–250, 281–285
- `Services/WatchlistService.cs` — lines 128–132
- `Services/NotificationService.cs` — lines 130–143, 205–218

---

#### 1.5 Username/User Resolution (MEDIUM — 3+ services)

Identical Discord REST API lookups with fallback:

**Files:**
- `Services/Moderation/ModerationService.cs` — lines 474–486
- `Services/WatchlistService.cs` — lines 233–245
- `Services/Audit/AuditLogService.cs` — lines 304–330 (batch variant)

**Recommendation:** Extract to `IDiscordUserResolver` with optional caching.

---

#### 1.6 Oversized Services

| Service | Lines | Responsibilities | Action |
|---------|------:|-----------------|--------|
| `PlaybackService.cs` | 978 | Queue + streaming + FFmpeg + caching + broadcasting | Split into `IPlaybackQueue`, `IAudioStreamer`, `IFfmpegTranscoder` |
| `SearchService.cs` | 947 | Auth + caching + 9 category searches + scoring + ranking | Extract `ISearchProvider<T>` per category |
| `NotificationService.cs` | 689 | Creation + broadcast + CRUD + DTO mapping | Split creation from broadcasting |
| `AlertMonitoringService.cs` | 629 | Monitoring + metrics + incidents + notifications | Split into `IMetricsCollector` and `IAlertManager` |

---

### 2. Discord Command Handler Duplication

#### 2.1 Error Embed Building (CRITICAL — 80+ instances across 12 modules)

Every command module builds error embeds identically:

```csharp
var errorEmbed = new EmbedBuilder()
    .WithTitle("❌ Error")
    .WithDescription(ex.Message)
    .WithColor(Color.Red)
    .WithCurrentTimestamp()
    .Build();
await RespondAsync(embed: errorEmbed, ephemeral: true);
```

**Files (showing count of instances per file):**

| File | Error Embeds | Success Embeds | Empty-State Embeds |
|------|:-----------:|:-------------:|:-----------------:|
| `Commands/ScheduleModule.cs` | 14 | 3 | 1 |
| `Commands/TtsModule.cs` | 12 | 2 | 0 |
| `Commands/SoundboardModule.cs` | 6 | 3 | 1 |
| `Commands/ModTagModule.cs` | 5 | 5 | 0 |
| `Commands/VoiceModule.cs` | 4 | 3 | 0 |
| `Commands/ReminderModule.cs` | 5 | 2 | 1 |
| `Commands/ConsentModule.cs` | 3 | 2 | 0 |
| `Commands/ModNoteModule.cs` | 3 | 1 | 0 |
| `Commands/ModerationHistoryModule.cs` | 2 | 0 | 0 |
| `Commands/RatWatchModule.cs` | 0 | 2 | 1 |
| `Commands/AdminModule.cs` | 0 | 0 | 1 |

**Estimated savings:** ~1,200 lines by extracting to `EmbedHelper.Error()`, `EmbedHelper.Success()`, `EmbedHelper.EmptyState()`.

---

#### 2.2 Voice Channel Validation (MEDIUM — 5 instances)

Identical voice channel connection checks:

**Files:**
- `Commands/SoundboardModule.cs` — lines 99–119
- `Commands/VoiceModule.cs` — lines 53–73, 152–172
- `Commands/TtsModule.cs` — lines 143–163, 465–485

**Recommendation:** Extract to `VoiceChannelHelper.ValidateUserInVoiceChannel()`.

---

#### 2.3 Pagination in List Commands (MEDIUM — 4+ modules)

Each list command implements its own page size, bounds checking, and button building:

**Files:**
- `Commands/ReminderModule.cs` — line 23 (`RemindersPerPage = 10`), lines 192–253
- `Commands/AdminModule.cs` — lines 127–161
- `Commands/ModerationHistoryModule.cs` — line 25, lines 66–162
- `Commands/ScheduleModule.cs` — lines 54–133

---

#### 2.4 GUID Parsing Validation (MEDIUM — 3+ modules)

Repeated `Guid.TryParse` with error embed on failure:

**Files:**
- `Commands/ReminderModule.cs` — lines 271–287
- `Commands/ModNoteModule.cs` — lines 220–231
- `Commands/ScheduleModule.cs` — lines 276–288

---

#### 2.5 DateTimeOffset Unix Timestamp Conversion (LOW-MEDIUM — 40+ instances)

`new DateTimeOffset(dateTime).ToUnixTimeSeconds()` repeated throughout embed field values.

---

#### 2.6 Inconsistencies Across Modules

| Pattern | Inconsistency |
|---------|--------------|
| Ephemeral responses | Some commands always use `ephemeral: true`, others only for errors |
| Exception handling | Some catch specific exceptions, others use generic `catch (Exception)` |
| Service result checking | Mix of `result == null` vs `!success` vs `.IsSuccess` patterns |
| Logging | No standard entry/exit logging pattern for commands |

---

### 3. Data Access Layer Issues

#### 3.1 Repository Pattern Violations (HIGH)

Two repositories bypass the `Repository<T>` base class entirely:

- **`SettingsRepository.cs`** (lines 13–18) — directly injects `BotDbContext`
- **`ThemeRepository.cs`** (lines 13–18) — same issue

**Impact:** Bypasses base class logging, tracing, and performance monitoring.

---

#### 3.2 GetByIdAsync Override Pattern (MEDIUM — 7 repositories)

Seven repositories override `GetByIdAsync` with nearly identical code to add navigation property Includes:

```csharp
public override async Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
{
    if (id is not Guid guidId) { _logger.LogWarning(...); return null; }
    return await DbSet.Include(x => x.Navigation).FirstOrDefaultAsync(x => x.Id == guidId, ct);
}
```

**Files:** `ModNoteRepository`, `RatWatchRepository`, `CommandLogRepository`, `ReminderRepository`, `ModerationCaseRepository`, `MessageLogRepository`, `WatchlistRepository`

**Recommendation:** Add a protected method to `Repository<T>`:
```csharp
protected Task<T?> GetByIdWithIncludesAsync<TKey>(object id, Func<IQueryable<T>, IQueryable<T>> include, CancellationToken ct)
```

---

#### 3.3 Pagination Tuple Pattern (MEDIUM — 4+ repositories)

Identical pagination logic returning `(IEnumerable<T>, int TotalCount)`:

```csharp
var query = DbSet.AsNoTracking().Where(...);
var totalCount = await query.CountAsync(ct);
var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
return (items, totalCount);
```

**Files:** `ModerationCaseRepository`, `ReminderRepository`, `WatchlistRepository`, `RatWatchRepository`

**Recommendation:** Add `GetPagedAsync()` to `Repository<T>` base class.

---

#### 3.4 Complex Filtering Pattern (MEDIUM — 4 repositories)

10+ parameter filter methods with chained `.Where()` clauses:

**Files:**
- `CommandLogRepository.cs` — lines 285–369
- `GuildMemberRepository.cs` — lines 288–397
- `RatWatchRepository.cs` — lines 333–445
- `AuditLogRepository.cs` — lines 27–128

**Recommendation:** Consider a specification pattern or filter builder.

---

#### 3.5 GetOrCreate Pattern (LOW — 2-5 instances)

Repeated across `AssistantUsageMetricsRepository`, `GuildTtsSettingsRepository`, `UserRepository`.

---

#### 3.6 Batch Upsert Pattern (LOW — 2 instances)

Identical 500-item batching with transactions in `UserRepository` (lines 82–169) and `GuildMemberRepository` (lines 99–191).

---

### 4. Web Layer Duplication

#### 4.1 Guild Layout ViewModel Initialization (CRITICAL — 20+ pages)

Every guild-scoped Razor page initializes identical Breadcrumb, Header, and Navigation view models (~50 lines each):

```csharp
Breadcrumb = new GuildBreadcrumbViewModel { Items = new List<BreadcrumbItem> { ... } };
Header = new GuildHeaderViewModel { GuildId = guild.Id, GuildName = guild.Name, ... };
Navigation = new GuildNavBarViewModel { GuildId = guild.Id, ActiveTab = "...", ... };
```

**Files (partial):**
- `Pages/Guilds/Details.cshtml.cs` — lines 391–439
- `Pages/Guilds/Edit.cshtml.cs` — lines 131–157, 227–258
- `Pages/Guilds/ScheduledMessages/Create.cshtml.cs` — lines 168–194, 300–332
- `Pages/Guilds/ScheduledMessages/Edit.cshtml.cs` — lines 232–259, 372–398
- `Pages/Guilds/RatWatch/Index.cshtml.cs` — lines 88–116
- `Pages/Guilds/Reminders/Index.cshtml.cs` — lines 108–133
- `Pages/Guilds/Analytics/Index.cshtml.cs` — lines 131–156

**Recommendation:** Create `GuildPageModelBase` with a `PopulateGuildLayout(guild, activeTab, pageTitle)` method.

---

#### 4.2 TempData Message Properties (HIGH — 15+ pages)

Every page model declares:
```csharp
[TempData] public string? SuccessMessage { get; set; }
[TempData] public string? ErrorMessage { get; set; }
```

**Recommendation:** Move to base page model class.

---

#### 4.3 Pagination BindProperties (HIGH — 5+ pages)

Identical property declarations across page models:

```csharp
[BindProperty(SupportsGet = true)] public string SortBy { get; set; } = "Name";
[BindProperty(SupportsGet = true)] public bool SortDescending { get; set; }
[BindProperty(SupportsGet = true, Name = "pageNumber")] public int CurrentPage { get; set; } = 1;
[BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 10;
```

**Files:**
- `Pages/Guilds/Index.cshtml.cs` — lines 38–75
- `Pages/Guilds/RatWatch/Incidents.cshtml.cs` — lines 56–87
- `Pages/Guilds/Reminders/Index.cshtml.cs` — lines 72–97
- `Pages/Guilds/ScheduledMessages/Index.cshtml.cs` — lines 66–77
- `Pages/Admin/MessageLogs/Index.cshtml.cs` — lines 38–63

**Recommendation:** Create `PaginatedPageModel` base class.

---

#### 4.4 Discord Channel/User Resolution (HIGH — 5+ pages and 5+ controllers)

Identical `ResolveChannelName()` helper copied across:

**Pages:** `ScheduledMessages/Index`, `ScheduledMessages/Create`, `ScheduledMessages/Edit`, `Reminders/Index`, `Analytics/Index`
**Controllers:** `GuildsController`, `AuditLogsController`, `CommandLogsController`, `MessagesController`, `ModerationCasesController`

**Recommendation:** Extract to `IDiscordChannelResolver` service.

---

#### 4.5 Controller Error Response Pattern (MEDIUM — 5+ controllers)

Repeated `NotFound(new ApiErrorDto { ... })` pattern:

```csharp
return NotFound(new ApiErrorDto
{
    Message = "Guild not found",
    Detail = $"No guild with ID {id} exists.",
    StatusCode = StatusCodes.Status404NotFound,
    TraceId = HttpContext.GetCorrelationId()
});
```

**Files:** `GuildsController`, `AuditLogsController`, `CommandLogsController`, `MessagesController`, `ModerationCasesController`

**Recommendation:** Create `ApiControllerBase` with `NotFoundError()`, `BadRequestError()`, `ValidationError()` helpers.

---

#### 4.6 ModelState Validation + Recovery (MEDIUM — 3+ pages)

Identical OnPost validation:

```csharp
if (!ModelState.IsValid)
{
    _logger.LogWarning("ModelState invalid for guild {GuildId}. Errors: {Errors}",
        Input.GuildId, string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
    await PopulateLayoutViewModelsAsync(Input.GuildId, ct);
    return Page();
}
```

---

#### 4.7 DateTime ISO Conversion (LOW — 10+ view models)

Repeated UTC-to-ISO conversion:

```csharp
public string JoinedAtUtcIso => DateTime.SpecifyKind(JoinedAt, DateTimeKind.Utc).ToString("o");
```

**Recommendation:** Add `DateTimeExtensions.ToUtcIso()` extension.

---

#### 4.8 Identical GetTextChannels() Helper (LOW — 2 files)

`ScheduledMessages/Create.cshtml.cs` (lines 340–399) and `ScheduledMessages/Edit.cshtml.cs` (lines 449–507) contain the exact same channel-fetching logic.

---

## Detailed Findings by Category

### Inconsistencies

| Area | Inconsistency | Recommendation |
|------|--------------|----------------|
| **Logging levels** | Services use different levels for similar operations (Debug vs Info) | Define a logging level policy document |
| **Audit log error handling** | Some services try-catch audit writes, others fire-and-forget | Standardize on fire-and-forget with background error logging |
| **Activity tagging** | Different services use different attribute naming conventions | Define attribute naming standard |
| **Async patterns** | Mix of `await` and `_ = Task` for broadcasts | Document when fire-and-forget is acceptable |
| **Result types** | `null` vs `bool` vs custom result types across services | Standardize on `Result<T>` pattern |
| **Ephemeral responses** | Inconsistent across commands | Define ephemeral policy per command type |

### Non-Standard Components

| Component | Issue | Location |
|-----------|-------|----------|
| `SettingsRepository` | Doesn't inherit `Repository<T>` | `Infrastructure/Data/Repositories/SettingsRepository.cs` |
| `ThemeRepository` | Doesn't inherit `Repository<T>` | `Infrastructure/Data/Repositories/ThemeRepository.cs` |
| JSON column searching | `CachedRolesJson.Contains()` can't use indexes | `GuildMemberRepository.cs:336` |
| Cross-entity queries | Direct `Context.Users` / `Context.SoundPlayLogs` access in repos | `ReminderRepository.cs:212`, `SoundRepository.cs:128` |
| Dual Anthropic SDKs | Both `Anthropic` (v12.2.0) in Infrastructure AND `Anthropic.SDK` (v5.8.0) in Bot | `.csproj` files |

---

## Recommended Abstractions

### Phase 1: High-Impact, Low-Risk Helpers

```
src/DiscordBot.Bot/
├── Helpers/
│   ├── EmbedHelper.cs           — Error(), Success(), EmptyState(), Confirmation()
│   ├── VoiceChannelHelper.cs    — ValidateUserInVoiceChannel()
│   └── PaginationHelper.cs      — BuildComponents(), CalculatePages()
├── Extensions/
│   ├── PaginationQueryExtensions.cs  — Normalize()
│   ├── DateTimeExtensions.cs         — ToUtcIso(), ToDiscordTimestamp()
│   └── MappingExtensions.cs          — MapToDtosAsync<TEntity, TDto>()

src/DiscordBot.Core/
└── Utilities/
    └── StringExtensions.cs      — TruncateWithEllipsis()
```

### Phase 2: Base Classes

```
src/DiscordBot.Bot/
├── Pages/
│   ├── GuildPageModelBase.cs     — Layout init, TempData, guild loading
│   └── PaginatedPageModel.cs     — Sort/page/pageSize BindProperties
├── Controllers/
│   └── ApiControllerBase.cs      — NotFoundError(), BadRequestError()
├── Services/
│   └── BackgroundTaskRunner.cs   — IBackgroundTaskRunner with error logging
```

### Phase 3: Repository Improvements

```
src/DiscordBot.Infrastructure/Data/Repositories/
└── Repository.cs (extend)
    ├── GetByIdWithIncludesAsync()
    ├── GetPagedAsync()
    ├── GetOrCreateAsync()
    └── BatchUpsertAsync()
```

### Phase 4: Architectural Improvements

- Split `PlaybackService` (978 lines) → `IPlaybackQueue` + `IAudioStreamer` + `IFfmpegTranscoder`
- Split `SearchService` (947 lines) → `ISearchProvider<T>` implementations per category
- Split `AlertMonitoringService` (629 lines) → `IMetricsCollector` + `IAlertManager`
- Refactor `SettingsRepository` and `ThemeRepository` to inherit `Repository<T>`
- Consider a service activity decorator/interceptor to eliminate the 13-line tracing boilerplate

---

## Prioritized Refactoring Roadmap

| Phase | Task | Impact | Risk | Est. LOC Saved |
|:-----:|------|:------:|:----:|:--------------:|
| **1** | Create `EmbedHelper` (error/success/empty) | CRITICAL | LOW | ~1,200 |
| **1** | Create `PaginationQuery.Normalize()` | HIGH | LOW | ~100 |
| **1** | Create `DateTimeExtensions.ToUtcIso()` | MEDIUM | LOW | ~60 |
| **1** | Create `StringExtensions.TruncateWithEllipsis()` | LOW | LOW | ~30 |
| **2** | Create `GuildPageModelBase` | HIGH | MEDIUM | ~1,000 |
| **2** | Create `PaginatedPageModel` | HIGH | MEDIUM | ~150 |
| **2** | Create `ApiControllerBase` | MEDIUM | LOW | ~200 |
| **2** | Extract `IDiscordChannelResolver` service | HIGH | LOW | ~200 |
| **2** | Extract `VoiceChannelHelper` | MEDIUM | LOW | ~100 |
| **2** | Create `IBackgroundTaskRunner` | HIGH | MEDIUM | ~100 |
| **3** | Extend `Repository<T>` with shared patterns | MEDIUM | MEDIUM | ~300 |
| **3** | Fix `SettingsRepository` / `ThemeRepository` | MEDIUM | LOW | ~50 |
| **3** | Extract `IDiscordUserResolver` with caching | MEDIUM | LOW | ~100 |
| **4** | Split `PlaybackService` into 3 services | HIGH | HIGH | ~0 (complexity) |
| **4** | Split `SearchService` with `ISearchProvider<T>` | MEDIUM | HIGH | ~0 (complexity) |
| **4** | Service activity interceptor/decorator | HIGH | HIGH | ~650 |
| **4** | Resolve dual Anthropic SDK dependency | LOW | MEDIUM | — |

**Total estimated LOC reduction from Phases 1–3:** ~3,500 lines
**Total estimated complexity reduction from Phase 4:** Major (3 oversized services split, tracing boilerplate eliminated)

---

## Appendix: File Reference

All paths are relative to `/home/user/discordbot/src/DiscordBot.Bot/` unless otherwise noted.

### Most-Affected Service Files
- `Services/AlertMonitoringService.cs` (629 lines, 8+ responsibilities)
- `Services/PlaybackService.cs` (978 lines, 7+ responsibilities)
- `Services/SearchService.cs` (947 lines, 6+ responsibilities)
- `Services/NotificationService.cs` (689 lines, 8+ responsibilities)
- `Services/BotHostedService.cs` (11+ fire-and-forget instances)

### Most-Affected Command Modules
- `Commands/ScheduleModule.cs` (14 error embeds)
- `Commands/TtsModule.cs` (12 error embeds)
- `Commands/SoundboardModule.cs` (6 error embeds, voice checks)
- `Commands/ModTagModule.cs` (5 error embeds, 5 success embeds)

### Most-Affected Pages
- `Pages/Guilds/ScheduledMessages/Create.cshtml.cs`
- `Pages/Guilds/ScheduledMessages/Edit.cshtml.cs`
- `Pages/Guilds/Details.cshtml.cs`
- `Pages/Guilds/Edit.cshtml.cs`
- `Pages/Admin/Performance/Index.cshtml.cs`

### Most-Affected Controllers
- `Controllers/GuildsController.cs`
- `Controllers/AuditLogsController.cs`
- `Controllers/CommandLogsController.cs`
- `Controllers/MessagesController.cs`
- `Controllers/ModerationCasesController.cs`

### Repository Files Needing Refactoring
- `Infrastructure/Data/Repositories/SettingsRepository.cs` — pattern violation
- `Infrastructure/Data/Repositories/ThemeRepository.cs` — pattern violation
- `Infrastructure/Data/Repositories/Repository.cs` — needs shared methods added
