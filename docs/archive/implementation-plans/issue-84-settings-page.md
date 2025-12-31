# Implementation Plan: Issue #84 - Application Settings Page

**Issue:** #84 - Application Settings Page
**Created:** 2025-12-23
**Status:** Planning

---

## 1. Requirement Summary

Create an Application Settings page (`Pages/Admin/Settings.cshtml`) that allows administrators to configure bot settings through a web UI. The page should:

- Organize settings into categories (General, Logging, Features) with tab navigation
- Provide appropriate form controls (text inputs, dropdowns, toggles) for different setting types
- Validate inputs before saving
- Indicate which settings require a bot restart to take effect
- Persist settings to a database-backed configuration store

---

## 2. Architectural Considerations

### 2.1 Settings Storage Decision

**Recommendation: Option C - Hybrid Approach (Database + appsettings defaults)**

| Aspect | Database Storage | appsettings.json |
|--------|-----------------|------------------|
| Persistence across deployments | Yes | No (overwritten) |
| Runtime editability | Yes | No (requires file access) |
| Default values | Fall back to appsettings | Primary source for defaults |
| Sensitive values | Never stored here | Via User Secrets only |

**Rationale:**
- Settings modified via UI are stored in the database
- Default values come from `appsettings.json` if no override exists
- Sensitive settings (Token, OAuth secrets) remain in User Secrets and are NOT editable via UI
- This matches patterns used by similar admin systems

### 2.2 Existing System Integration

**Relevant Components:**
- `BotDbContext` - Requires new `DbSet<ApplicationSetting>`
- `IBotService` / `BotHostedService` - Already has restart capability
- `BotConfigurationDto` - Shows current configuration (read-only)
- Existing ViewModels: `FormInputViewModel`, `FormSelectViewModel` for form controls
- Shared Components: `_FormInput`, `_FormSelect`, `_ConfirmationModal`, `_ToastContainer`

### 2.3 Settings Categories and Scope

Based on `appsettings.json` and prototype analysis:

| Category | Settings | Restart Required |
|----------|----------|-----------------|
| **General** | DefaultTimezone, StatusMessage, BotEnabled | Some |
| **Logging** | MinimumLogLevel, RetainedFileCountLimit | Yes (for Serilog) |
| **Features** | EnabledCommandModules (future), RateLimitInvokes, RateLimitPeriodSeconds | No |
| **Advanced** | DebugMode, CacheEnabled, DataRetentionDays | Some |

**Not Editable via UI (security):**
- Discord Token
- OAuth ClientId/ClientSecret
- Database connection strings
- Any value in User Secrets

### 2.4 Data Model Design

Settings will be stored as key-value pairs with metadata:

```
Setting
├── Key (PK, string)
├── Value (string, JSON-serialized for complex types)
├── Category (enum: General, Logging, Features, Advanced)
├── DataType (enum: String, Integer, Boolean, Decimal, Json)
├── RequiresRestart (bool)
├── LastModifiedAt (DateTime)
├── LastModifiedBy (string - user ID)
```

### 2.5 Restart Required Tracking

**Implementation Strategy:**
1. Track pending restart flag in `ISettingsService` (in-memory)
2. Set flag when any `RequiresRestart = true` setting is changed
3. Display persistent banner on Settings page when restart pending
4. Clear flag when bot is restarted
5. Banner links to Bot Control page for restart action

---

## 3. Subagent Task Plan

### 3.1 Design Specialist Tasks

**No new design work required** - The prototype at `docs/prototypes/pages/settings.html` already provides:
- Tab navigation pattern for settings categories
- Toggle switch styles (`.form-toggle` component)
- Form input and select styles
- Settings card layout
- Danger zone styling
- Toast notifications
- "Restart required" visual treatment (warning banner)

**Verification Task:**
- Confirm existing Tailwind classes in `tailwind.config.js` cover all needed styles
- Verify toggle switch component exists or document need to create

### 3.2 Prototyper Tasks

**No additional prototyping required** - Use existing `docs/prototypes/pages/settings.html` as reference.

### 3.3 .NET Specialist Tasks

#### 3.3.1 Core Layer (DiscordBot.Core)

**File: `src/DiscordBot.Core/Entities/ApplicationSetting.cs`**
```csharp
namespace DiscordBot.Core.Entities;

public class ApplicationSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public SettingCategory Category { get; set; }
    public SettingDataType DataType { get; set; }
    public bool RequiresRestart { get; set; }
    public DateTime LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }
}
```

**File: `src/DiscordBot.Core/Enums/SettingCategory.cs`**
```csharp
namespace DiscordBot.Core.Enums;

public enum SettingCategory
{
    General,
    Logging,
    Features,
    Advanced
}
```

**File: `src/DiscordBot.Core/Enums/SettingDataType.cs`**
```csharp
namespace DiscordBot.Core.Enums;

public enum SettingDataType
{
    String,
    Integer,
    Boolean,
    Decimal,
    Json
}
```

**File: `src/DiscordBot.Core/DTOs/SettingDto.cs`**
```csharp
namespace DiscordBot.Core.DTOs;

public record SettingDto
{
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public SettingCategory Category { get; init; }
    public SettingDataType DataType { get; init; }
    public bool RequiresRestart { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ValidationRules { get; init; } // JSON: min, max, pattern, etc.
    public List<string>? AllowedValues { get; init; } // For dropdowns
    public string? DefaultValue { get; init; }
}
```

**File: `src/DiscordBot.Core/DTOs/SettingsUpdateDto.cs`**
```csharp
namespace DiscordBot.Core.DTOs;

public record SettingsUpdateDto
{
    public Dictionary<string, string> Settings { get; init; } = new();
}
```

**File: `src/DiscordBot.Core/DTOs/SettingsUpdateResultDto.cs`**
```csharp
namespace DiscordBot.Core.DTOs;

public record SettingsUpdateResultDto
{
    public bool Success { get; init; }
    public List<string> Errors { get; init; } = new();
    public bool RestartRequired { get; init; }
    public List<string> UpdatedKeys { get; init; } = new();
}
```

**File: `src/DiscordBot.Core/Interfaces/ISettingsService.cs`**
```csharp
namespace DiscordBot.Core.Interfaces;

public interface ISettingsService
{
    /// <summary>
    /// Gets all settings for a category.
    /// </summary>
    Task<IReadOnlyList<SettingDto>> GetSettingsByCategoryAsync(
        SettingCategory category,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all settings across all categories.
    /// </summary>
    Task<IReadOnlyList<SettingDto>> GetAllSettingsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets a single setting value.
    /// </summary>
    Task<T?> GetSettingValueAsync<T>(string key, CancellationToken ct = default);

    /// <summary>
    /// Updates multiple settings.
    /// </summary>
    Task<SettingsUpdateResultDto> UpdateSettingsAsync(
        SettingsUpdateDto updates,
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// Resets a category to default values.
    /// </summary>
    Task<SettingsUpdateResultDto> ResetCategoryAsync(
        SettingCategory category,
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// Resets all settings to defaults.
    /// </summary>
    Task<SettingsUpdateResultDto> ResetAllAsync(
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets whether a restart is pending due to setting changes.
    /// </summary>
    bool IsRestartPending { get; }

    /// <summary>
    /// Clears the restart pending flag.
    /// </summary>
    void ClearRestartPending();
}
```

**File: `src/DiscordBot.Core/Interfaces/ISettingsRepository.cs`**
```csharp
namespace DiscordBot.Core.Interfaces;

public interface ISettingsRepository
{
    Task<ApplicationSetting?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyList<ApplicationSetting>> GetByCategoryAsync(
        SettingCategory category,
        CancellationToken ct = default);
    Task<IReadOnlyList<ApplicationSetting>> GetAllAsync(CancellationToken ct = default);
    Task UpsertAsync(ApplicationSetting setting, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
    Task DeleteByCategoryAsync(SettingCategory category, CancellationToken ct = default);
}
```

#### 3.3.2 Infrastructure Layer (DiscordBot.Infrastructure)

**File: `src/DiscordBot.Infrastructure/Data/Configurations/ApplicationSettingConfiguration.cs`**
```csharp
// EF Core entity configuration for ApplicationSetting
// - Key as primary key (string, max 100)
// - Category index
// - Value max length 4000
```

**Modify: `src/DiscordBot.Infrastructure/Data/BotDbContext.cs`**
- Add `DbSet<ApplicationSetting> ApplicationSettings`
- Add entity configuration in `OnModelCreating`

**File: `src/DiscordBot.Infrastructure/Data/Repositories/SettingsRepository.cs`**
- Implement `ISettingsRepository`
- Standard repository pattern following existing `Repository<T>` base

**File: `src/DiscordBot.Infrastructure/Services/SettingsService.cs`**
- Implement `ISettingsService`
- Inject `ISettingsRepository`, `IConfiguration`, `ILogger<SettingsService>`
- Maintain in-memory `_restartPending` flag
- Define `SettingDefinitions` - static list of all supported settings with metadata
- Validate setting values based on `SettingDataType` and validation rules
- Merge database values with defaults from `IConfiguration`

**File: `src/DiscordBot.Infrastructure/Services/SettingDefinitions.cs`**
```csharp
// Static class defining all available settings:
public static class SettingDefinitions
{
    public static readonly IReadOnlyList<SettingDefinition> All = new List<SettingDefinition>
    {
        // General
        new("General:DefaultTimezone", "Default Timezone", SettingCategory.General,
            SettingDataType.String, "UTC", false,
            description: "Timezone for scheduled tasks and timestamps",
            allowedValues: TimezoneList),
        new("General:StatusMessage", "Bot Status Message", SettingCategory.General,
            SettingDataType.String, "", false,
            description: "Status message shown in Discord"),
        new("General:BotEnabled", "Bot Enabled", SettingCategory.General,
            SettingDataType.Boolean, "true", false),

        // Logging
        new("Serilog:MinimumLevel:Default", "Minimum Log Level", SettingCategory.Logging,
            SettingDataType.String, "Information", true,
            allowedValues: LogLevels),
        new("Serilog:WriteTo:1:Args:retainedFileCountLimit", "Log Retention Days",
            SettingCategory.Logging, SettingDataType.Integer, "7", true,
            validation: new { min = 1, max = 90 }),

        // Features
        new("Discord:DefaultRateLimitInvokes", "Rate Limit Invocations", SettingCategory.Features,
            SettingDataType.Integer, "3", false,
            description: "Max command invocations before rate limiting",
            validation: new { min = 1, max = 100 }),
        new("Discord:DefaultRateLimitPeriodSeconds", "Rate Limit Period (seconds)",
            SettingCategory.Features, SettingDataType.Decimal, "60", false,
            validation: new { min = 10, max = 3600 }),

        // Advanced
        new("Advanced:DebugMode", "Debug Mode", SettingCategory.Advanced,
            SettingDataType.Boolean, "false", true),
        new("Advanced:CacheEnabled", "Enable Caching", SettingCategory.Advanced,
            SettingDataType.Boolean, "true", false),
        new("Advanced:DataRetentionDays", "Data Retention Days", SettingCategory.Advanced,
            SettingDataType.Integer, "90", false,
            validation: new { min = 1, max = 365 }),
    };
}
```

**Modify: `src/DiscordBot.Infrastructure/Extensions/ServiceCollectionExtensions.cs`**
- Register `ISettingsRepository` -> `SettingsRepository`
- Register `ISettingsService` -> `SettingsService` (singleton for restart flag)

#### 3.3.3 Application Layer (DiscordBot.Bot)

**File: `src/DiscordBot.Bot/ViewModels/Pages/SettingsViewModel.cs`**
```csharp
public class SettingsViewModel
{
    public string ActiveCategory { get; set; } = "General";
    public IReadOnlyList<SettingDto> GeneralSettings { get; set; } = [];
    public IReadOnlyList<SettingDto> LoggingSettings { get; set; } = [];
    public IReadOnlyList<SettingDto> FeaturesSettings { get; set; } = [];
    public IReadOnlyList<SettingDto> AdvancedSettings { get; set; } = [];
    public bool IsRestartPending { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
}
```

**File: `src/DiscordBot.Bot/ViewModels/Components/FormToggleViewModel.cs`**
```csharp
public record FormToggleViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Label { get; init; }
    public string? Description { get; init; }
    public bool IsChecked { get; init; }
    public bool IsDisabled { get; init; }
}
```

**File: `src/DiscordBot.Bot/Pages/Shared/Components/_FormToggle.cshtml`**
- Implement toggle switch component matching prototype styles
- Use existing `.form-toggle` CSS classes

**File: `src/DiscordBot.Bot/Pages/Shared/Components/_RestartBanner.cshtml`**
- Warning banner indicating restart is required
- Links to Bot Control page

**File: `src/DiscordBot.Bot/Pages/Admin/Settings.cshtml`**
```razor
@page
@model DiscordBot.Bot.Pages.Admin.SettingsModel
@{
    ViewData["Title"] = "Settings";
}

<!-- Restart Required Banner (conditional) -->
@if (Model.ViewModel.IsRestartPending)
{
    <partial name="Components/_RestartBanner" />
}

<!-- Page Header with Reset All / Save All buttons -->

<!-- Tab Navigation (General, Logging, Features, Advanced) -->

<!-- Settings Sections (one per category) -->
<!-- Each section contains: -->
<!--   - Settings card with form inputs/toggles/selects -->
<!--   - Section Reset / Save buttons -->

<!-- Danger Zone (Reset to Defaults, Delete All Data) -->

<!-- Confirmation Modals -->
<partial name="Components/_ConfirmationModal" model="Model.ResetCategoryModal" />
<partial name="Components/_ConfirmationModal" model="Model.ResetAllModal" />
<partial name="Components/_TypedConfirmationModal" model="Model.DeleteDataModal" />

@section Scripts {
    <script src="~/js/settings.js"></script>
}
```

**File: `src/DiscordBot.Bot/Pages/Admin/Settings.cshtml.cs`**
```csharp
[Authorize(Policy = "RequireAdmin")]
public class SettingsModel : PageModel
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsModel> _logger;

    public SettingsViewModel ViewModel { get; private set; } = new();
    public ConfirmationModalViewModel ResetCategoryModal { get; private set; } = null!;
    public ConfirmationModalViewModel ResetAllModal { get; private set; } = null!;
    public TypedConfirmationModalViewModel DeleteDataModal { get; private set; } = null!;

    [BindProperty]
    public Dictionary<string, string> FormSettings { get; set; } = new();

    [BindProperty]
    public string? ActiveCategory { get; set; }

    public async Task OnGetAsync(string? category = null)
    {
        ActiveCategory = category ?? "General";
        await LoadViewModelAsync();
    }

    public async Task<IActionResult> OnPostSaveCategoryAsync(string category)
    {
        // Validate and save settings for specific category
        // Return JSON result for AJAX handling
    }

    public async Task<IActionResult> OnPostSaveAllAsync()
    {
        // Validate and save all settings
    }

    public async Task<IActionResult> OnPostResetCategoryAsync(string category)
    {
        // Reset category to defaults
    }

    public async Task<IActionResult> OnPostResetAllAsync()
    {
        // Reset all settings to defaults
    }
}
```

**File: `src/DiscordBot.Bot/wwwroot/js/settings.js`**
```javascript
// Tab switching functionality
// Form submission handlers (AJAX)
// Toast notifications on save/error
// Confirmation modal triggers
// Dirty form tracking (unsaved changes warning)
```

**Add Navigation Link:**
- Modify sidebar in `_Layout.cshtml` to include Settings link (if not already present)

#### 3.3.4 Database Migration

**Migration: `Add_ApplicationSettings_Table`**
```bash
dotnet ef migrations add Add_ApplicationSettings_Table \
    --project src/DiscordBot.Infrastructure \
    --startup-project src/DiscordBot.Bot
```

### 3.4 Documentation Writer Tasks

**File: `docs/articles/settings-management.md`**
- Document available settings and their effects
- Explain which settings require restart
- Document the settings storage mechanism
- Provide troubleshooting guide

**Update: `docs/articles/api-endpoints.md`**
- Document any new API endpoints (if Settings API is added)

**Update: `CHANGELOG.md`**
- Add entry for Settings Page feature

---

## 4. Timeline / Dependency Map

```
Phase 1: Foundation (Core + Infrastructure)
├── Create Core entities and enums
├── Create Core DTOs
├── Create Core interfaces
├── Create Infrastructure repository
├── Create Infrastructure service
├── Create EF migration
└── Register DI services

Phase 2: UI Components (Bot)
├── Create FormToggleViewModel
├── Create _FormToggle component
├── Create _RestartBanner component
└── Create SettingsViewModel

Phase 3: Page Implementation (Bot)
├── Create Settings.cshtml.cs
├── Create Settings.cshtml
├── Create settings.js
└── Add navigation link

Phase 4: Testing & Documentation
├── Write unit tests for SettingsService
├── Write integration tests
└── Create documentation
```

**Parallel Opportunities:**
- Phase 1 tasks can run in parallel
- UI components (Phase 2) can be developed in parallel with backend (Phase 1)
- Documentation can begin once architecture is finalized

**Dependencies:**
- Migration must complete before service testing
- Core interfaces required before Infrastructure implementation
- All Phase 1-2 complete before Phase 3

---

## 5. Acceptance Criteria

### 5.1 Functional Requirements

- [ ] Settings page accessible at `/Admin/Settings` for Admin+ roles
- [ ] Tab navigation switches between General, Logging, Features, Advanced categories
- [ ] Each category displays appropriate settings with correct control types:
  - Text inputs for string settings
  - Dropdowns for settings with allowed values (timezone, log level)
  - Toggle switches for boolean settings
  - Number inputs for integer/decimal settings
- [ ] Form validation prevents invalid values:
  - Required fields cannot be empty
  - Numbers respect min/max constraints
  - Only allowed values accepted for dropdowns
- [ ] Save button persists settings to database
- [ ] Reset button restores category to defaults (with confirmation)
- [ ] "Restart Required" banner appears when restart-required settings change
- [ ] Banner links to Bot Control page
- [ ] Settings persist across application restarts

### 5.2 Technical Requirements

- [ ] `ApplicationSetting` entity created with migration
- [ ] `ISettingsService` implemented with all methods
- [ ] `ISettingsRepository` implemented
- [ ] Settings merged from database + appsettings defaults
- [ ] Sensitive settings (Token, OAuth) NOT editable
- [ ] Validation occurs server-side before save
- [ ] JavaScript handles AJAX form submission
- [ ] Toast notifications for success/error feedback

### 5.3 Security Requirements

- [ ] Page requires `RequireAdmin` policy
- [ ] No sensitive configuration values exposed
- [ ] CSRF protection on all form submissions
- [ ] Audit logging for setting changes (LastModifiedBy)

### 5.4 UI/UX Requirements

- [ ] Matches prototype design (`docs/prototypes/pages/settings.html`)
- [ ] Responsive layout for mobile/tablet
- [ ] Loading states during save operations
- [ ] Confirmation dialogs for destructive actions
- [ ] Unsaved changes warning when navigating away

---

## 6. Risks and Mitigations

### 6.1 Configuration Reload Complexity

**Risk:** Some settings (Serilog) require application restart and cannot be hot-reloaded.

**Mitigation:**
- Clear `RequiresRestart` flag on settings that need restart
- Prominent restart banner with link to Bot Control
- Do not attempt runtime reconfiguration for these settings

### 6.2 Settings Validation Edge Cases

**Risk:** Complex validation rules may not cover all edge cases.

**Mitigation:**
- Start with basic validation (type, min/max, allowed values)
- Add server-side validation in service layer
- Log validation failures for debugging

### 6.3 Database vs Configuration Conflicts

**Risk:** Database settings may conflict with appsettings.json defaults after updates.

**Mitigation:**
- Database values always take precedence
- Reset function clears database, falling back to appsettings
- Document this behavior clearly

### 6.4 Concurrent Edits

**Risk:** Multiple admins editing settings simultaneously.

**Mitigation:**
- Last-write-wins approach (simple, acceptable for admin UI)
- `LastModifiedAt` timestamp for audit trail
- Consider optimistic concurrency in future if needed

---

## 7. Files Summary

### New Files to Create

| Path | Description |
|------|-------------|
| `src/DiscordBot.Core/Entities/ApplicationSetting.cs` | Setting entity |
| `src/DiscordBot.Core/Enums/SettingCategory.cs` | Category enum |
| `src/DiscordBot.Core/Enums/SettingDataType.cs` | Data type enum |
| `src/DiscordBot.Core/DTOs/SettingDto.cs` | Setting DTO |
| `src/DiscordBot.Core/DTOs/SettingsUpdateDto.cs` | Update request DTO |
| `src/DiscordBot.Core/DTOs/SettingsUpdateResultDto.cs` | Update result DTO |
| `src/DiscordBot.Core/Interfaces/ISettingsService.cs` | Service interface |
| `src/DiscordBot.Core/Interfaces/ISettingsRepository.cs` | Repository interface |
| `src/DiscordBot.Infrastructure/Data/Configurations/ApplicationSettingConfiguration.cs` | EF config |
| `src/DiscordBot.Infrastructure/Data/Repositories/SettingsRepository.cs` | Repository impl |
| `src/DiscordBot.Infrastructure/Services/SettingsService.cs` | Service impl |
| `src/DiscordBot.Infrastructure/Services/SettingDefinitions.cs` | Setting metadata |
| `src/DiscordBot.Bot/ViewModels/Pages/SettingsViewModel.cs` | Page view model |
| `src/DiscordBot.Bot/ViewModels/Components/FormToggleViewModel.cs` | Toggle view model |
| `src/DiscordBot.Bot/Pages/Shared/Components/_FormToggle.cshtml` | Toggle component |
| `src/DiscordBot.Bot/Pages/Shared/Components/_RestartBanner.cshtml` | Banner component |
| `src/DiscordBot.Bot/Pages/Admin/Settings.cshtml` | Settings page view |
| `src/DiscordBot.Bot/Pages/Admin/Settings.cshtml.cs` | Settings page model |
| `src/DiscordBot.Bot/wwwroot/js/settings.js` | Client-side JS |
| `docs/articles/settings-management.md` | Documentation |

### Files to Modify

| Path | Changes |
|------|---------|
| `src/DiscordBot.Infrastructure/Data/BotDbContext.cs` | Add DbSet, configure entity |
| `src/DiscordBot.Infrastructure/Extensions/ServiceCollectionExtensions.cs` | Register services |
| `docs/articles/api-endpoints.md` | Document settings endpoints (if API added) |
| `CHANGELOG.md` | Add feature entry |

---

## 8. Implementation Notes for Subagents

### For .NET Specialist

1. **Start with Core layer** - Create all entities, enums, DTOs, and interfaces first
2. **Follow existing patterns** - Repository pattern matches `Repository<T>` base class
3. **Service as Singleton** - `SettingsService` must be singleton to maintain restart flag
4. **Use `IConfiguration`** - For default values, inject and read from appsettings sections
5. **Validation in Service** - Do not rely solely on client-side validation

### For Docs Writer

1. **Reference prototype** - `docs/prototypes/pages/settings.html` shows all UI elements
2. **Document restart behavior** - Critical for users to understand
3. **Include troubleshooting** - Common issues like settings not persisting

---

*End of Implementation Plan*
