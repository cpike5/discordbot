# Issue #76: Guild Settings Edit Page - Implementation Plan

**Version:** 1.0
**Created:** 2025-12-23
**Status:** Planning
**Target Framework:** ASP.NET Core Razor Pages (.NET 8)
**Scope:** Medium

---

## 1. Requirement Summary

Create a Guild Settings Edit Page that allows administrators to modify guild-specific bot settings. The page must include:

- An editable form displaying current guild settings
- Editable fields: Command prefix, notification toggles, channel selections, auto-moderation settings
- Server-side and client-side form validation
- Save and Cancel functionality
- Success/error messaging via TempData
- Authorization restricted to Admin role or higher

### Acceptance Criteria

1. Form displays current settings pre-populated from database
2. Editable fields include:
   - Command prefix (text input, 1-3 characters)
   - Welcome messages toggle
   - Leave messages toggle
   - Moderation alerts toggle
   - Command logging toggle
   - Welcome channel (text input/dropdown)
   - Log channel (text input/dropdown)
   - Auto-moderation enabled toggle
3. Save button submits changes and calls `IGuildService.UpdateGuildAsync()`
4. Cancel button returns to the guild detail page without saving
5. Validation errors displayed inline with field highlighting
6. Success message shown after save (via TempData)
7. Error message shown on service failure (via TempData or inline)

---

## 2. Architectural Considerations

### 2.1 Existing System Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `GuildDto` | `src/DiscordBot.Core/DTOs/GuildDto.cs` | Read model with Id, Name, Prefix, Settings (JSON), IsActive |
| `GuildUpdateRequestDto` | `src/DiscordBot.Core/DTOs/GuildUpdateRequestDto.cs` | Update model with Prefix, Settings (JSON), IsActive |
| `IGuildService` | `src/DiscordBot.Core/Interfaces/IGuildService.cs` | Service interface with `UpdateGuildAsync()` method |
| `GuildService` | `src/DiscordBot.Bot/Services/GuildService.cs` | Implementation that updates via `IGuildRepository` |
| `GuildDetailViewModel` | `src/DiscordBot.Bot/ViewModels/Pages/GuildDetailViewModel.cs` | Existing display model with `GuildSettingsViewModel` |
| `GuildSettingsViewModel` | `src/DiscordBot.Bot/ViewModels/Pages/GuildDetailViewModel.cs` | Nested record for parsed settings (WelcomeChannel, LogChannel, AutoModEnabled) |
| `Details.cshtml` | `src/DiscordBot.Bot/Pages/Guilds/Details.cshtml` | Existing detail page with Edit button link |
| Shared Form Components | `src/DiscordBot.Bot/Pages/Shared/Components/` | `_FormInput.cshtml`, `_FormSelect.cshtml`, `_Alert.cshtml` |

### 2.2 Design System References

The UI must follow the design system in `docs/articles/design-system.md`:

| Feature | Design System Section | Notes |
|---------|----------------------|-------|
| Form Inputs | Section 4: Form Inputs | Text inputs, validation states |
| Toggle Switches | Section 4: Toggle Switch | For boolean settings |
| Buttons | Section 4: Buttons | Primary (Save), Secondary (Cancel) |
| Cards | Section 4: Cards and Panels | Form sections |
| Alerts | Status Indicators & Badges | Success/error messages |
| Color Palette | Section 1: Color Palette | Semantic colors for validation |

### 2.3 Prototype Reference

The Settings tab in `docs/prototypes/pages/server-detail.html` (lines 936-1093) provides the visual reference:

**General Settings Section:**
- Command Prefix (text input, maxlength=3)
- Bot Nickname (future, not in MVP)
- Language (future, not in MVP)

**Notifications Section:**
- Welcome Messages (toggle)
- Leave Messages (toggle)
- Moderation Alerts (toggle)
- Command Logging (toggle)

**Advanced Settings Section:**
- Log Channel (text input or dropdown)
- Welcome Channel (text input or dropdown)
- Auto-Moderation Level (dropdown or toggle for MVP)

### 2.4 Data Model: Settings JSON Structure

The `Settings` field in `Guild` entity stores JSON. The expanded structure for this feature:

```json
{
  "welcomeChannel": "general",
  "logChannel": "bot-logs",
  "autoModEnabled": true,
  "welcomeMessagesEnabled": true,
  "leaveMessagesEnabled": false,
  "moderationAlertsEnabled": true,
  "commandLoggingEnabled": true
}
```

### 2.5 Authorization Requirements

- Page requires `[Authorize(Policy = "RequireAdmin")]`
- Admin, SuperAdmin roles can edit guild settings
- Future: Guild-specific access control via `GuildAccessRequirement`

### 2.6 Validation Rules

| Field | Type | Validation |
|-------|------|------------|
| Prefix | string? | MaxLength(3), optional |
| WelcomeChannel | string? | MaxLength(100), optional |
| LogChannel | string? | MaxLength(100), optional |
| AutoModEnabled | bool | Required |
| WelcomeMessagesEnabled | bool | Required |
| LeaveMessagesEnabled | bool | Required |
| ModerationAlertsEnabled | bool | Required |
| CommandLoggingEnabled | bool | Required |
| IsActive | bool | Required |

---

## 3. Files to Create/Modify

### 3.1 New Files

| File | Purpose |
|------|---------|
| `src/DiscordBot.Bot/Pages/Guilds/Edit.cshtml` | Razor Page view with edit form |
| `src/DiscordBot.Bot/Pages/Guilds/Edit.cshtml.cs` | Page model with OnGet/OnPost handlers |
| `src/DiscordBot.Bot/ViewModels/Pages/GuildEditViewModel.cs` | View model for form binding and display |

### 3.2 Modified Files

| File | Changes |
|------|---------|
| `src/DiscordBot.Bot/ViewModels/Pages/GuildDetailViewModel.cs` | Extend `GuildSettingsViewModel` with additional properties |

---

## 4. Detailed Implementation Specifications

### 4.1 GuildEditViewModel

**File:** `src/DiscordBot.Bot/ViewModels/Pages/GuildEditViewModel.cs`

```csharp
using System.Text.Json;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the guild settings edit form.
/// </summary>
public class GuildEditViewModel
{
    /// <summary>
    /// Gets or sets the guild's Discord snowflake ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// Gets or sets the guild name (display only).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the guild icon URL (display only).
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Gets or sets whether the guild is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the custom command prefix (1-3 characters).
    /// </summary>
    public string? Prefix { get; set; }

    // Notification Settings

    /// <summary>
    /// Gets or sets whether welcome messages are enabled.
    /// </summary>
    public bool WelcomeMessagesEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether leave messages are enabled.
    /// </summary>
    public bool LeaveMessagesEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether moderation alerts are enabled.
    /// </summary>
    public bool ModerationAlertsEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether command logging is enabled.
    /// </summary>
    public bool CommandLoggingEnabled { get; set; }

    // Advanced Settings

    /// <summary>
    /// Gets or sets the welcome channel name or ID.
    /// </summary>
    public string? WelcomeChannel { get; set; }

    /// <summary>
    /// Gets or sets the log channel name or ID.
    /// </summary>
    public string? LogChannel { get; set; }

    /// <summary>
    /// Gets or sets whether auto-moderation is enabled.
    /// </summary>
    public bool AutoModEnabled { get; set; }

    /// <summary>
    /// Creates a GuildEditViewModel from a GuildDto.
    /// </summary>
    public static GuildEditViewModel FromDto(GuildDto dto)
    {
        var settings = ParseSettings(dto.Settings);

        return new GuildEditViewModel
        {
            Id = dto.Id,
            Name = dto.Name,
            IconUrl = dto.IconUrl,
            IsActive = dto.IsActive,
            Prefix = dto.Prefix,
            WelcomeMessagesEnabled = settings.WelcomeMessagesEnabled,
            LeaveMessagesEnabled = settings.LeaveMessagesEnabled,
            ModerationAlertsEnabled = settings.ModerationAlertsEnabled,
            CommandLoggingEnabled = settings.CommandLoggingEnabled,
            WelcomeChannel = settings.WelcomeChannel,
            LogChannel = settings.LogChannel,
            AutoModEnabled = settings.AutoModEnabled
        };
    }

    /// <summary>
    /// Serializes the settings properties to JSON for storage.
    /// </summary>
    public string ToSettingsJson()
    {
        var settings = new GuildSettingsData
        {
            WelcomeChannel = WelcomeChannel,
            LogChannel = LogChannel,
            AutoModEnabled = AutoModEnabled,
            WelcomeMessagesEnabled = WelcomeMessagesEnabled,
            LeaveMessagesEnabled = LeaveMessagesEnabled,
            ModerationAlertsEnabled = ModerationAlertsEnabled,
            CommandLoggingEnabled = CommandLoggingEnabled
        };

        return JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static GuildSettingsData ParseSettings(string? settingsJson)
    {
        if (string.IsNullOrEmpty(settingsJson))
            return new GuildSettingsData();

        try
        {
            return JsonSerializer.Deserialize<GuildSettingsData>(settingsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new GuildSettingsData();
        }
        catch (JsonException)
        {
            return new GuildSettingsData();
        }
    }

    /// <summary>
    /// Internal data structure for JSON serialization.
    /// </summary>
    private class GuildSettingsData
    {
        public string? WelcomeChannel { get; set; }
        public string? LogChannel { get; set; }
        public bool AutoModEnabled { get; set; }
        public bool WelcomeMessagesEnabled { get; set; }
        public bool LeaveMessagesEnabled { get; set; }
        public bool ModerationAlertsEnabled { get; set; }
        public bool CommandLoggingEnabled { get; set; }
    }
}
```

### 4.2 Edit.cshtml.cs (Page Model)

**File:** `src/DiscordBot.Bot/Pages/Guilds/Edit.cshtml.cs`

```csharp
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Bot.Pages.Guilds;

/// <summary>
/// Page model for editing guild settings.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class EditModel : PageModel
{
    private readonly IGuildService _guildService;
    private readonly ILogger<EditModel> _logger;

    public EditModel(
        IGuildService guildService,
        ILogger<EditModel> logger)
    {
        _guildService = guildService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>
    /// View model for display-only properties (name, icon).
    /// </summary>
    public GuildEditViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// Error message to display on the page.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Success message from TempData.
    /// </summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// Input model for form binding with validation attributes.
    /// </summary>
    public class InputModel
    {
        public ulong GuildId { get; set; }

        [StringLength(3, ErrorMessage = "Prefix cannot exceed 3 characters")]
        [Display(Name = "Command Prefix")]
        public string? Prefix { get; set; }

        [Display(Name = "Bot Active")]
        public bool IsActive { get; set; } = true;

        // Notification Settings

        [Display(Name = "Welcome Messages")]
        public bool WelcomeMessagesEnabled { get; set; }

        [Display(Name = "Leave Messages")]
        public bool LeaveMessagesEnabled { get; set; }

        [Display(Name = "Moderation Alerts")]
        public bool ModerationAlertsEnabled { get; set; }

        [Display(Name = "Command Logging")]
        public bool CommandLoggingEnabled { get; set; }

        // Advanced Settings

        [StringLength(100, ErrorMessage = "Channel name cannot exceed 100 characters")]
        [Display(Name = "Welcome Channel")]
        public string? WelcomeChannel { get; set; }

        [StringLength(100, ErrorMessage = "Channel name cannot exceed 100 characters")]
        [Display(Name = "Log Channel")]
        public string? LogChannel { get; set; }

        [Display(Name = "Auto-Moderation")]
        public bool AutoModEnabled { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(ulong id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing guild edit page for guild {GuildId}", id);

        var guild = await _guildService.GetGuildByIdAsync(id, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", id);
            return NotFound();
        }

        // Populate display view model
        ViewModel = GuildEditViewModel.FromDto(guild);

        // Populate form input model
        Input = new InputModel
        {
            GuildId = guild.Id,
            Prefix = ViewModel.Prefix,
            IsActive = ViewModel.IsActive,
            WelcomeMessagesEnabled = ViewModel.WelcomeMessagesEnabled,
            LeaveMessagesEnabled = ViewModel.LeaveMessagesEnabled,
            ModerationAlertsEnabled = ViewModel.ModerationAlertsEnabled,
            CommandLoggingEnabled = ViewModel.CommandLoggingEnabled,
            WelcomeChannel = ViewModel.WelcomeChannel,
            LogChannel = ViewModel.LogChannel,
            AutoModEnabled = ViewModel.AutoModEnabled
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("User submitting guild edit for guild {GuildId}", Input.GuildId);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState is invalid for guild {GuildId}. Errors: {Errors}",
                Input.GuildId,
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            await LoadViewModelAsync(Input.GuildId, cancellationToken);
            return Page();
        }

        // Build the settings JSON from input
        var settingsJson = BuildSettingsJson();

        // Create the update request
        var updateRequest = new GuildUpdateRequestDto
        {
            Prefix = Input.Prefix,
            Settings = settingsJson,
            IsActive = Input.IsActive
        };

        var result = await _guildService.UpdateGuildAsync(Input.GuildId, updateRequest, cancellationToken);

        if (result == null)
        {
            _logger.LogWarning("Failed to update guild {GuildId} - guild not found", Input.GuildId);
            ErrorMessage = "Guild not found. It may have been removed.";
            await LoadViewModelAsync(Input.GuildId, cancellationToken);
            return Page();
        }

        _logger.LogInformation("Successfully updated guild {GuildId}", Input.GuildId);
        SuccessMessage = "Guild settings saved successfully.";

        return RedirectToPage("Details", new { id = Input.GuildId });
    }

    private string BuildSettingsJson()
    {
        var viewModel = new GuildEditViewModel
        {
            WelcomeMessagesEnabled = Input.WelcomeMessagesEnabled,
            LeaveMessagesEnabled = Input.LeaveMessagesEnabled,
            ModerationAlertsEnabled = Input.ModerationAlertsEnabled,
            CommandLoggingEnabled = Input.CommandLoggingEnabled,
            WelcomeChannel = Input.WelcomeChannel,
            LogChannel = Input.LogChannel,
            AutoModEnabled = Input.AutoModEnabled
        };

        return viewModel.ToSettingsJson();
    }

    private async Task LoadViewModelAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild != null)
        {
            ViewModel = GuildEditViewModel.FromDto(guild);
            // Preserve form input values for redisplay
            ViewModel = ViewModel with
            {
                Prefix = Input.Prefix,
                IsActive = Input.IsActive,
                WelcomeMessagesEnabled = Input.WelcomeMessagesEnabled,
                LeaveMessagesEnabled = Input.LeaveMessagesEnabled,
                ModerationAlertsEnabled = Input.ModerationAlertsEnabled,
                CommandLoggingEnabled = Input.CommandLoggingEnabled,
                WelcomeChannel = Input.WelcomeChannel,
                LogChannel = Input.LogChannel,
                AutoModEnabled = Input.AutoModEnabled
            };
        }
    }
}
```

### 4.3 Edit.cshtml (Razor View)

**File:** `src/DiscordBot.Bot/Pages/Guilds/Edit.cshtml`

```html
@page "{id:long}"
@model DiscordBot.Bot.Pages.Guilds.EditModel
@using DiscordBot.Bot.ViewModels.Components
@{
    ViewData["Title"] = $"Edit {Model.ViewModel.Name}";
}

<div class="max-w-3xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
    <!-- Breadcrumb Navigation -->
    <nav aria-label="Breadcrumb" class="mb-6">
        <ol class="flex items-center gap-2 text-sm">
            <li>
                <a asp-page="/Index" class="text-text-secondary hover:text-accent-blue transition-colors">Home</a>
            </li>
            <li class="text-text-tertiary">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
                </svg>
            </li>
            <li>
                <a asp-page="Index" class="text-text-secondary hover:text-accent-blue transition-colors">Servers</a>
            </li>
            <li class="text-text-tertiary">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
                </svg>
            </li>
            <li>
                <a asp-page="Details" asp-route-id="@Model.ViewModel.Id" class="text-text-secondary hover:text-accent-blue transition-colors">@Model.ViewModel.Name</a>
            </li>
            <li class="text-text-tertiary">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
                </svg>
            </li>
            <li>
                <span class="text-text-primary font-medium">Edit Settings</span>
            </li>
        </ol>
    </nav>

    <!-- Page Header -->
    <div class="mb-8">
        <div class="flex items-center gap-4">
            @if (!string.IsNullOrEmpty(Model.ViewModel.IconUrl))
            {
                <img src="@Model.ViewModel.IconUrl" alt="@Model.ViewModel.Name" class="w-12 h-12 rounded-full flex-shrink-0" />
            }
            else
            {
                <div class="w-12 h-12 rounded-full bg-gradient-to-br from-accent-blue to-accent-orange flex items-center justify-center text-white font-bold text-lg flex-shrink-0">
                    @(Model.ViewModel.Name.Length >= 2 ? Model.ViewModel.Name[..2].ToUpper() : Model.ViewModel.Name.ToUpper())
                </div>
            }
            <div>
                <h1 class="text-2xl font-bold text-text-primary">Edit Settings</h1>
                <p class="mt-1 text-sm text-text-secondary">Configure bot settings for @Model.ViewModel.Name</p>
            </div>
        </div>
    </div>

    <!-- Success Message -->
    @if (!string.IsNullOrEmpty(Model.SuccessMessage))
    {
        <div class="mb-6">
            <partial name="Shared/Components/_Alert" model="new AlertViewModel {
                Variant = AlertVariant.Success,
                Message = Model.SuccessMessage,
                IsDismissible = true
            }" />
        </div>
    }

    <!-- Error Message -->
    @if (!string.IsNullOrEmpty(Model.ErrorMessage))
    {
        <div class="mb-6">
            <partial name="Shared/Components/_Alert" model="new AlertViewModel {
                Variant = AlertVariant.Error,
                Message = Model.ErrorMessage,
                IsDismissible = true
            }" />
        </div>
    }

    <form method="post">
        <input type="hidden" name="Input.GuildId" value="@Model.Input.GuildId" />
        <div asp-validation-summary="ModelOnly" class="text-error text-sm mb-4"></div>

        <!-- General Settings Section -->
        <div class="bg-bg-secondary border border-border-primary rounded-lg mb-6">
            <div class="px-6 py-4 border-b border-border-primary">
                <h2 class="text-lg font-semibold text-text-primary">General Settings</h2>
            </div>
            <div class="p-6 space-y-6">
                <!-- Command Prefix -->
                <div>
                    @{
                        var prefixValidation = !ViewData.ModelState.IsValid && ViewData.ModelState["Input.Prefix"]?.Errors.Count > 0
                            ? ValidationState.Error
                            : ValidationState.None;
                        var prefixError = ViewData.ModelState["Input.Prefix"]?.Errors.FirstOrDefault()?.ErrorMessage;
                        var prefixModel = new FormInputViewModel {
                            Id = "Input_Prefix",
                            Name = "Input.Prefix",
                            Label = "Command Prefix",
                            Type = "text",
                            Value = Model.Input.Prefix,
                            MaxLength = 3,
                            Placeholder = "e.g., ! or ?",
                            HelpText = "Custom prefix for text commands (1-3 characters). Leave empty for default.",
                            ValidationState = prefixValidation,
                            ValidationMessage = prefixError
                        };
                    }
                    <partial name="Shared/Components/_FormInput" model="prefixModel" />
                </div>

                <!-- Is Active Toggle -->
                <div class="flex items-center justify-between py-2">
                    <div class="flex-1 pr-4">
                        <label for="Input_IsActive" class="text-sm font-medium text-text-primary">Bot Active</label>
                        <p class="text-xs text-text-tertiary mt-0.5">Enable or disable the bot for this server</p>
                    </div>
                    <label class="relative inline-flex items-center cursor-pointer">
                        <input type="hidden" name="Input.IsActive" value="false" />
                        <input type="checkbox"
                               id="Input_IsActive"
                               name="Input.IsActive"
                               value="true"
                               checked="@Model.Input.IsActive"
                               class="sr-only peer" />
                        <div class="w-11 h-6 bg-bg-tertiary peer-focus:outline-none peer-focus:ring-2 peer-focus:ring-accent-blue/50 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-text-primary after:border-border-primary after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-accent-orange"></div>
                    </label>
                </div>
            </div>
        </div>

        <!-- Notification Settings Section -->
        <div class="bg-bg-secondary border border-border-primary rounded-lg mb-6">
            <div class="px-6 py-4 border-b border-border-primary">
                <h2 class="text-lg font-semibold text-text-primary">Notifications</h2>
            </div>
            <div class="divide-y divide-border-secondary">
                <!-- Welcome Messages -->
                <div class="p-4 flex items-center justify-between">
                    <div class="flex-1 pr-4">
                        <label for="Input_WelcomeMessagesEnabled" class="text-sm font-medium text-text-primary">Welcome Messages</label>
                        <p class="text-xs text-text-tertiary mt-0.5">Send a message when new members join</p>
                    </div>
                    <label class="relative inline-flex items-center cursor-pointer">
                        <input type="hidden" name="Input.WelcomeMessagesEnabled" value="false" />
                        <input type="checkbox"
                               id="Input_WelcomeMessagesEnabled"
                               name="Input.WelcomeMessagesEnabled"
                               value="true"
                               checked="@Model.Input.WelcomeMessagesEnabled"
                               class="sr-only peer" />
                        <div class="w-11 h-6 bg-bg-tertiary peer-focus:outline-none peer-focus:ring-2 peer-focus:ring-accent-blue/50 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-text-primary after:border-border-primary after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-accent-orange"></div>
                    </label>
                </div>

                <!-- Leave Messages -->
                <div class="p-4 flex items-center justify-between">
                    <div class="flex-1 pr-4">
                        <label for="Input_LeaveMessagesEnabled" class="text-sm font-medium text-text-primary">Leave Messages</label>
                        <p class="text-xs text-text-tertiary mt-0.5">Announce when members leave the server</p>
                    </div>
                    <label class="relative inline-flex items-center cursor-pointer">
                        <input type="hidden" name="Input.LeaveMessagesEnabled" value="false" />
                        <input type="checkbox"
                               id="Input_LeaveMessagesEnabled"
                               name="Input.LeaveMessagesEnabled"
                               value="true"
                               checked="@Model.Input.LeaveMessagesEnabled"
                               class="sr-only peer" />
                        <div class="w-11 h-6 bg-bg-tertiary peer-focus:outline-none peer-focus:ring-2 peer-focus:ring-accent-blue/50 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-text-primary after:border-border-primary after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-accent-orange"></div>
                    </label>
                </div>

                <!-- Moderation Alerts -->
                <div class="p-4 flex items-center justify-between">
                    <div class="flex-1 pr-4">
                        <label for="Input_ModerationAlertsEnabled" class="text-sm font-medium text-text-primary">Moderation Alerts</label>
                        <p class="text-xs text-text-tertiary mt-0.5">Send alerts for moderation actions</p>
                    </div>
                    <label class="relative inline-flex items-center cursor-pointer">
                        <input type="hidden" name="Input.ModerationAlertsEnabled" value="false" />
                        <input type="checkbox"
                               id="Input_ModerationAlertsEnabled"
                               name="Input.ModerationAlertsEnabled"
                               value="true"
                               checked="@Model.Input.ModerationAlertsEnabled"
                               class="sr-only peer" />
                        <div class="w-11 h-6 bg-bg-tertiary peer-focus:outline-none peer-focus:ring-2 peer-focus:ring-accent-blue/50 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-text-primary after:border-border-primary after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-accent-orange"></div>
                    </label>
                </div>

                <!-- Command Logging -->
                <div class="p-4 flex items-center justify-between">
                    <div class="flex-1 pr-4">
                        <label for="Input_CommandLoggingEnabled" class="text-sm font-medium text-text-primary">Command Logging</label>
                        <p class="text-xs text-text-tertiary mt-0.5">Log all command usage to a designated channel</p>
                    </div>
                    <label class="relative inline-flex items-center cursor-pointer">
                        <input type="hidden" name="Input.CommandLoggingEnabled" value="false" />
                        <input type="checkbox"
                               id="Input_CommandLoggingEnabled"
                               name="Input.CommandLoggingEnabled"
                               value="true"
                               checked="@Model.Input.CommandLoggingEnabled"
                               class="sr-only peer" />
                        <div class="w-11 h-6 bg-bg-tertiary peer-focus:outline-none peer-focus:ring-2 peer-focus:ring-accent-blue/50 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-text-primary after:border-border-primary after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-accent-orange"></div>
                    </label>
                </div>
            </div>
        </div>

        <!-- Advanced Settings Section -->
        <div class="bg-bg-secondary border border-border-primary rounded-lg mb-6">
            <div class="px-6 py-4 border-b border-border-primary">
                <h2 class="text-lg font-semibold text-text-primary">Advanced Settings</h2>
            </div>
            <div class="p-6 space-y-6">
                <!-- Log Channel -->
                <div>
                    @{
                        var logChannelValidation = !ViewData.ModelState.IsValid && ViewData.ModelState["Input.LogChannel"]?.Errors.Count > 0
                            ? ValidationState.Error
                            : ValidationState.None;
                        var logChannelError = ViewData.ModelState["Input.LogChannel"]?.Errors.FirstOrDefault()?.ErrorMessage;
                        var logChannelModel = new FormInputViewModel {
                            Id = "Input_LogChannel",
                            Name = "Input.LogChannel",
                            Label = "Log Channel",
                            Type = "text",
                            Value = Model.Input.LogChannel,
                            MaxLength = 100,
                            Placeholder = "e.g., bot-logs",
                            HelpText = "Channel name for bot activity logs",
                            ValidationState = logChannelValidation,
                            ValidationMessage = logChannelError
                        };
                    }
                    <partial name="Shared/Components/_FormInput" model="logChannelModel" />
                </div>

                <!-- Welcome Channel -->
                <div>
                    @{
                        var welcomeChannelValidation = !ViewData.ModelState.IsValid && ViewData.ModelState["Input.WelcomeChannel"]?.Errors.Count > 0
                            ? ValidationState.Error
                            : ValidationState.None;
                        var welcomeChannelError = ViewData.ModelState["Input.WelcomeChannel"]?.Errors.FirstOrDefault()?.ErrorMessage;
                        var welcomeChannelModel = new FormInputViewModel {
                            Id = "Input_WelcomeChannel",
                            Name = "Input.WelcomeChannel",
                            Label = "Welcome Channel",
                            Type = "text",
                            Value = Model.Input.WelcomeChannel,
                            MaxLength = 100,
                            Placeholder = "e.g., welcome",
                            HelpText = "Channel for welcome and leave messages",
                            ValidationState = welcomeChannelValidation,
                            ValidationMessage = welcomeChannelError
                        };
                    }
                    <partial name="Shared/Components/_FormInput" model="welcomeChannelModel" />
                </div>

                <!-- Auto-Moderation Toggle -->
                <div class="flex items-center justify-between py-2">
                    <div class="flex-1 pr-4">
                        <label for="Input_AutoModEnabled" class="text-sm font-medium text-text-primary">Auto-Moderation</label>
                        <p class="text-xs text-text-tertiary mt-0.5">Enable automatic content moderation features</p>
                    </div>
                    <label class="relative inline-flex items-center cursor-pointer">
                        <input type="hidden" name="Input.AutoModEnabled" value="false" />
                        <input type="checkbox"
                               id="Input_AutoModEnabled"
                               name="Input.AutoModEnabled"
                               value="true"
                               checked="@Model.Input.AutoModEnabled"
                               class="sr-only peer" />
                        <div class="w-11 h-6 bg-bg-tertiary peer-focus:outline-none peer-focus:ring-2 peer-focus:ring-accent-blue/50 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-text-primary after:border-border-primary after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-accent-orange"></div>
                    </label>
                </div>
            </div>
        </div>

        <!-- Form Actions -->
        <div class="flex items-center justify-end gap-4">
            <a asp-page="Details" asp-route-id="@Model.ViewModel.Id" class="btn btn-secondary">
                Cancel
            </a>
            <button type="submit" class="btn btn-primary">
                <svg class="w-5 h-5 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                </svg>
                Save Settings
            </button>
        </div>
    </form>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

### 4.4 Update GuildSettingsViewModel (Optional Enhancement)

**File:** `src/DiscordBot.Bot/ViewModels/Pages/GuildDetailViewModel.cs`

Add additional properties to `GuildSettingsViewModel` to support displaying all settings on the Details page:

```csharp
/// <summary>
/// Parsed guild settings for display.
/// </summary>
public record GuildSettingsViewModel
{
    /// <summary>
    /// Gets the welcome channel ID or name.
    /// </summary>
    public string? WelcomeChannel { get; init; }

    /// <summary>
    /// Gets the log channel ID or name.
    /// </summary>
    public string? LogChannel { get; init; }

    /// <summary>
    /// Gets whether auto-moderation is enabled.
    /// </summary>
    public bool AutoModEnabled { get; init; }

    // New properties to add:

    /// <summary>
    /// Gets whether welcome messages are enabled.
    /// </summary>
    public bool WelcomeMessagesEnabled { get; init; }

    /// <summary>
    /// Gets whether leave messages are enabled.
    /// </summary>
    public bool LeaveMessagesEnabled { get; init; }

    /// <summary>
    /// Gets whether moderation alerts are enabled.
    /// </summary>
    public bool ModerationAlertsEnabled { get; init; }

    /// <summary>
    /// Gets whether command logging is enabled.
    /// </summary>
    public bool CommandLoggingEnabled { get; init; }

    /// <summary>
    /// Gets whether any custom settings are configured.
    /// </summary>
    public bool HasSettings => !string.IsNullOrEmpty(WelcomeChannel)
        || !string.IsNullOrEmpty(LogChannel)
        || AutoModEnabled
        || WelcomeMessagesEnabled
        || LeaveMessagesEnabled
        || ModerationAlertsEnabled
        || CommandLoggingEnabled;

    // Update Parse method to include new properties
    public static GuildSettingsViewModel Parse(string? settingsJson)
    {
        if (string.IsNullOrEmpty(settingsJson))
            return new GuildSettingsViewModel();

        try
        {
            return JsonSerializer.Deserialize<GuildSettingsViewModel>(settingsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new GuildSettingsViewModel();
        }
        catch (JsonException)
        {
            return new GuildSettingsViewModel();
        }
    }
}
```

---

## 5. Subagent Task Plan

### 5.1 design-specialist

**Not required for this issue.** The design system already defines all necessary components (form inputs, toggles, buttons, alerts, cards). No new design tokens or component specifications are needed.

### 5.2 html-prototyper

**Not required for this issue.** The prototype already exists in `docs/prototypes/pages/server-detail.html` (Settings tab). No additional prototype work is needed.

### 5.3 dotnet-specialist

Implement the following:

| Task ID | Description | Files |
|---------|-------------|-------|
| 4.3.1 | Create `GuildEditViewModel` with all editable properties and JSON serialization | `src/DiscordBot.Bot/ViewModels/Pages/GuildEditViewModel.cs` |
| 4.3.2 | Create `Edit.cshtml.cs` page model with `InputModel`, `OnGetAsync`, `OnPostAsync` | `src/DiscordBot.Bot/Pages/Guilds/Edit.cshtml.cs` |
| 4.3.3 | Create `Edit.cshtml` Razor view with form sections matching prototype | `src/DiscordBot.Bot/Pages/Guilds/Edit.cshtml` |
| 4.3.4 | Update `GuildSettingsViewModel` with additional notification properties | `src/DiscordBot.Bot/ViewModels/Pages/GuildDetailViewModel.cs` |
| 4.3.5 | Add success message handling to `Details.cshtml` for TempData | `src/DiscordBot.Bot/Pages/Guilds/Details.cshtml` |
| 4.3.6 | Write unit tests for `GuildEditViewModel` JSON serialization | `tests/DiscordBot.Tests/ViewModels/GuildEditViewModelTests.cs` |

### 5.4 docs-writer

| Task ID | Description | Files |
|---------|-------------|-------|
| 4.3.7 | Update user management docs with guild settings editing instructions | `docs/articles/user-management.md` |
| 4.3.8 | Add API documentation for guild settings JSON schema | `docs/articles/api-endpoints.md` |

---

## 6. Timeline / Dependency Map

```
Phase 1: Core Implementation (Can run in parallel)
  [4.3.1] GuildEditViewModel ─────────┐
  [4.3.4] Update GuildSettingsViewModel┴─► [4.3.2] Edit.cshtml.cs ─► [4.3.3] Edit.cshtml

Phase 2: Integration (Sequential)
  [4.3.3] Edit.cshtml ─► [4.3.5] Details.cshtml TempData handling

Phase 3: Testing & Documentation (Parallel)
  [4.3.6] Unit tests
  [4.3.7] User docs update
  [4.3.8] API docs update
```

### Estimated Effort

| Phase | Tasks | Estimated Time |
|-------|-------|----------------|
| Phase 1 | ViewModel + PageModel + View | 3-4 hours |
| Phase 2 | Integration | 30 minutes |
| Phase 3 | Tests + Docs | 1-2 hours |
| **Total** | | **4.5-6.5 hours** |

---

## 7. Acceptance Criteria Checklist

| # | Criterion | Validation Method |
|---|-----------|-------------------|
| 1 | Form displays current settings pre-populated | Manual test: Navigate to Edit page, verify fields match database values |
| 2 | Prefix field validates max 3 characters | Manual test: Enter >3 chars, verify error message |
| 3 | All toggle switches function correctly | Manual test: Toggle each switch, verify form submission |
| 4 | Save button calls UpdateGuildAsync | Unit test: Mock IGuildService, verify method called with correct DTO |
| 5 | Cancel button returns to Details page | Manual test: Click Cancel, verify navigation |
| 6 | Validation errors shown inline | Manual test: Submit invalid data, verify error messages appear |
| 7 | Success message shown after save | Manual test: Save valid changes, verify TempData message on Details page |
| 8 | Authorization requires Admin role | Manual test: Access as Viewer, verify 403/redirect |
| 9 | Form preserves values on validation error | Manual test: Submit invalid, verify form retains entered values |
| 10 | Settings JSON correctly serialized | Unit test: Verify ToSettingsJson output matches expected structure |

---

## 8. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| JSON schema mismatch with existing data | Medium | High | Add migration logic to handle legacy settings; use PropertyNameCaseInsensitive |
| Boolean checkbox binding issues | Medium | Medium | Use hidden field pattern (`<input type="hidden" value="false" />` before checkbox) |
| TempData not persisting across redirect | Low | Medium | Verify cookie-based TempData provider is configured; use `[TempData]` attribute |
| Toggle switch styling inconsistency | Low | Low | Use consistent Tailwind classes matching design system |
| Authorization bypass | Low | High | Use `[Authorize(Policy = "RequireAdmin")]` at page level; verify in tests |

---

## 9. Testing Strategy

### 9.1 Unit Tests

**File:** `tests/DiscordBot.Tests/ViewModels/GuildEditViewModelTests.cs`

```csharp
public class GuildEditViewModelTests
{
    [Fact]
    public void FromDto_WithValidSettings_PopulatesAllProperties()
    {
        // Arrange
        var dto = new GuildDto
        {
            Id = 123456789,
            Name = "Test Guild",
            Prefix = "!",
            Settings = "{\"welcomeChannel\":\"general\",\"autoModEnabled\":true}"
        };

        // Act
        var viewModel = GuildEditViewModel.FromDto(dto);

        // Assert
        viewModel.WelcomeChannel.Should().Be("general");
        viewModel.AutoModEnabled.Should().BeTrue();
    }

    [Fact]
    public void ToSettingsJson_SerializesCorrectly()
    {
        // Arrange
        var viewModel = new GuildEditViewModel
        {
            WelcomeChannel = "welcome",
            AutoModEnabled = true,
            WelcomeMessagesEnabled = true
        };

        // Act
        var json = viewModel.ToSettingsJson();

        // Assert
        json.Should().Contain("\"welcomeChannel\":\"welcome\"");
        json.Should().Contain("\"autoModEnabled\":true");
    }

    [Fact]
    public void FromDto_WithNullSettings_ReturnsDefaults()
    {
        // Arrange
        var dto = new GuildDto { Settings = null };

        // Act
        var viewModel = GuildEditViewModel.FromDto(dto);

        // Assert
        viewModel.AutoModEnabled.Should().BeFalse();
        viewModel.WelcomeChannel.Should().BeNull();
    }
}
```

### 9.2 Integration Tests

**File:** `tests/DiscordBot.Tests/Pages/Guilds/EditPageTests.cs`

- Test OnGetAsync returns NotFound for non-existent guild
- Test OnPostAsync calls UpdateGuildAsync with correct parameters
- Test OnPostAsync returns Page with errors when ModelState invalid
- Test authorization policy enforcement

### 9.3 Manual Testing Checklist

- [ ] Navigate to guild list, click guild, click Edit
- [ ] Verify all fields populated correctly
- [ ] Modify prefix, save, verify change persists
- [ ] Toggle all notification switches, save
- [ ] Enter invalid prefix (>3 chars), verify error
- [ ] Click Cancel, verify returns to Details without saving
- [ ] Verify success message appears after save
- [ ] Access Edit page as Viewer role, verify denied

---

## 10. Implementation Notes

### 10.1 Hidden Field Pattern for Checkboxes

ASP.NET Core does not submit unchecked checkboxes. The hidden field pattern ensures `false` is submitted:

```html
<input type="hidden" name="Input.IsActive" value="false" />
<input type="checkbox" name="Input.IsActive" value="true" checked="@Model.Input.IsActive" />
```

When checkbox is checked, both values are submitted but the model binder uses the last value (`true`).
When unchecked, only `false` is submitted.

### 10.2 TempData for Success Messages

The `[TempData]` attribute automatically handles serialization. After redirect, the value is available once then cleared:

```csharp
[TempData]
public string? SuccessMessage { get; set; }

// In OnPostAsync:
SuccessMessage = "Guild settings saved successfully.";
return RedirectToPage("Details", new { id = Input.GuildId });
```

In Details.cshtml, read from TempData:

```html
@if (TempData["SuccessMessage"] != null)
{
    <partial name="Shared/Components/_Alert" model="..." />
}
```

### 10.3 Route Constraint

The page route uses `{id:long}` to ensure the guild ID is a valid 64-bit integer (Discord snowflake):

```html
@page "{id:long}"
```

---

## Changelog

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-12-23 | Systems Architect | Initial plan creation |
