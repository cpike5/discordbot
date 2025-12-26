# Implementation Plan: Issue #243 - Configure SignalR Infrastructure

**Document Version:** 1.0
**Date:** 2025-12-26
**Issue Reference:** GitHub Issue #243
**Parent Feature:** #218 (Real-time Dashboard)

---

## 1. Requirement Summary

Configure SignalR infrastructure to enable real-time communication between the Discord bot backend and the admin dashboard UI. This foundational issue establishes:

1. SignalR services registration in Dependency Injection
2. `DashboardHub` with authorization (accessible to all authenticated users with Viewer role or higher)
3. Endpoint mapping at `/hubs/dashboard`
4. SignalR JavaScript client library integrated into the layout
5. Hub methods for guild-specific group subscriptions and status retrieval

### Key Constraints

- **Authorization:** Hub requires "RequireViewer" policy (consistent with dashboard access)
- **No Additional NuGet Packages:** SignalR is included in ASP.NET Core 8
- **Client Library:** Add via npm for build-time bundling or CDN for simplicity
- **Group Pattern:** Use `guild-{guildId}` naming for guild-specific subscriptions
- **Endpoint Position:** Map hub after `UseAuthorization()` middleware

---

## 2. Architectural Considerations

### 2.1 Existing Infrastructure

| Component | Location | Relevance |
|-----------|----------|-----------|
| `Program.cs` | `src/DiscordBot.Bot/Program.cs` | Services registration (lines 39-240), endpoint mapping (lines 281-282) |
| `_Layout.cshtml` | `src/DiscordBot.Bot/Pages/Shared/_Layout.cshtml` | Scripts section for SignalR client |
| `Index.cshtml` (Dashboard) | `src/DiscordBot.Bot/Pages/Index.cshtml` | Primary consumer of real-time updates |
| `IBotService` | `src/DiscordBot.Core/Interfaces/IBotService.cs` | Bot status retrieval pattern |
| `BotService` | `src/DiscordBot.Bot/Services/BotService.cs` | Implementation pattern for status |
| Authorization Policies | `src/DiscordBot.Bot/Program.cs` (lines 134-165) | RequireViewer policy for dashboard access |
| `package.json` | `src/DiscordBot.Bot/package.json` | npm for Tailwind; can add SignalR client |

### 2.2 SignalR Architecture

```
                                    +------------------+
                                    |   DashboardHub   |
                                    |   /hubs/dashboard|
                                    +--------+---------+
                                             |
            +--------------------------------+--------------------------------+
            |                                |                                |
    +-------v--------+              +--------v-------+              +--------v--------+
    | JoinGuildGroup |              | LeaveGuildGroup|              | GetCurrentStatus|
    | (guildId)      |              | (guildId)      |              | ()              |
    +----------------+              +----------------+              +-----------------+
            |                                |                                |
            v                                v                                v
    Groups.AddToGroupAsync          Groups.RemoveFromGroupAsync       Return BotStatusDto
    ("guild-{guildId}")             ("guild-{guildId}")
```

**Group Naming Convention:**
- Guild-specific: `guild-{guildId}` (e.g., `guild-123456789012345678`)
- Future: Could add `all-guilds` for broadcast to all connected admins

### 2.3 Authorization Model

The hub uses the same authorization as the dashboard page:

```csharp
// Policy: RequireViewer (already defined in Program.cs)
// Allows: SuperAdmin, Admin, Moderator, Viewer
options.AddPolicy("RequireViewer", policy =>
    policy.RequireRole(
        IdentitySeeder.Roles.SuperAdmin,
        IdentitySeeder.Roles.Admin,
        IdentitySeeder.Roles.Moderator,
        IdentitySeeder.Roles.Viewer));
```

### 2.4 Client Library Options

| Option | Pros | Cons | Recommendation |
|--------|------|------|----------------|
| **npm + bundling** | Version controlled, offline capable | Adds build complexity | Preferred for production |
| **CDN (unpkg/jsdelivr)** | Simple setup, no build changes | External dependency, requires internet | Good for initial setup |
| **libman** | Visual Studio integrated | Less common in CLI workflows | Not recommended |

**Recommendation:** Use CDN initially for faster implementation, with a follow-up task to bundle via npm if needed.

### 2.5 Security Considerations

1. **Authentication Required:** Hub decorated with `[Authorize]` attribute
2. **Cookie Authentication:** SignalR uses existing cookie auth for WebSocket connections
3. **Guild Access Validation:** Future enhancement - validate user has access to requested guild before joining group
4. **Connection Limits:** Consider implementing per-user connection limits in future
5. **Message Size:** Default SignalR limits are sufficient for dashboard data

### 2.6 Integration Points

| Integration | Approach |
|-------------|----------|
| Authentication | Uses existing cookie authentication |
| Authorization | Applies RequireViewer policy |
| Bot Status | Inject IBotService for GetCurrentStatus |
| Future: Bot Events | BotHostedService will call hub to broadcast updates |
| Future: Guild Events | GuildService will notify hub of guild changes |

---

## 3. Subagent Task Plan

### 3.1 dotnet-specialist Tasks

#### Task 243.1: Create DashboardHub Class

**Description:** Create the SignalR hub for dashboard real-time communication.

**Directory to Create:** `src/DiscordBot.Bot/Hubs/`

**File to Create:** `src/DiscordBot.Bot/Hubs/DashboardHub.cs`

```csharp
using Discord.WebSocket;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DiscordBot.Bot.Hubs;

/// <summary>
/// SignalR hub for real-time dashboard updates.
/// Provides methods for guild-specific subscriptions and status retrieval.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class DashboardHub : Hub
{
    private readonly IBotService _botService;
    private readonly ILogger<DashboardHub> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardHub"/> class.
    /// </summary>
    /// <param name="botService">The bot service for status retrieval.</param>
    /// <param name="logger">The logger.</param>
    public DashboardHub(
        IBotService botService,
        ILogger<DashboardHub> logger)
    {
        _botService = botService;
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier ?? "unknown";
        var userName = Context.User?.Identity?.Name ?? "unknown";

        _logger.LogInformation(
            "Dashboard client connected: ConnectionId={ConnectionId}, User={UserName}",
            Context.ConnectionId,
            userName);

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    /// <param name="exception">The exception that caused the disconnect, if any.</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userName = Context.User?.Identity?.Name ?? "unknown";

        if (exception != null)
        {
            _logger.LogWarning(
                exception,
                "Dashboard client disconnected with error: ConnectionId={ConnectionId}, User={UserName}",
                Context.ConnectionId,
                userName);
        }
        else
        {
            _logger.LogInformation(
                "Dashboard client disconnected: ConnectionId={ConnectionId}, User={UserName}",
                Context.ConnectionId,
                userName);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Joins a guild-specific group to receive updates for that guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID to subscribe to.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task JoinGuildGroup(ulong guildId)
    {
        var groupName = GetGuildGroupName(guildId);
        var userName = Context.User?.Identity?.Name ?? "unknown";

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogDebug(
            "Client joined guild group: ConnectionId={ConnectionId}, User={UserName}, GuildId={GuildId}",
            Context.ConnectionId,
            userName,
            guildId);
    }

    /// <summary>
    /// Leaves a guild-specific group to stop receiving updates for that guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID to unsubscribe from.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LeaveGuildGroup(ulong guildId)
    {
        var groupName = GetGuildGroupName(guildId);
        var userName = Context.User?.Identity?.Name ?? "unknown";

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        _logger.LogDebug(
            "Client left guild group: ConnectionId={ConnectionId}, User={UserName}, GuildId={GuildId}",
            Context.ConnectionId,
            userName,
            guildId);
    }

    /// <summary>
    /// Gets the current bot status.
    /// </summary>
    /// <returns>The current bot status.</returns>
    public BotStatusDto GetCurrentStatus()
    {
        _logger.LogDebug(
            "Status requested by client: ConnectionId={ConnectionId}",
            Context.ConnectionId);

        return _botService.GetStatus();
    }

    /// <summary>
    /// Gets the group name for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The group name.</returns>
    private static string GetGuildGroupName(ulong guildId) => $"guild-{guildId}";
}
```

**Acceptance Criteria:**
- [ ] Hub created in `Hubs/` directory
- [ ] `[Authorize(Policy = "RequireViewer")]` attribute applied
- [ ] `JoinGuildGroup(ulong guildId)` method implemented
- [ ] `LeaveGuildGroup(ulong guildId)` method implemented
- [ ] `GetCurrentStatus()` method returns `BotStatusDto`
- [ ] Connection/disconnection logging implemented
- [ ] XML documentation on all public members
- [ ] Solution builds without errors

---

#### Task 243.2: Update Program.cs - Add SignalR Services

**Description:** Register SignalR services in the DI container.

**File to Modify:** `src/DiscordBot.Bot/Program.cs`

Add after line 220 (`builder.Services.AddRazorPages();`):

```csharp
// Add SignalR for real-time dashboard updates
builder.Services.AddSignalR(options =>
{
    // Configure SignalR options
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
});
```

**Rationale for Configuration:**
- `EnableDetailedErrors`: Only in development for debugging
- `KeepAliveInterval`: 15 seconds (default) keeps connections alive
- `ClientTimeoutInterval`: 30 seconds before considering client disconnected
- `HandshakeTimeout`: 15 seconds for initial connection handshake

**Acceptance Criteria:**
- [ ] `AddSignalR()` called in service registration
- [ ] Options configured appropriately for development/production
- [ ] Solution builds without errors

---

#### Task 243.3: Update Program.cs - Map Hub Endpoint

**Description:** Map the DashboardHub endpoint.

**File to Modify:** `src/DiscordBot.Bot/Program.cs`

Add required using statement at top of file:

```csharp
using DiscordBot.Bot.Hubs;
```

Add after line 282 (`app.MapRazorPages();`):

```csharp
// Map SignalR hub for real-time dashboard
app.MapHub<DashboardHub>("/hubs/dashboard");
```

**Endpoint Position:** After `UseAuthorization()` but with other endpoint mappings ensures:
1. Authentication middleware has run
2. Authorization can be evaluated
3. Consistent with other endpoints

**Acceptance Criteria:**
- [ ] Using statement added for `DiscordBot.Bot.Hubs`
- [ ] Hub mapped to `/hubs/dashboard`
- [ ] Endpoint mapped after authorization middleware
- [ ] Solution builds without errors

---

#### Task 243.4: Create IDashboardNotifier Interface

**Description:** Create an interface for services to send notifications through the hub.

**File to Create:** `src/DiscordBot.Core/Interfaces/IDashboardNotifier.cs`

```csharp
using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Interface for sending real-time notifications to dashboard clients.
/// Used by services to broadcast updates through SignalR.
/// </summary>
public interface IDashboardNotifier
{
    /// <summary>
    /// Broadcasts a bot status update to all connected clients.
    /// </summary>
    /// <param name="status">The updated bot status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BroadcastBotStatusAsync(BotStatusDto status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a guild-specific update to clients subscribed to that guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="eventName">The event name for client-side handling.</param>
    /// <param name="data">The event data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendGuildUpdateAsync(ulong guildId, string eventName, object data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a message to all connected dashboard clients.
    /// </summary>
    /// <param name="eventName">The event name for client-side handling.</param>
    /// <param name="data">The event data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BroadcastToAllAsync(string eventName, object data, CancellationToken cancellationToken = default);
}
```

**Acceptance Criteria:**
- [ ] Interface defined in Core project
- [ ] Methods for broadcast and guild-specific notifications
- [ ] XML documentation on all members
- [ ] Solution builds without errors

---

#### Task 243.5: Create DashboardNotifier Service

**Description:** Implement the dashboard notifier using SignalR hub context.

**File to Create:** `src/DiscordBot.Bot/Services/DashboardNotifier.cs`

```csharp
using DiscordBot.Bot.Hubs;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for sending real-time notifications to dashboard clients via SignalR.
/// </summary>
public class DashboardNotifier : IDashboardNotifier
{
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly ILogger<DashboardNotifier> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardNotifier"/> class.
    /// </summary>
    /// <param name="hubContext">The SignalR hub context.</param>
    /// <param name="logger">The logger.</param>
    public DashboardNotifier(
        IHubContext<DashboardHub> hubContext,
        ILogger<DashboardNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task BroadcastBotStatusAsync(BotStatusDto status, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Broadcasting bot status update to all clients");

        await _hubContext.Clients.All.SendAsync(
            "BotStatusUpdated",
            status,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SendGuildUpdateAsync(
        ulong guildId,
        string eventName,
        object data,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"guild-{guildId}";

        _logger.LogDebug(
            "Sending guild update: GuildId={GuildId}, Event={EventName}",
            guildId,
            eventName);

        await _hubContext.Clients.Group(groupName).SendAsync(
            eventName,
            data,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task BroadcastToAllAsync(
        string eventName,
        object data,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Broadcasting to all clients: Event={EventName}", eventName);

        await _hubContext.Clients.All.SendAsync(
            eventName,
            data,
            cancellationToken);
    }
}
```

**File to Modify:** `src/DiscordBot.Bot/Program.cs`

Add service registration after other singleton services (around line 177):

```csharp
// Add SignalR dashboard notifier
builder.Services.AddSingleton<IDashboardNotifier, DashboardNotifier>();
```

**Acceptance Criteria:**
- [ ] DashboardNotifier implements IDashboardNotifier
- [ ] Uses IHubContext<DashboardHub> for sending messages
- [ ] Proper logging for debugging
- [ ] Registered as singleton in DI
- [ ] Solution builds without errors

---

### 3.2 html-prototyper Tasks

#### Task 243.6: Add SignalR Client Script to Layout

**Description:** Add the SignalR JavaScript client library to the shared layout.

**File to Modify:** `src/DiscordBot.Bot/Pages/Shared/_Layout.cshtml`

Add before the navigation.js script (around line 42):

```html
    <!-- SignalR Client Library -->
    <script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js"
            integrity="sha512-7rhBJh1vn+Fs27RkAMXvjgL+WJiMUjYqMOl5E2V+8rLfUypXbprEWQGZGxKgaEHR4TKZJaBoVpchN8SWdKv+jg=="
            crossorigin="anonymous"
            referrerpolicy="no-referrer"></script>
```

**CDN Choice:** Using cdnjs.cloudflare.com with SRI hash for security.

**Alternative (npm bundling - future):** If bundling via npm is preferred later:

**File to Modify:** `src/DiscordBot.Bot/package.json`

Add to devDependencies:
```json
"@microsoft/signalr": "^8.0.0"
```

Then create `wwwroot/js/signalr-bundle.js` that imports and exposes the SignalR connection.

**Acceptance Criteria:**
- [ ] SignalR client script added to layout
- [ ] Integrity hash included for security
- [ ] Script loads before page-specific scripts
- [ ] No console errors on page load

---

#### Task 243.7: Create Dashboard SignalR Connection Manager

**Description:** Create a JavaScript module for managing the dashboard SignalR connection.

**File to Create:** `src/DiscordBot.Bot/wwwroot/js/dashboard-hub.js`

```javascript
/**
 * Dashboard Hub Connection Manager
 * Manages the SignalR connection to the DashboardHub for real-time updates.
 */
const DashboardHub = (function() {
    'use strict';

    let connection = null;
    let isConnected = false;
    let reconnectAttempts = 0;
    const maxReconnectAttempts = 5;
    const reconnectDelayMs = 2000;

    // Event handlers storage
    const eventHandlers = {};

    /**
     * Initializes the SignalR connection to the dashboard hub.
     * @returns {Promise<boolean>} True if connection successful, false otherwise.
     */
    async function connect() {
        if (connection && isConnected) {
            console.log('[DashboardHub] Already connected');
            return true;
        }

        try {
            connection = new signalR.HubConnectionBuilder()
                .withUrl('/hubs/dashboard')
                .withAutomaticReconnect({
                    nextRetryDelayInMilliseconds: (retryContext) => {
                        // Exponential backoff: 0s, 2s, 4s, 8s, 16s, then stop
                        if (retryContext.previousRetryCount >= maxReconnectAttempts) {
                            return null; // Stop reconnecting
                        }
                        return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 16000);
                    }
                })
                .configureLogging(signalR.LogLevel.Information)
                .build();

            // Set up connection state handlers
            connection.onreconnecting((error) => {
                console.warn('[DashboardHub] Connection lost, attempting to reconnect...', error);
                isConnected = false;
                triggerEvent('reconnecting', { error });
            });

            connection.onreconnected((connectionId) => {
                console.log('[DashboardHub] Reconnected with ID:', connectionId);
                isConnected = true;
                reconnectAttempts = 0;
                triggerEvent('reconnected', { connectionId });
            });

            connection.onclose((error) => {
                console.warn('[DashboardHub] Connection closed', error);
                isConnected = false;
                triggerEvent('disconnected', { error });
            });

            // Start the connection
            await connection.start();
            isConnected = true;
            reconnectAttempts = 0;
            console.log('[DashboardHub] Connected successfully');
            triggerEvent('connected', { connectionId: connection.connectionId });

            return true;
        } catch (error) {
            console.error('[DashboardHub] Failed to connect:', error);
            isConnected = false;
            triggerEvent('connectionFailed', { error });
            return false;
        }
    }

    /**
     * Disconnects from the dashboard hub.
     * @returns {Promise<void>}
     */
    async function disconnect() {
        if (connection) {
            try {
                await connection.stop();
                console.log('[DashboardHub] Disconnected');
            } catch (error) {
                console.error('[DashboardHub] Error during disconnect:', error);
            }
            isConnected = false;
        }
    }

    /**
     * Joins a guild-specific group to receive updates for that guild.
     * @param {string} guildId - The Discord guild ID.
     * @returns {Promise<void>}
     */
    async function joinGuildGroup(guildId) {
        if (!connection || !isConnected) {
            console.warn('[DashboardHub] Not connected, cannot join guild group');
            return;
        }

        try {
            await connection.invoke('JoinGuildGroup', guildId);
            console.log('[DashboardHub] Joined guild group:', guildId);
        } catch (error) {
            console.error('[DashboardHub] Failed to join guild group:', error);
        }
    }

    /**
     * Leaves a guild-specific group.
     * @param {string} guildId - The Discord guild ID.
     * @returns {Promise<void>}
     */
    async function leaveGuildGroup(guildId) {
        if (!connection || !isConnected) {
            console.warn('[DashboardHub] Not connected, cannot leave guild group');
            return;
        }

        try {
            await connection.invoke('LeaveGuildGroup', guildId);
            console.log('[DashboardHub] Left guild group:', guildId);
        } catch (error) {
            console.error('[DashboardHub] Failed to leave guild group:', error);
        }
    }

    /**
     * Gets the current bot status from the server.
     * @returns {Promise<object|null>} The bot status object or null on error.
     */
    async function getCurrentStatus() {
        if (!connection || !isConnected) {
            console.warn('[DashboardHub] Not connected, cannot get status');
            return null;
        }

        try {
            const status = await connection.invoke('GetCurrentStatus');
            return status;
        } catch (error) {
            console.error('[DashboardHub] Failed to get status:', error);
            return null;
        }
    }

    /**
     * Registers a handler for a specific server event.
     * @param {string} eventName - The event name from the server.
     * @param {function} handler - The callback function.
     */
    function on(eventName, handler) {
        if (!eventHandlers[eventName]) {
            eventHandlers[eventName] = [];
        }
        eventHandlers[eventName].push(handler);

        // Register with SignalR if connected
        if (connection) {
            connection.on(eventName, handler);
        }
    }

    /**
     * Removes a handler for a specific server event.
     * @param {string} eventName - The event name.
     * @param {function} handler - The handler to remove.
     */
    function off(eventName, handler) {
        if (eventHandlers[eventName]) {
            const index = eventHandlers[eventName].indexOf(handler);
            if (index > -1) {
                eventHandlers[eventName].splice(index, 1);
            }
        }

        if (connection) {
            connection.off(eventName, handler);
        }
    }

    /**
     * Triggers local event handlers (for connection state events).
     * @param {string} eventName - The event name.
     * @param {object} data - The event data.
     */
    function triggerEvent(eventName, data) {
        if (eventHandlers[eventName]) {
            eventHandlers[eventName].forEach(handler => {
                try {
                    handler(data);
                } catch (error) {
                    console.error('[DashboardHub] Error in event handler:', error);
                }
            });
        }
    }

    /**
     * Checks if currently connected.
     * @returns {boolean} True if connected.
     */
    function getIsConnected() {
        return isConnected;
    }

    /**
     * Gets the current connection ID.
     * @returns {string|null} The connection ID or null if not connected.
     */
    function getConnectionId() {
        return connection ? connection.connectionId : null;
    }

    // Public API
    return {
        connect,
        disconnect,
        joinGuildGroup,
        leaveGuildGroup,
        getCurrentStatus,
        on,
        off,
        isConnected: getIsConnected,
        connectionId: getConnectionId
    };
})();

// Auto-export for module systems if available
if (typeof module !== 'undefined' && module.exports) {
    module.exports = DashboardHub;
}
```

**Acceptance Criteria:**
- [ ] Module created with IIFE pattern for encapsulation
- [ ] Connection management with automatic reconnect
- [ ] Guild group subscription methods
- [ ] Event handler registration
- [ ] Connection state events (connected, disconnected, reconnecting)
- [ ] Error handling and logging
- [ ] No global pollution (single `DashboardHub` object)

---

### 3.3 docs-writer Tasks

#### Task 243.8: Document SignalR Infrastructure

**Description:** Create technical documentation for the SignalR infrastructure.

**File to Create:** `docs/articles/signalr-realtime.md`

```markdown
# SignalR Real-Time Dashboard

This document describes the SignalR infrastructure used for real-time updates in the Discord Bot Admin Dashboard.

## Overview

The dashboard uses SignalR to push real-time updates to connected clients, enabling live bot status, guild events, and command execution notifications without page refresh.

## Architecture

### Server Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `DashboardHub` | `src/DiscordBot.Bot/Hubs/DashboardHub.cs` | SignalR hub for client connections |
| `DashboardNotifier` | `src/DiscordBot.Bot/Services/DashboardNotifier.cs` | Service for broadcasting from other services |
| `IDashboardNotifier` | `src/DiscordBot.Core/Interfaces/IDashboardNotifier.cs` | Abstraction for notifier |

### Client Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `dashboard-hub.js` | `wwwroot/js/dashboard-hub.js` | JavaScript connection manager |
| SignalR Client | CDN | Microsoft SignalR JavaScript client |

## Hub Endpoint

- **URL:** `/hubs/dashboard`
- **Authentication:** Required (uses cookie authentication)
- **Authorization:** `RequireViewer` policy (all authenticated users with roles)

## Hub Methods

### JoinGuildGroup(guildId)

Subscribes the client to updates for a specific guild.

```javascript
await DashboardHub.joinGuildGroup('123456789012345678');
```

### LeaveGuildGroup(guildId)

Unsubscribes from guild-specific updates.

```javascript
await DashboardHub.leaveGuildGroup('123456789012345678');
```

### GetCurrentStatus()

Retrieves the current bot status synchronously.

```javascript
const status = await DashboardHub.getCurrentStatus();
console.log(status.connectionState, status.guildCount);
```

## Server-to-Client Events

### BotStatusUpdated

Fired when bot status changes (connection state, latency, guild count).

```javascript
DashboardHub.on('BotStatusUpdated', (status) => {
    console.log('Bot status:', status);
    updateBotStatusUI(status);
});
```

### GuildUpdated

Fired when a guild's data changes (sent only to clients subscribed to that guild).

```javascript
DashboardHub.on('GuildUpdated', (guildData) => {
    console.log('Guild update:', guildData);
    refreshGuildCard(guildData);
});
```

### CommandExecuted

Fired when a command is executed (future implementation).

```javascript
DashboardHub.on('CommandExecuted', (commandLog) => {
    console.log('Command executed:', commandLog);
    addToActivityFeed(commandLog);
});
```

## Client Usage

### Basic Connection

```javascript
// Connect on page load
document.addEventListener('DOMContentLoaded', async () => {
    const connected = await DashboardHub.connect();
    if (connected) {
        console.log('Connected to dashboard hub');

        // Register event handlers
        DashboardHub.on('BotStatusUpdated', handleStatusUpdate);

        // Get initial status
        const status = await DashboardHub.getCurrentStatus();
        updateUI(status);
    }
});

// Disconnect on page unload
window.addEventListener('beforeunload', () => {
    DashboardHub.disconnect();
});
```

### Connection State Events

```javascript
DashboardHub.on('connected', ({ connectionId }) => {
    console.log('Connected with ID:', connectionId);
    showConnectionIndicator('connected');
});

DashboardHub.on('disconnected', ({ error }) => {
    console.warn('Disconnected:', error);
    showConnectionIndicator('disconnected');
});

DashboardHub.on('reconnecting', ({ error }) => {
    console.log('Reconnecting...');
    showConnectionIndicator('reconnecting');
});

DashboardHub.on('reconnected', ({ connectionId }) => {
    console.log('Reconnected with ID:', connectionId);
    showConnectionIndicator('connected');
    // Re-fetch any missed data
    refreshDashboardData();
});
```

## Broadcasting from Services

Services can send real-time updates by injecting `IDashboardNotifier`:

```csharp
public class SomeService
{
    private readonly IDashboardNotifier _notifier;

    public SomeService(IDashboardNotifier notifier)
    {
        _notifier = notifier;
    }

    public async Task DoSomethingAsync()
    {
        // ... perform operation ...

        // Notify all clients
        await _notifier.BroadcastBotStatusAsync(newStatus);

        // Or notify specific guild subscribers
        await _notifier.SendGuildUpdateAsync(
            guildId,
            "GuildSettingsChanged",
            new { Setting = "value" });
    }
}
```

## Security Considerations

1. **Authentication:** All connections require valid authentication cookies
2. **Authorization:** Hub requires at least Viewer role
3. **Guild Access:** Future: Validate user has access to guild before allowing group join
4. **Rate Limiting:** SignalR has built-in connection limits

## Configuration

SignalR is configured in `Program.cs`:

```csharp
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});
```

## Troubleshooting

### Connection Fails

1. Check authentication - user must be logged in
2. Verify browser allows WebSocket connections
3. Check for CORS issues if running on different ports
4. Review browser console for detailed errors

### Messages Not Received

1. Verify event handler is registered before connection
2. Check the event name matches exactly (case-sensitive)
3. Ensure client is subscribed to the correct guild group
4. Check server logs for broadcast activity

### Reconnection Loops

1. Check network connectivity
2. Verify server is running
3. Check for authentication expiry
4. Review server logs for connection rejection reasons
```

**Acceptance Criteria:**
- [ ] Architecture documented with component locations
- [ ] All hub methods documented with examples
- [ ] Client usage patterns explained
- [ ] Broadcasting from services demonstrated
- [ ] Security considerations listed
- [ ] Troubleshooting section included

---

### 3.4 Testing Considerations

#### Task 243.9: Manual Testing Checklist

**Description:** Define manual testing steps to verify SignalR infrastructure.

**Testing Steps:**

1. **Basic Connection Test**
   - [ ] Navigate to dashboard page
   - [ ] Open browser developer tools (Network tab)
   - [ ] Verify WebSocket connection established to `/hubs/dashboard`
   - [ ] Check console for "Connected successfully" log

2. **Authentication Test**
   - [ ] Log out of application
   - [ ] Open browser console
   - [ ] Attempt to connect: `DashboardHub.connect()`
   - [ ] Verify connection fails with 401 Unauthorized

3. **Status Retrieval Test**
   - [ ] Connect to hub
   - [ ] Call `await DashboardHub.getCurrentStatus()`
   - [ ] Verify returned object has expected properties (uptime, guildCount, etc.)

4. **Guild Group Test**
   - [ ] Connect to hub
   - [ ] Call `await DashboardHub.joinGuildGroup('123456789')`
   - [ ] Verify no errors in console
   - [ ] Call `await DashboardHub.leaveGuildGroup('123456789')`
   - [ ] Verify no errors

5. **Reconnection Test**
   - [ ] Connect to hub
   - [ ] Stop the server (Ctrl+C)
   - [ ] Verify "reconnecting" event fires
   - [ ] Restart the server
   - [ ] Verify "reconnected" event fires

6. **Page Navigation Test**
   - [ ] Connect on dashboard
   - [ ] Navigate to another page
   - [ ] Return to dashboard
   - [ ] Verify new connection established

**Acceptance Criteria:**
- [ ] All manual tests pass
- [ ] No JavaScript errors in console
- [ ] Connection indicators work correctly

---

## 4. Timeline / Dependency Map

```
Phase 1: Core Infrastructure (Day 1)
├── Task 243.1: Create DashboardHub Class
├── Task 243.2: Add SignalR Services to Program.cs
├── Task 243.3: Map Hub Endpoint
└── Task 243.4: Create IDashboardNotifier Interface

Phase 2: Service Layer (Day 1)
└── Task 243.5: Create DashboardNotifier Service (depends on 243.1, 243.4)

Phase 3: Client Integration (Day 1-2)
├── Task 243.6: Add SignalR Client to Layout
└── Task 243.7: Create Dashboard Hub JavaScript Module (depends on 243.6)

Phase 4: Documentation & Testing (Day 2)
├── Task 243.8: Document SignalR Infrastructure
└── Task 243.9: Manual Testing (depends on all above)
```

**Parallel Opportunities:**
- Tasks 243.1, 243.2, 243.3 can be done together (all Program.cs/Hub setup)
- Task 243.4 can be done in parallel with hub creation
- Tasks 243.6, 243.7 can start once backend is complete
- Task 243.8 can start once design is finalized

**Estimated Timeline:** 1-2 days

---

## 5. Acceptance Criteria Summary

### Functional Requirements

- [ ] SignalR hub accessible at `/hubs/dashboard`
- [ ] Hub requires authentication (returns 401 if not logged in)
- [ ] Hub methods work: JoinGuildGroup, LeaveGuildGroup, GetCurrentStatus
- [ ] JavaScript client can connect and invoke methods
- [ ] Automatic reconnection works when connection is lost
- [ ] Event handlers receive server-pushed messages

### Technical Requirements

- [ ] `DashboardHub` class in `Hubs/` directory with `[Authorize]` attribute
- [ ] SignalR services registered in DI container
- [ ] Hub endpoint mapped after authorization middleware
- [ ] `IDashboardNotifier` interface in Core project
- [ ] `DashboardNotifier` service registered as singleton
- [ ] SignalR client library loaded in layout
- [ ] `dashboard-hub.js` module created with connection management

### Security Requirements

- [ ] Hub requires authentication
- [ ] Hub requires "RequireViewer" authorization policy
- [ ] Client library loaded with SRI integrity hash
- [ ] No sensitive data exposed in hub methods

### Documentation Requirements

- [ ] Technical documentation created
- [ ] Client usage examples provided
- [ ] Troubleshooting guide included

---

## 6. Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| WebSocket blocked by proxy/firewall | Low | Medium | SignalR auto-falls back to long polling |
| Memory leak from connection accumulation | Low | High | Proper disconnect on page unload, server-side timeout |
| Stale data after reconnection | Medium | Low | Re-fetch data on reconnect event |
| CDN unavailability | Low | Medium | Consider bundling via npm as fallback |
| Authentication cookie expiry during connection | Low | Medium | Handle 401 and redirect to login |

### Performance Considerations

1. **Connection Overhead:** Each dashboard client maintains one WebSocket
2. **Message Size:** Keep payloads small; avoid sending large data sets
3. **Broadcast Frequency:** Future work should throttle rapid status updates
4. **Group Size:** Monitor guild group sizes; consider fan-out strategies for large groups

---

## 7. File Summary

### Files to Create

| File | Project | Description |
|------|---------|-------------|
| `Hubs/DashboardHub.cs` | Bot | SignalR hub for dashboard |
| `Interfaces/IDashboardNotifier.cs` | Core | Notifier interface |
| `Services/DashboardNotifier.cs` | Bot | Notifier implementation |
| `wwwroot/js/dashboard-hub.js` | Bot | JavaScript connection manager |
| `articles/signalr-realtime.md` | docs | Technical documentation |

### Files to Modify

| File | Changes |
|------|---------|
| `Program.cs` | Add SignalR services, map hub endpoint |
| `_Layout.cshtml` | Add SignalR client script reference |

### Dependencies

| Package | Version | Source |
|---------|---------|--------|
| SignalR Server | Included | ASP.NET Core 8 |
| @microsoft/signalr | 8.0.0 | CDN (cdnjs.cloudflare.com) |

---

## 8. Future Enhancements

This issue establishes the foundation. Future issues will build upon it:

1. **Issue #244:** Integrate bot status events (BotHostedService broadcasts status changes)
2. **Issue #245:** Integrate guild events (join/leave guild notifications)
3. **Issue #246:** Real-time command activity feed
4. **Issue #247:** Connection status indicator UI component
5. **Issue #248:** Guild access validation in JoinGuildGroup

---

*Document prepared by: Systems Architect*
*Ready for implementation by: dotnet-specialist, html-prototyper, docs-writer*
