# Notification System

The notification system provides real-time alerts to admin UI users for important events like performance alerts, bot status changes, guild events, and command errors. Notifications are delivered via a bell icon in the navbar with SignalR-powered real-time updates.

## Overview

| Component | Purpose |
|-----------|---------|
| `INotificationService` | Core service for creating and managing notifications |
| `NotificationRepository` | Data access layer with optimized queries |
| `DashboardHub` | SignalR hub methods for real-time notification delivery |
| `NotificationRetentionService` | Background service for automatic cleanup |
| `notification-bell.js` | Client-side UI component |

## Notification Types

```csharp
public enum NotificationType
{
    PerformanceAlert = 1,  // Metrics exceeding thresholds
    BotStatus = 2,         // Bot connected/disconnected/restarted
    GuildEvent = 3,        // Guild joined/left events
    CommandError = 4       // Unhandled command exceptions
}
```

## Configuration

### Notification Options

Configure which events generate notifications in `appsettings.json`:

```json
{
  "Notification": {
    "EnablePerformanceAlerts": true,
    "EnableBotStatusChanges": true,
    "EnableGuildEvents": true,
    "EnableCommandErrors": true,
    "DuplicateSuppressionMinutes": 5
  }
}
```

| Setting | Description | Default |
|---------|-------------|---------|
| `EnablePerformanceAlerts` | Create notifications for Critical/Warning performance alerts | `true` |
| `EnableBotStatusChanges` | Create notifications when bot connects/disconnects | `true` |
| `EnableGuildEvents` | Create notifications when bot joins/leaves guilds | `true` |
| `EnableCommandErrors` | Create notifications for unhandled command exceptions | `true` |
| `DuplicateSuppressionMinutes` | Time window to suppress duplicate notifications | `5` |

### Retention Options

Configure automatic cleanup in `appsettings.json`:

```json
{
  "NotificationRetention": {
    "DismissedRetentionDays": 7,
    "ReadRetentionDays": 30,
    "UnreadRetentionDays": 90,
    "CleanupBatchSize": 1000,
    "CleanupIntervalHours": 24,
    "Enabled": true
  }
}
```

| Setting | Description | Default |
|---------|-------------|---------|
| `DismissedRetentionDays` | Days to keep dismissed notifications | `7` |
| `ReadRetentionDays` | Days to keep read notifications | `30` |
| `UnreadRetentionDays` | Days to keep unread notifications (0 = never delete) | `90` |
| `CleanupBatchSize` | Max records per cleanup batch | `1000` |
| `CleanupIntervalHours` | Hours between cleanup runs | `24` |
| `Enabled` | Enable automatic cleanup | `true` |

## Creating Notifications from Services

### Basic Pattern

All services that create notifications should follow the fire-and-forget pattern with proper error handling:

```csharp
public class MyService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<NotificationOptions> _notificationOptions;
    private readonly ILogger<MyService> _logger;

    public MyService(
        IServiceScopeFactory scopeFactory,
        IOptions<NotificationOptions> notificationOptions,
        ILogger<MyService> logger)
    {
        _scopeFactory = scopeFactory;
        _notificationOptions = notificationOptions.Value;
        _logger = logger;
    }

    private async Task CreateNotificationAsync(string title, string message)
    {
        // Check if this notification type is enabled
        if (!_notificationOptions.EnableMyFeature)
        {
            _logger.LogDebug("My feature notifications are disabled");
            return;
        }

        try
        {
            // Create a scope to resolve scoped services
            using var scope = _scopeFactory.CreateScope();
            var notificationService = scope.ServiceProvider
                .GetRequiredService<INotificationService>();

            var deduplicationWindow = TimeSpan.FromMinutes(
                _notificationOptions.DuplicateSuppressionMinutes);

            await notificationService.CreateForAllAdminsAsync(
                NotificationType.MyType,
                title,
                message,
                linkUrl: "/MyPage",
                relatedEntityType: "MyEntity",
                relatedEntityId: "unique-id",
                deduplicationWindow: deduplicationWindow);

            _logger.LogDebug("Created notification: {Title}", title);
        }
        catch (Exception ex)
        {
            // Log but don't throw - notifications should never break main functionality
            _logger.LogWarning(ex, "Failed to create notification, continuing normal operation");
        }
    }
}
```

### Key Principles

1. **Fire-and-Forget**: Notification failures should never break the calling code
2. **Check Configuration**: Always check if the notification type is enabled first
3. **Use Service Scope**: Create a scope for scoped services like `INotificationService`
4. **Deduplication**: Use `relatedEntityType` + `relatedEntityId` with `deduplicationWindow` to prevent duplicate notifications
5. **Meaningful Links**: Provide `linkUrl` to navigate users to relevant pages

### Service Interface Methods

#### Create for Specific User

```csharp
await notificationService.CreateForUserAsync(
    userId: "user-guid-string",
    type: NotificationType.GuildEvent,
    title: "Guild Settings Updated",
    message: "Settings for MyGuild were updated by Admin.",
    linkUrl: "/Guilds/Details?id=123456789",
    guildId: 123456789UL,
    relatedEntityType: "GuildSettings",
    relatedEntityId: "123456789");
```

#### Broadcast to All Admins

Used for system-wide events that all SuperAdmin and Admin users should see:

```csharp
var created = await notificationService.CreateForAllAdminsAsync(
    type: NotificationType.BotStatus,
    title: "Bot Reconnected",
    message: "The bot has reconnected to Discord gateway.",
    linkUrl: "/Admin/Performance",
    relatedEntityType: "BotStatus",
    relatedEntityId: "connected",
    deduplicationWindow: TimeSpan.FromMinutes(5));

// Returns false if suppressed as duplicate
if (!created)
{
    _logger.LogDebug("Notification suppressed as duplicate");
}
```

#### Broadcast to Guild Admins

Used for guild-specific events that only users with Admin/Owner access to that guild should see:

```csharp
await notificationService.CreateForGuildAdminsAsync(
    guildId: 123456789UL,
    type: NotificationType.GuildEvent,
    title: "Moderation Action Required",
    message: "User flagged for suspicious activity.",
    linkUrl: $"/Guilds/{guildId}/FlaggedEvents",
    relatedEntityType: "FlaggedEvent",
    relatedEntityId: eventId.ToString(),
    deduplicationWindow: TimeSpan.FromMinutes(5));
```

## Existing Integrations

### AlertMonitoringService (Performance Alerts)

Creates notifications for Critical and Warning severity performance alerts:

```csharp
// Location: src/DiscordBot.Bot/Services/AlertMonitoringService.cs:545-580
await notificationService.CreateForAllAdminsAsync(
    NotificationType.PerformanceAlert,
    title: $"{incident.MetricName} Alert",  // or "Resolved"
    message: incident.Message,
    linkUrl: "/Admin/Performance/Alerts",
    severity: incident.Severity,
    relatedEntityType: "PerformanceIncident",
    relatedEntityId: incident.Id.ToString(),
    deduplicationWindow: deduplicationWindow);
```

### BotHostedService (Bot Status & Guild Events)

Creates notifications for bot status changes:

```csharp
// Location: src/DiscordBot.Bot/Services/BotHostedService.cs:619-650
await notificationService.CreateForAllAdminsAsync(
    NotificationType.BotStatus,
    title: "Bot Connected",  // or "Bot Disconnected"
    message: "The bot has connected to Discord gateway.",
    linkUrl: "/Admin/Performance",
    relatedEntityType: "BotStatus",
    relatedEntityId: "connected",  // or "disconnected"
    deduplicationWindow: deduplicationWindow);
```

Creates notifications for guild join/leave events:

```csharp
// Location: src/DiscordBot.Bot/Services/BotHostedService.cs:660-691
await notificationService.CreateForAllAdminsAsync(
    NotificationType.GuildEvent,
    title: $"Joined Guild: {guild.Name}",  // or "Left Guild"
    message: $"Bot joined {guild.Name} ({guild.Id})",
    linkUrl: $"/Guilds/Details?id={guildId}",
    relatedEntityType: "Guild",
    relatedEntityId: $"{guildId}:joined",  // or "left"
    deduplicationWindow: deduplicationWindow);
```

### InteractionHandler (Command Errors)

Creates notifications for unhandled command exceptions:

```csharp
// Location: src/DiscordBot.Bot/Handlers/InteractionHandler.cs:565-606
await notificationService.CreateForAllAdminsAsync(
    NotificationType.CommandError,
    title: $"Command Error: /{commandName}",
    message: $"Command /{commandName} threw an exception. User: {user}. Error: {errorReason}",
    linkUrl: "/CommandLogs",
    relatedEntityType: "Command",
    relatedEntityId: commandName,
    deduplicationWindow: deduplicationWindow);
```

## SignalR Real-Time Updates

Notifications are automatically broadcast via SignalR when created. The `DashboardHub` provides these methods for client interaction:

### Hub Events (Server → Client)

| Event | Payload | Description |
|-------|---------|-------------|
| `OnNotificationReceived` | `UserNotificationDto` | New notification created |
| `OnNotificationCountChanged` | `int` (count) | Unread count changed |
| `OnNotificationMarkedRead` | `Guid` (notification ID) | Single notification marked read |
| `OnAllNotificationsRead` | (none) | All notifications marked read |

### Hub Methods (Client → Server)

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `GetNotificationSummary` | (none) | `NotificationSummaryDto` | Get unread counts by type |
| `GetNotifications` | `limit: int` (default 15, max 100) | `IEnumerable<UserNotificationDto>` | Get recent notifications |
| `MarkNotificationRead` | `notificationId: Guid` | (none) | Mark single notification read |
| `MarkAllNotificationsRead` | (none) | (none) | Mark all notifications read |
| `DismissNotification` | `notificationId: Guid` | (none) | Soft-delete notification |

### JavaScript Integration

The notification bell component automatically handles SignalR events:

```javascript
// Connect to existing DashboardHub
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/dashboard")
    .withAutomaticReconnect()
    .build();

// Listen for new notifications
connection.on("OnNotificationReceived", (notification) => {
    addNotificationToList(notification);
    updateBadgeCount(++unreadCount);
    showPulseAnimation();
});

// Listen for count changes
connection.on("OnNotificationCountChanged", (count) => {
    updateBadgeCount(count);
});

// Fetch initial data
await connection.start();
const summary = await connection.invoke("GetNotificationSummary");
const notifications = await connection.invoke("GetNotifications", 15);
```

## Database Schema

### UserNotifications Table

| Column | Type | Description |
|--------|------|-------------|
| `Id` | `uniqueidentifier` | Primary key |
| `UserId` | `nvarchar(450)` | FK to AspNetUsers |
| `Type` | `int` | NotificationType enum |
| `Severity` | `int?` | AlertSeverity for performance alerts |
| `Title` | `nvarchar(200)` | Short title |
| `Message` | `nvarchar(2000)` | Detailed message |
| `LinkUrl` | `nvarchar(500)` | Navigation URL |
| `GuildId` | `bigint?` | FK to Guilds (unsigned) |
| `IsRead` | `bit` | Read status |
| `CreatedAt` | `datetime2` | Creation timestamp |
| `ReadAt` | `datetime2?` | When marked read |
| `DismissedAt` | `datetime2?` | When dismissed (soft delete) |
| `RelatedEntityType` | `nvarchar(100)` | Entity type name |
| `RelatedEntityId` | `nvarchar(200)` | Entity ID |

### Indexes

- `IX_UserNotifications_UserId_IsRead_CreatedAt` - Primary query index
- `IX_UserNotifications_UserId_DismissedAt` - For filtering dismissed
- `IX_UserNotifications_CreatedAt` - For retention cleanup

## UI Components

### Notification Bell

Located in the navbar (`_Navbar.cshtml`), the notification bell displays:

- Unread count badge with pulse animation on new notifications
- Dropdown panel with up to 15 recent notifications
- Type-specific icons (alert, status, server, error)
- Visual distinction between read/unread states
- Mark as read (individual and all) actions
- Dismiss action to remove notifications
- Empty state when no notifications

### Accessibility Features

- ARIA labels and live regions for screen readers
- Keyboard navigation (Tab, Escape, Arrow keys)
- Focus management when opening/closing dropdown
- Reduced motion support for users who prefer it

### Mobile Responsiveness

- Full-screen slide-in panel on smaller screens
- Touch-friendly tap targets
- Proper z-index stacking above other elements

## Adding a New Notification Type

To add a new notification type:

1. **Add enum value** in `src/DiscordBot.Core/Enums/NotificationType.cs`:

```csharp
/// <summary>
/// Notification about my new feature.
/// </summary>
MyFeature = 5
```

2. **Update NotificationOptions** in `src/DiscordBot.Core/Configuration/NotificationOptions.cs`:

```csharp
/// <summary>
/// Gets or sets whether to create notifications for my feature events.
/// Default is true.
/// </summary>
public bool EnableMyFeatureEvents { get; set; } = true;
```

3. **Add configuration** in `appsettings.json`:

```json
{
  "Notification": {
    "EnableMyFeatureEvents": true
  }
}
```

4. **Update type display name** in `NotificationService.GetTypeDisplayName()`:

```csharp
NotificationType.MyFeature => "My Feature"
```

5. **Add icon in JavaScript** in `notification-bell.js`:

```javascript
function getTypeIcon(type) {
    switch (type) {
        // ... existing cases
        case 5: // MyFeature
            return '<svg>...</svg>';
    }
}
```

6. **Integrate in your service** following the pattern above.

## Best Practices

### DO

- Check configuration before creating notifications
- Use fire-and-forget pattern with try-catch
- Provide meaningful `linkUrl` for navigation
- Use `relatedEntityType` and `relatedEntityId` for deduplication
- Keep titles short (under 50 characters)
- Include relevant context in messages

### DON'T

- Don't let notification failures break main functionality
- Don't create notifications for every minor event
- Don't use Info severity for admin notifications (too noisy)
- Don't forget to add new types to the JavaScript icon mapping
- Don't create duplicate notifications without deduplication window

## Troubleshooting

### Notifications not appearing

1. Check if the notification type is enabled in configuration
2. Verify user has Admin or SuperAdmin role
3. Check browser console for SignalR connection errors
4. Verify notification was created in database

### Duplicate notifications

1. Ensure `relatedEntityType` and `relatedEntityId` are unique
2. Check `DuplicateSuppressionMinutes` setting
3. Verify the deduplication window is being passed

### SignalR connection issues

1. Check browser console for connection errors
2. Verify user is authenticated
3. Check network tab for WebSocket connection
4. Ensure `/hubs/dashboard` endpoint is accessible

## Related Documentation

- [SignalR Real-Time Updates](signalr-realtime.md)
- [Bot Performance Dashboard](bot-performance-dashboard.md)
- [Design System](design-system.md)
- [Authorization Policies](authorization-policies.md)
