# Discord Bot Management System - MVP Implementation Plan

## Executive Summary

This document outlines the Minimum Viable Product (MVP) implementation plan for a Discord bot management system built on .NET 8 with Discord.NET. The MVP focuses on establishing a functional bot with essential command capabilities, a basic API layer for management, and foundational data persistence using the repository pattern.

**Primary Objectives:**
- Establish a stable Discord.NET bot with proper lifecycle management
- Implement core slash commands (/ping, /status, /shutdown) with permission controls
- Create a command framework supporting detection, registration, and handling
- Build foundational data layer with EF Core and repository pattern
- Expose basic API endpoints for bot and guild management
- Configure structured logging with Serilog

**Out of Scope for MVP:**
- Razor Pages UI (deferred to Phase 2)
- Advanced interactivity (modals, complex select menus)
- Text-based prefix commands
- External log aggregation services
- Production database configuration (MySQL/PostgreSQL/MSSQL)

---

## Architecture Overview

### Solution Structure

```
DiscordBot.sln
|
+-- src/
|   +-- DiscordBot.Core/           # Domain entities, interfaces, DTOs
|   +-- DiscordBot.Infrastructure/ # EF Core, repositories, logging config
|   +-- DiscordBot.Bot/            # Web API + Bot hosted service (combined)
|
+-- tests/
    +-- DiscordBot.Tests/          # Unit and integration tests
```

### Layer Responsibilities

| Layer | Project | Responsibility |
|-------|---------|----------------|
| Domain | DiscordBot.Core | Entities, interfaces, DTOs, enums, constants |
| Infrastructure | DiscordBot.Infrastructure | Data access, EF Core DbContext, repositories, Serilog configuration |
| Application | DiscordBot.Bot | Web API controllers, bot hosted service, command modules, DI composition |

### High-Level Data Flow

```
Discord Gateway  <-->  DiscordSocketClient (Singleton)
                              |
                              v
                       BotHostedService
                              |
                    +---------+---------+
                    |                   |
                    v                   v
            Command Modules      Event Handlers
                    |                   |
                    v                   v
              Service Layer  <-->  Repositories
                              |
                              v
                         DbContext
                              |
                              v
                      SQLite Database
```

---

## Component Breakdown

### 1. Discord.NET Bot Implementation

**Location:** `DiscordBot.Bot/Services/`

| Component | Description | Dependencies |
|-----------|-------------|--------------|
| `BotHostedService` | IHostedService managing bot lifecycle | DiscordSocketClient, ILogger |
| `DiscordSocketClient` | Singleton client for Discord gateway | Discord.NET |
| `BotConfiguration` | Strongly-typed options for bot settings | IOptions pattern |

**Key Files to Create:**
- `Services/BotHostedService.cs`
- `Services/BotConfiguration.cs`
- `Extensions/DiscordServiceExtensions.cs`

### 2. Command Framework

**Location:** `DiscordBot.Bot/Commands/`

| Component | Description | Dependencies |
|-----------|-------------|--------------|
| `InteractionHandler` | Discovers and registers command modules | InteractionService, IServiceProvider |
| `CommandModule` base | Abstract base for command modules | InteractionModuleBase |
| `PingCommand` | Health check slash command | None |
| `AdminModule` | Status/shutdown commands | IGuildService, BotHostedService |

**Key Files to Create:**
- `Handlers/InteractionHandler.cs`
- `Commands/GeneralModule.cs` (contains /ping)
- `Commands/AdminModule.cs` (contains /status, /shutdown)
- `Preconditions/RequireAdminAttribute.cs`

### 3. Data Layer

**Location:** `DiscordBot.Core/Entities/` and `DiscordBot.Infrastructure/Data/`

| Component | Description | Location |
|-----------|-------------|----------|
| `Guild` entity | Server configuration and settings | Core |
| `User` entity | User preferences and metadata | Core |
| `CommandLog` entity | Audit trail for command usage | Core |
| `BotDbContext` | EF Core DbContext | Infrastructure |
| `IGuildRepository` | Guild data access interface | Core |
| `GuildRepository` | Guild repository implementation | Infrastructure |

**Core Entities (MVP):**

```
Guild
- Id (ulong, PK)
- Name (string)
- JoinedAt (DateTime)
- IsActive (bool)
- Prefix (string, nullable)
- Settings (JSON column)

User
- Id (ulong, PK)
- Username (string)
- Discriminator (string)
- FirstSeenAt (DateTime)
- LastSeenAt (DateTime)

CommandLog
- Id (Guid, PK)
- GuildId (ulong, FK)
- UserId (ulong, FK)
- CommandName (string)
- Parameters (string, JSON)
- ExecutedAt (DateTime)
- ResponseTimeMs (int)
- Success (bool)
- ErrorMessage (string, nullable)
```

### 4. API Layer

**Location:** `DiscordBot.Bot/Controllers/`

| Endpoint Group | Description | Endpoints |
|----------------|-------------|-----------|
| Health | System health and status | GET /api/health |
| Bot | Bot management | GET /api/bot/status, POST /api/bot/restart |
| Guilds | Guild management | GET /api/guilds, GET /api/guilds/{id}, PUT /api/guilds/{id} |
| Commands | Command configuration | GET /api/commands, GET /api/commands/logs |

### 5. Logging Configuration

**Location:** `DiscordBot.Infrastructure/Logging/`

| Component | Description |
|-----------|-------------|
| `SerilogConfiguration` | Extension methods for Serilog setup |
| Console sink | Development logging |
| File sink | Rolling file logs with retention |
| Structured properties | Correlation IDs, guild context |

---

## Implementation Phases

### Phase 1: Foundation (Week 1)

**Priority: Critical**

**Objective:** Establish working bot that connects to Discord and responds to basic health check.

**Tasks:**

1.1 **Bot Hosted Service Setup**
- Create `BotHostedService` implementing `IHostedService`
- Configure `DiscordSocketClient` as singleton
- Implement `StartAsync` with login and connection
- Implement `StopAsync` with graceful disconnect
- Add reconnection logic with exponential backoff

1.2 **Configuration**
- Create `BotConfiguration` options class
- Configure `appsettings.json` with token placeholder
- Add `appsettings.Development.json` with local overrides
- Implement user secrets for token storage

1.3 **Logging Foundation**
- Configure Serilog in `Program.cs`
- Set up console sink with output template
- Set up file sink with rolling configuration
- Configure log levels per namespace

1.4 **Basic Slash Command**
- Implement `InteractionHandler` for command discovery
- Create `GeneralModule` with `/ping` command
- Register commands on bot ready event
- Handle interaction responses

**Deliverables:**
- Bot connects to Discord successfully
- `/ping` command responds with latency
- Structured logs visible in console and file
- Clean shutdown on SIGTERM/SIGINT

**Acceptance Criteria:**
- [ ] Bot appears online in Discord
- [ ] `/ping` returns response within 3 seconds
- [ ] Logs include timestamp, level, and message
- [ ] `dotnet run` starts bot without errors
- [ ] Ctrl+C triggers graceful shutdown

---

### Phase 2: Data Layer (Week 2)

**Priority: High**

**Objective:** Establish data persistence with repository pattern.

**Tasks:**

2.1 **Entity Definitions**
- Create `Guild` entity in Core
- Create `User` entity in Core
- Create `CommandLog` entity in Core
- Define entity configurations (Fluent API)

2.2 **DbContext Setup**
- Create `BotDbContext` in Infrastructure
- Configure SQLite connection for development
- Add entity configurations
- Create initial migration

2.3 **Repository Pattern**
- Define `IRepository<T>` generic interface
- Define `IGuildRepository` with specific methods
- Define `IUserRepository` with specific methods
- Define `ICommandLogRepository` with specific methods
- Implement repositories in Infrastructure

2.4 **Unit of Work (Optional for MVP)**
- Define `IUnitOfWork` interface
- Implement in Infrastructure
- Wire into DI container

**Deliverables:**
- EF Core migrations generated
- Repositories injectable via DI
- Database created on first run

**Acceptance Criteria:**
- [x] `dotnet ef migrations add` succeeds
- [x] `dotnet ef database update` creates SQLite file
- [x] Repository methods return expected data
- [x] Unit tests pass for repository operations

**Status:** ✓ COMPLETED

**Documentation:**
- [Database Schema](database-schema.md) - Entity definitions, relationships, and SQL schema
- [Repository Pattern](repository-pattern.md) - Implementation guide and usage examples

---

### Phase 3: Admin Commands & Permissions (Week 3)

**Priority: High**

**Objective:** Implement admin commands with permission-based access control.

**Tasks:**

3.1 **Admin Module**
- Create `AdminModule` with slash commands
- Implement `/status` command (bot uptime, guild count, latency)
- Implement `/shutdown` command (graceful bot shutdown)
- Implement `/guilds` command (list connected guilds)

3.2 **Permission Framework**
- Create `RequireAdminAttribute` precondition
- Create `RequireOwnerAttribute` precondition
- Configure command permissions in Discord
- Handle permission denied responses

3.3 **Command Logging**
- Log command executions to `CommandLog` entity
- Include execution time metrics
- Log errors with stack traces
- Add correlation IDs

3.4 **Rate Limiting**
- Implement per-user rate limiting
- Implement per-guild rate limiting
- Create `RateLimitAttribute` precondition
- Configure default and per-command limits

**Deliverables:**
- Admin commands functional
- Permission checks enforced
- Command audit trail persisted

**Acceptance Criteria:**
- [x] `/status` returns accurate metrics
- [x] `/shutdown` only works for bot owner
- [x] Non-admins receive permission denied message
- [x] Command logs appear in database
- [x] Rate limited users receive cooldown message

**Status:** ✓ COMPLETED

**Documentation:**
- [Admin Commands](admin-commands.md) - Slash command reference and configuration
- [Permissions System](permissions.md) - Precondition attributes and custom permission logic

---

### Phase 4: API Layer (Week 4)

**Priority: Medium**

**Objective:** Expose REST API for external management.

**Tasks:**

4.1 **API Controllers**
- Create `HealthController` with status endpoint
- Create `BotController` for bot management
- Create `GuildsController` for guild operations
- Create `CommandsController` for command logs

4.2 **DTOs**
- Create response DTOs in Core
- Create request DTOs for updates
- Implement AutoMapper profiles (or manual mapping)

4.3 **Service Layer**
- Create `IBotService` interface
- Create `IGuildService` interface
- Create `ICommandService` interface
- Implement services with repository injection

4.4 **API Documentation**
- Add Swagger/OpenAPI configuration
- Document endpoints with XML comments
- Configure response types

**Deliverables:**
- API endpoints accessible via HTTP
- Swagger UI available at `/swagger`
- Services encapsulate business logic

**Acceptance Criteria:**
- [x] GET /api/health returns 200
- [x] GET /api/guilds returns list of guilds
- [x] PUT /api/guilds/{id} updates guild settings
- [x] Swagger UI displays all endpoints
- [x] Invalid requests return appropriate status codes

**Status:** ✓ COMPLETED

**Documentation:**
- [API Endpoints](api-endpoints.md) - Complete REST API reference with request/response examples

---

### Phase 5: Interactivity (Week 5)

**Priority: Medium**

**Objective:** Add button and select menu support.

**Tasks:**

5.1 **Component Handlers**
- Create component interaction handler
- Implement button click handlers
- Implement select menu handlers
- Add component ID conventions

5.2 **Interactive Commands**
- Add confirmation buttons to `/shutdown`
- Add pagination to `/guilds` command
- Create help command with category selection

5.3 **State Management**
- Implement interaction state storage
- Handle interaction timeouts
- Clean up expired interactions

**Deliverables:**
- Buttons respond to clicks
- Select menus filter results
- Interactive workflows complete

**Acceptance Criteria:**
- [x] Shutdown requires confirmation button
- [x] Guild list paginates with buttons
- [x] Components timeout after 15 minutes
- [x] Unknown component clicks handled gracefully

**Status:** ✓ COMPLETED

**Documentation:**
- [Interactive Components](interactive-components.md) - Component interactions, state management, and developer guide
- [Admin Commands](admin-commands.md) - Updated with interactive `/shutdown` and `/guilds` workflows

---

## Key Design Decisions

### 1. Combined API and Bot in Single Host

**Decision:** Host both Web API and Discord bot in `DiscordBot.Bot` project.

**Rationale:**
- Simplifies deployment for MVP
- Shared DI container reduces duplication
- Single process to monitor
- Easy to split later if needed

**Trade-offs:**
- Bot and API share same lifecycle
- Resource contention possible under load

### 2. Repository Pattern over Direct DbContext

**Decision:** Use repository pattern with interfaces in Core.

**Rationale:**
- Enables unit testing with mocks
- Abstracts data access implementation
- Supports future database provider changes
- Aligns with clean architecture principles

**Trade-offs:**
- Additional abstraction layer
- More interfaces to maintain

### 3. Slash Commands Only (No Prefix Commands)

**Decision:** Implement only slash commands for MVP.

**Rationale:**
- Discord's recommended approach
- Better discoverability for users
- Built-in permission integration
- Simpler implementation

**Trade-offs:**
- No legacy command support
- Requires bot to have `applications.commands` scope

### 4. SQLite for All Non-Production Environments

**Decision:** Use SQLite for development and testing.

**Rationale:**
- Zero configuration required
- Fast for local development
- Easy to reset/recreate
- Same EF Core patterns apply

**Trade-offs:**
- Some SQL features unavailable
- Not representative of production performance

### 5. Serilog with ILogger Abstraction

**Decision:** Use Serilog as provider, inject `ILogger<T>`.

**Rationale:**
- Follows Microsoft best practices
- Serilog provides rich sink ecosystem
- Application code remains provider-agnostic
- Easy to swap providers if needed

**Trade-offs:**
- Two packages to understand
- Configuration split between Serilog and MS Logging

---

## Risk Considerations

### Technical Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Discord.NET breaking changes | Low | High | Pin specific version (3.18.0), monitor releases |
| Rate limiting by Discord | Medium | Medium | Implement proper rate limiting, respect gateway limits |
| SQLite concurrency issues | Low | Medium | Use proper connection handling, consider WAL mode |
| Memory leaks in long-running bot | Medium | High | Implement proper disposal, monitor memory usage |

### Operational Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Token exposure | Low | Critical | Use user secrets, never commit tokens |
| Bot goes offline unnoticed | Medium | Medium | Add health check endpoint, external monitoring |
| Database corruption | Low | High | Regular backups, migration testing |

### Schedule Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Discord API changes | Low | Medium | Review Discord changelog regularly |
| Scope creep | High | Medium | Strict MVP boundaries, defer features |
| Integration complexity | Medium | Medium | Early integration testing, incremental builds |

---

## Subagent Task Assignments

### dotnet-specialist

**Phase 1 Deliverables:**
- `BotHostedService` implementation with full lifecycle management
- `DiscordSocketClient` DI registration and configuration
- `InteractionHandler` for command module discovery
- `GeneralModule` with `/ping` command
- `Program.cs` composition root setup

**Phase 2 Deliverables:**
- Entity classes with EF Core configurations
- `BotDbContext` with SQLite configuration
- Repository interfaces and implementations
- Initial EF Core migration

**Phase 3 Deliverables:**
- `AdminModule` with admin commands
- Precondition attributes for permissions
- Rate limiting implementation
- Command logging integration

**Phase 4 Deliverables:**
- API controllers with CRUD operations
- Service layer implementations
- DTOs and mapping
- Swagger configuration

**Phase 5 Deliverables:**
- Component interaction handlers
- Interactive command implementations
- State management for interactions

### docs-writer

**Deliverables:**
- API endpoint documentation
- Configuration guide (appsettings, user secrets)
- Development setup instructions
- Command reference documentation
- Architecture decision records (ADRs)

### design-specialist

**MVP Deliverables:** None (UI deferred to Phase 2)

**Pre-work for Phase 2:**
- Design token definitions
- Color palette proposal
- Component specifications

### html-prototyper

**MVP Deliverables:** None (UI deferred to Phase 2)

---

## Appendix A: File Structure Reference

```
src/
+-- DiscordBot.Core/
|   +-- Entities/
|   |   +-- Guild.cs
|   |   +-- User.cs
|   |   +-- CommandLog.cs
|   +-- Interfaces/
|   |   +-- IRepository.cs
|   |   +-- IGuildRepository.cs
|   |   +-- IUserRepository.cs
|   |   +-- ICommandLogRepository.cs
|   +-- DTOs/
|   |   +-- GuildDto.cs
|   |   +-- BotStatusDto.cs
|   |   +-- CommandLogDto.cs
|   +-- Enums/
|   |   +-- CommandCategory.cs
|   |   +-- LogLevel.cs
|
+-- DiscordBot.Infrastructure/
|   +-- Data/
|   |   +-- BotDbContext.cs
|   |   +-- Configurations/
|   |   |   +-- GuildConfiguration.cs
|   |   |   +-- UserConfiguration.cs
|   |   |   +-- CommandLogConfiguration.cs
|   |   +-- Repositories/
|   |       +-- Repository.cs
|   |       +-- GuildRepository.cs
|   |       +-- UserRepository.cs
|   |       +-- CommandLogRepository.cs
|   +-- Logging/
|   |   +-- SerilogConfiguration.cs
|   +-- Extensions/
|       +-- ServiceCollectionExtensions.cs
|
+-- DiscordBot.Bot/
    +-- Services/
    |   +-- BotHostedService.cs
    |   +-- BotConfiguration.cs
    |   +-- BotService.cs
    |   +-- GuildService.cs
    |   +-- CommandService.cs
    +-- Handlers/
    |   +-- InteractionHandler.cs
    |   +-- ComponentHandler.cs
    +-- Commands/
    |   +-- GeneralModule.cs
    |   +-- AdminModule.cs
    +-- Preconditions/
    |   +-- RequireAdminAttribute.cs
    |   +-- RequireOwnerAttribute.cs
    |   +-- RateLimitAttribute.cs
    +-- Controllers/
    |   +-- HealthController.cs
    |   +-- BotController.cs
    |   +-- GuildsController.cs
    |   +-- CommandsController.cs
    +-- Extensions/
    |   +-- DiscordServiceExtensions.cs
    +-- Program.cs
    +-- appsettings.json
    +-- appsettings.Development.json
```

## Appendix B: NuGet Packages

### DiscordBot.Core
- `Microsoft.Extensions.Logging.Abstractions` (10.0.0)

### DiscordBot.Infrastructure
- `Microsoft.EntityFrameworkCore` (8.x)
- `Microsoft.EntityFrameworkCore.Sqlite` (8.x)
- `Microsoft.EntityFrameworkCore.SqlServer` (8.x)
- `Serilog.Extensions.Logging` (10.0.0)
- `Serilog.Sinks.Console` (6.1.1)
- `Serilog.Sinks.File` (7.0.0)

### DiscordBot.Bot
- `Discord.Net` (3.18.0)
- `Swashbuckle.AspNetCore` (6.x) - To be added
- `Microsoft.AspNetCore.OpenApi` (8.x) - To be added

---

*Document Version: 1.0*
*Created: December 2024*
*Status: Draft*
