# Repository Pattern Implementation

## Overview

The repository pattern provides an abstraction layer between the application logic and data access layer. This project uses a generic repository pattern with specialized repositories for each entity type.

**Benefits:**
- **Testability:** Repositories can be easily mocked for unit testing
- **Separation of Concerns:** Business logic separated from data access
- **Flexibility:** Easy to swap data providers (SQLite → SQL Server)
- **Consistency:** Standardized data access methods across entities
- **Encapsulation:** Complex queries encapsulated in repository methods

---

## Architecture

### Interface Hierarchy

```
IRepository<T>                      (Generic base interface)
    ↑
    ├── IGuildRepository            (Guild-specific operations)
    ├── IUserRepository             (User-specific operations)
    └── ICommandLogRepository       (CommandLog-specific operations)
```

### Implementation Hierarchy

```
Repository<T>                       (Generic base implementation)
    ↑
    ├── GuildRepository             (Implements IGuildRepository)
    ├── UserRepository              (Implements IUserRepository)
    └── CommandLogRepository        (Implements ICommandLogRepository)
```

---

## Generic Repository Interface

**Location:** `DiscordBot.Core/Interfaces/IRepository.cs`

Provides standard CRUD operations for all entities.

### Methods

| Method | Return Type | Description |
|--------|-------------|-------------|
| `GetByIdAsync(id)` | `Task<T?>` | Retrieves entity by primary key |
| `GetAllAsync()` | `Task<IReadOnlyList<T>>` | Retrieves all entities |
| `FindAsync(predicate)` | `Task<IReadOnlyList<T>>` | Finds entities matching predicate |
| `AddAsync(entity)` | `Task<T>` | Adds new entity and saves |
| `UpdateAsync(entity)` | `Task` | Updates existing entity and saves |
| `DeleteAsync(entity)` | `Task` | Deletes entity and saves |
| `ExistsAsync(predicate)` | `Task<bool>` | Checks if any entity matches predicate |
| `CountAsync(predicate?)` | `Task<int>` | Counts entities matching optional predicate |

### Usage Example

```csharp
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);
    Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default);
}
```

---

## Specialized Repository Interfaces

### IGuildRepository

**Location:** `DiscordBot.Core/Interfaces/IGuildRepository.cs`

Extends `IRepository<Guild>` with Discord guild-specific operations.

**Additional Methods:**

| Method | Return Type | Description |
|--------|-------------|-------------|
| `GetByDiscordIdAsync(discordId)` | `Task<Guild?>` | Gets guild by Discord snowflake ID |
| `GetActiveGuildsAsync()` | `Task<IReadOnlyList<Guild>>` | Gets all active guilds |
| `GetWithCommandLogsAsync(discordId)` | `Task<Guild?>` | Gets guild with command logs included |
| `SetActiveStatusAsync(discordId, isActive)` | `Task` | Updates guild active status efficiently |
| `UpsertAsync(guild)` | `Task<Guild>` | Creates or updates guild (insert/update) |

**Example:**
```csharp
public interface IGuildRepository : IRepository<Guild>
{
    Task<Guild?> GetByDiscordIdAsync(ulong discordId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guild>> GetActiveGuildsAsync(CancellationToken cancellationToken = default);
    Task<Guild?> GetWithCommandLogsAsync(ulong discordId, CancellationToken cancellationToken = default);
    Task SetActiveStatusAsync(ulong discordId, bool isActive, CancellationToken cancellationToken = default);
    Task<Guild> UpsertAsync(Guild guild, CancellationToken cancellationToken = default);
}
```

---

### IUserRepository

**Location:** `DiscordBot.Core/Interfaces/IUserRepository.cs`

Extends `IRepository<User>` with Discord user-specific operations.

**Additional Methods:**

| Method | Return Type | Description |
|--------|-------------|-------------|
| `GetByDiscordIdAsync(discordId)` | `Task<User?>` | Gets user by Discord snowflake ID |
| `GetWithCommandLogsAsync(discordId)` | `Task<User?>` | Gets user with command history |
| `UpdateLastSeenAsync(discordId)` | `Task` | Updates user's last seen timestamp |
| `UpsertAsync(user)` | `Task<User>` | Creates or updates user |
| `GetRecentlyActiveAsync(timeframe)` | `Task<IReadOnlyList<User>>` | Gets users active within timeframe |

**Example:**
```csharp
public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByDiscordIdAsync(ulong discordId, CancellationToken cancellationToken = default);
    Task<User?> GetWithCommandLogsAsync(ulong discordId, CancellationToken cancellationToken = default);
    Task UpdateLastSeenAsync(ulong discordId, CancellationToken cancellationToken = default);
    Task<User> UpsertAsync(User user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetRecentlyActiveAsync(
        TimeSpan timeframe,
        CancellationToken cancellationToken = default);
}
```

---

### ICommandLogRepository

**Location:** `DiscordBot.Core/Interfaces/ICommandLogRepository.cs`

Extends `IRepository<CommandLog>` with audit and analytics operations.

**Additional Methods:**

| Method | Return Type | Description |
|--------|-------------|-------------|
| `GetByGuildAsync(guildId, limit)` | `Task<IReadOnlyList<CommandLog>>` | Gets command logs for a guild |
| `GetByUserAsync(userId, limit)` | `Task<IReadOnlyList<CommandLog>>` | Gets command logs for a user |
| `GetByCommandNameAsync(commandName, limit)` | `Task<IReadOnlyList<CommandLog>>` | Gets logs for a specific command |
| `GetByDateRangeAsync(start, end)` | `Task<IReadOnlyList<CommandLog>>` | Gets logs within date range |
| `GetFailedCommandsAsync(limit)` | `Task<IReadOnlyList<CommandLog>>` | Gets failed command executions |
| `GetCommandUsageStatsAsync(since?)` | `Task<IDictionary<string, int>>` | Gets command usage statistics |
| `LogCommandAsync(...)` | `Task<CommandLog>` | Logs a command execution |

**Example:**
```csharp
public interface ICommandLogRepository : IRepository<CommandLog>
{
    Task<IReadOnlyList<CommandLog>> GetByGuildAsync(
        ulong guildId,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommandLog>> GetFailedCommandsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<IDictionary<string, int>> GetCommandUsageStatsAsync(
        DateTime? since = null,
        CancellationToken cancellationToken = default);

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

## Dependency Injection Registration

**Location:** `DiscordBot.Infrastructure/Extensions/ServiceCollectionExtensions.cs`

Repositories are registered as **Scoped** services to align with DbContext lifetime.

```csharp
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Register DbContext with SQLite
    var connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=discordbot.db";

    services.AddDbContext<BotDbContext>(options =>
        options.UseSqlite(connectionString));

    // Register repositories as Scoped
    services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    services.AddScoped<IGuildRepository, GuildRepository>();
    services.AddScoped<IUserRepository, UserRepository>();
    services.AddScoped<ICommandLogRepository, CommandLogRepository>();

    return services;
}
```

**In `Program.cs`:**
```csharp
builder.Services.AddInfrastructure(builder.Configuration);
```

---

## Scoped Lifetime and DbContext Management

### Why Scoped?

Repositories use **Scoped** lifetime because:
1. DbContext is registered as Scoped by default
2. Each HTTP request or command execution gets its own DbContext instance
3. Prevents concurrency issues and tracking conflicts
4. Automatic disposal at end of scope

### Lifetime Diagram

```
HTTP Request / Command Execution
    ↓
Dependency Injection Scope Created
    ↓
Repository Instance Created (Scoped)
    ↓
DbContext Instance Created (Scoped)
    ↓
Database Operations Performed
    ↓
Scope Disposed
    ↓
Repository Disposed
DbContext Disposed (changes saved)
```

### Important Considerations

**DO:**
- Inject repositories into services, controllers, and command modules
- Let DI manage repository lifetime
- Use `using` statements or DI scopes for manual instantiation

**DON'T:**
- Store repository instances in singleton services
- Share repository instances across multiple scopes
- Manually dispose repositories injected by DI

---

## Usage Examples

### In Command Modules

```csharp
public class AdminModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IGuildRepository _guildRepository;
    private readonly ILogger<AdminModule> _logger;

    public AdminModule(IGuildRepository guildRepository, ILogger<AdminModule> logger)
    {
        _guildRepository = guildRepository;
        _logger = logger;
    }

    [SlashCommand("guilds", "List all guilds the bot is in")]
    [RequireAdmin]
    public async Task ListGuildsAsync()
    {
        var guilds = await _guildRepository.GetActiveGuildsAsync();

        var response = string.Join("\n", guilds.Select(g => $"- {g.Name} ({g.Id})"));
        await RespondAsync($"Active Guilds:\n{response}");
    }
}
```

### In API Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class GuildsController : ControllerBase
{
    private readonly IGuildRepository _guildRepository;
    private readonly ILogger<GuildsController> _logger;

    public GuildsController(IGuildRepository guildRepository, ILogger<GuildsController> logger)
    {
        _guildRepository = guildRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Guild>>> GetGuilds()
    {
        var guilds = await _guildRepository.GetActiveGuildsAsync();
        return Ok(guilds);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Guild>> GetGuild(ulong id)
    {
        var guild = await _guildRepository.GetByDiscordIdAsync(id);

        if (guild == null)
            return NotFound();

        return Ok(guild);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateGuild(ulong id, [FromBody] Guild updatedGuild)
    {
        if (id != updatedGuild.Id)
            return BadRequest();

        var guild = await _guildRepository.UpsertAsync(updatedGuild);
        return Ok(guild);
    }
}
```

### In Services

```csharp
public interface IGuildService
{
    Task<Guild> RegisterGuildAsync(ulong guildId, string guildName);
    Task DeactivateGuildAsync(ulong guildId);
    Task<IEnumerable<Guild>> GetActiveGuildsAsync();
}

public class GuildService : IGuildService
{
    private readonly IGuildRepository _guildRepository;
    private readonly ILogger<GuildService> _logger;

    public GuildService(IGuildRepository guildRepository, ILogger<GuildService> logger)
    {
        _guildRepository = guildRepository;
        _logger = logger;
    }

    public async Task<Guild> RegisterGuildAsync(ulong guildId, string guildName)
    {
        _logger.LogInformation("Registering guild {GuildId} ({GuildName})", guildId, guildName);

        var guild = new Guild
        {
            Id = guildId,
            Name = guildName,
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        return await _guildRepository.UpsertAsync(guild);
    }

    public async Task DeactivateGuildAsync(ulong guildId)
    {
        _logger.LogInformation("Deactivating guild {GuildId}", guildId);
        await _guildRepository.SetActiveStatusAsync(guildId, false);
    }

    public async Task<IEnumerable<Guild>> GetActiveGuildsAsync()
    {
        return await _guildRepository.GetActiveGuildsAsync();
    }
}
```

### Complex Queries with FindAsync

```csharp
// Find guilds by custom prefix
var guildsWithPrefix = await _guildRepository.FindAsync(g => g.Prefix == "!");

// Find users seen in the last 24 hours
var cutoff = DateTime.UtcNow.AddHours(-24);
var recentUsers = await _userRepository.FindAsync(u => u.LastSeenAt >= cutoff);

// Find failed commands for a specific command
var failedPings = await _commandLogRepository.FindAsync(
    c => c.CommandName == "ping" && !c.Success);
```

### Upsert Pattern

The `UpsertAsync` method handles both insert and update operations:

```csharp
// Guild may or may not exist - upsert handles both cases
var guild = new Guild
{
    Id = 123456789,
    Name = "Updated Guild Name",
    JoinedAt = DateTime.UtcNow,
    IsActive = true,
    Prefix = "!",
    Settings = "{\"feature\": true}"
};

var result = await _guildRepository.UpsertAsync(guild);
// If guild existed: updates Name, IsActive, Prefix, Settings
// If guild didn't exist: inserts new record
```

---

## Testing with Repositories

### Unit Testing with Mocks

```csharp
public class GuildServiceTests
{
    private readonly Mock<IGuildRepository> _mockGuildRepository;
    private readonly Mock<ILogger<GuildService>> _mockLogger;
    private readonly GuildService _service;

    public GuildServiceTests()
    {
        _mockGuildRepository = new Mock<IGuildRepository>();
        _mockLogger = new Mock<ILogger<GuildService>>();
        _service = new GuildService(_mockGuildRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task RegisterGuildAsync_CreatesNewGuild()
    {
        // Arrange
        var guildId = 123456789UL;
        var guildName = "Test Guild";

        _mockGuildRepository
            .Setup(r => r.UpsertAsync(It.IsAny<Guild>(), default))
            .ReturnsAsync((Guild g, CancellationToken ct) => g);

        // Act
        var result = await _service.RegisterGuildAsync(guildId, guildName);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(guildId);
        result.Name.Should().Be(guildName);
        result.IsActive.Should().BeTrue();

        _mockGuildRepository.Verify(
            r => r.UpsertAsync(It.Is<Guild>(g => g.Id == guildId && g.Name == guildName), default),
            Times.Once);
    }
}
```

### Integration Testing with In-Memory Database

```csharp
public class GuildRepositoryTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly GuildRepository _repository;

    public GuildRepositoryTests()
    {
        // Create in-memory SQLite database
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BotDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new BotDbContext(options);
        _context.Database.EnsureCreated();

        var mockLogger = new Mock<ILogger<GuildRepository>>();
        _repository = new GuildRepository(_context, mockLogger.Object);
    }

    [Fact]
    public async Task GetActiveGuildsAsync_ReturnsOnlyActiveGuilds()
    {
        // Arrange
        var activeGuild = new Guild
        {
            Id = 111111111,
            Name = "Active Guild",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        var inactiveGuild = new Guild
        {
            Id = 222222222,
            Name = "Inactive Guild",
            JoinedAt = DateTime.UtcNow,
            IsActive = false
        };

        await _context.Guilds.AddRangeAsync(activeGuild, inactiveGuild);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetActiveGuildsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.First().Id.Should().Be(111111111);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
```

---

## Performance Tips

### 1. Use AsNoTracking for Read-Only Queries

When retrieving data that won't be modified, use `AsNoTracking()` for better performance:

```csharp
public async Task<IReadOnlyList<Guild>> GetActiveGuildsForDisplayAsync(
    CancellationToken cancellationToken = default)
{
    return await DbSet
        .AsNoTracking()  // Improves performance for read-only scenarios
        .Where(g => g.IsActive)
        .ToListAsync(cancellationToken);
}
```

### 2. Eager Loading vs Lazy Loading

**Eager Loading (Recommended):**
```csharp
// Load related data in one query
var guild = await DbSet
    .Include(g => g.CommandLogs)
    .FirstOrDefaultAsync(g => g.Id == guildId);
```

**Lazy Loading (Avoid):**
```csharp
// Triggers additional database queries (N+1 problem)
var guild = await DbSet.FindAsync(guildId);
var logs = guild.CommandLogs; // Separate query
```

### 3. Bulk Operations

For bulk updates, use `ExecuteUpdateAsync` instead of loading entities:

```csharp
// Efficient: Single UPDATE statement
await DbSet
    .Where(g => g.Id == guildId)
    .ExecuteUpdateAsync(setters => setters.SetProperty(g => g.IsActive, false));

// Inefficient: Load, modify, save
var guild = await DbSet.FindAsync(guildId);
guild.IsActive = false;
await Context.SaveChangesAsync();
```

### 4. Pagination

Always paginate large result sets:

```csharp
public async Task<IReadOnlyList<CommandLog>> GetPaginatedLogsAsync(
    int page,
    int pageSize,
    CancellationToken cancellationToken = default)
{
    return await DbSet
        .OrderByDescending(c => c.ExecutedAt)
        .Skip(page * pageSize)
        .Take(pageSize)
        .ToListAsync(cancellationToken);
}
```

---

## Common Patterns

### Upsert Implementation

```csharp
public async Task<Guild> UpsertAsync(Guild guild, CancellationToken cancellationToken = default)
{
    var existing = await GetByDiscordIdAsync(guild.Id, cancellationToken);

    if (existing == null)
    {
        // Insert
        await DbSet.AddAsync(guild, cancellationToken);
    }
    else
    {
        // Update
        existing.Name = guild.Name;
        existing.IsActive = guild.IsActive;
        existing.Prefix = guild.Prefix;
        existing.Settings = guild.Settings;
        DbSet.Update(existing);
        guild = existing;
    }

    await Context.SaveChangesAsync(cancellationToken);
    return guild;
}
```

### Conditional Updates

```csharp
public async Task UpdateLastSeenAsync(ulong discordId, CancellationToken cancellationToken = default)
{
    await DbSet
        .Where(u => u.Id == discordId)
        .ExecuteUpdateAsync(
            setters => setters.SetProperty(u => u.LastSeenAt, DateTime.UtcNow),
            cancellationToken);
}
```

### Query with Filtering and Sorting

```csharp
public async Task<IReadOnlyList<CommandLog>> GetByDateRangeAsync(
    DateTime start,
    DateTime end,
    CancellationToken cancellationToken = default)
{
    return await DbSet
        .Where(c => c.ExecutedAt >= start && c.ExecutedAt <= end)
        .OrderByDescending(c => c.ExecutedAt)
        .ToListAsync(cancellationToken);
}
```

---

## Troubleshooting

### Issue: "Cannot access a disposed DbContext"

**Cause:** Repository or DbContext disposed before operation completes.

**Solution:** Ensure proper scoping or use `using` statements:

```csharp
// Correct: DI handles scope
public class MyService
{
    private readonly IGuildRepository _repository;

    public MyService(IGuildRepository repository)
    {
        _repository = repository; // Scoped to service lifetime
    }
}

// Correct: Manual scope management
using (var scope = serviceProvider.CreateScope())
{
    var repository = scope.ServiceProvider.GetRequiredService<IGuildRepository>();
    var guild = await repository.GetByDiscordIdAsync(123);
}
```

### Issue: Entity tracking conflicts

**Cause:** Multiple entities with same key being tracked.

**Solution:** Use `AsNoTracking()` for queries or detach entities:

```csharp
// Option 1: Use AsNoTracking
var guilds = await _context.Guilds
    .AsNoTracking()
    .ToListAsync();

// Option 2: Detach entity
_context.Entry(existingGuild).State = EntityState.Detached;
```

### Issue: Changes not persisted

**Cause:** Forgot to call `SaveChangesAsync()`.

**Solution:** Repository methods automatically call `SaveChangesAsync()`, but if you're using DbContext directly:

```csharp
_context.Guilds.Update(guild);
await _context.SaveChangesAsync(); // Required!
```

---

## Best Practices

1. **Always use interfaces for dependency injection**
   ```csharp
   // Good
   public MyService(IGuildRepository repository) { }

   // Bad
   public MyService(GuildRepository repository) { }
   ```

2. **Return read-only collections from repository methods**
   ```csharp
   Task<IReadOnlyList<Guild>> GetAllAsync();  // Good
   Task<List<Guild>> GetAllAsync();           // Bad
   ```

3. **Use CancellationToken for async operations**
   ```csharp
   public async Task<Guild?> GetByIdAsync(
       ulong id,
       CancellationToken cancellationToken = default)  // Good
   ```

4. **Log repository operations for debugging**
   ```csharp
   _logger.LogDebug("Retrieving guild {GuildId}", guildId);
   ```

5. **Handle null returns gracefully**
   ```csharp
   var guild = await _repository.GetByDiscordIdAsync(id);
   if (guild == null)
       return NotFound();
   ```

---

*Document Version: 1.0*
*Created: December 2024*
*Last Updated: December 2024*
