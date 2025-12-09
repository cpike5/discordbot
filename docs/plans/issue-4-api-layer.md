# Implementation Plan: Issue #4 - API Layer & Management Endpoints

## Overview

This document provides the implementation plan for Phase 4 of the Discord Bot Management System MVP: the REST API layer. The API will expose endpoints for health monitoring, bot management, guild operations, and command log retrieval.

### Current State

- **Completed**: Bot foundation (Phase 1), Data layer with Entity Framework Core (Phase 2), Admin commands and permissions (Phase 3)
- **Existing Components**:
  - Entities: `Guild`, `User`, `CommandLog` in `DiscordBot.Core`
  - DTOs: `BotStatusDto`, `GuildInfoDto` in `DiscordBot.Core`
  - Repositories: `IGuildRepository`, `IUserRepository`, `ICommandLogRepository` with implementations
  - Basic health endpoint exists in `Program.cs` as inline minimal API
  - No formal controllers, services, or Swagger configuration

### Goals

1. Implement structured API controllers following REST conventions
2. Create service layer abstractions between controllers and repositories
3. Add comprehensive DTOs for all request/response models
4. Configure Swagger/OpenAPI documentation
5. Implement proper error handling and validation

---

## Architecture Considerations

### Layering Strategy

```
Controllers (DiscordBot.Bot/Controllers)
    |
    v
Services (DiscordBot.Bot/Services + Interfaces in DiscordBot.Core)
    |
    v
Repositories (DiscordBot.Infrastructure/Data/Repositories)
    |
    v
DbContext (DiscordBot.Infrastructure/Data)
```

### Design Decisions

| Decision | Rationale |
|----------|-----------|
| Service interfaces in Core, implementations in Bot | Maintains clean architecture; Core has no dependencies on Bot |
| Manual DTO mapping (no AutoMapper) | Reduces dependencies for MVP; mapping is straightforward |
| Synchronous validation in controllers | Keeps validation close to entry point; easier to test |
| API versioning deferred | MVP scope; can add later without breaking changes |

### Integration Points

- **DiscordSocketClient**: Required for real-time bot status (connection state, latency, live guild list)
- **Repositories**: For persisted data (guild settings, command logs)
- **IHostApplicationLifetime**: For bot restart/shutdown operations

### Security Considerations

- API authentication/authorization deferred to future phase
- Rate limiting deferred to future phase
- All endpoints initially public (development only)

---

## Component Breakdown

### 1. DTOs (DiscordBot.Core/DTOs)

#### New Files to Create

| File | Purpose |
|------|---------|
| `HealthResponseDto.cs` | Health check response model |
| `GuildDto.cs` | Full guild data for API responses |
| `GuildUpdateRequestDto.cs` | Request model for updating guild settings |
| `CommandLogDto.cs` | Command log entry for API responses |
| `CommandLogQueryDto.cs` | Query parameters for filtering command logs |
| `ApiErrorDto.cs` | Standardized error response model |
| `PaginatedResponseDto.cs` | Generic wrapper for paginated results |

#### Existing DTOs to Retain

- `BotStatusDto.cs` - Already suitable for API use
- `GuildInfoDto.cs` - Suitable for lightweight guild listings

### 2. Service Interfaces (DiscordBot.Core/Interfaces)

#### New Files to Create

| File | Purpose |
|------|---------|
| `IBotService.cs` | Bot status, restart, shutdown operations |
| `IGuildService.cs` | Guild CRUD and settings management |
| `ICommandLogService.cs` | Command log retrieval and statistics |

### 3. Service Implementations (DiscordBot.Bot/Services)

#### New Files to Create

| File | Purpose |
|------|---------|
| `BotService.cs` | Implements `IBotService` with DiscordSocketClient |
| `GuildService.cs` | Implements `IGuildService` with repository |
| `CommandLogService.cs` | Implements `ICommandLogService` with repository |

### 4. Controllers (DiscordBot.Bot/Controllers)

#### New Files to Create

| File | Purpose |
|------|---------|
| `HealthController.cs` | System health and readiness checks |
| `BotController.cs` | Bot status, restart, shutdown |
| `GuildsController.cs` | Guild listing, details, settings updates |
| `CommandLogsController.cs` | Command log retrieval and statistics |

### 5. Configuration

#### Files to Modify

| File | Changes |
|------|---------|
| `Program.cs` | Add Swagger, remove inline health endpoint, register services |
| `DiscordBot.Bot.csproj` | Add Swashbuckle.AspNetCore package |

---

## Detailed Task Plan

### Task 1: Add NuGet Packages

**File**: `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\DiscordBot.Bot.csproj`

Add package references:
```xml
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
```

**Acceptance Criteria**:
- Project builds successfully with new packages
- No version conflicts

---

### Task 2: Create DTO Models

#### 2.1 HealthResponseDto

**File**: `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\DTOs\HealthResponseDto.cs`

```csharp
namespace DiscordBot.Core.DTOs;

public class HealthResponseDto
{
    public string Status { get; set; } = "Healthy";
    public DateTime Timestamp { get; set; }
    public string Version { get; set; } = string.Empty;
    public Dictionary<string, string> Checks { get; set; } = new();
}
```

#### 2.2 GuildDto

**File**: `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\DTOs\GuildDto.cs`

```csharp
namespace DiscordBot.Core.DTOs;

public class GuildDto
{
    public ulong Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; }
    public string? Prefix { get; set; }
    public string? Settings { get; set; }
    public int? MemberCount { get; set; }  // From live Discord data
    public string? IconUrl { get; set; }   // From live Discord data
}
```

#### 2.3 GuildUpdateRequestDto

**File**: `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\DTOs\GuildUpdateRequestDto.cs`

```csharp
namespace DiscordBot.Core.DTOs;

public class GuildUpdateRequestDto
{
    public string? Prefix { get; set; }
    public string? Settings { get; set; }
    public bool? IsActive { get; set; }
}
```

#### 2.4 CommandLogDto

**File**: `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\DTOs\CommandLogDto.cs`

```csharp
namespace DiscordBot.Core.DTOs;

public class CommandLogDto
{
    public Guid Id { get; set; }
    public ulong? GuildId { get; set; }
    public string? GuildName { get; set; }
    public ulong UserId { get; set; }
    public string? Username { get; set; }
    public string CommandName { get; set; } = string.Empty;
    public string? Parameters { get; set; }
    public DateTime ExecutedAt { get; set; }
    public int ResponseTimeMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
```

#### 2.5 CommandLogQueryDto

**File**: `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\DTOs\CommandLogQueryDto.cs`

```csharp
namespace DiscordBot.Core.DTOs;

public class CommandLogQueryDto
{
    public ulong? GuildId { get; set; }
    public ulong? UserId { get; set; }
    public string? CommandName { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool? SuccessOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
```

#### 2.6 ApiErrorDto

**File**: `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\DTOs\ApiErrorDto.cs`

```csharp
namespace DiscordBot.Core.DTOs;

public class ApiErrorDto
{
    public string Message { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public int StatusCode { get; set; }
    public string? TraceId { get; set; }
}
```

#### 2.7 PaginatedResponseDto

**File**: `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\DTOs\PaginatedResponseDto.cs`

```csharp
namespace DiscordBot.Core.DTOs;

public class PaginatedResponseDto<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
```

**Acceptance Criteria**:
- All DTOs compile without errors
- DTOs follow naming conventions
- XML documentation added to all public members

---

### Task 3: Create Service Interfaces

#### 3.1 IBotService

**File**: `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Interfaces\IBotService.cs`

```csharp
using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

public interface IBotService
{
    BotStatusDto GetStatus();
    IReadOnlyList<GuildInfoDto> GetConnectedGuilds();
    Task RestartAsync(CancellationToken cancellationToken = default);
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
```

#### 3.2 IGuildService

**File**: `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Interfaces\IGuildService.cs`

```csharp
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

public interface IGuildService
{
    Task<IReadOnlyList<GuildDto>> GetAllGuildsAsync(CancellationToken cancellationToken = default);
    Task<GuildDto?> GetGuildByIdAsync(ulong guildId, CancellationToken cancellationToken = default);
    Task<GuildDto?> UpdateGuildAsync(ulong guildId, GuildUpdateRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> SyncGuildAsync(ulong guildId, CancellationToken cancellationToken = default);
}
```

#### 3.3 ICommandLogService

**File**: `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Interfaces\ICommandLogService.cs`

```csharp
using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

public interface ICommandLogService
{
    Task<PaginatedResponseDto<CommandLogDto>> GetLogsAsync(CommandLogQueryDto query, CancellationToken cancellationToken = default);
    Task<IDictionary<string, int>> GetCommandStatsAsync(DateTime? since = null, CancellationToken cancellationToken = default);
}
```

**Acceptance Criteria**:
- Interfaces defined with async methods where appropriate
- Return types use DTOs, not entities
- CancellationToken parameter on all async methods

---

### Task 4: Implement Services

#### 4.1 BotService

**File**: `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Services\BotService.cs`

Key implementation points:
- Inject `DiscordSocketClient` (singleton)
- Inject `IHostApplicationLifetime` for shutdown
- `GetStatus()` reads live data from client
- `GetConnectedGuilds()` maps from `SocketGuild` to `GuildInfoDto`
- `ShutdownAsync()` calls `_lifetime.StopApplication()`
- `RestartAsync()` deferred (placeholder returns NotSupportedException)

#### 4.2 GuildService

**File**: `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Services\GuildService.cs`

Key implementation points:
- Inject `IGuildRepository`
- Inject `DiscordSocketClient` for live guild data enrichment
- `GetAllGuildsAsync()` merges DB data with live Discord data
- `UpdateGuildAsync()` validates guild exists, updates via repository
- `SyncGuildAsync()` creates/updates guild record from Discord

#### 4.3 CommandLogService

**File**: `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Services\CommandLogService.cs`

Key implementation points:
- Inject `ICommandLogRepository`
- `GetLogsAsync()` applies query filters and pagination
- `GetCommandStatsAsync()` delegates to repository

**Acceptance Criteria**:
- Services implement their interfaces correctly
- Proper logging with `ILogger<T>`
- Exception handling with meaningful error messages
- No direct entity exposure; all returns are DTOs

---

### Task 5: Implement Controllers

#### 5.1 HealthController

**File**: `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Controllers\HealthController.cs`

```csharp
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponseDto), StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        // Return health status with DB connectivity check
    }
}
```

Endpoints:
- `GET /api/health` - Returns health status

#### 5.2 BotController

**File**: `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Controllers\BotController.cs`

```csharp
[ApiController]
[Route("api/[controller]")]
public class BotController : ControllerBase
{
    // GET /api/bot/status
    // POST /api/bot/restart
    // POST /api/bot/shutdown
}
```

Endpoints:
- `GET /api/bot/status` - Returns `BotStatusDto`
- `POST /api/bot/restart` - Triggers restart (deferred)
- `POST /api/bot/shutdown` - Triggers graceful shutdown

#### 5.3 GuildsController

**File**: `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Controllers\GuildsController.cs`

```csharp
[ApiController]
[Route("api/[controller]")]
public class GuildsController : ControllerBase
{
    // GET /api/guilds
    // GET /api/guilds/{id}
    // PUT /api/guilds/{id}
    // POST /api/guilds/{id}/sync
}
```

Endpoints:
- `GET /api/guilds` - List all guilds (merged DB + Discord)
- `GET /api/guilds/{id}` - Get single guild details
- `PUT /api/guilds/{id}` - Update guild settings
- `POST /api/guilds/{id}/sync` - Sync guild data from Discord

#### 5.4 CommandLogsController

**File**: `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Controllers\CommandLogsController.cs`

```csharp
[ApiController]
[Route("api/[controller]")]
public class CommandLogsController : ControllerBase
{
    // GET /api/commandlogs
    // GET /api/commandlogs/stats
}
```

Endpoints:
- `GET /api/commandlogs` - Query command logs with filters
- `GET /api/commandlogs/stats` - Get command usage statistics

**Acceptance Criteria**:
- Controllers use `[ApiController]` attribute
- All actions have `[ProducesResponseType]` attributes
- Proper HTTP status codes returned
- XML documentation for Swagger

---

### Task 6: Configure Swagger and Update Program.cs

**File**: `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Program.cs`

Changes:
1. Remove inline `/health` endpoint
2. Add Swagger configuration:
```csharp
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Discord Bot Management API",
        Version = "v1",
        Description = "API for managing the Discord bot"
    });
    // Include XML comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});
```
3. Register services:
```csharp
builder.Services.AddScoped<IBotService, BotService>();
builder.Services.AddScoped<IGuildService, GuildService>();
builder.Services.AddScoped<ICommandLogService, CommandLogService>();
```
4. Add Swagger middleware:
```csharp
app.UseSwagger();
app.UseSwaggerUI();
```

**File**: `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\DiscordBot.Bot.csproj`

Add XML documentation generation:
```xml
<PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

**Acceptance Criteria**:
- Swagger UI accessible at `/swagger`
- All endpoints documented with descriptions
- XML comments visible in Swagger UI
- Application starts without errors

---

### Task 7: Write Unit Tests

**New Test Files**:

| File | Purpose |
|------|---------|
| `C:\Users\cpike\workspace\discordbot\tests\DiscordBot.Tests\Services\BotServiceTests.cs` | Test BotService methods |
| `C:\Users\cpike\workspace\discordbot\tests\DiscordBot.Tests\Services\GuildServiceTests.cs` | Test GuildService methods |
| `C:\Users\cpike\workspace\discordbot\tests\DiscordBot.Tests\Services\CommandLogServiceTests.cs` | Test CommandLogService methods |
| `C:\Users\cpike\workspace\discordbot\tests\DiscordBot.Tests\Controllers\HealthControllerTests.cs` | Test HealthController |
| `C:\Users\cpike\workspace\discordbot\tests\DiscordBot.Tests\Controllers\GuildsControllerTests.cs` | Test GuildsController |

**Acceptance Criteria**:
- All service methods have unit tests
- Controllers tested with mocked services
- Tests use FluentAssertions and Moq
- All tests pass

---

## File Summary

### New Files (17 total)

**DiscordBot.Core/DTOs/** (7 files):
1. `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\DTOs\HealthResponseDto.cs`
2. `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\DTOs\GuildDto.cs`
3. `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\DTOs\GuildUpdateRequestDto.cs`
4. `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\DTOs\CommandLogDto.cs`
5. `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\DTOs\CommandLogQueryDto.cs`
6. `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\DTOs\ApiErrorDto.cs`
7. `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\DTOs\PaginatedResponseDto.cs`

**DiscordBot.Core/Interfaces/** (3 files):
8. `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Interfaces\IBotService.cs`
9. `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Interfaces\IGuildService.cs`
10. `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Interfaces\ICommandLogService.cs`

**DiscordBot.Bot/Services/** (3 files):
11. `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Services\BotService.cs`
12. `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Services\GuildService.cs`
13. `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Services\CommandLogService.cs`

**DiscordBot.Bot/Controllers/** (4 files):
14. `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Controllers\HealthController.cs`
15. `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Controllers\BotController.cs`
16. `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Controllers\GuildsController.cs`
17. `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Controllers\CommandLogsController.cs`

### Modified Files (2 total)

1. `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Program.cs`
2. `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\DiscordBot.Bot.csproj`

### Test Files (5 total)

1. `C:\Users\cpike\workspace\discordbot\tests\DiscordBot.Tests\Services\BotServiceTests.cs`
2. `C:\Users\cpike\workspace\discordbot\tests\DiscordBot.Tests\Services\GuildServiceTests.cs`
3. `C:\Users\cpike\workspace\discordbot\tests\DiscordBot.Tests\Services\CommandLogServiceTests.cs`
4. `C:\Users\cpike\workspace\discordbot\tests\DiscordBot.Tests\Controllers\HealthControllerTests.cs`
5. `C:\Users\cpike\workspace\discordbot\tests\DiscordBot.Tests\Controllers\GuildsControllerTests.cs`

---

## Dependency Graph

```
Task 1: NuGet Packages
    |
    v
Task 2: DTOs (can run parallel with Task 3)
    |
    +---> Task 3: Service Interfaces
              |
              v
          Task 4: Service Implementations
              |
              v
          Task 5: Controllers
              |
              v
          Task 6: Swagger & Program.cs Configuration
              |
              v
          Task 7: Unit Tests
```

### Parallelization Opportunities

- Tasks 2 and 3 can be completed in parallel
- Individual DTOs within Task 2 can be created independently
- Individual service implementations in Task 4 can be developed in parallel
- Controller implementations in Task 5 can be developed in parallel

---

## Acceptance Criteria Summary

| Criterion | Endpoint/Feature | Expected Result |
|-----------|------------------|-----------------|
| Health check works | `GET /api/health` | Returns 200 with status |
| Bot status available | `GET /api/bot/status` | Returns `BotStatusDto` |
| Guild listing works | `GET /api/guilds` | Returns list of guilds |
| Guild update works | `PUT /api/guilds/{id}` | Updates and returns guild |
| Command logs queryable | `GET /api/commandlogs` | Returns paginated logs |
| Swagger functional | `/swagger` | Displays all endpoints |
| Invalid ID handled | `GET /api/guilds/0` | Returns 404 |
| Invalid request handled | `PUT /api/guilds/{id}` with bad data | Returns 400 |

---

## Risks and Mitigations

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| DiscordSocketClient threading issues | High | Medium | Use thread-safe access patterns; document concurrency expectations |
| Swagger conflicts with minimal API | Low | Low | Remove inline health endpoint before adding controller |
| Service/Repository circular dependencies | Medium | Low | Keep service interfaces in Core; implementations in Bot |
| Large guild lists cause performance issues | Medium | Medium | Implement pagination from the start |
| Authentication bypass in development | High | High | Document clearly; defer auth to future phase |

---

## Subagent Assignments

### dotnet-specialist

**Primary Responsibilities**:
- All code implementation (DTOs, interfaces, services, controllers)
- NuGet package additions
- Program.cs modifications
- Unit test implementation

**Deliverables**:
- 17 new source files
- 2 modified files
- 5 test files
- Working Swagger UI

### docs-writer

**Deliverables** (after implementation):
- API endpoint documentation
- Swagger configuration guide
- Update to MVP plan marking Phase 4 complete

---

## Implementation Order Recommendation

1. **Day 1**: Tasks 1-3 (Packages, DTOs, Interfaces)
2. **Day 2**: Task 4 (Service Implementations)
3. **Day 3**: Tasks 5-6 (Controllers, Swagger)
4. **Day 4**: Task 7 (Unit Tests) + Integration Testing

---

*Document Version: 1.0*
*Created: December 2024*
*Status: Ready for Implementation*
