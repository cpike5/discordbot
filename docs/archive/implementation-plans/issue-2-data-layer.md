# Implementation Plan: Issue #2 - Data Layer & Persistence

## Document Information

| Field | Value |
|-------|-------|
| Issue | #2 - Epic: Data Layer & Persistence |
| Phase | MVP Phase 2 |
| Priority | High |
| Dependencies | Phase 1 Complete (Bot Foundation) |
| Target Duration | Week 2 |

---

## 1. Requirement Summary

Implement the foundational data persistence layer for the Discord bot management system using Entity Framework Core with the repository pattern. This phase establishes:

- **Domain Entities**: Guild, User, and CommandLog entities in DiscordBot.Core
- **Repository Pattern**: Generic and specific repository interfaces in Core with implementations in Infrastructure
- **DbContext**: EF Core database context with Fluent API configurations
- **SQLite Provider**: Development database using SQLite
- **DI Integration**: Extension methods for clean service registration

The data layer must follow clean architecture principles with entities and interfaces in the Core project (no EF Core dependencies) and implementations in the Infrastructure project.

---

## 2. Architectural Considerations

### 2.1 Existing System Components

| Component | Project | Status |
|-----------|---------|--------|
| BotHostedService | DiscordBot.Bot | Complete |
| InteractionHandler | DiscordBot.Bot | Complete |
| DiscordServiceExtensions | DiscordBot.Bot | Complete - needs extension for data services |
| Serilog Configuration | DiscordBot.Bot | Complete |

### 2.2 Integration Requirements

1. **DbContext Registration**: Must be registered as Scoped lifetime to work with ASP.NET Core request pipeline
2. **Repository Lifetime**: Repositories should be Scoped to match DbContext lifetime
3. **Connection String**: Must support configuration via appsettings.json with environment-specific overrides
4. **Migration Support**: Database must be creatable via EF Core migrations

### 2.3 Architectural Patterns

| Pattern | Implementation |
|---------|----------------|
| Repository Pattern | Generic `IRepository<T>` with entity-specific interfaces |
| Fluent API | Entity configurations in separate configuration classes |
| Clean Architecture | Interfaces in Core, implementations in Infrastructure |
| Options Pattern | Database configuration via `IOptions<DatabaseConfiguration>` |

### 2.4 Key Constraints

1. **No EF Core in Core**: The Core project must not reference Entity Framework Core packages
2. **ulong Primary Keys**: Discord IDs are ulong (UInt64), which requires special EF Core handling
3. **JSON Columns**: Guild.Settings and CommandLog.Parameters use JSON serialization
4. **SQLite Limitations**: JSON querying is limited; design for simple storage/retrieval

### 2.5 Data Model Relationships

```
Guild (1) ----< (many) CommandLog
User (1) ----< (many) CommandLog

Guild
  - Id (ulong, PK) - Discord Guild ID
  - Has many CommandLogs

User
  - Id (ulong, PK) - Discord User ID
  - Has many CommandLogs

CommandLog
  - Id (Guid, PK) - Generated
  - GuildId (ulong, FK) - References Guild
  - UserId (ulong, FK) - References User
```

---

## 3. File Creation Order

Files must be created in this sequence to satisfy compilation dependencies:

### Phase 2.1: Core Entities (No Dependencies)

```
1. src/DiscordBot.Core/Entities/Guild.cs
2. src/DiscordBot.Core/Entities/User.cs
3. src/DiscordBot.Core/Entities/CommandLog.cs
```

### Phase 2.2: Core Interfaces (Depend on Entities)

```
4. src/DiscordBot.Core/Interfaces/IRepository.cs
5. src/DiscordBot.Core/Interfaces/IGuildRepository.cs
6. src/DiscordBot.Core/Interfaces/IUserRepository.cs
7. src/DiscordBot.Core/Interfaces/ICommandLogRepository.cs
```

### Phase 2.3: Infrastructure DbContext (Depends on Core Entities)

```
8. src/DiscordBot.Infrastructure/Data/BotDbContext.cs
9. src/DiscordBot.Infrastructure/Data/Configurations/GuildConfiguration.cs
10. src/DiscordBot.Infrastructure/Data/Configurations/UserConfiguration.cs
11. src/DiscordBot.Infrastructure/Data/Configurations/CommandLogConfiguration.cs
```

### Phase 2.4: Infrastructure Repositories (Depends on DbContext and Interfaces)

```
12. src/DiscordBot.Infrastructure/Data/Repositories/Repository.cs
13. src/DiscordBot.Infrastructure/Data/Repositories/GuildRepository.cs
14. src/DiscordBot.Infrastructure/Data/Repositories/UserRepository.cs
15. src/DiscordBot.Infrastructure/Data/Repositories/CommandLogRepository.cs
```

### Phase 2.5: DI Registration (Depends on All Above)

```
16. src/DiscordBot.Infrastructure/Extensions/ServiceCollectionExtensions.cs
```

### Phase 2.6: Bot Integration

```
17. Update: src/DiscordBot.Bot/Program.cs (add data services registration)
18. Update: src/DiscordBot.Bot/appsettings.json (add connection string)
```

---

## 4. Entity Classes Specification

### 4.1 Guild Entity

**File**: `src/DiscordBot.Core/Entities/Guild.cs`

```csharp
namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a Discord guild (server) registered with the bot.
/// </summary>
public class Guild
{
    /// <summary>
    /// Discord guild snowflake ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// Display name of the guild.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the bot joined this guild.
    /// </summary>
    public DateTime JoinedAt { get; set; }

    /// <summary>
    /// Whether the bot is currently active in this guild.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional custom command prefix for text commands.
    /// Null uses the default prefix.
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// JSON-serialized guild-specific settings.
    /// </summary>
    public string? Settings { get; set; }

    /// <summary>
    /// Navigation property for command logs in this guild.
    /// </summary>
    public ICollection<CommandLog> CommandLogs { get; set; } = new List<CommandLog>();
}
```

**Key Points**:
- `Id` is ulong (Discord snowflake)
- `Settings` stored as JSON string (no complex type mapping for SQLite compatibility)
- Navigation property for CommandLogs

### 4.2 User Entity

**File**: `src/DiscordBot.Core/Entities/User.cs`

```csharp
namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a Discord user known to the bot.
/// </summary>
public class User
{
    /// <summary>
    /// Discord user snowflake ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// Discord username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Discord discriminator (legacy, may be "0" for new usernames).
    /// </summary>
    public string Discriminator { get; set; } = "0";

    /// <summary>
    /// Timestamp when the user was first seen by the bot.
    /// </summary>
    public DateTime FirstSeenAt { get; set; }

    /// <summary>
    /// Timestamp of the user's most recent interaction.
    /// </summary>
    public DateTime LastSeenAt { get; set; }

    /// <summary>
    /// Navigation property for command logs by this user.
    /// </summary>
    public ICollection<CommandLog> CommandLogs { get; set; } = new List<CommandLog>();
}
```

**Key Points**:
- `Id` is ulong (Discord snowflake)
- `Discriminator` defaults to "0" for new Discord username system
- Tracks first/last seen timestamps

### 4.3 CommandLog Entity

**File**: `src/DiscordBot.Core/Entities/CommandLog.cs`

```csharp
namespace DiscordBot.Core.Entities;

/// <summary>
/// Audit log entry for a command execution.
/// </summary>
public class CommandLog
{
    /// <summary>
    /// Unique identifier for this log entry.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the guild where the command was executed.
    /// Null if executed in DMs.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// ID of the user who executed the command.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Name of the command that was executed.
    /// </summary>
    public string CommandName { get; set; } = string.Empty;

    /// <summary>
    /// JSON-serialized command parameters.
    /// </summary>
    public string? Parameters { get; set; }

    /// <summary>
    /// Timestamp when the command was executed.
    /// </summary>
    public DateTime ExecutedAt { get; set; }

    /// <summary>
    /// Command execution duration in milliseconds.
    /// </summary>
    public int ResponseTimeMs { get; set; }

    /// <summary>
    /// Whether the command completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the command failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Navigation property for the guild (nullable for DM commands).
    /// </summary>
    public Guild? Guild { get; set; }

    /// <summary>
    /// Navigation property for the user.
    /// </summary>
    public User User { get; set; } = null!;
}
```

**Key Points**:
- `Id` is Guid (auto-generated)
- `GuildId` is nullable (commands can be executed in DMs)
- `Parameters` stored as JSON string
- Foreign keys to Guild and User entities

---

## 5. Repository Interfaces Specification

### 5.1 Generic Repository Interface

**File**: `src/DiscordBot.Core/Interfaces/IRepository.cs`

```csharp
using System.Linq.Expressions;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Generic repository interface providing basic CRUD operations.
/// </summary>
/// <typeparam name="T">Entity type.</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Gets an entity by its primary key.
    /// </summary>
    Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all entities.
    /// </summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds entities matching the specified predicate.
    /// </summary>
    Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity.
    /// </summary>
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity.
    /// </summary>
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity.
    /// </summary>
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any entity matches the predicate.
    /// </summary>
    Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of entities matching the predicate.
    /// </summary>
    Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default);
}
```

### 5.2 Guild Repository Interface

**File**: `src/DiscordBot.Core/Interfaces/IGuildRepository.cs`

```csharp
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for Guild entities with Discord-specific operations.
/// </summary>
public interface IGuildRepository : IRepository<Guild>
{
    /// <summary>
    /// Gets a guild by its Discord snowflake ID.
    /// </summary>
    Task<Guild?> GetByDiscordIdAsync(ulong discordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active guilds.
    /// </summary>
    Task<IReadOnlyList<Guild>> GetActiveGuildsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a guild with its command logs.
    /// </summary>
    Task<Guild?> GetWithCommandLogsAsync(ulong discordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the guild's active status.
    /// </summary>
    Task SetActiveStatusAsync(ulong discordId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a guild record.
    /// </summary>
    Task<Guild> UpsertAsync(Guild guild, CancellationToken cancellationToken = default);
}
```

### 5.3 User Repository Interface

**File**: `src/DiscordBot.Core/Interfaces/IUserRepository.cs`

```csharp
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for User entities with Discord-specific operations.
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>
    /// Gets a user by their Discord snowflake ID.
    /// </summary>
    Task<User?> GetByDiscordIdAsync(ulong discordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user with their command history.
    /// </summary>
    Task<User?> GetWithCommandLogsAsync(ulong discordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the user's last seen timestamp.
    /// </summary>
    Task UpdateLastSeenAsync(ulong discordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a user record.
    /// </summary>
    Task<User> UpsertAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets users who have been active within the specified timeframe.
    /// </summary>
    Task<IReadOnlyList<User>> GetRecentlyActiveAsync(
        TimeSpan timeframe,
        CancellationToken cancellationToken = default);
}
```

### 5.4 CommandLog Repository Interface

**File**: `src/DiscordBot.Core/Interfaces/ICommandLogRepository.cs`

```csharp
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for CommandLog entities with audit-specific operations.
/// </summary>
public interface ICommandLogRepository : IRepository<CommandLog>
{
    /// <summary>
    /// Gets command logs for a specific guild.
    /// </summary>
    Task<IReadOnlyList<CommandLog>> GetByGuildAsync(
        ulong guildId,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets command logs for a specific user.
    /// </summary>
    Task<IReadOnlyList<CommandLog>> GetByUserAsync(
        ulong userId,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets command logs for a specific command name.
    /// </summary>
    Task<IReadOnlyList<CommandLog>> GetByCommandNameAsync(
        string commandName,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets command logs within a date range.
    /// </summary>
    Task<IReadOnlyList<CommandLog>> GetByDateRangeAsync(
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets failed command logs.
    /// </summary>
    Task<IReadOnlyList<CommandLog>> GetFailedCommandsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets command usage statistics.
    /// </summary>
    Task<IDictionary<string, int>> GetCommandUsageStatsAsync(
        DateTime? since = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a command execution.
    /// </summary>
    Task<CommandLog> LogCommandAsync(
        ulong? guildId,
        ulong userId,
        string commandName,
        string? parameters,
        int responseTimeMs,
        bool success,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);
}
```

---

## 6. DbContext Configuration Specification

### 6.1 BotDbContext

**File**: `src/DiscordBot.Infrastructure/Data/BotDbContext.cs`

```csharp
using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Infrastructure.Data;

/// <summary>
/// Entity Framework Core database context for the Discord bot.
/// </summary>
public class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> options) : base(options)
    {
    }

    public DbSet<Guild> Guilds => Set<Guild>();
    public DbSet<User> Users => Set<User>();
    public DbSet<CommandLog> CommandLogs => Set<CommandLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BotDbContext).Assembly);
    }
}
```

### 6.2 Guild Configuration

**File**: `src/DiscordBot.Infrastructure/Data/Configurations/GuildConfiguration.cs`

```csharp
using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the Guild entity.
/// </summary>
public class GuildConfiguration : IEntityTypeConfiguration<Guild>
{
    public void Configure(EntityTypeBuilder<Guild> builder)
    {
        builder.ToTable("Guilds");

        builder.HasKey(g => g.Id);

        // ulong is not natively supported, store as long and convert
        builder.Property(g => g.Id)
            .HasConversion<long>()
            .ValueGeneratedNever();

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(g => g.JoinedAt)
            .IsRequired();

        builder.Property(g => g.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(g => g.Prefix)
            .HasMaxLength(10);

        builder.Property(g => g.Settings)
            .HasColumnType("TEXT");

        // Index for active guild queries
        builder.HasIndex(g => g.IsActive);
    }
}
```

### 6.3 User Configuration

**File**: `src/DiscordBot.Infrastructure/Data/Configurations/UserConfiguration.cs`

```csharp
using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the User entity.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        // ulong is not natively supported, store as long and convert
        builder.Property(u => u.Id)
            .HasConversion<long>()
            .ValueGeneratedNever();

        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(u => u.Discriminator)
            .IsRequired()
            .HasMaxLength(4)
            .HasDefaultValue("0");

        builder.Property(u => u.FirstSeenAt)
            .IsRequired();

        builder.Property(u => u.LastSeenAt)
            .IsRequired();

        // Index for recently active user queries
        builder.HasIndex(u => u.LastSeenAt);
    }
}
```

### 6.4 CommandLog Configuration

**File**: `src/DiscordBot.Infrastructure/Data/Configurations/CommandLogConfiguration.cs`

```csharp
using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the CommandLog entity.
/// </summary>
public class CommandLogConfiguration : IEntityTypeConfiguration<CommandLog>
{
    public void Configure(EntityTypeBuilder<CommandLog> builder)
    {
        builder.ToTable("CommandLogs");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd();

        // ulong foreign keys converted to long
        builder.Property(c => c.GuildId)
            .HasConversion<long?>();

        builder.Property(c => c.UserId)
            .HasConversion<long>();

        builder.Property(c => c.CommandName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.Parameters)
            .HasColumnType("TEXT");

        builder.Property(c => c.ExecutedAt)
            .IsRequired();

        builder.Property(c => c.ResponseTimeMs)
            .IsRequired();

        builder.Property(c => c.Success)
            .IsRequired();

        builder.Property(c => c.ErrorMessage)
            .HasMaxLength(2000);

        // Relationships
        builder.HasOne(c => c.Guild)
            .WithMany(g => g.CommandLogs)
            .HasForeignKey(c => c.GuildId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.User)
            .WithMany(u => u.CommandLogs)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for common queries
        builder.HasIndex(c => c.GuildId);
        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.CommandName);
        builder.HasIndex(c => c.ExecutedAt);
        builder.HasIndex(c => c.Success);
    }
}
```

---

## 7. Repository Implementation Specification

### 7.1 Generic Repository

**File**: `src/DiscordBot.Infrastructure/Data/Repositories/Repository.cs`

```csharp
using System.Linq.Expressions;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Generic repository implementation providing basic CRUD operations.
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly BotDbContext Context;
    protected readonly DbSet<T> DbSet;

    public Repository(BotDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FindAsync(new[] { id }, cancellationToken);
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.ToListAsync(cancellationToken);
    }

    public virtual async Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public virtual async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        DbSet.Update(entity);
        await Context.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
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

### 7.2 Guild Repository

**File**: `src/DiscordBot.Infrastructure/Data/Repositories/GuildRepository.cs`

Key implementation details:
- Override `GetByIdAsync` to handle ulong conversion
- Implement `UpsertAsync` using `ExecuteUpdateAsync` or Add/Update logic
- Use `Include()` for eager loading in `GetWithCommandLogsAsync`

### 7.3 User Repository

**File**: `src/DiscordBot.Infrastructure/Data/Repositories/UserRepository.cs`

Key implementation details:
- Implement `UpdateLastSeenAsync` as a single UPDATE statement for efficiency
- `GetRecentlyActiveAsync` should use proper DateTime comparison
- `UpsertAsync` similar pattern to Guild

### 7.4 CommandLog Repository

**File**: `src/DiscordBot.Infrastructure/Data/Repositories/CommandLogRepository.cs`

Key implementation details:
- All query methods should return ordered by `ExecutedAt` descending
- `GetCommandUsageStatsAsync` uses `GroupBy` for aggregation
- `LogCommandAsync` is a convenience method that creates and saves the entity

---

## 8. DI Registration Specification

### 8.1 Infrastructure Service Extensions

**File**: `src/DiscordBot.Infrastructure/Extensions/ServiceCollectionExtensions.cs`

```csharp
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering Infrastructure services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Infrastructure services including DbContext and repositories.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register DbContext with SQLite
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=discordbot.db";

        services.AddDbContext<BotDbContext>(options =>
            options.UseSqlite(connectionString));

        // Register repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IGuildRepository, GuildRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICommandLogRepository, CommandLogRepository>();

        return services;
    }
}
```

### 8.2 Program.cs Updates

Add to `src/DiscordBot.Bot/Program.cs` after `AddDiscordBot`:

```csharp
// Add Infrastructure services (database and repositories)
builder.Services.AddInfrastructure(builder.Configuration);
```

### 8.3 appsettings.json Updates

Add connection string section:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=discordbot.db"
  },
  // ... existing configuration
}
```

---

## 9. Migration Commands

### 9.1 Prerequisites

Ensure EF Core tools are installed:

```bash
dotnet tool install --global dotnet-ef
```

Or update if already installed:

```bash
dotnet tool update --global dotnet-ef
```

### 9.2 Add Initial Migration

From the solution root directory:

```bash
dotnet ef migrations add InitialCreate --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
```

This creates:
- `src/DiscordBot.Infrastructure/Migrations/[timestamp]_InitialCreate.cs`
- `src/DiscordBot.Infrastructure/Migrations/[timestamp]_InitialCreate.Designer.cs`
- `src/DiscordBot.Infrastructure/Migrations/BotDbContextModelSnapshot.cs`

### 9.3 Apply Migration

```bash
dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
```

This creates `discordbot.db` in the `src/DiscordBot.Bot` directory.

### 9.4 Verify Database

```bash
# Using SQLite CLI (if installed)
sqlite3 src/DiscordBot.Bot/discordbot.db ".tables"

# Expected output:
# CommandLogs  Guilds  Users  __EFMigrationsHistory
```

---

## 10. Testing Strategy

### 10.1 Test Categories

| Category | Focus | Location |
|----------|-------|----------|
| Unit Tests | Repository logic, Entity validation | `tests/DiscordBot.Tests/Data/` |
| Integration Tests | DbContext operations, Query correctness | `tests/DiscordBot.Tests/Integration/` |

### 10.2 Test Files to Create

```
tests/DiscordBot.Tests/
+-- Data/
|   +-- Repositories/
|   |   +-- GuildRepositoryTests.cs
|   |   +-- UserRepositoryTests.cs
|   |   +-- CommandLogRepositoryTests.cs
|   +-- Configurations/
|       +-- EntityConfigurationTests.cs
```

### 10.3 Test Infrastructure

Create a test helper for in-memory SQLite:

**File**: `tests/DiscordBot.Tests/TestHelpers/TestDbContextFactory.cs`

```csharp
using DiscordBot.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Tests.TestHelpers;

public static class TestDbContextFactory
{
    public static BotDbContext CreateInMemory()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<BotDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new BotDbContext(options);
        context.Database.EnsureCreated();

        return context;
    }
}
```

### 10.4 Sample Test Cases

**GuildRepositoryTests.cs**:
- `GetByDiscordIdAsync_WithExistingGuild_ReturnsGuild`
- `GetByDiscordIdAsync_WithNonExistentGuild_ReturnsNull`
- `GetActiveGuildsAsync_ReturnsOnlyActiveGuilds`
- `UpsertAsync_WithNewGuild_CreatesGuild`
- `UpsertAsync_WithExistingGuild_UpdatesGuild`
- `SetActiveStatusAsync_UpdatesStatus`

**UserRepositoryTests.cs**:
- `GetByDiscordIdAsync_WithExistingUser_ReturnsUser`
- `UpdateLastSeenAsync_UpdatesTimestamp`
- `GetRecentlyActiveAsync_ReturnsUsersWithinTimeframe`

**CommandLogRepositoryTests.cs**:
- `LogCommandAsync_CreatesNewLog`
- `GetByGuildAsync_ReturnsLogsForGuild`
- `GetByUserAsync_ReturnsLogsForUser`
- `GetFailedCommandsAsync_ReturnsOnlyFailedCommands`
- `GetCommandUsageStatsAsync_ReturnsCorrectCounts`

### 10.5 Test Project Dependencies

Add to `tests/DiscordBot.Tests/DiscordBot.Tests.csproj`:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.*" />
```

---

## 11. Subagent Task Plan

### 11.1 dotnet-specialist Tasks

**Priority Order**: Execute in sequence

#### Task 1: Create Core Entities
- Create `Guild.cs`, `User.cs`, `CommandLog.cs` in `src/DiscordBot.Core/Entities/`
- Follow exact specifications from Section 4
- Ensure proper namespaces and XML documentation

#### Task 2: Create Repository Interfaces
- Create `IRepository.cs`, `IGuildRepository.cs`, `IUserRepository.cs`, `ICommandLogRepository.cs` in `src/DiscordBot.Core/Interfaces/`
- Follow exact specifications from Section 5

#### Task 3: Create DbContext and Configurations
- Create `BotDbContext.cs` in `src/DiscordBot.Infrastructure/Data/`
- Create configuration classes in `src/DiscordBot.Infrastructure/Data/Configurations/`
- Follow exact specifications from Section 6

#### Task 4: Create Repository Implementations
- Create `Repository.cs`, `GuildRepository.cs`, `UserRepository.cs`, `CommandLogRepository.cs` in `src/DiscordBot.Infrastructure/Data/Repositories/`
- Follow exact specifications from Section 7

#### Task 5: Create DI Registration
- Create `ServiceCollectionExtensions.cs` in `src/DiscordBot.Infrastructure/Extensions/`
- Update `Program.cs` to call `AddInfrastructure`
- Update `appsettings.json` with connection string

#### Task 6: Generate and Apply Migration
- Run migration commands from Section 9
- Verify database is created successfully

#### Task 7: Create Unit Tests
- Create test helper infrastructure
- Create repository test classes
- Ensure all tests pass

### 11.2 docs-writer Tasks

After dotnet-specialist completes implementation:

#### Task 1: Update Architecture Documentation
- Document the data layer in existing architecture docs
- Include entity relationship diagram
- Document repository pattern implementation

#### Task 2: Create Database Schema Reference
- Document all tables and columns
- Document indexes and relationships
- Document migration procedures

---

## 12. Timeline / Dependency Map

```
Day 1: Entity Creation
  |
  +-> Task 1: Core Entities (No dependencies)
  |
  +-> Task 2: Repository Interfaces (Depends on Task 1)

Day 2: Infrastructure Implementation
  |
  +-> Task 3: DbContext & Configurations (Depends on Task 1)
  |
  +-> Task 4: Repository Implementations (Depends on Tasks 2, 3)

Day 3: Integration & Testing
  |
  +-> Task 5: DI Registration (Depends on Task 4)
  |
  +-> Task 6: Migration (Depends on Tasks 3, 5)
  |
  +-> Task 7: Unit Tests (Depends on Task 4)

Day 4: Documentation (Parallel with Day 3 testing)
  |
  +-> docs-writer Task 1: Architecture Docs
  |
  +-> docs-writer Task 2: Schema Reference
```

### Parallel Execution Opportunities

- Tasks 1 and 2 can be partially parallelized (interfaces can be stubbed)
- docs-writer tasks can start once Task 3 is complete
- Unit tests can be written alongside repository implementations

---

## 13. Acceptance Criteria

### 13.1 Build Verification

| Criterion | Command | Expected Result |
|-----------|---------|-----------------|
| Solution builds | `dotnet build` | Build succeeded with no errors |
| Tests pass | `dotnet test` | All tests pass |

### 13.2 Migration Verification

| Criterion | Command | Expected Result |
|-----------|---------|-----------------|
| Migration adds | `dotnet ef migrations add Test --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot` | Migration files created |
| Database updates | `dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot` | Database file created |

### 13.3 Functional Verification

| Criterion | Verification Method |
|-----------|---------------------|
| Guilds table exists | Query `SELECT * FROM Guilds` succeeds |
| Users table exists | Query `SELECT * FROM Users` succeeds |
| CommandLogs table exists | Query `SELECT * FROM CommandLogs` succeeds |
| Foreign keys work | Insert CommandLog with valid GuildId/UserId |
| ulong conversion works | Insert Guild with Id > Int64.MaxValue |

### 13.4 Code Quality Criteria

| Criterion | Requirement |
|-----------|-------------|
| No EF Core in Core | `DiscordBot.Core.csproj` has no EF Core packages |
| All interfaces have implementations | Each `I*Repository` has corresponding implementation |
| XML documentation | All public members have XML comments |
| Nullable enabled | No nullable warnings |

---

## 14. Risks & Mitigations

### 14.1 Technical Risks

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| ulong conversion issues | High | Medium | Test with values > Int64.MaxValue; use explicit conversion |
| SQLite JSON limitations | Medium | Low | Store as TEXT, parse in application code |
| Migration conflicts | Medium | Low | Run migrations on clean database; use explicit ordering |
| Concurrency issues | Medium | Medium | Use optimistic concurrency; add RowVersion if needed |

### 14.2 Integration Risks

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| DbContext lifetime mismatch | High | Medium | Ensure Scoped registration; don't capture in singletons |
| Missing DI registration | Medium | Low | Add startup validation; integration tests |
| Connection string missing | High | Low | Use default fallback; validate on startup |

### 14.3 Recommendations

1. **Add Startup Validation**: Consider adding a health check that validates database connectivity on startup
2. **Consider Row Versioning**: For Guild entity, consider adding a `RowVersion` property for optimistic concurrency
3. **Index Review**: After initial usage patterns emerge, review and optimize indexes
4. **Soft Delete**: Consider changing `IsActive` to support soft delete with filtered queries

---

## 15. Future Considerations

### 15.1 Phase 3 Dependencies

The Command Logging feature (Phase 3) will use:
- `ICommandLogRepository.LogCommandAsync()` for audit logging
- Guild and User repositories for ensuring entities exist before logging

### 15.2 Production Database Migration

When moving to production (MSSQL/PostgreSQL/MySQL):
1. Update connection string in production appsettings
2. Add appropriate NuGet package reference
3. Regenerate migrations for target database
4. Review and optimize JSON column handling for target database

### 15.3 Potential Enhancements

- **Unit of Work Pattern**: Consider if complex transactions require UoW
- **Specification Pattern**: For complex query composition
- **Audit Fields**: CreatedAt, UpdatedAt, CreatedBy for all entities
- **Soft Delete**: Filter interface for globally applying IsDeleted filters

---

## Appendix A: Directory Structure After Implementation

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
|   +-- DiscordBot.Core.csproj
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
|   +-- Extensions/
|   |   +-- ServiceCollectionExtensions.cs
|   +-- Migrations/
|   |   +-- [timestamp]_InitialCreate.cs
|   |   +-- [timestamp]_InitialCreate.Designer.cs
|   |   +-- BotDbContextModelSnapshot.cs
|   +-- DiscordBot.Infrastructure.csproj
|
+-- DiscordBot.Bot/
    +-- ... (existing files)
    +-- appsettings.json (updated with ConnectionStrings)
    +-- discordbot.db (created after migration)

tests/
+-- DiscordBot.Tests/
    +-- Data/
    |   +-- Repositories/
    |       +-- GuildRepositoryTests.cs
    |       +-- UserRepositoryTests.cs
    |       +-- CommandLogRepositoryTests.cs
    +-- TestHelpers/
        +-- TestDbContextFactory.cs
```

---

## Appendix B: Package References Summary

### DiscordBot.Core.csproj (No Changes)

```xml
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
```

### DiscordBot.Infrastructure.csproj (Existing - No Changes)

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.*" />
```

### DiscordBot.Tests.csproj (Add SQLite for In-Memory Testing)

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.*" />
```

---

*Document Version: 1.0*
*Created: December 2024*
*Status: Ready for Implementation*
