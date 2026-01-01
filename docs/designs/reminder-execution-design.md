# Reminder Execution Service Design

**Version:** 1.0
**Created:** 2025-12-31
**Status:** Draft
**Feature:** Reminder System - Background Execution

---

## Overview

This document specifies the `ReminderExecutionService` background service responsible for polling due reminders and delivering them to users via Discord DM. The service follows the pattern established by `ScheduledMessageExecutionService` with concurrent execution, timeout protection, and retry logic.

## Design Goals

1. **Reliable delivery:** Poll for due reminders at regular intervals and deliver via DM
2. **Concurrent processing:** Execute multiple reminder deliveries simultaneously with semaphore limiting
3. **Timeout protection:** Prevent individual deliveries from blocking the service indefinitely
4. **Retry logic:** Handle transient failures (DMs disabled) with configurable retry attempts
5. **Error handling:** Gracefully handle permanent failures and update reminder status
6. **Resource efficiency:** Use scoped services and respect Discord rate limits
7. **Observability:** Comprehensive logging for monitoring and debugging

## Existing Pattern Reference

The `ScheduledMessageExecutionService` provides the foundation pattern for this design.

**Reference implementation:** `src/DiscordBot.Bot/Services/ScheduledMessageExecutionService.cs`

**Key patterns to reuse:**
- `BackgroundService` lifecycle with cancellation token
- `IServiceScopeFactory` for scoped service resolution
- `SemaphoreSlim` for concurrent execution limiting
- `CancellationTokenSource.CreateLinkedTokenSource` for timeout protection
- Structured logging with contextual information

---

## Service Implementation

### Class: ReminderExecutionService

**Location:** `src/DiscordBot.Bot/Services/ReminderExecutionService.cs`

```csharp
namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically checks for and delivers due reminders via Discord DM.
/// Runs at configured intervals and processes reminders concurrently with timeout and retry protection.
/// </summary>
public class ReminderExecutionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<ReminderOptions> _options;
    private readonly ILogger<ReminderExecutionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReminderExecutionService"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="options">The reminder configuration options.</param>
    /// <param name="logger">The logger.</param>
    public ReminderExecutionService(
        IServiceScopeFactory scopeFactory,
        IOptions<ReminderOptions> options,
        ILogger<ReminderExecutionService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Reminder execution service starting");

        _logger.LogInformation(
            "Reminder execution service enabled. Check interval: {IntervalSeconds}s, Max concurrent: {MaxConcurrent}, Timeout: {TimeoutSeconds}s, Max attempts: {MaxAttempts}",
            _options.Value.CheckIntervalSeconds,
            _options.Value.MaxConcurrentDeliveries,
            _options.Value.ExecutionTimeoutSeconds,
            _options.Value.MaxDeliveryAttempts);

        // Initial delay to let the app start up and Discord client connect
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueRemindersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during reminder processing");
            }

            // Wait for next check interval
            var interval = TimeSpan.FromSeconds(_options.Value.CheckIntervalSeconds);
            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("Reminder execution service stopping");
    }

    /// <summary>
    /// Processes all due reminders by delivering them concurrently with timeout protection.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to respect during processing.</param>
    private async Task ProcessDueRemindersAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Checking for due reminders");

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReminderRepository>();
        var deliveryService = scope.ServiceProvider.GetRequiredService<IReminderDeliveryService>();

        // Get all due reminders (pending and TriggerAt <= UtcNow)
        var dueReminders = await repository.GetDueRemindersAsync(stoppingToken);
        var reminderList = dueReminders.ToList();

        if (reminderList.Count == 0)
        {
            _logger.LogTrace("No reminders due for delivery");
            return;
        }

        _logger.LogInformation("Found {Count} reminders due for delivery", reminderList.Count);

        // Create a semaphore to limit concurrent deliveries
        using var semaphore = new SemaphoreSlim(_options.Value.MaxConcurrentDeliveries);
        var executionTimeout = TimeSpan.FromSeconds(_options.Value.ExecutionTimeoutSeconds);

        // Execute deliveries concurrently with semaphore and timeout protection
        var deliveryTasks = reminderList.Select(async reminder =>
        {
            await semaphore.WaitAsync(stoppingToken);
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(executionTimeout);

                _logger.LogDebug(
                    "Delivering reminder {ReminderId} to user {UserId}: {Message}",
                    reminder.Id, reminder.UserId, reminder.Message);

                var result = await deliveryService.DeliverReminderAsync(reminder.Id, cts.Token);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Successfully delivered reminder {ReminderId} to user {UserId}",
                        reminder.Id, reminder.UserId);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to deliver reminder {ReminderId} to user {UserId}. Attempt {Attempt}/{MaxAttempts}. Error: {Error}",
                        reminder.Id, reminder.UserId, result.AttemptNumber, _options.Value.MaxDeliveryAttempts, result.ErrorMessage);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Reminder delivery cancelled due to shutdown: {ReminderId}",
                    reminder.Id);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Reminder delivery timed out after {Timeout}s: {ReminderId}",
                    executionTimeout.TotalSeconds, reminder.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error delivering reminder {ReminderId} to user {UserId}",
                    reminder.Id, reminder.UserId);
            }
            finally
            {
                semaphore.Release();
            }
        });

        // Wait for all deliveries to complete
        await Task.WhenAll(deliveryTasks);

        _logger.LogInformation("Completed processing {Count} reminders", reminderList.Count);
    }
}
```

---

## Delivery Service

### Interface: IReminderDeliveryService

**Location:** `src/DiscordBot.Core/Interfaces/IReminderDeliveryService.cs`

```csharp
namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service responsible for delivering reminders to users via Discord DM.
/// </summary>
public interface IReminderDeliveryService
{
    /// <summary>
    /// Delivers a reminder to the user via DM.
    /// Handles retry logic, status updates, and error tracking.
    /// </summary>
    /// <param name="reminderId">The reminder ID to deliver.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Delivery result containing success status and metadata.</returns>
    Task<DeliveryResult> DeliverReminderAsync(Guid reminderId, CancellationToken ct = default);
}
```

### DeliveryResult

**Location:** `src/DiscordBot.Core/DTOs/DeliveryResult.cs`

```csharp
namespace DiscordBot.Core.DTOs;

/// <summary>
/// Result of a reminder delivery attempt.
/// </summary>
public class DeliveryResult
{
    /// <summary>
    /// Whether the delivery was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Current delivery attempt number.
    /// </summary>
    public int AttemptNumber { get; init; }

    /// <summary>
    /// Error message if delivery failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Whether the failure is permanent (no retry).
    /// </summary>
    public bool IsPermanentFailure { get; init; }

    public static DeliveryResult Ok(int attemptNumber) =>
        new() { Success = true, AttemptNumber = attemptNumber };

    public static DeliveryResult Retry(int attemptNumber, string error) =>
        new()
        {
            Success = false,
            AttemptNumber = attemptNumber,
            ErrorMessage = error,
            IsPermanentFailure = false
        };

    public static DeliveryResult PermanentFailure(int attemptNumber, string error) =>
        new()
        {
            Success = false,
            AttemptNumber = attemptNumber,
            ErrorMessage = error,
            IsPermanentFailure = true
        };
}
```

### Implementation: ReminderDeliveryService

**Location:** `src/DiscordBot.Bot/Services/ReminderDeliveryService.cs`

```csharp
namespace DiscordBot.Bot.Services;

public class ReminderDeliveryService : IReminderDeliveryService
{
    private readonly IReminderRepository _repository;
    private readonly DiscordSocketClient _client;
    private readonly IOptions<ReminderOptions> _options;
    private readonly ILogger<ReminderDeliveryService> _logger;

    public ReminderDeliveryService(
        IReminderRepository repository,
        DiscordSocketClient client,
        IOptions<ReminderOptions> options,
        ILogger<ReminderDeliveryService> logger)
    {
        _repository = repository;
        _client = client;
        _options = options;
        _logger = logger;
    }

    public async Task<DeliveryResult> DeliverReminderAsync(Guid reminderId, CancellationToken ct = default)
    {
        var reminder = await _repository.GetByIdAsync(reminderId, ct);
        if (reminder == null)
        {
            _logger.LogWarning("Reminder {ReminderId} not found", reminderId);
            return DeliveryResult.PermanentFailure(0, "Reminder not found");
        }

        // Ensure reminder is still pending
        if (reminder.Status != ReminderStatus.Pending)
        {
            _logger.LogDebug(
                "Reminder {ReminderId} has status {Status}, skipping delivery",
                reminderId, reminder.Status);
            return DeliveryResult.Ok(reminder.DeliveryAttempts);
        }

        // Increment attempt counter
        reminder.DeliveryAttempts++;

        try
        {
            var user = _client.GetUser(reminder.UserId);
            if (user == null)
            {
                // User not found - permanent failure
                _logger.LogWarning(
                    "User {UserId} not found for reminder {ReminderId}",
                    reminder.UserId, reminderId);

                reminder.Status = ReminderStatus.Failed;
                reminder.LastError = "User not found";
                await _repository.UpdateAsync(reminder, ct);

                return DeliveryResult.PermanentFailure(
                    reminder.DeliveryAttempts,
                    "User not found");
            }

            // Build reminder embed
            var embed = BuildReminderEmbed(reminder);

            // Attempt DM delivery
            await user.SendMessageAsync(embed: embed);

            // Success - update reminder status
            reminder.Status = ReminderStatus.Delivered;
            reminder.DeliveredAt = DateTime.UtcNow;
            reminder.LastError = null;
            await _repository.UpdateAsync(reminder, ct);

            _logger.LogInformation(
                "Delivered reminder {ReminderId} to user {UserId} on attempt {Attempt}",
                reminderId, reminder.UserId, reminder.DeliveryAttempts);

            return DeliveryResult.Ok(reminder.DeliveryAttempts);
        }
        catch (Discord.Net.HttpException ex) when (ex.DiscordCode == DiscordErrorCode.CannotSendMessageToUser)
        {
            // User has DMs disabled - retry or fail
            _logger.LogWarning(
                "Cannot send DM to user {UserId} for reminder {ReminderId}. Attempt {Attempt}/{MaxAttempts}",
                reminder.UserId, reminderId, reminder.DeliveryAttempts, _options.Value.MaxDeliveryAttempts);

            reminder.LastError = "User has DMs disabled";

            if (reminder.DeliveryAttempts >= _options.Value.MaxDeliveryAttempts)
            {
                // Max attempts reached - permanent failure
                reminder.Status = ReminderStatus.Failed;
                await _repository.UpdateAsync(reminder, ct);

                return DeliveryResult.PermanentFailure(
                    reminder.DeliveryAttempts,
                    "User has DMs disabled (max attempts reached)");
            }
            else
            {
                // Will retry on next poll
                await _repository.UpdateAsync(reminder, ct);

                return DeliveryResult.Retry(
                    reminder.DeliveryAttempts,
                    "User has DMs disabled");
            }
        }
        catch (Exception ex)
        {
            // Unexpected error - log and retry
            _logger.LogError(ex,
                "Unexpected error delivering reminder {ReminderId} to user {UserId}",
                reminderId, reminder.UserId);

            reminder.LastError = ex.Message;

            if (reminder.DeliveryAttempts >= _options.Value.MaxDeliveryAttempts)
            {
                reminder.Status = ReminderStatus.Failed;
            }

            await _repository.UpdateAsync(reminder, ct);

            return reminder.Status == ReminderStatus.Failed
                ? DeliveryResult.PermanentFailure(reminder.DeliveryAttempts, ex.Message)
                : DeliveryResult.Retry(reminder.DeliveryAttempts, ex.Message);
        }
    }

    private Embed BuildReminderEmbed(Reminder reminder)
    {
        return new EmbedBuilder()
            .WithTitle("⏰ Reminder")
            .WithDescription(reminder.Message)
            .WithColor(Color.Blue)
            .WithFooter($"Reminder ID: {reminder.Id}")
            .WithTimestamp(DateTimeOffset.UtcNow)
            .AddField("Scheduled For", $"<t:{new DateTimeOffset(reminder.TriggerAt).ToUnixTimeSeconds()}:F>", inline: true)
            .AddField("Created", $"<t:{new DateTimeOffset(reminder.CreatedAt).ToUnixTimeSeconds()}:R>", inline: true)
            .Build();
    }
}
```

---

## Configuration Options

### ReminderOptions

**Location:** `src/DiscordBot.Core/Configuration/ReminderOptions.cs`

```csharp
namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for reminder execution and delivery.
/// </summary>
public class ReminderOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "Reminders";

    /// <summary>
    /// Gets or sets the interval (in seconds) between reminder checks.
    /// The background service will check for due reminders at this interval.
    /// Default is 30 seconds.
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum number of reminders to deliver concurrently.
    /// Used to prevent resource exhaustion when many reminders are due simultaneously.
    /// Default is 5.
    /// </summary>
    public int MaxConcurrentDeliveries { get; set; } = 5;

    /// <summary>
    /// Gets or sets the timeout (in seconds) for individual reminder delivery.
    /// If a delivery takes longer than this, it will be cancelled.
    /// Default is 30 seconds.
    /// </summary>
    public int ExecutionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum number of delivery attempts before marking as failed.
    /// Used when user has DMs disabled or other transient failures occur.
    /// Default is 3.
    /// </summary>
    public int MaxDeliveryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay (in minutes) between retry attempts.
    /// Currently not used - retries happen on next poll interval.
    /// Default is 5 minutes.
    /// </summary>
    public int RetryDelayMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of pending reminders a user can have.
    /// Used to prevent abuse and resource exhaustion.
    /// Default is 25.
    /// </summary>
    public int MaxRemindersPerUser { get; set; } = 25;

    /// <summary>
    /// Gets or sets the maximum number of days in advance a reminder can be scheduled.
    /// Default is 365 days (1 year).
    /// </summary>
    public int MaxAdvanceDays { get; set; } = 365;

    /// <summary>
    /// Gets or sets the minimum number of minutes in advance a reminder can be scheduled.
    /// Default is 1 minute.
    /// </summary>
    public int MinAdvanceMinutes { get; set; } = 1;
}
```

### Configuration in appsettings.json

```json
{
  "Reminders": {
    "CheckIntervalSeconds": 30,
    "MaxConcurrentDeliveries": 5,
    "ExecutionTimeoutSeconds": 30,
    "MaxDeliveryAttempts": 3,
    "RetryDelayMinutes": 5,
    "MaxRemindersPerUser": 25,
    "MaxAdvanceDays": 365,
    "MinAdvanceMinutes": 1
  }
}
```

---

## Execution Flow

### Polling Loop

```
┌─────────────────────────────────────────────────────────┐
│ ReminderExecutionService.ExecuteAsync()                 │
│                                                         │
│ 1. Log service startup                                 │
│ 2. Wait 10 seconds for Discord client to connect       │
│ 3. Enter polling loop:                                 │
│    a. Call ProcessDueRemindersAsync()                  │
│    b. Catch and log exceptions                         │
│    c. Wait CheckIntervalSeconds before next iteration  │
│ 4. Log service shutdown on cancellation                │
└─────────────────────────────────────────────────────────┘
```

### Reminder Processing

```
┌─────────────────────────────────────────────────────────┐
│ ProcessDueRemindersAsync()                              │
│                                                         │
│ 1. Create service scope                                │
│ 2. Get IReminderRepository                             │
│ 3. Get IReminderDeliveryService                        │
│ 4. Query GetDueRemindersAsync()                        │
│    → SELECT * FROM Reminders                           │
│      WHERE Status = Pending AND TriggerAt <= UtcNow    │
│      ORDER BY TriggerAt ASC                            │
│                                                         │
│ 5. If no reminders, log and return                     │
│ 6. Create semaphore with MaxConcurrentDeliveries       │
│ 7. For each reminder (parallel):                       │
│    a. Wait on semaphore                                │
│    b. Create timeout cancellation token                │
│    c. Call DeliverReminderAsync(reminderId)            │
│    d. Log result                                       │
│    e. Release semaphore                                │
│                                                         │
│ 8. Wait for all tasks to complete                      │
│ 9. Log completion                                      │
└─────────────────────────────────────────────────────────┘
```

### Delivery Attempt

```
┌─────────────────────────────────────────────────────────┐
│ DeliverReminderAsync(reminderId)                        │
│                                                         │
│ 1. Load reminder from database                         │
│ 2. Validate status is Pending                          │
│ 3. Increment DeliveryAttempts                          │
│ 4. Get Discord user by UserId                          │
│                                                         │
│ 5. If user not found:                                  │
│    → Status = Failed                                   │
│    → LastError = "User not found"                      │
│    → Return PermanentFailure                           │
│                                                         │
│ 6. Build reminder embed                                │
│ 7. Attempt SendMessageAsync() to user DM               │
│                                                         │
│ 8. On success:                                         │
│    → Status = Delivered                                │
│    → DeliveredAt = UtcNow                              │
│    → LastError = null                                  │
│    → Return Ok                                         │
│                                                         │
│ 9. On CannotSendMessageToUser exception:               │
│    → LastError = "User has DMs disabled"               │
│    → If DeliveryAttempts >= MaxDeliveryAttempts:       │
│       • Status = Failed                                │
│       • Return PermanentFailure                        │
│    → Else:                                             │
│       • Return Retry (will retry on next poll)         │
│                                                         │
│ 10. On other exception:                                │
│     → LastError = exception.Message                    │
│     → If DeliveryAttempts >= MaxDeliveryAttempts:      │
│        • Status = Failed                               │
│        • Return PermanentFailure                       │
│     → Else:                                            │
│        • Return Retry                                  │
└─────────────────────────────────────────────────────────┘
```

---

## Retry Logic

### Retry Strategy

**Type:** Polling-based retry (not immediate)

When a delivery fails with a transient error:
1. Increment `DeliveryAttempts`
2. Set `LastError` to error message
3. Keep `Status = Pending`
4. Do NOT update `TriggerAt`
5. Return `DeliveryResult.Retry`

On the next polling interval (30 seconds default), the reminder will be picked up again by `GetDueRemindersAsync()` since it's still Pending and TriggerAt has passed.

### Retry Limits

- **MaxDeliveryAttempts:** 3 (default)
- **Retry interval:** Equals `CheckIntervalSeconds` (30s default)
- **Maximum retry window:** ~90 seconds (3 attempts × 30s interval)

### Permanent Failures

Do NOT retry in these cases:
- User not found (`DiscordSocketClient.GetUser()` returns null)
- User no longer in any mutual guilds with the bot
- Reminder deleted or cancelled between poll cycles

---

## Error Handling

### Exception Types

| Exception | Handling | Status Update | Retry? |
|-----------|----------|---------------|--------|
| `HttpException: CannotSendMessageToUser` | Log warning, increment attempts | Pending or Failed | Yes (up to max) |
| User not found | Log warning | Failed | No |
| `OperationCanceledException` (timeout) | Log warning | No change | Yes (next poll) |
| `OperationCanceledException` (shutdown) | Log info | No change | No |
| Any other exception | Log error, increment attempts | Pending or Failed | Yes (up to max) |

### Logging Strategy

**Log Levels:**

- **Trace:** No due reminders found
- **Debug:** Checking for due reminders, delivering specific reminder
- **Information:** Service start/stop, successful delivery, processing summary
- **Warning:** Failed delivery with retries remaining, timeout
- **Error:** Unexpected exceptions, permanent failures

**Structured Logging Fields:**

- `ReminderId` (Guid)
- `UserId` (ulong)
- `AttemptNumber` (int)
- `MaxAttempts` (int)
- `ErrorMessage` (string)
- `Count` (int) - number of reminders processed

---

## Reminder Embed Format

### Visual Design

```
╔══════════════════════════════════════════════════════╗
║ ⏰ Reminder                              [Blue Color]║
╟──────────────────────────────────────────────────────╢
║                                                      ║
║ {reminder.Message}                                   ║
║                                                      ║
║ Scheduled For          Created                       ║
║ Dec 31, 2024 3:00 PM   5 hours ago                   ║
║                                                      ║
╟──────────────────────────────────────────────────────╢
║ Reminder ID: {reminder.Id}              [Timestamp] ║
╚══════════════════════════════════════════════════════╝
```

### Embed Properties

```csharp
new EmbedBuilder()
    .WithTitle("⏰ Reminder")
    .WithDescription(reminder.Message)
    .WithColor(Color.Blue)
    .WithFooter($"Reminder ID: {reminder.Id}")
    .WithTimestamp(DateTimeOffset.UtcNow)
    .AddField("Scheduled For", $"<t:{scheduledTimestamp}:F>", inline: true)
    .AddField("Created", $"<t:{createdTimestamp}:R>", inline: true)
    .Build();
```

**Discord timestamp formats:**
- `:F` - Full date and time (e.g., "Monday, December 31, 2024 3:00 PM")
- `:R` - Relative time (e.g., "5 hours ago")

---

## Service Registration

### DI Container Registration

**Location:** `src/DiscordBot.Bot/Extensions/ServiceCollectionExtensions.cs` or `Program.cs`

```csharp
// Register reminder services
services.Configure<ReminderOptions>(configuration.GetSection(ReminderOptions.SectionName));
services.AddScoped<IReminderRepository, ReminderRepository>();
services.AddScoped<IReminderDeliveryService, ReminderDeliveryService>();

// Register background service
services.AddHostedService<ReminderExecutionService>();
```

**Service Lifetimes:**

- `ReminderExecutionService` - Singleton (hosted service)
- `IReminderRepository` - Scoped (created per poll cycle)
- `IReminderDeliveryService` - Scoped (created per poll cycle)
- `DiscordSocketClient` - Singleton (shared)
- `ReminderOptions` - Singleton (IOptions<T>)

---

## Performance Considerations

### Database Query Optimization

**Critical query:** `GetDueRemindersAsync()`

```sql
SELECT * FROM Reminders
WHERE Status = 0 -- Pending
  AND TriggerAt <= GETUTCDATE()
ORDER BY TriggerAt ASC
```

**Index requirement:** `IX_Reminders_Status_TriggerAt (Status, TriggerAt)`

This composite index allows fast lookup of pending reminders that are due.

**Expected load:**
- 1000 active users × 5 avg reminders = 5000 pending reminders
- Polling every 30 seconds = 120 queries/hour
- Index scan returns only due reminders (typically <100 per poll)

### Concurrency Limits

**MaxConcurrentDeliveries:** 5 (default)

With 5 concurrent deliveries and 30-second timeout, the service can deliver up to:
- **Best case:** 10 reminders/second (instant delivery)
- **Worst case:** 5 reminders/30s = 0.17/second (all timeouts)
- **Typical case:** 5 reminders/5s = 1/second (average 5s per DM)

**Scaling:** If reminder volume exceeds capacity, consider:
1. Increase `MaxConcurrentDeliveries` (more Discord API calls)
2. Decrease `CheckIntervalSeconds` (more frequent polls)
3. Add multiple instances (distributed processing - requires locking)

### Discord Rate Limits

**DM rate limits (per bot):**
- Global: 50 requests/second
- Per-channel (DM): 5 requests/5 seconds

With `MaxConcurrentDeliveries = 5`, we stay well under the global limit and distribute per-DM-channel load.

**429 Handling:** The Discord.NET library automatically handles rate limit responses. If a 429 is received, the library will wait and retry.

---

## Testing Strategy

### Unit Tests

**Test class:** `ReminderDeliveryServiceTests.cs`

```csharp
[Fact]
public async Task DeliverReminderAsync_Success_UpdatesStatusToDelivered()
{
    // Arrange: Mock repository, Discord client, reminder
    // Act: Call DeliverReminderAsync
    // Assert: Status = Delivered, DeliveredAt set, LastError null
}

[Fact]
public async Task DeliverReminderAsync_UserDMsDisabled_RetriesUpToMaxAttempts()
{
    // Arrange: Mock HttpException with CannotSendMessageToUser
    // Act: Call DeliverReminderAsync 3 times
    // Assert: First 2 return Retry, 3rd returns PermanentFailure, Status = Failed
}

[Fact]
public async Task DeliverReminderAsync_UserNotFound_ImmediatelyFails()
{
    // Arrange: Mock GetUser returns null
    // Act: Call DeliverReminderAsync
    // Assert: Status = Failed, LastError = "User not found", no retry
}
```

### Integration Tests

**Test class:** `ReminderExecutionServiceIntegrationTests.cs`

```csharp
[Fact]
public async Task ProcessDueReminders_DeliversMultipleConcurrently()
{
    // Arrange: Create 10 due reminders in database
    // Act: Run ProcessDueRemindersAsync once
    // Assert: All 10 reminders delivered, Status = Delivered
}

[Fact]
public async Task ProcessDueReminders_RespectsTimeout()
{
    // Arrange: Mock slow Discord API (delay 60s)
    // Act: Run with 5s timeout
    // Assert: Delivery cancelled, reminder still Pending
}
```

### Manual Testing Checklist

- [ ] Create reminder for 1 minute in future, verify DM received
- [ ] Create reminder with DMs disabled, verify retry behavior
- [ ] Create reminder, delete Discord account, verify permanent failure
- [ ] Create 10 reminders with same trigger time, verify concurrent delivery
- [ ] Restart bot service during delivery, verify graceful shutdown
- [ ] Monitor logs for 1 hour, verify no memory leaks or exceptions

---

## Monitoring and Observability

### Key Metrics

**Application Insights / Prometheus Metrics:**

1. `reminders.due.count` - Number of due reminders per poll
2. `reminders.delivered.count` - Successful deliveries
3. `reminders.failed.count` - Permanent failures
4. `reminders.retry.count` - Retry attempts
5. `reminders.delivery.duration` - Time to deliver (histogram)
6. `reminders.poll.duration` - Time to process poll cycle
7. `reminders.pending.total` - Total pending reminders (gauge)

### Health Checks

**Endpoint:** `/health`

```csharp
services.AddHealthChecks()
    .AddCheck<ReminderExecutionHealthCheck>("reminder_execution");
```

**Health check criteria:**
- Background service is running
- Last poll completed within 2× CheckIntervalSeconds
- No critical exceptions in last 5 minutes
- Database connectivity

### Alerts

**Recommended alerts:**

1. **High failure rate:** >10% of deliveries failing permanently
2. **Stalled polling:** No successful poll in 5 minutes
3. **Large backlog:** >1000 due reminders pending
4. **Service down:** Background service not running

---

## Future Considerations

### Potential Enhancements

1. **Distributed execution:** Multiple bot instances with distributed locking (Redis)
2. **Priority queue:** Deliver overdue reminders first (ordered by TriggerAt)
3. **Channel fallback:** If DM fails, optionally post in original channel (opt-in setting)
4. **Snooze functionality:** User can delay reminder by N minutes from DM button
5. **Delivery confirmation:** Track if user acknowledged reminder (message reaction)
6. **Batch delivery:** Group multiple reminders to same user in single DM
7. **Smart retry:** Exponential backoff instead of fixed interval
8. **User notification:** Alert user their reminder failed after max attempts (via server message)
9. **Analytics dashboard:** Delivery success rate, average latency, user engagement
10. **Recurring reminders:** Auto-schedule next occurrence after delivery

### Scaling Considerations

**Current design limitations:**

- Single-instance execution (no distributed coordination)
- Polling-based (not event-driven)
- Fixed retry interval (not adaptive)

**Scaling options:**

1. **Horizontal scaling:** Add distributed lock (Redis, SQL) to prevent duplicate deliveries
2. **Event-driven:** Trigger delivery on exact time via scheduled task (Hangfire, Quartz.NET)
3. **Partitioning:** Shard reminders by UserId, assign shards to instances
4. **Queue-based:** Push due reminders to Azure Queue/RabbitMQ for worker processing

---

## Related Documentation

- [Reminder Entity Design](reminder-entity-design.md) - Entity and repository design
- [Time Parsing Service Design](time-parsing-service-design.md) - Time input parsing
- Scheduled Message Execution Service (reference implementation)
- Background Services documentation

---

## Dependencies

**NuGet Packages:**
- Microsoft.Extensions.Hosting (BackgroundService)
- Microsoft.Extensions.DependencyInjection (IServiceScopeFactory)
- Microsoft.Extensions.Options (IOptions<T>)
- Discord.Net (DiscordSocketClient, SendMessageAsync)
- Microsoft.Extensions.Logging (ILogger<T>)

**Existing Services:**
- IReminderRepository (entity access)
- DiscordSocketClient (singleton)
- Configuration system (appsettings.json binding)

---

## Acceptance Criteria

- [ ] ReminderExecutionService implemented as BackgroundService
- [ ] IReminderDeliveryService interface and implementation created
- [ ] DeliveryResult DTO created
- [ ] ReminderOptions configuration class created
- [ ] Service registered in DI container
- [ ] Concurrent delivery with semaphore limiting
- [ ] Timeout protection on individual deliveries
- [ ] Retry logic for transient failures (DMs disabled)
- [ ] Permanent failure handling (user not found)
- [ ] Comprehensive structured logging
- [ ] Reminder embed format implemented
- [ ] Unit tests for delivery service (success, retry, failure)
- [ ] Integration tests for execution service
- [ ] Configuration documented in appsettings.json
- [ ] Service starts automatically with bot
- [ ] Graceful shutdown on cancellation token
