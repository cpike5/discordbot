# PostgreSQL Support — Implementation Plan

**Milestone:** PostgreSQL Support (#43)
**Spec:** [postgresql-support-spec.md](postgresql-support-spec.md)

---

## Phase 1: Foundation

### [EPIC] Phase 1 — Foundation (NuGet, Provider Detection, Timestamp Switch)

- **Labels:** `epic`, `postgresql`, `component:infra`

Establish the base infrastructure for dual database provider support. This phase adds the Npgsql NuGet package, removes the unused SqlServer package, implements two-tier provider detection logic, and adds the Npgsql legacy timestamp switch.

**Children:** 1a, 1b, 1c

---

### 1a: Update NuGet packages (add Npgsql, remove SqlServer)

- **Type:** task
- **Labels:** `task`, `postgresql`, `component:infra`, `cleanup`
- **Dependencies:** None

The Infrastructure project currently references `Microsoft.EntityFrameworkCore.SqlServer` but no `UseSqlServer` call exists anywhere. Remove it and add the PostgreSQL provider.

**Files to modify:**
- `src/DiscordBot.Infrastructure/DiscordBot.Infrastructure.csproj`

**Changes:**
1. Remove `Microsoft.EntityFrameworkCore.SqlServer`
2. Add `Npgsql.EntityFrameworkCore.PostgreSQL` version `8.*`
3. Keep `Microsoft.EntityFrameworkCore.Sqlite` unchanged

**Acceptance criteria:**
- [ ] `Microsoft.EntityFrameworkCore.SqlServer` removed from .csproj
- [ ] `Npgsql.EntityFrameworkCore.PostgreSQL` version `8.*` added
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes

---

### 1b: Implement two-tier provider detection in ServiceCollectionExtensions

- **Type:** feature
- **Labels:** `feature`, `postgresql`, `component:infra`, `database`
- **Dependencies:** Blocked by 1a

Replace the SQLite-only `AddInfrastructure()` with dual-provider registration:

1. **Explicit config (primary):** `Database:Provider` — values `Sqlite` or `PostgreSql`
2. **Auto-detection (fallback):** Connection string heuristic — `Host=`/`Server=` → PostgreSQL, file-path-like `Data Source` → SQLite
3. **Ultimate fallback:** `Data Source=data/discordbot.db`

**Files to modify:**
- `src/DiscordBot.Infrastructure/Extensions/ServiceCollectionExtensions.cs`
- `src/DiscordBot.Infrastructure/Configuration/DatabaseSettings.cs` — add `Provider` property

**Implementation:**
- Add `IsPostgreSqlConnectionString()` static helper
- Call `UseNpgsql()` or `UseSqlite()` based on detection
- Specify `MigrationsAssembly("DiscordBot.Infrastructure")` for both providers
- Log which provider was selected at Information level

**Acceptance criteria:**
- [ ] `Database:Provider = "Sqlite"` forces SQLite
- [ ] `Database:Provider = "PostgreSql"` forces Npgsql
- [ ] Auto-detection works for both connection string formats
- [ ] No config defaults to SQLite at `Data Source=data/discordbot.db`
- [ ] Startup logs indicate selected provider
- [ ] `dotnet build` succeeds

**Technical note:** Npgsql accepts `Data Source=` as alias for `Host`, making pure keyword matching unreliable — this is why the explicit config key is the primary mechanism.

---

### 1c: Add Npgsql legacy timestamp switch in Program.cs

- **Type:** task
- **Labels:** `task`, `postgresql`, `component:infra`
- **Dependencies:** Blocked by 1a

Npgsql 6+ requires explicit opt-in for legacy `DateTime` handling. The codebase uses `DateTime` throughout with 10 `HasDefaultValueSql("CURRENT_TIMESTAMP")` calls. Without this switch, Npgsql throws runtime exceptions.

**Files to modify:**
- `src/DiscordBot.Bot/Program.cs` — add `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` before `WebApplication.CreateBuilder`
- `src/DiscordBot.Bot/appsettings.json` — add `"Provider": null` to `Database` section

**Acceptance criteria:**
- [ ] Switch called before `WebApplication.CreateBuilder`
- [ ] Comment explains why and links to Npgsql docs
- [ ] `Database:Provider` key in appsettings.json with null default
- [ ] Existing SQLite behavior unchanged
- [ ] `dotnet build` succeeds

---

## Phase 2: Migration Infrastructure

### [EPIC] Phase 2 — Migration Infrastructure

- **Labels:** `epic`, `postgresql`, `database`, `migration`
- **Dependencies:** Phase 1

Reorganize EF Core migrations into provider-specific folders, create design-time factories, and generate the initial PostgreSQL migration set. Highest-risk phase due to namespace stability requirements.

**Children:** 2a, 2b, 2c, 2d

---

### 2a: Create design-time DbContext factories

- **Type:** feature
- **Labels:** `feature`, `postgresql`, `database`, `migration`, `component:data`
- **Dependencies:** Blocked by 1b

Create `SqliteBotDbContext` and `PostgresBotDbContext` subclasses (design-time only) with `IDesignTimeDbContextFactory<T>` implementations.

**Files to create:**
- `src/DiscordBot.Infrastructure/Data/SqliteDesignTimeFactory.cs`
- `src/DiscordBot.Infrastructure/Data/PostgresDesignTimeFactory.cs`

Runtime still uses single `BotDbContext`. Subclasses pass `DbContextOptions<BotDbContext>` (not their own generic type) for `ApplyConfigurationsFromAssembly` compatibility.

**Acceptance criteria:**
- [ ] Both factory classes compile
- [ ] `dotnet ef dbcontext list` shows both subclasses
- [ ] Runtime DI still uses `BotDbContext`

---

### 2b: Move existing SQLite migrations to Sqlite subfolder

- **Type:** task
- **Labels:** `task`, `postgresql`, `database`, `migration`
- **Dependencies:** Blocked by 2a

Move all 75 migration files from `Migrations/` to `Migrations/Sqlite/` using `git mv`.

**CRITICAL:** Do NOT change namespaces. Every file must retain `namespace DiscordBot.Infrastructure.Migrations`. Changing it breaks `__EFMigrationsHistory` matching.

**Acceptance criteria:**
- [ ] All files moved to `Migrations/Sqlite/`
- [ ] `Migrations/` root has no .cs files
- [ ] Every file retains original namespace
- [ ] `dotnet build` succeeds
- [ ] Existing SQLite databases not affected (verify with `dotnet ef migrations list --context SqliteBotDbContext`)

---

### 2c: Generate initial PostgreSQL migration set

- **Type:** task
- **Labels:** `task`, `postgresql`, `database`, `migration`
- **Dependencies:** Blocked by 2b

Generate a single "Initial" migration for PostgreSQL from the current model:

```bash
dotnet ef migrations add InitialPostgresql \
  --project src/DiscordBot.Infrastructure \
  --startup-project src/DiscordBot.Bot \
  --context PostgresBotDbContext \
  -o Migrations/Postgresql
```

**Acceptance criteria:**
- [ ] Migration files exist in `Migrations/Postgresql/`
- [ ] Migration creates all tables matching current model
- [ ] Uses PostgreSQL-compatible SQL
- [ ] `dotnet build` succeeds

---

### 2d: Verify runtime migration selection works for both providers

- **Type:** task
- **Labels:** `task`, `postgresql`, `database`, `migration`, `component:infra`
- **Dependencies:** Blocked by 2b, 2c

Verify and adjust the `MigrationsAssembly` configuration so runtime correctly selects the right migration set per provider.

**Acceptance criteria:**
- [ ] Fresh SQLite startup creates all tables via auto-migration
- [ ] Fresh PostgreSQL startup creates all tables via auto-migration
- [ ] Existing SQLite databases not re-migrated
- [ ] `dotnet build` succeeds

---

## Phase 3: Docker and Configuration

### [EPIC] Phase 3 — Docker and Configuration Updates

- **Labels:** `epic`, `postgresql`, `docker`
- **Dependencies:** Phase 1

Update Docker Compose and environment configuration for the `--profile postgres` workflow.

**Children:** 3a, 3b, 3c

---

### 3a: Update docker-compose.yml for PostgreSQL provider integration

- **Type:** task
- **Labels:** `task`, `postgresql`, `docker`
- **Dependencies:** Blocked by 1b

Add environment variables for database provider selection to bot service. PostgreSQL stays as a profile (`--profile postgres`).

**Changes:**
- Pass `Database__Provider` and `ConnectionStrings__DefaultConnection` env vars with defaults
- Update header comments to document PostgreSQL workflow

**Acceptance criteria:**
- [ ] `docker compose up -d` still works with SQLite (zero change)
- [ ] `docker compose --profile postgres up -d` with correct .env starts bot with PostgreSQL
- [ ] Header comments document the workflow

---

### 3b: Update .env.example with PostgreSQL configuration

- **Type:** task
- **Labels:** `task`, `postgresql`, `docker`
- **Dependencies:** Blocked by 3a

Replace the DATABASE SETTINGS section with clear dual-provider documentation including `DATABASE_PROVIDER` variable and both connection string examples.

**Acceptance criteria:**
- [ ] `DATABASE_PROVIDER` documented with valid values
- [ ] PostgreSQL connection string example uses `postgres` hostname
- [ ] SQLite default clearly documented

---

### 3c: Update Dockerfile comment

- **Type:** task
- **Labels:** `task`, `postgresql`, `docker`, `cleanup`
- **Dependencies:** None

Update line 62 comment: "Create data directory for SQLite database volume mount (not used when running PostgreSQL)"

**Acceptance criteria:**
- [ ] Comment updated, no functional changes

---

## Phase 4: Data Migration Utility

### [EPIC] Phase 4 — SQLite to PostgreSQL Data Migration Utility

- **Labels:** `epic`, `postgresql`, `database`, `migration`
- **Dependencies:** Phase 2

Build CLI utility for migrating data from SQLite to PostgreSQL.

**Children:** 4a

---

### 4a: Implement data migration CLI command

- **Type:** feature
- **Labels:** `feature`, `postgresql`, `database`, `migration`, `component:infra`
- **Dependencies:** Blocked by 2c, 2d

Add `migrate-data` CLI verb: `dotnet run -- migrate-data --source "..." --target "..."`

**Implementation:**
1. Argument parsing for `migrate-data` verb with `--source` and `--target`
2. Pre-flight: run `MigrateAsync()` on target, verify source is at latest migration
3. Data transfer: tables in dependency order, wrapped in transaction, EF Core for reads/writes
4. Post-transfer: reset PostgreSQL sequences via `setval()` for all auto-increment tables
5. Progress output per table, error reporting with table-level granularity

**Files to create:**
- `src/DiscordBot.Bot/Commands/MigrateDataCommand.cs`

**Files to modify:**
- `src/DiscordBot.Bot/Program.cs` — intercept `migrate-data` verb before `WebApplication.CreateBuilder`

**Acceptance criteria:**
- [ ] `dotnet run -- migrate-data --help` prints usage
- [ ] Pre-flight checks verify migration levels match
- [ ] All tables (including Identity) transferred in FK order
- [ ] PostgreSQL sequences reset after transfer
- [ ] Progress printed per-table
- [ ] Transaction rolled back on failure
- [ ] Normal `dotnet run` still starts web host

---

## Phase 5: Documentation

### [EPIC] Phase 5 — Documentation Updates

- **Labels:** `epic`, `postgresql`, `documentation`
- **Dependencies:** Phase 1, Phase 3

Update 16 documentation files across three tiers.

**Children:** 5a, 5b, 5c

---

### 5a: Tier 1 documentation updates (critical)

- **Type:** task
- **Labels:** `task`, `postgresql`, `documentation`
- **Dependencies:** Blocked by 1b, 1c, 3a, 3b

Update critical docs that ship with implementation:

1. **`docs/articles/docker-deployment.md`** — expand Database Options, add PostgreSQL profile workflow, migration guide, troubleshooting
2. **`CLAUDE.md`** — dual-provider EF CLI commands, `Database:Provider` config, timestamp switch gotcha
3. **`CLAUDE-REFERENCE.md`** — add `Database:Provider` to config options table

**Acceptance criteria:**
- [ ] docker-deployment.md documents PostgreSQL workflow end-to-end
- [ ] CLAUDE.md EF commands updated for dual-provider
- [ ] CLAUDE-REFERENCE.md includes new config option

---

### 5b: Tier 2 documentation updates (important)

- **Type:** task
- **Labels:** `task`, `postgresql`, `documentation`
- **Dependencies:** Blocked by 5a

1. **`README.md`** — PostgreSQL as "supported", Getting Started update
2. **`docs/articles/linux-deployment.md`** — PostgreSQL setup for bare-metal
3. **`docs/articles/database-schema.md`** — dual-provider notes, type mapping
4. **`docs/articles/environment-configuration.md`** — provider selection docs

**Acceptance criteria:**
- [ ] README reflects PostgreSQL as supported
- [ ] Linux deployment covers PostgreSQL setup
- [ ] Database schema covers type mapping differences
- [ ] Environment config covers provider selection

---

### 5c: Tier 3 documentation updates (supporting)

- **Type:** task
- **Labels:** `task`, `postgresql`, `documentation`, `cleanup`
- **Dependencies:** Blocked by 5b

Update for consistency:
1. `docs/articles/testing-guide.md` — note test provider strategy
2. `docs/articles/troubleshooting-guide.md` — PostgreSQL troubleshooting section
3. `docs/articles/repository-pattern.md` — update provider swap references
4. `docs/articles/consent-privacy.md` — update storage description
5. `docs/requirements/docker-containerization.md` — update database strategy table

**Acceptance criteria:**
- [ ] All 5 files updated
- [ ] No "SQLite-only" references remain in docs

---

## Execution Order and Dependencies

```
Phase 1 (Foundation):
  1a NuGet packages ──────────┐
                               ├──> 1b Provider detection ──┐
  1c Timestamp switch ────────┘                              │
  (1a and 1c can run in parallel)                            │
                                                             │
Phase 2 (Migration Infrastructure):                          │
  2a Design-time factories <─────────────────────────────────┘
    └──> 2b Move SQLite migrations
           └──> 2c Generate PG migrations
                  └──> 2d Verify runtime migration selection

Phase 3 (Docker & Config):               (can start after Phase 1)
  3a docker-compose.yml ──> 3b .env.example
  3c Dockerfile comment    (independent, any time)

Phase 4 (Data Migration):                (requires Phase 2)
  4a migrate-data CLI command

Phase 5 (Documentation):                 (Tier 1 after Phase 3)
  5a Tier 1 docs ──> 5b Tier 2 docs ──> 5c Tier 3 docs
```

## Key Risks

| Risk | Issue | Mitigation |
|------|-------|------------|
| `BotDbContext` subclass constructor mismatch | 2a | Test with `dotnet ef dbcontext list` immediately |
| Namespace change during migration move | 2b | Use `git mv`, grep to verify zero namespace changes |
| PostgreSQL sequence desync after data import | 4a | Explicit `setval()` for every auto-increment table |
| Connection string with password logged | 1b | Verify Serilog sanitization redacts connection strings |
| Race conditions from PostgreSQL concurrent access | Post-launch | Review check-then-act patterns as follow-up |
