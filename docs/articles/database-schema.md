# Database Schema Documentation

## Overview

The Discord bot uses Entity Framework Core with SQLite for local development and supports MSSQL, MySQL, and PostgreSQL for production deployments. The schema consists of three core tables: `Guilds`, `Users`, and `CommandLogs`.

## Data Type Considerations

### Discord Snowflake IDs

Discord uses 64-bit unsigned integers (ulong in C#) for snowflake IDs. Since most databases don't natively support ulong, these are converted to signed long (Int64) during storage using EF Core value conversions.

**Important:** The application layer always works with ulong, but the database stores these as long. This conversion is handled transparently by Entity Framework Core.

```csharp
// Example from GuildConfiguration.cs
builder.Property(g => g.Id)
    .HasConversion<long>()
    .ValueGeneratedNever();
```

## Tables

### Guilds

Stores Discord server (guild) metadata and bot-specific configuration.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | INTEGER (long) | No | - | PRIMARY KEY | Discord guild snowflake ID |
| Name | TEXT | No | - | MaxLength: 100 | Display name of the guild |
| JoinedAt | TEXT (DateTime) | No | - | - | Timestamp when bot joined the guild |
| IsActive | INTEGER (bool) | No | true | - | Whether bot is currently active in guild |
| Prefix | TEXT | Yes | NULL | MaxLength: 10 | Custom command prefix (optional) |
| Settings | TEXT (JSON) | Yes | NULL | - | JSON-serialized guild-specific settings |

**Indexes:**
- `IX_Guilds_IsActive` on `IsActive` - Optimizes queries for active guilds

**SQL Schema:**
```sql
CREATE TABLE Guilds (
    Id INTEGER NOT NULL PRIMARY KEY,
    Name TEXT NOT NULL,
    JoinedAt TEXT NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1,
    Prefix TEXT,
    Settings TEXT
);

CREATE INDEX IX_Guilds_IsActive ON Guilds (IsActive);
```

---

### Users

Stores Discord user information and tracking metadata.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | INTEGER (long) | No | - | PRIMARY KEY | Discord user snowflake ID |
| Username | TEXT | No | - | MaxLength: 32 | Discord username |
| Discriminator | TEXT | No | "0" | MaxLength: 4 | Discord discriminator (legacy) |
| FirstSeenAt | TEXT (DateTime) | No | - | - | Timestamp when user first interacted |
| LastSeenAt | TEXT (DateTime) | No | - | - | Timestamp of most recent interaction |

**Indexes:**
- `IX_Users_LastSeenAt` on `LastSeenAt` - Optimizes queries for recently active users

**SQL Schema:**
```sql
CREATE TABLE Users (
    Id INTEGER NOT NULL PRIMARY KEY,
    Username TEXT NOT NULL,
    Discriminator TEXT NOT NULL DEFAULT '0',
    FirstSeenAt TEXT NOT NULL,
    LastSeenAt TEXT NOT NULL
);

CREATE INDEX IX_Users_LastSeenAt ON Users (LastSeenAt);
```

**Notes:**
- `Discriminator` defaults to "0" for new Discord usernames (post-discriminator system)
- Legacy usernames have 4-digit discriminators (e.g., "1234")

---

### CommandLogs

Audit log for command executions with performance metrics and error tracking.

| Column | Type | Nullable | Default | Constraints | Description |
|--------|------|----------|---------|-------------|-------------|
| Id | BLOB (Guid) | No | Auto-generated | PRIMARY KEY | Unique log entry identifier |
| GuildId | INTEGER (long) | Yes | NULL | FOREIGN KEY → Guilds(Id) | Guild where command executed (NULL for DMs) |
| UserId | INTEGER (long) | No | - | FOREIGN KEY → Users(Id) | User who executed the command |
| CommandName | TEXT | No | - | MaxLength: 50 | Name of the executed command |
| Parameters | TEXT (JSON) | Yes | NULL | - | JSON-serialized command parameters |
| ExecutedAt | TEXT (DateTime) | No | - | - | Command execution timestamp |
| ResponseTimeMs | INTEGER | No | - | - | Execution duration in milliseconds |
| Success | INTEGER (bool) | No | - | - | Whether command completed successfully |
| ErrorMessage | TEXT | Yes | NULL | MaxLength: 2000 | Error message if command failed |

**Indexes:**
- `IX_CommandLogs_GuildId` on `GuildId` - Query logs by guild
- `IX_CommandLogs_UserId` on `UserId` - Query logs by user
- `IX_CommandLogs_CommandName` on `CommandName` - Query logs by command
- `IX_CommandLogs_ExecutedAt` on `ExecutedAt` - Query logs by date/time
- `IX_CommandLogs_Success` on `Success` - Query failed commands

**Foreign Keys:**
- `GuildId` → `Guilds(Id)` with `ON DELETE SET NULL` - Preserve logs if guild deleted
- `UserId` → `Users(Id)` with `ON DELETE CASCADE` - Remove logs when user deleted

**SQL Schema:**
```sql
CREATE TABLE CommandLogs (
    Id BLOB NOT NULL PRIMARY KEY,
    GuildId INTEGER,
    UserId INTEGER NOT NULL,
    CommandName TEXT NOT NULL,
    Parameters TEXT,
    ExecutedAt TEXT NOT NULL,
    ResponseTimeMs INTEGER NOT NULL,
    Success INTEGER NOT NULL,
    ErrorMessage TEXT,
    CONSTRAINT FK_CommandLogs_Guilds_GuildId FOREIGN KEY (GuildId)
        REFERENCES Guilds (Id) ON DELETE SET NULL,
    CONSTRAINT FK_CommandLogs_Users_UserId FOREIGN KEY (UserId)
        REFERENCES Users (Id) ON DELETE CASCADE
);

CREATE INDEX IX_CommandLogs_GuildId ON CommandLogs (GuildId);
CREATE INDEX IX_CommandLogs_UserId ON CommandLogs (UserId);
CREATE INDEX IX_CommandLogs_CommandName ON CommandLogs (CommandName);
CREATE INDEX IX_CommandLogs_ExecutedAt ON CommandLogs (ExecutedAt);
CREATE INDEX IX_CommandLogs_Success ON CommandLogs (Success);
```

---

## Entity Relationships

```
┌─────────────┐                    ┌─────────────────┐
│   Guilds    │                    │      Users      │
├─────────────┤                    ├─────────────────┤
│ Id (PK)     │                    │ Id (PK)         │
│ Name        │                    │ Username        │
│ JoinedAt    │                    │ Discriminator   │
│ IsActive    │                    │ FirstSeenAt     │
│ Prefix      │                    │ LastSeenAt      │
│ Settings    │                    │                 │
└──────┬──────┘                    └────────┬────────┘
       │                                    │
       │ 1                              1   │
       │                                    │
       │         ┌──────────────────┐       │
       └─────────┤   CommandLogs    ├───────┘
            0..* │                  │ 1..*
                 ├──────────────────┤
                 │ Id (PK)          │
                 │ GuildId (FK)     │  ← Nullable (for DM commands)
                 │ UserId (FK)      │
                 │ CommandName      │
                 │ Parameters       │
                 │ ExecutedAt       │
                 │ ResponseTimeMs   │
                 │ Success          │
                 │ ErrorMessage     │
                 └──────────────────┘
```

**Relationship Details:**

| Relationship | Type | Delete Behavior | Notes |
|--------------|------|-----------------|-------|
| Guild → CommandLogs | One-to-Many (0..*) | SET NULL | Logs preserved when guild removed |
| User → CommandLogs | One-to-Many (1..*) | CASCADE | Logs deleted with user |

---

## Configuration Classes

Entity configurations are defined using EF Core's Fluent API in separate configuration classes:

| Entity | Configuration Class | Location |
|--------|---------------------|----------|
| Guild | `GuildConfiguration` | `Infrastructure/Data/Configurations/GuildConfiguration.cs` |
| User | `UserConfiguration` | `Infrastructure/Data/Configurations/UserConfiguration.cs` |
| CommandLog | `CommandLogConfiguration` | `Infrastructure/Data/Configurations/CommandLogConfiguration.cs` |

**Example:**
```csharp
public class GuildConfiguration : IEntityTypeConfiguration<Guild>
{
    public void Configure(EntityTypeBuilder<Guild> builder)
    {
        builder.ToTable("Guilds");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id)
            .HasConversion<long>()
            .ValueGeneratedNever();

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(100);

        // ... additional configuration
    }
}
```

---

## Migrations

### Initial Migration

**Name:** `InitialCreate`
**Created:** 2025-12-09 01:19:28 UTC

Creates all three tables with indexes and foreign key constraints.

**Apply Migration:**
```bash
dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
```

**Create New Migration:**
```bash
dotnet ef migrations add MigrationName --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
```

---

## Database Providers

### SQLite (Development/Testing)

**Connection String Format:**
```
Data Source=discordbot.db
```

**Characteristics:**
- File-based, zero configuration
- Excellent for local development
- Limited concurrency support
- Some SQL features unavailable

### Production Databases

The schema supports MySQL, PostgreSQL, and SQL Server with minimal changes. Provider-specific configurations (if needed) are handled in `BotDbContext`.

**Example for SQL Server:**
```csharp
services.AddDbContext<BotDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
```

---

## Common Queries

### Get All Active Guilds
```csharp
var activeGuilds = await context.Guilds
    .Where(g => g.IsActive)
    .ToListAsync();
```

### Get Recent Command Logs for a Guild
```csharp
var logs = await context.CommandLogs
    .Where(c => c.GuildId == guildId)
    .OrderByDescending(c => c.ExecutedAt)
    .Take(100)
    .Include(c => c.User)
    .ToListAsync();
```

### Get Failed Commands
```csharp
var failedCommands = await context.CommandLogs
    .Where(c => !c.Success)
    .OrderByDescending(c => c.ExecutedAt)
    .Take(50)
    .ToListAsync();
```

### Get Recently Active Users
```csharp
var cutoff = DateTime.UtcNow.AddDays(-7);
var activeUsers = await context.Users
    .Where(u => u.LastSeenAt >= cutoff)
    .OrderByDescending(u => u.LastSeenAt)
    .ToListAsync();
```

---

## Performance Considerations

### Indexes

All indexes are created to optimize common query patterns:
- Guild active status filtering
- User activity tracking
- Command log filtering by guild, user, command name, and date
- Failed command queries

### Query Tips

1. **Use AsNoTracking for Read-Only Queries:**
   ```csharp
   var guilds = await context.Guilds
       .AsNoTracking()
       .Where(g => g.IsActive)
       .ToListAsync();
   ```

2. **Include Related Data Only When Needed:**
   ```csharp
   // Only include if you need command logs
   var guild = await context.Guilds
       .Include(g => g.CommandLogs)
       .FirstOrDefaultAsync(g => g.Id == guildId);
   ```

3. **Use Pagination for Large Result Sets:**
   ```csharp
   var logs = await context.CommandLogs
       .OrderByDescending(c => c.ExecutedAt)
       .Skip(page * pageSize)
       .Take(pageSize)
       .ToListAsync();
   ```

---

## Backup and Maintenance

### SQLite Backup
```bash
# Simple file copy (ensure bot is stopped)
copy discordbot.db discordbot.backup.db

# Or use SQLite command line tool
sqlite3 discordbot.db ".backup 'discordbot.backup.db'"
```

### Database Cleanup

Consider implementing periodic cleanup for old command logs:
```csharp
// Delete logs older than 90 days
var cutoff = DateTime.UtcNow.AddDays(-90);
await context.CommandLogs
    .Where(c => c.ExecutedAt < cutoff)
    .ExecuteDeleteAsync();
```

---

## Troubleshooting

### Migration Issues

**Problem:** Migration fails with "table already exists"
**Solution:** Drop database and recreate, or manually align schema

```bash
# Reset database (development only)
dotnet ef database drop --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
```

### ulong Conversion Issues

**Problem:** Snowflake IDs appear as negative numbers in database
**Solution:** This is expected. ulong values > Int64.MaxValue are stored as negative signed longs. The conversion back to ulong is handled by EF Core.

**Example:**
- Application: `987654321098765432` (ulong)
- Database: `-8792092752610786184` (long)
- Retrieved: `987654321098765432` (ulong) ✓

### Connection String Not Found

**Problem:** `GetConnectionString("DefaultConnection")` returns null
**Solution:** Add connection string to `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=discordbot.db"
  }
}
```

---

*Document Version: 1.0*
*Created: December 2024*
*Last Updated: December 2024*
