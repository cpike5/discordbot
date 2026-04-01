# Interactive Components

This document provides comprehensive documentation for Discord component interactions in the bot, including buttons and future support for select menus. Interactive components enable rich user experiences with confirmation dialogs, pagination, and multi-step workflows.

## Overview

The bot currently supports the following Discord component types:

| Component Type | Status | Use Cases |
|----------------|--------|-----------|
| Buttons | ✓ Implemented | Confirmations, pagination, action triggers |
| Select Menus | Planned | Category selection, option filtering, multi-choice workflows |
| Modals | Future | Text input forms, configuration wizards |

All interactive components use a standardized ID convention and state management system to maintain context across user interactions.

---

## Component ID Convention

Discord component custom IDs follow a structured format to ensure proper routing, security, and state management.

### Format Specification

```
{handler}:{action}:{userId}:{correlationId}:{data}
```

**Segment Breakdown:**

| Segment | Required | Description | Example |
|---------|----------|-------------|---------|
| `handler` | Yes | The component handler module (routes to specific handler method) | `shutdown`, `guilds`, `help` |
| `action` | Yes | The action to perform within the handler | `confirm`, `cancel`, `page`, `next`, `prev` |
| `userId` | Yes | The Discord user ID who can interact with this component (security) | `123456789012345678` |
| `correlationId` | Yes | An 8-character identifier for state lookup | `a7b3c9d1` |
| `data` | No | Optional additional data for the action | `next`, `prev`, `5` (page number) |

### Example Component IDs

```
shutdown:confirm:123456789012345678:a7b3c9d1:
shutdown:cancel:123456789012345678:a7b3c9d1:
guilds:page:123456789012345678:b4f8e2a6:next
guilds:page:123456789012345678:b4f8e2a6:prev
help:select:123456789012345678:c3d7f1b9:admin
```

### Purpose of Each Segment

#### Handler
The handler segment routes the component interaction to the appropriate module class. Discord.NET matches this against `[ComponentInteraction]` attribute patterns.

**Usage:**
- Must be unique across all component handlers
- Use lowercase, no spaces
- Represents the feature or command group

#### Action
The action segment specifies what operation to perform within the handler. This allows a single handler to manage multiple related actions.

**Usage:**
- Describes the specific operation (e.g., `confirm`, `cancel`, `page`)
- Multiple actions can share the same handler
- Use descriptive, action-oriented names

#### User ID
The user ID segment provides built-in security by restricting component interactions to the original command invoker.

**Security Implications:**
- Prevents unauthorized users from clicking someone else's buttons
- Validated in every component handler before processing
- Reduces risk of accidental or malicious interaction hijacking

**Validation Example:**
```csharp
if (parts.UserId != Context.User.Id)
{
    await RespondAsync("You cannot interact with this component.", ephemeral: true);
    return;
}
```

#### Correlation ID
The correlation ID is an 8-character unique identifier used to retrieve interaction state from the state service.

**Format:** First 8 characters of a GUID (lowercase, no hyphens)

**Generation:**
```csharp
Guid.NewGuid().ToString("N")[..8]  // Example: "a7b3c9d1"
```

**Purpose:**
- Links component clicks to stored state data
- Enables complex multi-step workflows
- Prevents state pollution with short, unique IDs
- Automatically expires with state expiration

**Security Note:** Correlation IDs are not cryptographically secure tokens. They rely on state expiration and user ID validation for security.

#### Data (Optional)
The data segment carries additional context-specific information for the action.

**Common Use Cases:**
- Pagination direction: `next`, `prev`
- Page numbers: `1`, `2`, `3`
- Selection values: `admin`, `general`, `moderation`
- Entity IDs: `5`, `guild123`

**Handling Empty Data:**
```csharp
// ComponentIdBuilder automatically handles null/empty data
var id = ComponentIdBuilder.Build("handler", "action", userId, correlationId);
// Result: "handler:action:123456789012345678:a7b3c9d1:"

// With data
var id = ComponentIdBuilder.Build("handler", "action", userId, correlationId, "next");
// Result: "handler:action:123456789012345678:a7b3c9d1:next"
```

---

## State Management

Interactive workflows require maintaining state across multiple interactions. The bot uses an in-memory state service to store temporary data associated with component interactions.

### Architecture

The state management system consists of three core components:

| Component | Type | Responsibility |
|-----------|------|----------------|
| `IInteractionStateService` | Interface | State service contract |
| `InteractionStateService` | Service | In-memory state storage and retrieval |
| `InteractionStateCleanupService` | Hosted Service | Background cleanup of expired states |

### Storage Mechanism

**Technology:** `ConcurrentDictionary<string, object>` for thread-safe in-memory storage

**Key Structure:**
- Key: 8-character correlation ID
- Value: `InteractionState<T>` wrapper containing state data

**Example State Wrapper:**
```csharp
public class InteractionState<T>
{
    public string CorrelationId { get; set; }
    public ulong UserId { get; set; }
    public T Data { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}
```

### State Expiration

**Default Expiration:** 15 minutes from creation

**Expiration Behavior:**
- State is checked for expiration on every retrieval attempt
- Expired state is automatically removed when accessed
- Background cleanup service runs every 1 minute to remove orphaned expired states

**Custom Expiration:**
```csharp
// Use default 15-minute expiration
var correlationId = _stateService.CreateState(userId, state);

// Use custom 5-minute expiration
var correlationId = _stateService.CreateState(userId, state, TimeSpan.FromMinutes(5));
```

### Background Cleanup

The `InteractionStateCleanupService` is a background hosted service that periodically removes expired state entries.

**Cleanup Interval:** Every 1 minute

**Process:**
1. Iterate through all state entries
2. Check `ExpiresAt` property via reflection
3. Remove expired entries
4. Log cleanup results at TRACE level

**Why Background Cleanup?**
- Prevents memory leaks from expired but unaccessed states
- Handles edge cases where users never interact after component expiration
- Maintains predictable memory usage

### State Persistence

**Important:** State is stored in-memory only and is **not persisted** to disk or database.

**Implications:**
- State is lost on bot restart
- State is not shared across multiple bot instances
- Acceptable trade-off for temporary interaction workflows

**Mitigations:**
- Commands can be re-run to generate new state
- Critical operations (like shutdown) require re-confirmation after restart
- Long-term data is stored in the database via repositories

---

## Adding New Interactive Commands

This section provides a step-by-step guide for developers implementing new interactive workflows.

### Step 1: Create State DTO

Define a class to represent the state data for your interactive workflow.

**Location:** `src/DiscordBot.Bot/Models/`

**Example:**
```csharp
namespace DiscordBot.Bot.Models;

/// <summary>
/// State data for shutdown confirmation interactions.
/// </summary>
public class ShutdownState
{
    /// <summary>
    /// The timestamp when the shutdown was requested.
    /// </summary>
    public DateTime RequestedAt { get; set; }
}
```

**Best Practices:**
- Keep state classes simple and focused
- Include only data needed for the interaction workflow
- Use descriptive property names with XML documentation
- Avoid storing large objects or collections when possible

### Step 2: Create State in Command Handler

When responding to a slash command, create the state and store it in the state service.

**Example:**
```csharp
[SlashCommand("shutdown", "Gracefully shut down the bot")]
[RequireOwner]
public async Task ShutdownAsync()
{
    // Create state
    var state = new ShutdownState
    {
        RequestedAt = DateTime.UtcNow
    };

    // Store state and get correlation ID
    var correlationId = _stateService.CreateState(Context.User.Id, state);

    // Build component IDs
    var confirmId = ComponentIdBuilder.Build("shutdown", "confirm", Context.User.Id, correlationId);
    var cancelId = ComponentIdBuilder.Build("shutdown", "cancel", Context.User.Id, correlationId);

    // Build buttons
    var components = new ComponentBuilder()
        .WithButton("Confirm Shutdown", confirmId, ButtonStyle.Danger)
        .WithButton("Cancel", cancelId, ButtonStyle.Secondary)
        .Build();

    // Send response with buttons
    await RespondAsync(
        embed: embedBuilder.Build(),
        components: components,
        ephemeral: true);
}
```

### Step 3: Create Component Handler

Create a handler method to process component interactions.

**Location:** `src/DiscordBot.Bot/Commands/AdminComponentModule.cs` (or create new module)

**Example:**
```csharp
/// <summary>
/// Handles the shutdown confirmation button interaction.
/// </summary>
[ComponentInteraction("shutdown:confirm:*:*:")]
[RequireOwner]
public async Task HandleShutdownConfirmAsync()
{
    var customId = ((SocketMessageComponent)Context.Interaction).Data.CustomId;

    // Parse component ID
    if (!ComponentIdBuilder.TryParse(customId, out var parts))
    {
        await RespondAsync("This interaction is invalid or has expired.", ephemeral: true);
        _logger.LogWarning("Invalid component ID format: {CustomId}", customId);
        return;
    }

    // Validate user
    if (parts.UserId != Context.User.Id)
    {
        await RespondAsync("You cannot interact with this component.", ephemeral: true);
        _logger.LogWarning(
            "User {ActualUserId} attempted to interact with component for user {ExpectedUserId}",
            Context.User.Id,
            parts.UserId);
        return;
    }

    // Retrieve state
    if (!_stateService.TryGetState<ShutdownState>(parts.CorrelationId, out var state) || state == null)
    {
        await RespondAsync("This interaction has expired. Please run the command again.", ephemeral: true);
        _logger.LogDebug("Expired or missing state for correlation ID {CorrelationId}", parts.CorrelationId);
        return;
    }

    // Remove state (cleanup)
    _stateService.TryRemoveState(parts.CorrelationId);

    // Perform action
    var originalMessage = (SocketMessageComponent)Context.Interaction;
    await originalMessage.UpdateAsync(msg =>
    {
        msg.Content = "Shutdown confirmed. The bot is shutting down...";
        msg.Components = new ComponentBuilder().Build();  // Remove buttons
    });

    _logger.LogInformation("Shutdown confirmed by user {UserId}", Context.User.Id);

    // Execute business logic
    _applicationLifetime.StopApplication();
}
```

### Step 4: Handle Component Patterns

The `[ComponentInteraction]` attribute uses pattern matching with wildcards (`*`).

**Pattern Examples:**

| Pattern | Matches |
|---------|---------|
| `"shutdown:confirm:*:*:"` | Any user, any correlation ID, no data |
| `"guilds:page:*:*:*"` | Any user, any correlation ID, any data |
| `"help:select:123:*:*"` | Specific user, any correlation ID, any data |

**Best Practice:** Use wildcards for user ID, correlation ID, and data segments to allow flexible matching.

### Step 5: Update Message (Optional)

Component handlers can update the original message to reflect the interaction result.

**Update Message Content:**
```csharp
var originalMessage = (SocketMessageComponent)Context.Interaction;
await originalMessage.UpdateAsync(msg =>
{
    msg.Content = "Operation completed successfully!";
    msg.Components = new ComponentBuilder().Build();  // Remove buttons
});
```

**Update Embed:**
```csharp
await originalMessage.UpdateAsync(msg =>
{
    msg.Embed = newEmbedBuilder.Build();
    msg.Components = newComponentBuilder.Build();
});
```

---

## Pagination Pattern

Pagination is a common interactive workflow for displaying large datasets across multiple pages.

### Implementation Example

**Command Handler:**
```csharp
[SlashCommand("guilds", "List all guilds the bot is connected to")]
public async Task GuildsAsync()
{
    var guilds = _client.Guilds
        .OrderByDescending(g => g.MemberCount)
        .Select(g => new GuildDto { /* ... */ })
        .ToList();

    // Set up pagination
    const int pageSize = 5;
    var totalPages = (int)Math.Ceiling(guilds.Count / (double)pageSize);
    var currentPage = 0;

    var state = new GuildsPaginationState
    {
        Guilds = guilds,
        CurrentPage = currentPage,
        PageSize = pageSize,
        TotalPages = totalPages
    };

    var correlationId = _stateService.CreateState(Context.User.Id, state);

    // Build first page
    var pageGuilds = guilds.Take(pageSize).ToList();
    var embed = new EmbedBuilder()
        .WithTitle($"Connected Guilds (Page 1/{totalPages})")
        .WithDescription(string.Join("\n", pageGuilds.Select(g => $"**{g.Name}** (ID: {g.Id})")))
        .WithColor(Color.Blue)
        .WithFooter($"Total: {guilds.Count} guilds")
        .Build();

    // Build pagination buttons
    var components = new ComponentBuilder();
    if (totalPages > 1)
    {
        var nextId = ComponentIdBuilder.Build("guilds", "page", Context.User.Id, correlationId, "next");
        components.WithButton("Next", nextId, ButtonStyle.Primary);
    }

    await RespondAsync(embed: embed, components: components.Build(), ephemeral: true);
}
```

**Pagination Handler:**
```csharp
[ComponentInteraction("guilds:page:*:*:*")]
public async Task HandleGuildsPageAsync()
{
    var customId = ((SocketMessageComponent)Context.Interaction).Data.CustomId;

    if (!ComponentIdBuilder.TryParse(customId, out var parts))
    {
        await RespondAsync("This interaction is invalid or has expired.", ephemeral: true);
        return;
    }

    // Validate user
    if (parts.UserId != Context.User.Id)
    {
        await RespondAsync("You cannot interact with this component.", ephemeral: true);
        return;
    }

    // Retrieve state
    if (!_stateService.TryGetState<GuildsPaginationState>(parts.CorrelationId, out var state) || state == null)
    {
        await RespondAsync("This interaction has expired. Please run the command again.", ephemeral: true);
        return;
    }

    // Update page based on direction
    var direction = parts.Data;
    if (direction == "next" && state.CurrentPage < state.TotalPages - 1)
    {
        state.CurrentPage++;
    }
    else if (direction == "prev" && state.CurrentPage > 0)
    {
        state.CurrentPage--;
    }

    // Create new state with updated page
    _stateService.TryRemoveState(parts.CorrelationId);
    var newCorrelationId = _stateService.CreateState(Context.User.Id, state);

    // Build page content
    var pageGuilds = state.Guilds
        .Skip(state.CurrentPage * state.PageSize)
        .Take(state.PageSize)
        .ToList();

    var embed = new EmbedBuilder()
        .WithTitle($"Connected Guilds (Page {state.CurrentPage + 1}/{state.TotalPages})")
        .WithDescription(string.Join("\n", pageGuilds.Select(g => $"**{g.Name}** (ID: {g.Id})")))
        .WithColor(Color.Blue)
        .WithFooter($"Total: {state.Guilds.Count} guilds")
        .Build();

    // Build pagination buttons
    var components = new ComponentBuilder();

    if (state.CurrentPage > 0)
    {
        var prevId = ComponentIdBuilder.Build("guilds", "page", Context.User.Id, newCorrelationId, "prev");
        components.WithButton("Previous", prevId, ButtonStyle.Primary);
    }

    if (state.CurrentPage < state.TotalPages - 1)
    {
        var nextId = ComponentIdBuilder.Build("guilds", "page", Context.User.Id, newCorrelationId, "next");
        components.WithButton("Next", nextId, ButtonStyle.Primary);
    }

    // Update message
    var originalMessage = (SocketMessageComponent)Context.Interaction;
    await originalMessage.UpdateAsync(msg =>
    {
        msg.Embed = embed;
        msg.Components = components.Build();
    });
}
```

### Pagination State DTO

```csharp
namespace DiscordBot.Bot.Models;

public class GuildsPaginationState
{
    public required List<GuildDto> Guilds { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
```

---

## Timeout Behavior

Component interactions have a built-in timeout mechanism to prevent stale interactions and memory leaks.

### Default Timeout

**Duration:** 15 minutes from state creation

**What Happens on Timeout:**
1. State expires in the state service
2. Component interaction attempts return "This interaction has expired" error
3. Background cleanup service removes the state entry within 1 minute
4. Discord buttons become unresponsive (no visual change)

### User Experience

When a user clicks an expired button:

```
This interaction has expired. Please run the command again.
```

**Ephemeral Response:** The error message is only visible to the user who clicked the button.

### Preventing Timeout Issues

**Best Practices:**
1. **Set Appropriate Expiration Times:**
   - Short workflows: 5 minutes
   - Standard workflows: 15 minutes (default)
   - Long workflows: 30 minutes (max recommended)

2. **Provide Clear Instructions:**
   - Inform users of timeout duration in embed footer
   - Suggest re-running command if timeout occurs

3. **Handle Gracefully:**
   - Always check for state existence before processing
   - Provide helpful error messages with re-run instructions

**Example Footer:**
```csharp
var embed = new EmbedBuilder()
    .WithFooter("This interaction will expire in 15 minutes. Run the command again if it expires.")
    .Build();
```

### Discord Component Timeout

**Discord Limit:** Discord.NET components remain interactive for up to 15 minutes by default.

**Alignment:** Our state expiration matches Discord's component timeout to ensure consistent behavior.

---

## Security Considerations

Interactive components introduce security challenges that must be addressed to prevent unauthorized access and malicious behavior.

### User Validation

**Mechanism:** Every component ID includes the user ID of the command invoker.

**Enforcement:**
```csharp
// Validate user
if (parts.UserId != Context.User.Id)
{
    await RespondAsync("You cannot interact with this component.", ephemeral: true);
    _logger.LogWarning(
        "User {ActualUserId} attempted to interact with component for user {ExpectedUserId}",
        Context.User.Id,
        parts.UserId);
    return;
}
```

**Why This Matters:**
- Prevents users from clicking buttons meant for someone else
- Reduces risk of accidental or malicious hijacking
- Ensures only the command invoker can complete the workflow

**Logging:** All unauthorized interaction attempts are logged at WARNING level for security monitoring.

### Permission Re-validation

For sensitive operations, permissions are re-validated when the component is clicked, not just when the command is invoked.

**Example: Shutdown Confirmation**
```csharp
[ComponentInteraction("shutdown:confirm:*:*:")]
[RequireOwner]  // Re-validates owner status
public async Task HandleShutdownConfirmAsync()
{
    // Handler logic
}
```

**Why Re-validate?**
- Permissions may change between command invocation and button click
- User roles may be revoked
- Prevents delayed attacks after permission loss

**Best Practice:** Always apply the same precondition attributes to both the slash command and component handlers.

### State Data Integrity

**Threat Model:**
- Correlation IDs are not cryptographically secure
- Attackers could theoretically guess correlation IDs (8 characters = 4.3 billion combinations)
- State data could be accessed or modified by malicious code

**Mitigations:**
1. **User ID Binding:** State is always associated with a specific user ID
2. **Short Expiration:** 15-minute default limits window for brute-force attacks
3. **Validation at Retrieval:** State type is validated on retrieval
4. **Logging:** State access is logged for audit trails

**Future Enhancements:**
- Increase correlation ID length for higher entropy
- Add HMAC signature validation
- Implement rate limiting on component interactions

### Sensitive Data in State

**Warning:** Avoid storing sensitive data (passwords, tokens, PII) in interaction state.

**Rationale:**
- State is in-memory and could be logged or dumped
- State is not encrypted at rest
- Correlation IDs could be guessed

**Best Practice:** Store only the minimum data needed for the workflow. Reference entities by ID and retrieve from database when needed.

**Example:**
```csharp
// BAD: Storing sensitive data
public class BadState
{
    public string UserPassword { get; set; }
    public string ApiToken { get; set; }
}

// GOOD: Storing only IDs
public class GoodState
{
    public ulong UserId { get; set; }
    public DateTime RequestedAt { get; set; }
}
```

---

## Testing Interactive Components

### Unit Testing State DTOs

**Example:**
```csharp
[Fact]
public void ShutdownState_ShouldStoreRequestedAt()
{
    var state = new ShutdownState
    {
        RequestedAt = DateTime.UtcNow
    };

    Assert.NotEqual(default, state.RequestedAt);
}
```

### Integration Testing Component Handlers

**Example:**
```csharp
[Fact]
public async Task HandleShutdownConfirmAsync_WithValidState_ShouldStopApplication()
{
    // Arrange
    var stateService = new InteractionStateService(logger);
    var state = new ShutdownState { RequestedAt = DateTime.UtcNow };
    var correlationId = stateService.CreateState(userId, state);

    // Act
    // Simulate button click with valid correlation ID

    // Assert
    // Verify application lifetime StopApplication was called
}
```

### Testing ComponentIdBuilder

**Example:**
```csharp
[Fact]
public void ComponentIdBuilder_Build_ShouldGenerateValidId()
{
    var id = ComponentIdBuilder.Build("shutdown", "confirm", 123456789, "a7b3c9d1");

    Assert.Equal("shutdown:confirm:123456789:a7b3c9d1:", id);
}

[Fact]
public void ComponentIdBuilder_Parse_ShouldExtractParts()
{
    var id = "guilds:page:123456789:b4f8e2a6:next";

    var parts = ComponentIdBuilder.Parse(id);

    Assert.Equal("guilds", parts.Handler);
    Assert.Equal("page", parts.Action);
    Assert.Equal(123456789ul, parts.UserId);
    Assert.Equal("b4f8e2a6", parts.CorrelationId);
    Assert.Equal("next", parts.Data);
}
```

---

## Related Documentation

- [Admin Commands](admin-commands.md) - Slash command reference including interactive `/shutdown` and `/guilds`
- [MVP Plan](mvp-plan.md) - Phase 5 implementation details and acceptance criteria
- [Permissions System](permissions.md) - Precondition attributes used in component handlers

---

*Document Version: 1.0*
*Last Updated: December 2025*
*Status: Phase 5 Implementation Complete*
