# Requirements: PostgreSQL Database Support

## Executive Summary

Add PostgreSQL as a production database provider alongside the existing SQLite support. SQLite remains the default for local development, while Docker deployments recommend PostgreSQL via a compose profile. Provider selection uses an explicit config key (`Database:Provider`) with connection-string auto-detection as fallback. Includes separate EF Core migration sets per provider, design-time DbContext factories, and a data migration utility for existing SQLite users upgrading to PostgreSQL.

## Problem Statement

The bot currently uses SQLite exclusively. While sufficient for development and small deployments, this limits future scalability and doesn't align with production best practices. Adding PostgreSQL support future-proofs the system before it becomes urgent.

## Primary Purpose

Enable PostgreSQL as the production database provider while keeping SQLite for development, with automatic provider detection and a smooth upgrade path.

## Target Users

- **Self-hosters (production)**: Run via Docker Compose with PostgreSQL profile
- **Developers**: Continue using SQLite locally with zero setup
- **Existing users**: Can migrate SQLite data to PostgreSQL via a built-in utility

## Core Features (MVP)

### 1. Dual Database Provider Support

- **Explicit provider config (primary)**: `Database:Provider` setting — values: `Sqlite`, `PostgreSql`
- **Auto-detection (fallback)**: If `Database:Provider` is not set, detect from connection string:
  - File-path-like `Data Source` (contains `.db`, `/`, or `\`) → SQLite
  - Otherwise (contains `Host=`, `Server=`) → PostgreSQL
- **Fallback default**: If nothing is configured, default to SQLite at `Data Source=data/discordbot.db`
- **Registration**: Update `ServiceCollectionExtensions.AddInfrastructure()` to call `options.UseNpgsql()` or `options.UseSqlite()` based on detection
- **Npgsql timestamp compatibility**: Add `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` early in `Program.cs` to prevent runtime exceptions with `DateTime` properties (the codebase uses `DateTime` throughout; Npgsql 6+ requires explicit opt-in for legacy behavior)

> **Architect note**: Npgsql accepts `Data Source=` as an alias for `Host`, making pure keyword matching unreliable. The two-tier approach (explicit config key first, then file-path heuristic) eliminates this ambiguity.

### 2. Separate EF Core Migration Sets

- **SQLite migrations**: `src/DiscordBot.Infrastructure/Migrations/Sqlite/`
- **PostgreSQL migrations**: `src/DiscordBot.Infrastructure/Migrations/Postgresql/`
- **Design-time factory pattern**: Create `SqliteBotDbContext` and `PostgresBotDbContext` subclasses (design-time only) with corresponding `IDesignTimeDbContextFactory<T>` implementations:
  - `SqliteDesignTimeFactory.cs` → returns `BotDbContext` configured with `UseSqlite`
  - `PostgresDesignTimeFactory.cs` → returns `BotDbContext` configured with `UseNpgsql`
- **Runtime**: Still uses a single `BotDbContext` — subclasses are design-time only
- **EF CLI commands**:
  ```bash
  # SQLite migration
  dotnet ef migrations add MigrationName --project src/DiscordBot.Infrastructure \
    --startup-project src/DiscordBot.Bot --context SqliteBotDbContext -o Migrations/Sqlite

  # PostgreSQL migration
  dotnet ef migrations add MigrationName --project src/DiscordBot.Infrastructure \
    --startup-project src/DiscordBot.Bot --context PostgresBotDbContext -o Migrations/Postgresql
  ```
- **Migration reorg safety**: When moving existing SQLite migrations to `Migrations/Sqlite/`, **keep the original namespace** (`DiscordBot.Infrastructure.Migrations`) in each file. Changing the namespace breaks `__EFMigrationsHistory` matching and causes EF Core to re-apply all migrations.
- Auto-migration on startup (already in `Program.cs`) works for both providers — it uses whichever migrations assembly was configured during DI registration

> **Architect note**: `HasDefaultValueSql("CURRENT_TIMESTAMP")` is used in 10 entity configurations. This SQL works on both SQLite and PostgreSQL, but the legacy timestamp switch (Feature 1) is required to avoid type mismatches.

### 3. Docker Compose with PostgreSQL Profile

- PostgreSQL **stays as a profile** (`--profile postgres`) to avoid breaking existing Docker users
- Documentation updated to recommend the PostgreSQL profile for production
- Bot service `depends_on` PostgreSQL with health check (when profile active)
- Default connection string in compose environment points to PostgreSQL container when profile is used
- PostgreSQL data persisted via named volume
- Update `.env.example` with PostgreSQL settings (credentials, database name)

> **Architect note**: Making PostgreSQL the default service would break existing users running `docker compose up -d` who currently get SQLite. Keeping it as a profile is non-disruptive while documentation guides new users toward PostgreSQL.

### 4. SQLite Data Migration Utility

- CLI verb on the bot project: `dotnet run --project src/DiscordBot.Bot -- migrate-data --source "..." --target "..."`
- Uses EF Core for both reading and writing (leverages existing value converters for ulong→long, DateTime, boolean)
- **Pre-flight checks**:
  - Runs `MigrateAsync()` on target PostgreSQL database
  - Verifies source SQLite is at the latest migration (queries `__EFMigrationsHistory`)
  - Refuses to proceed if migration levels differ
- **Data transfer**:
  - Processes tables in dependency order (parents before children) to satisfy foreign keys
  - Wraps entire operation in a PostgreSQL transaction
  - Provides progress output per table
- **Post-transfer**: Resets PostgreSQL sequences for all auto-increment tables:
  ```sql
  SELECT setval(pg_get_serial_sequence('table_name', 'id'), (SELECT MAX(id) FROM table_name));
  ```
- Error reporting with table-level granularity

> **Architect note**: Using EF Core (not raw ADO.NET) for the transfer is correct by construction — it handles all value converters. Performance is acceptable for a one-time migration on a bot database (likely under 1M rows total).

### 5. NuGet Package Changes

- **Add** `Npgsql.EntityFrameworkCore.PostgreSQL` (version `8.*`) to `DiscordBot.Infrastructure`
- **Keep** `Microsoft.EntityFrameworkCore.Sqlite` for continued SQLite support
- **Remove** `Microsoft.EntityFrameworkCore.SqlServer` — currently referenced but unused (no `UseSqlServer` call exists anywhere in the codebase)

### 6. Documentation Updates (16 files)

#### Tier 1 — Critical (update with implementation)

| File | Updates Needed |
|------|---------------|
| `ServiceCollectionExtensions.cs` | Provider detection logic, inline comments |
| `Program.cs` | Legacy timestamp switch, updated migration comments |
| `.env.example` | Clear dual-provider examples, PostgreSQL settings |
| `docker-compose.yml` | Profile documentation, connection string examples |
| `docs/articles/docker-deployment.md` | Expand Database Options section, add migration guide, PostgreSQL troubleshooting |

#### Tier 2 — Important (update shortly after)

| File | Updates Needed |
|------|---------------|
| `README.md` | Move PostgreSQL from "future" to "supported", update Getting Started |
| `CLAUDE.md` | Multi-provider EF CLI commands, new config options |
| `CLAUDE-REFERENCE.md` | New `Database:Provider` config option |
| `docs/articles/linux-deployment.md` | PostgreSQL setup for non-Docker deployments |
| `docs/articles/database-schema.md` | Clarify dual-provider support, type mapping notes |
| `docs/articles/environment-configuration.md` | Provider selection via env vars |
| `appsettings.json` | Add `Database:Provider` setting |

#### Tier 3 — Supporting (update for consistency)

| File | Updates Needed |
|------|---------------|
| `docs/articles/testing-guide.md` | Note on test provider strategy |
| `docs/articles/troubleshooting-guide.md` | PostgreSQL-specific troubleshooting section |
| `docs/articles/repository-pattern.md` | Update provider swap reference |
| `docs/articles/consent-privacy.md` | Update storage description |
| `docs/requirements/docker-containerization.md` | Update database strategy table |
| `Dockerfile` | Update comment on data directory purpose |

## Technical Design Notes

### Provider Detection Logic (ServiceCollectionExtensions.cs)

```csharp
var connectionString = configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=data/discordbot.db";

var providerSetting = configuration["Database:Provider"];
var isPostgreSql = providerSetting?.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase) == true
    || (string.IsNullOrEmpty(providerSetting) && IsPostgreSqlConnectionString(connectionString));

services.AddDbContext<BotDbContext>((serviceProvider, options) =>
{
    var interceptor = serviceProvider.GetRequiredService<QueryPerformanceInterceptor>();

    if (isPostgreSql)
    {
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsAssembly("DiscordBot.Infrastructure"))
            .AddInterceptors(interceptor);
    }
    else
    {
        options.UseSqlite(connectionString, sqlite =>
            sqlite.MigrationsAssembly("DiscordBot.Infrastructure"))
            .AddInterceptors(interceptor);
    }
});

// Heuristic: SQLite connection strings reference file paths
static bool IsPostgreSqlConnectionString(string connectionString)
{
    // SQLite: "Data Source=file.db", "Filename=...", contains path separators
    // PostgreSQL: "Host=...", "Server=..."
    if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
        connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
        return true;
    return false;
}
```

### Migration Folder Structure

```
src/DiscordBot.Infrastructure/
├── Data/
│   ├── BotDbContext.cs
│   ├── SqliteDesignTimeFactory.cs      ← NEW
│   └── PostgresDesignTimeFactory.cs    ← NEW
├── Migrations/
│   ├── Sqlite/
│   │   ├── 20240101000000_Initial.cs   ← moved, namespace UNCHANGED
│   │   ├── ...
│   │   └── BotDbContextModelSnapshot.cs
│   └── Postgresql/
│       ├── 20260219000000_Initial.cs   ← NEW, generated from current model
│       └── BotDbContextModelSnapshot.cs
```

### Docker Compose Structure

```yaml
services:
  bot:
    environment:
      # When using --profile postgres, override with:
      # ConnectionStrings__DefaultConnection=Host=postgres;Database=discordbot;...
      - ConnectionStrings__DefaultConnection=${ConnectionStrings__DefaultConnection:-Data Source=data/discordbot.db}
      - Database__Provider=${DATABASE_PROVIDER:-}
    depends_on:
      postgres:
        condition: service_healthy
        required: false

  postgres:
    profiles: ["postgres"]
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: ${POSTGRES_DB:-discordbot}
      POSTGRES_USER: ${POSTGRES_USER:-discordbot}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-changeme}
    volumes:
      - postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER:-discordbot}"]
      interval: 10s
      timeout: 5s
      retries: 5
```

## Design Constraints

- **Provider-agnostic data model**: No PostgreSQL-specific column types (JSONB, arrays, etc.). All entities and queries must work on both providers.
- **Simple connection handling**: Use Npgsql's built-in connection pooling with default settings. No external pooler (PgBouncer) or EF retry policies for now.
- **Existing SQLite behavior preserved**: Running without any connection string configured still uses SQLite at `data/discordbot.db`.
- **Legacy timestamp compatibility**: Required for Npgsql to work with existing `DateTime` properties.
- **Migration namespace stability**: Moved SQLite migrations retain original namespace to avoid breaking existing databases.

## Out of Scope

- PostgreSQL-specific features (JSONB, full-text search, array columns)
- External connection pooling (PgBouncer)
- EF Core retry/resilience policies (`EnableRetryOnFailure`)
- Read replicas or multi-database setups
- Automated performance benchmarking between providers
- Full CI test suite against PostgreSQL (single smoke test only, if any)

## Future Considerations

- Connection resilience (`EnableRetryOnFailure`) — add when transient failures become a real concern
- PgBouncer support if connection count becomes an issue
- PostgreSQL-specific optimizations (JSONB for settings storage, etc.)
- Full dual-provider CI test matrix
- `DateTimeOffset` migration to replace legacy timestamp switch (separate refactor)
- Review repository methods for race conditions masked by SQLite's serialized access

## Resolved Questions

| Question | Decision | Rationale |
|----------|----------|-----------|
| EF CLI workflow | Use `--context SqliteBotDbContext` / `--context PostgresBotDbContext` | Design-time factories give clean CLI targeting. Require both migrations per model change. |
| Migration utility schema mismatch | Require both DBs at same migration level, refuse otherwise | Avoids combinatorial complexity of version-to-version data transforms |
| CI testing both providers | Not now — keep SQLite for tests | Adding PostgreSQL CI requires a service container; revisit if provider-specific bugs emerge |

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Two-tier provider detection (explicit config + auto-detect fallback) | Eliminates `Data Source` ambiguity between Npgsql and SQLite |
| Design-time factory pattern for migrations | Clean CLI experience, separate snapshots, single runtime DbContext |
| Keep original namespace on moved SQLite migrations | Prevents `__EFMigrationsHistory` breakage |
| Legacy timestamp switch for Npgsql | Required for `DateTime` compatibility; `DateTimeOffset` refactor deferred |
| PostgreSQL as Docker profile (not default) | Avoids breaking existing Docker users |
| EF Core for data migration (not raw ADO.NET) | Leverages existing value converters; correct by construction |
| Sequence reset after bulk data import | PostgreSQL sequences are out of sync after explicit ID inserts |
| Remove unused SqlServer NuGet package | Cleanup — no `UseSqlServer` call exists in codebase |
| Provider-agnostic model | Keeps dual-provider support simple and maintainable |
| Simple connection handling | YAGNI — add pooling/retry when there's a real need |

## Recommended Next Steps

1. Create GitHub issues from this spec under the **PostgreSQL Support** milestone (#43)
2. Implementation order:
   - NuGet package changes (add Npgsql, remove SqlServer)
   - Provider detection logic + legacy timestamp switch
   - Design-time factories + migration reorganization
   - Generate initial PostgreSQL migration set
   - Update Docker Compose and `.env.example`
   - Build data migration utility
   - Documentation updates (Tier 1 → Tier 2 → Tier 3)
3. Test both providers end-to-end

## Architect Feasibility Notes

The architect assessed this spec as **FEASIBLE WITH MODIFICATIONS**. All modifications have been incorporated above. Key risks to monitor during implementation:

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Npgsql DateTime/timestamptz mismatch | High | Legacy timestamp switch (required) |
| Migration namespace breakage | High | Keep original namespaces on moved files |
| Connection string `Data Source` ambiguity | Medium | Explicit `Database:Provider` config as primary mechanism |
| Concurrent access race conditions (SQLite → PG) | Medium | Review check-then-act patterns during implementation |
| PostgreSQL sequence desync after data import | Medium | Explicit `setval()` in migration utility |
| Connection string logged with password | Low | Verify Serilog/interceptor redaction |
