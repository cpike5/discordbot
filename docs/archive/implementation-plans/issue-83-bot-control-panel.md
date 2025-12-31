# Issue #83 - Bot Control Panel

## Implementation Plan

**Document Version:** 1.0
**Date:** 2025-12-23
**Issue Reference:** GitHub Issue #83
**Dependencies:**
- Feature 3.1: Bot Status Widget (#68) - COMPLETED
- Feature 2.2: Authorization Policies (#65) - COMPLETED

---

## 1. Requirement Summary

Create a dedicated Bot Control Panel page at `/Admin/BotControl` for bot lifecycle management. The page will provide administrators with:

- **Restart Bot** - With simple confirmation modal
- **Shutdown Bot** - With typed confirmation requiring "SHUTDOWN" to be entered
- **View Bot Configuration** - Read-only display with masked sensitive values (tokens show as masked)
- **Real-time Status Updates** - JavaScript polling to keep status indicator current

This is an Admin-only feature that provides direct control over the bot's lifecycle from the web UI.

---

## 2. Architectural Considerations

### 2.1 Existing System Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `IBotService` | `src/DiscordBot.Core/Interfaces/IBotService.cs` | Service interface with `RestartAsync()` and `ShutdownAsync()` |
| `BotService` | `src/DiscordBot.Bot/Services/BotService.cs` | Implementation using `IHostApplicationLifetime` |
| `BotController` | `src/DiscordBot.Bot/Controllers/BotController.cs` | REST API for `/api/bot/status`, `/api/bot/restart`, `/api/bot/shutdown` |
| `BotStatusDto` | `src/DiscordBot.Core/DTOs/BotStatusDto.cs` | DTO for bot status information |
| `BotStatusViewModel` | `src/DiscordBot.Bot/ViewModels/Pages/BotStatusViewModel.cs` | ViewModel for dashboard status card |
| `_ConfirmationModal.cshtml` | `src/DiscordBot.Bot/Pages/Shared/Components/` | Existing modal component |
| `ConfirmationModalViewModel` | `src/DiscordBot.Bot/ViewModels/Components/` | Modal configuration record |
| `quick-actions.js` | `src/DiscordBot.Bot/wwwroot/js/` | Modal handling, AJAX submissions, toast notifications |
| `bot-status-refresh.js` | `src/DiscordBot.Bot/wwwroot/js/` | Status polling (30-second interval) |

### 2.2 Integration Requirements

1. **Authorization**
   - Page requires `[Authorize(Policy = "RequireAdmin")]`
   - OnPost handlers must verify admin role before executing actions

2. **Existing Modal Limitations**
   - Current `_ConfirmationModal.cshtml` does not support typed confirmation
   - Will need to create enhanced `_TypedConfirmationModal.cshtml` component for shutdown

3. **API Usage**
   - REST API endpoints already exist at `/api/bot/restart` and `/api/bot/shutdown`
   - Can use either API or Razor Page handlers for actions
   - Recommend Razor Page handlers for consistency with existing patterns (see `Index.cshtml.cs`)

4. **Configuration Access**
   - Need new service method to expose safe configuration values
   - Must mask sensitive values (tokens, secrets)

### 2.3 Architectural Patterns to Follow

Based on existing codebase patterns:

```
Pattern: Razor Page with OnPost handlers for AJAX actions
Example: Pages/Index.cshtml.cs - OnPostRestartBotAsync(), OnPostSyncAllGuildsAsync()

Pattern: Shared components with ViewModels
Example: _ConfirmationModal.cshtml + ConfirmationModalViewModel

Pattern: JavaScript modules for client-side functionality
Example: quick-actions.js, bot-status-refresh.js
```

### 2.4 Security Considerations

| Risk | Mitigation |
|------|------------|
| Unauthorized restart/shutdown | `[Authorize(Policy = "RequireAdmin")]` + handler-level role check |
| CSRF attacks | Anti-forgery tokens (default in Razor Pages) |
| Accidental shutdown | Typed confirmation requiring "SHUTDOWN" |
| Token exposure | Mask sensitive configuration values in display |
| Audit trail | Log all administrative actions with user ID and timestamp |

### 2.5 Current RestartAsync Limitation

**Important:** The current `BotService.RestartAsync()` throws `NotSupportedException`:

```csharp
public Task RestartAsync(CancellationToken cancellationToken = default)
{
    _logger.LogWarning("Restart requested but not implemented - this feature requires external process management");
    throw new NotSupportedException("Bot restart is not currently supported. Use an external process manager for restart capabilities.");
}
```

**Options:**
1. **Soft Restart** - Disconnect and reconnect the Discord client (preferred)
2. **Application Restart** - Requires external process manager (systemd, Docker, etc.)
3. **Disable Feature** - Show restart as unavailable with explanation

**Recommendation:** Implement soft restart (disconnect/reconnect Discord client) as it can be done within the application.

---

## 3. Data Models

### 3.1 New DTOs

#### `BotConfigurationDto.cs`

**Location:** `src/DiscordBot.Core/DTOs/BotConfigurationDto.cs`

```csharp
namespace DiscordBot.Core.DTOs;

/// <summary>
/// Read-only bot configuration for display purposes.
/// Sensitive values are masked.
/// </summary>
public class BotConfigurationDto
{
    /// <summary>
    /// Gets the bot token (masked, shows only last 4 characters).
    /// </summary>
    public string TokenMasked { get; set; } = string.Empty;

    /// <summary>
    /// Gets the test guild ID if configured.
    /// </summary>
    public ulong? TestGuildId { get; set; }

    /// <summary>
    /// Gets whether a test guild is configured.
    /// </summary>
    public bool HasTestGuild { get; set; }

    /// <summary>
    /// Gets the OAuth client ID (masked).
    /// </summary>
    public string OAuthClientIdMasked { get; set; } = string.Empty;

    /// <summary>
    /// Gets whether OAuth is configured.
    /// </summary>
    public bool IsOAuthConfigured { get; set; }

    /// <summary>
    /// Gets the database provider name.
    /// </summary>
    public string DatabaseProvider { get; set; } = string.Empty;

    /// <summary>
    /// Gets the Discord.NET version.
    /// </summary>
    public string DiscordNetVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets the application version.
    /// </summary>
    public string AppVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets the .NET runtime version.
    /// </summary>
    public string RuntimeVersion { get; set; } = string.Empty;
}
```

### 3.2 Interface Extension

Add to `IBotService.cs`:

```csharp
/// <summary>
/// Gets the bot configuration with sensitive values masked.
/// </summary>
/// <returns>Bot configuration information safe for display.</returns>
BotConfigurationDto GetConfiguration();
```

---

## 4. ViewModels

### 4.1 Bot Control Page ViewModel

**Location:** `src/DiscordBot.Bot/ViewModels/Pages/BotControlViewModel.cs`

```csharp
namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the Bot Control Panel page.
/// </summary>
public class BotControlViewModel
{
    /// <summary>
    /// Gets the current bot status.
    /// </summary>
    public BotStatusViewModel Status { get; set; } = null!;

    /// <summary>
    /// Gets the bot configuration (with masked sensitive values).
    /// </summary>
    public BotConfigurationDto Configuration { get; set; } = null!;

    /// <summary>
    /// Gets whether restart is available.
    /// </summary>
    public bool CanRestart { get; set; }

    /// <summary>
    /// Gets the reason restart is unavailable (if applicable).
    /// </summary>
    public string? RestartUnavailableReason { get; set; }

    /// <summary>
    /// Gets whether shutdown is available.
    /// </summary>
    public bool CanShutdown { get; set; }

    /// <summary>
    /// Gets the last action result message (for success/error display).
    /// </summary>
    public string? LastActionMessage { get; set; }

    /// <summary>
    /// Gets whether the last action was successful.
    /// </summary>
    public bool? LastActionSuccess { get; set; }
}
```

### 4.2 Typed Confirmation Modal ViewModel

**Location:** `src/DiscordBot.Bot/ViewModels/Components/TypedConfirmationModalViewModel.cs`

```csharp
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for typed confirmation modal dialogs.
/// User must type a specific phrase to enable the confirm button.
/// </summary>
public record TypedConfirmationModalViewModel
{
    /// <summary>
    /// Gets the unique identifier for the modal.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the title of the confirmation dialog.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the message explaining the action and consequences.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the exact text the user must type to confirm.
    /// </summary>
    public string RequiredText { get; init; } = string.Empty;

    /// <summary>
    /// Gets the label for the input field.
    /// </summary>
    public string InputLabel { get; init; } = string.Empty;

    /// <summary>
    /// Gets the text for the confirm button.
    /// </summary>
    public string ConfirmText { get; init; } = "Confirm";

    /// <summary>
    /// Gets the text for the cancel button.
    /// </summary>
    public string CancelText { get; init; } = "Cancel";

    /// <summary>
    /// Gets the visual variant of the confirmation dialog.
    /// </summary>
    public ConfirmationVariant Variant { get; init; } = ConfirmationVariant.Danger;

    /// <summary>
    /// Gets the form action URL for POST submissions.
    /// </summary>
    public string? FormAction { get; init; }

    /// <summary>
    /// Gets the Razor Page handler name for the form.
    /// </summary>
    public string? FormHandler { get; init; }
}
```

---

## 5. Service Layer Changes

### 5.1 IBotService Extension

Add new method to `src/DiscordBot.Core/Interfaces/IBotService.cs`:

```csharp
/// <summary>
/// Gets the bot configuration with sensitive values masked.
/// </summary>
/// <returns>Bot configuration information safe for display.</returns>
BotConfigurationDto GetConfiguration();
```

### 5.2 BotService Implementation Updates

Update `src/DiscordBot.Bot/Services/BotService.cs`:

1. Add `GetConfiguration()` implementation
2. Modify `RestartAsync()` to perform soft restart (disconnect/reconnect)

```csharp
/// <inheritdoc/>
public BotConfigurationDto GetConfiguration()
{
    var token = _config.Token ?? string.Empty;
    var maskedToken = token.Length > 4
        ? $"{new string('\u2022', 20)}{token[^4..]}"
        : new string('\u2022', 24);

    return new BotConfigurationDto
    {
        TokenMasked = maskedToken,
        TestGuildId = _config.TestGuildId,
        HasTestGuild = _config.TestGuildId.HasValue,
        OAuthClientIdMasked = MaskClientId(_config.OAuth?.ClientId),
        IsOAuthConfigured = !string.IsNullOrEmpty(_config.OAuth?.ClientId),
        DatabaseProvider = GetDatabaseProvider(),
        DiscordNetVersion = GetDiscordNetVersion(),
        AppVersion = GetAppVersion(),
        RuntimeVersion = Environment.Version.ToString()
    };
}

/// <inheritdoc/>
public async Task RestartAsync(CancellationToken cancellationToken = default)
{
    _logger.LogWarning("Bot soft restart requested");

    // Disconnect from Discord
    await _client.StopAsync();
    await _client.LogoutAsync();

    _logger.LogInformation("Bot disconnected, waiting before reconnect...");

    // Brief delay to ensure clean disconnect
    await Task.Delay(2000, cancellationToken);

    // Reconnect
    await _client.LoginAsync(TokenType.Bot, _config.Token);
    await _client.StartAsync();

    _logger.LogInformation("Bot reconnected successfully");
}
```

---

## 6. Razor Pages Structure

### 6.1 Page Overview

| Page | Route | Purpose |
|------|-------|---------|
| BotControl | `/Admin/BotControl` | Bot lifecycle management and configuration view |

### 6.2 File Structure

```
src/DiscordBot.Bot/
├── Pages/
│   └── Admin/
│       ├── BotControl.cshtml
│       └── BotControl.cshtml.cs
├── ViewModels/
│   ├── Pages/
│   │   └── BotControlViewModel.cs
│   └── Components/
│       └── TypedConfirmationModalViewModel.cs
├── wwwroot/
│   └── js/
│       └── bot-control.js
└── Pages/Shared/Components/
    └── _TypedConfirmationModal.cshtml
```

### 6.3 BotControl Page Specification

**Files:**
- `src/DiscordBot.Bot/Pages/Admin/BotControl.cshtml`
- `src/DiscordBot.Bot/Pages/Admin/BotControl.cshtml.cs`

**Page Layout:**

```
+------------------------------------------------------------------+
| Bot Control Panel                                                 |
| Manage your bot's lifecycle and view configuration               |
+------------------------------------------------------------------+

+----------------------------------+  +----------------------------------+
| Bot Status                       |  | Actions                          |
| [Status Card with real-time      |  | [Restart Bot] (warning button)   |
|  updates - polls every 5 sec]    |  | [Shutdown Bot] (danger button)   |
|  - Connection State              |  |                                  |
|  - Uptime                        |  | Note: Restart will briefly       |
|  - Latency                       |  | disconnect the bot from all      |
|  - Guild Count                   |  | servers.                         |
+----------------------------------+  +----------------------------------+

+------------------------------------------------------------------+
| Configuration                                                     |
+------------------------------------------------------------------+
| Token              | ••••••••••••••••••••abcd                     |
| Test Guild ID      | 1234567890123456789 (or "Not configured")    |
| OAuth Client ID    | ••••••••••••1234                             |
| Database Provider  | SQLite                                       |
| Discord.NET        | 3.18.0                                       |
| App Version        | 2.1.0                                        |
| .NET Runtime       | 8.0.0                                        |
+------------------------------------------------------------------+

+------------------------------------------------------------------+
| Danger Zone                                                       |
+------------------------------------------------------------------+
| [!] Shutdown Bot                                                  |
| Shutting down the bot will stop all functionality. The bot will  |
| need to be manually restarted from the server.                   |
|                                                [Shutdown Bot]     |
+------------------------------------------------------------------+
```

**PageModel Implementation:**

```csharp
namespace DiscordBot.Bot.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class BotControlModel : PageModel
{
    private readonly IBotService _botService;
    private readonly ILogger<BotControlModel> _logger;

    public BotControlViewModel ViewModel { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        LoadViewModel();
        return Page();
    }

    public async Task<IActionResult> OnPostRestartBotAsync()
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
        {
            _logger.LogWarning("Non-admin user {UserId} attempted to restart bot", User.Identity?.Name);
            return Forbid();
        }

        _logger.LogWarning("Bot restart requested by user {UserId}", User.Identity?.Name);

        try
        {
            await _botService.RestartAsync();
            return new JsonResult(new { success = true, message = "Bot is restarting..." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart bot");
            return new JsonResult(new { success = false, message = "Failed to restart bot." })
            {
                StatusCode = 500
            };
        }
    }

    public async Task<IActionResult> OnPostShutdownBotAsync()
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
        {
            _logger.LogWarning("Non-admin user {UserId} attempted to shutdown bot", User.Identity?.Name);
            return Forbid();
        }

        _logger.LogCritical("Bot SHUTDOWN requested by user {UserId}", User.Identity?.Name);

        try
        {
            await _botService.ShutdownAsync();
            return new JsonResult(new { success = true, message = "Bot is shutting down..." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to shutdown bot");
            return new JsonResult(new { success = false, message = "Failed to shutdown bot." })
            {
                StatusCode = 500
            };
        }
    }

    private void LoadViewModel()
    {
        var status = _botService.GetStatus();
        var config = _botService.GetConfiguration();

        ViewModel = new BotControlViewModel
        {
            Status = BotStatusViewModel.FromDto(status),
            Configuration = config,
            CanRestart = true,
            CanShutdown = true
        };
    }
}
```

---

## 7. Shared Components

### 7.1 Typed Confirmation Modal

**Location:** `src/DiscordBot.Bot/Pages/Shared/Components/_TypedConfirmationModal.cshtml`

This component extends the existing `_ConfirmationModal` pattern with typed confirmation:

- Text input field
- Validation that disables button until exact text is entered
- JavaScript for real-time validation
- Same visual styling as existing modal

**Key Features:**
- Input field with placeholder showing required text
- Confirm button disabled by default
- Button enables only when input matches required text exactly
- Form submits via AJAX like existing quick actions

---

## 8. JavaScript Implementation

### 8.1 Bot Control Module

**Location:** `src/DiscordBot.Bot/wwwroot/js/bot-control.js`

```javascript
// Bot Control Panel Module
// Handles typed confirmation, status polling, and action submissions

(function() {
    'use strict';

    // Configuration
    const STATUS_POLL_INTERVAL_MS = 5000; // 5 seconds for control panel
    const API_ENDPOINT = '/api/bot/status';

    /**
     * Initialize typed confirmation modal functionality
     */
    function initTypedConfirmation(modalId, inputId, confirmBtnId, requiredText) {
        const input = document.getElementById(inputId);
        const confirmBtn = document.getElementById(confirmBtnId);

        if (!input || !confirmBtn) return;

        input.addEventListener('input', function() {
            confirmBtn.disabled = input.value !== requiredText;
        });

        // Reset on modal close
        const modal = document.getElementById(modalId);
        if (modal) {
            const observer = new MutationObserver(function(mutations) {
                mutations.forEach(function(mutation) {
                    if (mutation.attributeName === 'class' && modal.classList.contains('hidden')) {
                        input.value = '';
                        confirmBtn.disabled = true;
                    }
                });
            });
            observer.observe(modal, { attributes: true });
        }
    }

    /**
     * Submit shutdown action via AJAX
     */
    async function submitShutdown(handler) {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (!token) {
            window.quickActions?.showToast('Security token not found. Please refresh.', 'error');
            return;
        }

        try {
            const response = await fetch(`?handler=${handler}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                    'RequestVerificationToken': token
                },
                body: `__RequestVerificationToken=${encodeURIComponent(token)}`
            });

            const data = await response.json();

            if (response.ok && data.success) {
                window.quickActions?.showToast(data.message, 'success');
                // Close modal
                window.quickActions?.hideConfirmationModal('shutdownModal');
            } else {
                window.quickActions?.showToast(data.message || 'Action failed.', 'error');
            }
        } catch (error) {
            console.error('Shutdown error:', error);
            window.quickActions?.showToast('An error occurred.', 'error');
        }
    }

    /**
     * Enhanced status polling for control panel (faster interval)
     */
    async function refreshStatus() {
        const statusContainer = document.querySelector('[data-bot-control-status]');
        if (!statusContainer) return;

        try {
            const response = await fetch(API_ENDPOINT);
            if (!response.ok) throw new Error('Status fetch failed');

            const data = await response.json();

            // Update status elements
            updateStatusElement('[data-connection-state]', data.connectionState);
            updateStatusElement('[data-latency]', data.latencyMs + 'ms');
            updateStatusElement('[data-guild-count]', data.guildCount);
            updateStatusElement('[data-uptime]', formatUptime(data.uptime));
            updateStatusIndicator(data.connectionState);

        } catch (error) {
            console.error('Status refresh failed:', error);
        }
    }

    function updateStatusElement(selector, value) {
        const el = document.querySelector(selector);
        if (el) el.textContent = value;
    }

    function updateStatusIndicator(state) {
        const indicator = document.querySelector('[data-status-indicator]');
        if (!indicator) return;

        const isOnline = state.toUpperCase() === 'CONNECTED';
        indicator.classList.toggle('bg-success', isOnline);
        indicator.classList.toggle('bg-error', !isOnline);
    }

    function formatUptime(timeSpanString) {
        // Parse TimeSpan format and return human-readable string
        // (Reuse logic from bot-status-refresh.js)
        // ... implementation ...
        return timeSpanString; // Placeholder
    }

    // Initialize on DOM ready
    function init() {
        // Init typed confirmation for shutdown
        initTypedConfirmation('shutdownModal', 'shutdownConfirmInput', 'shutdownConfirmBtn', 'SHUTDOWN');

        // Start enhanced status polling
        const statusContainer = document.querySelector('[data-bot-control-status]');
        if (statusContainer) {
            refreshStatus();
            setInterval(refreshStatus, STATUS_POLL_INTERVAL_MS);
        }
    }

    // Expose public API
    window.botControl = {
        initTypedConfirmation,
        submitShutdown,
        refreshStatus
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
```

---

## 9. Sidebar Navigation Update

Add Bot Control link to `_Sidebar.cshtml` under Administration section:

```razor
<!-- Bot Control - Admin only -->
<a asp-page="/Admin/BotControl" class="sidebar-link @(ViewContext.RouteData.Values["page"]?.ToString() == "/Admin/BotControl" ? "active" : "")">
    <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
    </svg>
    Bot Control
</a>
```

---

## 10. Implementation Sequence

### Phase 1: Foundation (Tasks 6.1.1)

1. Create `BotConfigurationDto.cs` in Core DTOs
2. Extend `IBotService` interface with `GetConfiguration()`
3. Implement `GetConfiguration()` in `BotService`
4. Update `RestartAsync()` to perform soft restart (disconnect/reconnect)
5. Create `BotControlViewModel.cs`

### Phase 2: Components (Task 6.1.2, 6.1.3)

6. Create `TypedConfirmationModalViewModel.cs`
7. Create `_TypedConfirmationModal.cshtml` component
8. Create `bot-control.js` JavaScript module

### Phase 3: Page Implementation (Tasks 6.1.1 - 6.1.5)

9. Create `Pages/Admin/BotControl.cshtml.cs` with handlers
10. Create `Pages/Admin/BotControl.cshtml` with layout
11. Add restart confirmation modal (simple)
12. Add shutdown confirmation modal (typed)
13. Add configuration display section
14. Add real-time status polling

### Phase 4: Navigation and Polish (Task 6.1.6)

15. Update `_Sidebar.cshtml` with Bot Control link
16. Add logging for administrative actions
17. Test all functionality

---

## 11. Subagent Task Plan

### 11.1 design-specialist

Not required - uses existing design system components and patterns from prototypes.

### 11.2 html-prototyper

Not required - page can be built directly using existing Razor components and prototype patterns.

### 11.3 dotnet-specialist

**Primary implementer for all tasks:**

| Task | Description | Estimated Effort |
|------|-------------|------------------|
| 6.1.1-a | Create `BotConfigurationDto.cs` | 30 min |
| 6.1.1-b | Extend `IBotService` interface | 15 min |
| 6.1.1-c | Implement `GetConfiguration()` in `BotService` | 45 min |
| 6.1.1-d | Update `RestartAsync()` for soft restart | 30 min |
| 6.1.1-e | Create `BotControlViewModel.cs` | 30 min |
| 6.1.2 | Create `TypedConfirmationModalViewModel.cs` | 20 min |
| 6.1.2 | Create `_TypedConfirmationModal.cshtml` component | 45 min |
| 6.1.2 | Implement restart with simple confirmation | 30 min |
| 6.1.3 | Implement shutdown with typed confirmation ("SHUTDOWN") | 45 min |
| 6.1.4 | Create configuration display section | 30 min |
| 6.1.5 | Create `bot-control.js` with status polling | 1 hour |
| 6.1.5 | Wire up real-time status updates | 30 min |
| 6.1.6 | Add logging for administrative actions | 30 min |
| 6.1.1 | Create `BotControl.cshtml` page | 1.5 hours |
| 6.1.1 | Create `BotControl.cshtml.cs` PageModel | 1 hour |
| - | Update `_Sidebar.cshtml` navigation | 15 min |

**Total Estimated Effort:** ~9 hours

### 11.4 docs-writer

| Task | Description | Estimated Effort |
|------|-------------|------------------|
| - | Document Bot Control Panel in admin guide | 1 hour |

---

## 12. Timeline / Dependency Map

```
Phase 1: Foundation (Day 1, Morning)
├── BotConfigurationDto (no dependencies)
├── IBotService extension (no dependencies)
├── BotService updates (depends on DTO and interface)
└── BotControlViewModel (depends on DTO)

Phase 2: Components (Day 1, Afternoon)
├── TypedConfirmationModalViewModel (no dependencies)
├── _TypedConfirmationModal.cshtml (depends on ViewModel)
└── bot-control.js (no dependencies)

Phase 3: Page Implementation (Day 2)
├── BotControl.cshtml.cs (depends on service, viewmodels)
├── BotControl.cshtml (depends on PageModel, components)
└── Wire up modals and polling (depends on page)

Phase 4: Integration (Day 2, Afternoon)
├── Update sidebar navigation
├── Add logging
└── Testing and polish
```

**Parallelization Opportunities:**
- DTOs and ViewModels can be created simultaneously
- JavaScript can be developed in parallel with Razor components
- Documentation can begin once page is functional

---

## 13. Acceptance Criteria

### 13.1 Page Access (Task 6.1.1)

- [ ] Bot Control Panel page exists at `/Admin/BotControl`
- [ ] Page requires Admin or SuperAdmin role
- [ ] Non-authenticated users redirected to login
- [ ] Non-admin users receive 403 Forbidden
- [ ] Page is accessible from sidebar under "Administration" section

### 13.2 Restart Functionality (Task 6.1.2)

- [ ] "Restart Bot" button is visible on the page
- [ ] Clicking button shows confirmation modal
- [ ] Confirmation modal has Warning variant styling
- [ ] Clicking "Cancel" closes modal without action
- [ ] Clicking "Restart Bot" initiates restart
- [ ] Success toast displayed after restart initiated
- [ ] Error toast displayed if restart fails
- [ ] Action is logged with user ID and timestamp

### 13.3 Shutdown Functionality (Task 6.1.3)

- [ ] "Shutdown Bot" button is in "Danger Zone" section
- [ ] Clicking button shows typed confirmation modal
- [ ] Modal shows Danger variant styling (red)
- [ ] Text input field is present with label
- [ ] Confirm button is disabled by default
- [ ] Typing anything other than "SHUTDOWN" keeps button disabled
- [ ] Typing exactly "SHUTDOWN" enables confirm button
- [ ] Clicking "Cancel" closes modal and resets input
- [ ] Clicking "Shutdown Bot" when enabled initiates shutdown
- [ ] Success toast displayed after shutdown initiated
- [ ] Action is logged at CRITICAL level with user ID

### 13.4 Configuration Display (Task 6.1.4)

- [ ] Configuration section shows read-only values
- [ ] Bot token is masked (shows dots + last 4 characters)
- [ ] OAuth Client ID is masked (if configured)
- [ ] Test Guild ID is displayed (or "Not configured")
- [ ] Database provider is displayed
- [ ] Discord.NET version is displayed
- [ ] Application version is displayed
- [ ] .NET runtime version is displayed

### 13.5 Real-time Status (Task 6.1.5)

- [ ] Status section shows connection state
- [ ] Status section shows uptime
- [ ] Status section shows latency
- [ ] Status section shows guild count
- [ ] Status updates automatically every 5 seconds
- [ ] Visual indicator reflects connection state (green/red)

### 13.6 Logging (Task 6.1.6)

- [ ] Restart attempts are logged at Warning level
- [ ] Successful restarts are logged at Information level
- [ ] Failed restarts are logged at Error level
- [ ] Shutdown attempts are logged at Critical level
- [ ] Logs include user ID of the actor
- [ ] Logs include timestamp

---

## 14. Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Restart fails to reconnect | Medium | High | Implement timeout and error handling; consider external process manager |
| Shutdown leaves bot unrecoverable | Low | Critical | Typed confirmation; clear warning messages; require external restart |
| Status polling overloads API | Low | Medium | 5-second interval is reasonable; use caching if needed |
| Token exposure in configuration | Low | Critical | Mask to show only last 4 characters |
| Unauthorized access bypass | Low | High | Server-side role verification in handlers |
| AJAX failure on slow networks | Medium | Low | Show loading states; timeout handling; retry logic |

---

## 15. File Summary

### New Files to Create

**Core Layer (`src/DiscordBot.Core/`):**
- `DTOs/BotConfigurationDto.cs`

**Bot Layer (`src/DiscordBot.Bot/`):**
- `ViewModels/Pages/BotControlViewModel.cs`
- `ViewModels/Components/TypedConfirmationModalViewModel.cs`
- `Pages/Admin/BotControl.cshtml`
- `Pages/Admin/BotControl.cshtml.cs`
- `Pages/Shared/Components/_TypedConfirmationModal.cshtml`
- `wwwroot/js/bot-control.js`

### Files to Modify

- `src/DiscordBot.Core/Interfaces/IBotService.cs` - Add `GetConfiguration()` method
- `src/DiscordBot.Bot/Services/BotService.cs` - Implement `GetConfiguration()`, update `RestartAsync()`
- `src/DiscordBot.Bot/Pages/Shared/_Sidebar.cshtml` - Add Bot Control navigation link

---

## 16. Testing Considerations

### Unit Tests

- `BotService.GetConfiguration()` masks tokens correctly
- `BotService.RestartAsync()` calls disconnect and reconnect in order
- `BotControlViewModel` correctly maps from DTOs

### Integration Tests

- Page requires authentication
- Page requires Admin role
- Restart handler returns correct JSON response
- Shutdown handler returns correct JSON response
- Configuration display does not expose full tokens

### Manual Testing

- Restart confirmation modal opens and closes correctly
- Shutdown typed confirmation validates input correctly
- Status updates in real-time
- Toast notifications appear for success/error
- Navigation link highlights when on page
- Mobile responsive layout works correctly

---

## 17. Prototype Reference

The typed confirmation modal pattern is demonstrated in:
- `docs/prototypes/feedback-confirmation.html` - Section 3: Typed Confirmation

The danger zone styling pattern is demonstrated in:
- `docs/prototypes/pages/settings.html` - `.danger-zone` CSS class

---

*Document prepared by: Systems Architect Agent*
*Review status: Ready for implementation*
