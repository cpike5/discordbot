# SignalR Real-Time Dashboard

This document describes the SignalR infrastructure used for real-time updates in the Discord Bot Admin Dashboard.

## Overview

The dashboard uses SignalR to push real-time updates from the Discord bot backend to connected admin clients, enabling live bot status updates, guild event notifications, and command execution monitoring without requiring page refresh. This bidirectional communication channel allows the admin interface to display current state and react instantly to backend events.

SignalR automatically negotiates the best transport protocol (WebSockets, Server-Sent Events, or Long Polling) based on client and server capabilities, falling back gracefully when necessary.

## Architecture

### Server Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `DashboardHub` | `src/DiscordBot.Bot/Hubs/DashboardHub.cs` | SignalR hub for managing client connections and providing hub methods |
| `DashboardNotifier` | `src/DiscordBot.Bot/Services/DashboardNotifier.cs` | Service for broadcasting events from backend services to connected clients |
| `IDashboardNotifier` | `src/DiscordBot.Core/Interfaces/IDashboardNotifier.cs` | Abstraction for the notifier service (Core layer) |

### Client Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `dashboard-hub.js` | `src/DiscordBot.Bot/wwwroot/js/dashboard-hub.js` | JavaScript connection manager with automatic reconnection and event handling |
| SignalR Client Library | CDN (cdnjs.cloudflare.com) | Microsoft SignalR JavaScript client (v8.0.0) |

### Communication Flow

```
┌─────────────────┐         SignalR          ┌─────────────────┐
│  Browser Client │◄─────────────────────────►│  DashboardHub   │
│ (dashboard.js)  │     /hubs/dashboard       │   (ASP.NET)     │
└─────────────────┘                           └────────┬────────┘
                                                       │
                                                       │ IHubContext<T>
                                                       │
                                              ┌────────▼────────┐
                                              │ DashboardNotifier│
                                              │   (Singleton)    │
                                              └────────▲────────┘
                                                       │
                                         ┌─────────────┴──────────────┐
                                         │                            │
                                  ┌──────▼──────┐            ┌───────▼────────┐
                                  │ BotService  │            │  Other Services │
                                  │  (Inject    │            │  (Future: Guild,│
                                  │ INotifier)  │            │   Commands, etc)│
                                  └─────────────┘            └────────────────┘
```

## Hub Endpoint

- **URL:** `/hubs/dashboard`
- **Authentication:** Required (uses cookie authentication)
- **Authorization:** `RequireViewer` policy (SuperAdmin, Admin, Moderator, or Viewer role)
- **Transport:** WebSockets (with fallback to Server-Sent Events or Long Polling)

The hub endpoint is mapped after the authentication and authorization middleware in `Program.cs`, ensuring all connections are authenticated before access is granted.

## Hub Methods

Hub methods are invoked by clients to interact with the server. These methods run on the server and can return values or perform actions.

### JoinGuildGroup(guildId)

Subscribes the client connection to a guild-specific group to receive real-time updates for that guild.

**Parameters:**
- `guildId` (ulong): The Discord guild ID as a numeric string

**Returns:** Task (no value)

**JavaScript Example:**

```javascript
// Join guild group to receive updates for guild 123456789012345678
await DashboardHub.joinGuildGroup('123456789012345678');
```

**Use Case:** When a user navigates to a guild details page, join the guild group to receive real-time updates about that guild (member count changes, settings updates, etc.).

**Group Naming Convention:** `guild-{guildId}` (e.g., `guild-123456789012345678`)

**Future Enhancement:** Validate that the user has permission to access the requested guild before allowing group membership.

---

### LeaveGuildGroup(guildId)

Unsubscribes the client from guild-specific updates when no longer interested in that guild's events.

**Parameters:**
- `guildId` (ulong): The Discord guild ID as a numeric string

**Returns:** Task (no value)

**JavaScript Example:**

```javascript
// Leave guild group when navigating away from guild details
await DashboardHub.leaveGuildGroup('123456789012345678');
```

**Use Case:** When a user navigates away from a guild details page, leave the group to stop receiving unnecessary updates and reduce server broadcast overhead.

---

### GetCurrentStatus()

Retrieves the current bot status synchronously from the server. This method returns data immediately rather than waiting for a broadcast event.

**Parameters:** None

**Returns:** `BotStatusDto` object

**JavaScript Example:**

```javascript
// Get current bot status on page load
const status = await DashboardHub.getCurrentStatus();
console.log('Bot state:', status.connectionState);
console.log('Guild count:', status.guildCount);
console.log('Uptime:', status.uptime);
console.log('Latency:', status.latency);
```

**BotStatusDto Properties:**
- `connectionState` (string): Current Discord connection state (Connected, Connecting, Disconnected, etc.)
- `guildCount` (int): Number of guilds the bot is currently in
- `uptime` (TimeSpan): How long the bot has been running
- `latency` (int?): Gateway latency in milliseconds (null if disconnected)
- `isReady` (bool): Whether the bot is fully connected and ready

**Use Case:** Fetch initial bot status when the dashboard page loads, before real-time updates begin arriving.

---

## Server-to-Client Events

Server-to-client events are pushed from the server to listening clients. Clients register handlers for these events to react to backend changes.

### BotStatusUpdated

Broadcast to all connected clients when the bot's status changes (connection state, latency, guild count, etc.).

**Event Data:** `BotStatusDto` object

**JavaScript Example:**

```javascript
// Register handler for bot status updates
DashboardHub.on('BotStatusUpdated', (status) => {
    console.log('Bot status updated:', status);
    updateBotStatusCard(status);
    updateConnectionIndicator(status.connectionState);
});
```

**Triggering Conditions:**
- Bot connects to Discord
- Bot disconnects from Discord
- Guild count changes (bot joins/leaves guild)
- Periodic status broadcasts (future implementation)

**Broadcast Scope:** All authenticated dashboard clients

---

### GuildUpdated

Sent to clients subscribed to a specific guild's group when that guild's data changes.

**Event Data:** Varies based on update type (typically includes `guildId` and updated properties)

**JavaScript Example:**

```javascript
// Register handler for guild-specific updates
DashboardHub.on('GuildUpdated', (guildData) => {
    console.log('Guild update received:', guildData);
    refreshGuildCard(guildData.guildId, guildData);
    showToast(`Guild ${guildData.name} updated`);
});
```

**Triggering Conditions (Future Implementation):**
- Guild settings changed via admin UI
- Guild member count changes
- Guild configuration updated
- Welcome message settings modified

**Broadcast Scope:** Only clients in the `guild-{guildId}` group

---

### CommandExecuted

Fired when a slash command is executed in any guild (future implementation for real-time activity feed).

**Event Data:** Command execution log object

**JavaScript Example:**

```javascript
// Register handler for command execution events
DashboardHub.on('CommandExecuted', (commandLog) => {
    console.log('Command executed:', commandLog);
    addToActivityFeed({
        timestamp: commandLog.timestamp,
        command: commandLog.commandName,
        user: commandLog.userName,
        guild: commandLog.guildName,
        success: commandLog.success
    });
});
```

**Future Event Data Properties:**
- `commandName` (string): The slash command that was executed
- `userName` (string): Discord user who invoked the command
- `guildId` (ulong): Guild where command was executed
- `guildName` (string): Guild name
- `timestamp` (DateTime): When the command was executed
- `success` (bool): Whether the command executed successfully
- `errorMessage` (string?): Error details if command failed

**Broadcast Scope:** All authenticated dashboard clients (or guild-specific if filtering is implemented)

---

## Client Usage

The `dashboard-hub.js` module provides a clean API for managing the SignalR connection and handling events.

### Basic Connection on Page Load

```javascript
// Connect to dashboard hub when the page loads
document.addEventListener('DOMContentLoaded', async () => {
    const connected = await DashboardHub.connect();

    if (connected) {
        console.log('Connected to dashboard hub');

        // Register event handlers
        DashboardHub.on('BotStatusUpdated', handleBotStatusUpdate);
        DashboardHub.on('GuildUpdated', handleGuildUpdate);

        // Get initial status
        const status = await DashboardHub.getCurrentStatus();
        updateDashboardUI(status);
    } else {
        console.error('Failed to connect to dashboard hub');
        showConnectionError();
    }
});

// Clean disconnect when page is unloaded
window.addEventListener('beforeunload', () => {
    DashboardHub.disconnect();
});
```

### Handling Connection State Events

The connection manager emits local events for connection state changes that don't come from the server.

```javascript
// Connected successfully
DashboardHub.on('connected', ({ connectionId }) => {
    console.log('Connected with ID:', connectionId);
    showConnectionIndicator('connected');
    hideReconnectingMessage();
});

// Disconnected (connection closed)
DashboardHub.on('disconnected', ({ error }) => {
    console.warn('Disconnected from hub:', error);
    showConnectionIndicator('disconnected');
    showReconnectingMessage();
});

// Reconnecting after connection loss
DashboardHub.on('reconnecting', ({ error }) => {
    console.log('Connection lost, attempting to reconnect...');
    showConnectionIndicator('reconnecting');
    showReconnectingMessage();
});

// Reconnected successfully after connection loss
DashboardHub.on('reconnected', ({ connectionId }) => {
    console.log('Reconnected with ID:', connectionId);
    showConnectionIndicator('connected');
    hideReconnectingMessage();

    // Re-fetch data that may have changed while disconnected
    refreshDashboardData();
});

// Connection failed (initial connection attempt)
DashboardHub.on('connectionFailed', ({ error }) => {
    console.error('Connection failed:', error);
    showConnectionIndicator('error');
    showConnectionError(error.message);
});
```

### Guild-Specific Subscriptions

```javascript
// When navigating to guild details page
async function loadGuildDetails(guildId) {
    // Join the guild group
    await DashboardHub.joinGuildGroup(guildId);

    // Register guild-specific event handler
    DashboardHub.on('GuildUpdated', handleGuildUpdate);

    // Load initial data
    const guildData = await fetchGuildData(guildId);
    renderGuildDetails(guildData);
}

// When navigating away from guild details page
async function unloadGuildDetails(guildId) {
    // Leave the guild group
    await DashboardHub.leaveGuildGroup(guildId);

    // Optionally remove handler if not needed on other pages
    DashboardHub.off('GuildUpdated', handleGuildUpdate);
}
```

### Checking Connection Status

```javascript
// Check if currently connected
if (DashboardHub.isConnected()) {
    console.log('Hub is connected');
    const connectionId = DashboardHub.connectionId();
    console.log('Connection ID:', connectionId);
}
```

---

## Broadcasting from Services

Backend services can send real-time updates to dashboard clients by injecting `IDashboardNotifier`.

### Injecting the Notifier

```csharp
public class BotService : IBotService
{
    private readonly IDashboardNotifier _notifier;
    private readonly ILogger<BotService> _logger;

    public BotService(
        IDashboardNotifier notifier,
        ILogger<BotService> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    public async Task OnBotStatusChangedAsync(BotStatusDto newStatus)
    {
        // Broadcast to all connected clients
        await _notifier.BroadcastBotStatusAsync(newStatus);

        _logger.LogInformation(
            "Broadcasted bot status update: State={State}, Guilds={GuildCount}",
            newStatus.ConnectionState,
            newStatus.GuildCount);
    }
}
```

### Broadcasting to All Clients

```csharp
// Broadcast bot status to all dashboard clients
var status = _botService.GetStatus();
await _notifier.BroadcastBotStatusAsync(status);
```

### Sending Guild-Specific Updates

```csharp
// Send update only to clients subscribed to this guild
await _notifier.SendGuildUpdateAsync(
    guildId: 123456789012345678,
    eventName: "GuildUpdated",
    data: new
    {
        GuildId = 123456789012345678,
        Name = "Updated Guild Name",
        MemberCount = 150,
        UpdatedAt = DateTime.UtcNow
    });
```

### Broadcasting Custom Events

```csharp
// Broadcast any custom event to all clients
await _notifier.BroadcastToAllAsync(
    eventName: "MaintenanceScheduled",
    data: new
    {
        Message = "Bot will restart in 5 minutes",
        ScheduledAt = DateTime.UtcNow.AddMinutes(5)
    });
```

---

## Security Considerations

### Authentication

All SignalR connections require valid authentication. The hub uses the same cookie-based authentication as the rest of the application:

- Users must be logged in with a valid session
- Unauthenticated connection attempts receive HTTP 401 Unauthorized
- Authentication state is validated on connection and reconnection

### Authorization

The `DashboardHub` is decorated with `[Authorize(Policy = "RequireViewer")]`, which enforces:

- User must have SuperAdmin, Admin, Moderator, or Viewer role
- Policy is defined in `Program.cs` authorization configuration
- Same authorization level as viewing the dashboard pages

### Future: Guild Access Validation

Currently, any authenticated user can join any guild group. Future enhancement should validate that the user has permission to access the requested guild before allowing `JoinGuildGroup`:

```csharp
public async Task JoinGuildGroup(ulong guildId)
{
    // TODO: Validate user has access to this guild
    // var hasAccess = await _guildService.ValidateGuildAccessAsync(
    //     Context.User, guildId);
    // if (!hasAccess)
    //     throw new HubException("Access denied to this guild");

    var groupName = GetGuildGroupName(guildId);
    await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
}
```

### Connection Limits

SignalR has built-in connection limits and timeout protection:

- `KeepAliveInterval`: 15 seconds (server pings clients)
- `ClientTimeoutInterval`: 30 seconds (server disconnects unresponsive clients)
- `HandshakeTimeout`: 15 seconds (initial connection handshake limit)

### Message Size and Rate Limiting

- Default SignalR message size limits are sufficient for dashboard data
- No custom rate limiting implemented currently
- Consider adding rate limiting for hub method invocations in future if abuse is detected

---

## Configuration

SignalR is configured in `src/DiscordBot.Bot/Program.cs` with the following options:

```csharp
builder.Services.AddSignalR(options =>
{
    // Enable detailed error messages only in development
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();

    // Send keep-alive pings every 15 seconds
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);

    // Disconnect clients that don't respond within 30 seconds
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);

    // Initial handshake must complete within 15 seconds
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
});
```

### Configuration Options Explained

| Option | Value | Purpose |
|--------|-------|---------|
| `EnableDetailedErrors` | `true` (dev only) | Exposes detailed exception messages to clients for debugging |
| `KeepAliveInterval` | 15 seconds | How often server sends ping frames to keep connection alive |
| `ClientTimeoutInterval` | 30 seconds | How long to wait for client response before disconnecting |
| `HandshakeTimeout` | 15 seconds | Maximum time allowed for initial connection handshake |

### Service Registration

The `DashboardNotifier` is registered as a singleton in the DI container:

```csharp
builder.Services.AddSingleton<IDashboardNotifier, DashboardNotifier>();
```

This allows any service to inject `IDashboardNotifier` and broadcast to connected clients.

### Endpoint Mapping

The hub endpoint is mapped after authentication and authorization middleware:

```csharp
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

// Map SignalR hub for real-time dashboard
app.MapHub<DashboardHub>("/hubs/dashboard");
```

---

## Troubleshooting

### Connection Fails with 401 Unauthorized

**Symptoms:**
- Browser console shows SignalR connection failed with 401 status
- `DashboardHub.connect()` returns `false`
- Network tab shows WebSocket upgrade request rejected

**Solutions:**
1. Verify user is logged in (check for authentication cookie)
2. Ensure user has at least Viewer role assigned
3. Check that authentication middleware is enabled in `Program.cs`
4. Verify user session hasn't expired (re-login if needed)
5. Check browser cookies are enabled and not being blocked

---

### Connection Fails with CORS Error

**Symptoms:**
- Browser console shows CORS policy error
- Connection fails when running dashboard on different port than API

**Solutions:**
1. Verify CORS is configured in `Program.cs` to allow the dashboard origin
2. Ensure credentials are included in SignalR connection options:
   ```javascript
   connection = new signalR.HubConnectionBuilder()
       .withUrl('/hubs/dashboard', {
           withCredentials: true
       })
       .build();
   ```
3. If using reverse proxy, ensure WebSocket upgrade headers are forwarded

---

### Messages Not Received by Client

**Symptoms:**
- Connection successful but event handlers never fire
- `BotStatusUpdated` events not appearing in console

**Solutions:**
1. Verify event handler is registered **before** calling `connect()`:
   ```javascript
   DashboardHub.on('BotStatusUpdated', handler);
   await DashboardHub.connect();
   ```
2. Check event name matches exactly (case-sensitive): `BotStatusUpdated` not `botStatusUpdated`
3. Ensure client is connected: `DashboardHub.isConnected()`
4. Check server logs to confirm broadcasts are being sent
5. Verify handler function is defined correctly:
   ```javascript
   function handler(data) {
       console.log('Received:', data);
   }
   ```

---

### Guild Updates Not Received

**Symptoms:**
- Connected to hub but not receiving guild-specific events
- `GuildUpdated` events not firing for a specific guild

**Solutions:**
1. Verify you joined the guild group: `await DashboardHub.joinGuildGroup(guildId)`
2. Check the `guildId` matches exactly (as string, not number)
3. Confirm the server is broadcasting to the correct group name (`guild-{guildId}`)
4. Check browser console for any errors during `JoinGuildGroup` call
5. Verify server logs show successful group join

---

### Reconnection Loops / Constant Disconnecting

**Symptoms:**
- Connection repeatedly connects and disconnects
- "reconnecting" events firing continuously
- Network tab shows many connection attempts

**Solutions:**
1. Check network connectivity and firewall settings
2. Verify server is running and accessible at `/hubs/dashboard`
3. Check for authentication cookie expiration (re-login may be needed)
4. Review server logs for connection rejection reasons
5. Ensure WebSockets are not being blocked by proxy or firewall
6. Check for conflicting browser extensions blocking WebSockets
7. If using nginx/IIS reverse proxy, verify WebSocket upgrade is configured

---

### WebSocket Connection Not Upgrading

**Symptoms:**
- Connection falls back to Long Polling instead of WebSockets
- "Negotiate" request succeeds but no WebSocket connection established

**Solutions:**
1. Verify server supports WebSockets (IIS requires WebSocket feature enabled)
2. Check reverse proxy configuration allows WebSocket upgrade headers
3. Ensure firewall/antivirus isn't blocking WebSocket protocol
4. Review browser console for WebSocket errors
5. Test with browser developer tools Network tab to see negotiation details

**nginx reverse proxy configuration example:**
```nginx
location /hubs/dashboard {
    proxy_pass http://localhost:5000;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_set_header Host $host;
    proxy_cache_bypass $http_upgrade;
}
```

---

### High Memory Usage / Connection Leaks

**Symptoms:**
- Server memory grows over time
- Many stale connections in logs
- Application performance degrades

**Solutions:**
1. Ensure clients call `DashboardHub.disconnect()` on page unload
2. Check that `beforeunload` event handler is registered:
   ```javascript
   window.addEventListener('beforeunload', () => {
       DashboardHub.disconnect();
   });
   ```
3. Verify server timeout settings are working (30-second client timeout)
4. Monitor connection count in server logs
5. Consider implementing server-side connection limits per user
6. Review for JavaScript errors preventing proper disconnection

---

## Related Documentation

- [API Endpoints](api-endpoints.md) - REST API documentation
- [Authorization Policies](authorization-policies.md) - Role hierarchy and policies
- [Identity Configuration](identity-configuration.md) - Authentication setup
- [Design System](design-system.md) - UI component guidelines

---

## Future Enhancements

This infrastructure provides the foundation for additional real-time features:

1. **Bot Event Integration (Issue #244):** Broadcast bot connection state changes from `BotHostedService`
2. **Guild Event Integration (Issue #245):** Notify clients when bot joins/leaves guilds
3. **Command Activity Feed (Issue #246):** Real-time command execution notifications
4. **Connection Indicator Component (Issue #247):** Visual UI component showing hub connection status
5. **Guild Access Validation (Issue #248):** Validate user permissions before allowing guild group join
6. **Presence Updates:** Show which admins are currently viewing the dashboard
7. **Collaborative Editing Indicators:** Show when multiple admins are editing same settings
8. **Real-time Metrics Streaming:** Push bot performance metrics to monitoring dashboards

---

**Version:** 0.3.0
**Last Updated:** 2025-12-26
**Issue Reference:** #243
