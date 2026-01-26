# Settings Page Documentation

**Version:** 1.1
**Last Updated:** 2025-12-30
**Route:** `/Admin/Settings`
**Authorization:** Requires `Admin` or `SuperAdmin` role

---

## Overview

The Settings page provides a web-based administrative interface for configuring bot behavior, feature toggles, and data retention policies. Settings are organized into logical categories with a tabbed interface for easy navigation.

### Key Features

- **Category-based organization**: Settings grouped into General, Features, and Advanced tabs
- **Per-category or global save**: Save individual categories or all settings at once
- **Reset functionality**: Reset individual categories or all settings to defaults
- **Real-time updates**: Some settings (like bot status) apply immediately without restart
- **Restart notifications**: Clear indicators when settings changes require bot restart
- **Type-safe inputs**: Form fields adapt to setting data types (boolean toggles, dropdowns, number inputs, text fields)
- **Validation**: Client and server-side validation with clear error messages
- **Audit trail**: All changes logged with user ID and timestamp

---

## Settings Storage Architecture

### Default Values

Default setting values are defined in two places:

1. **SettingDefinitions.cs** (Infrastructure Layer): Metadata including default values
2. **appsettings.json**: Configuration values that override definition defaults

**Priority order:** Database value > appsettings.json > SettingDefinitions default

### Database Overrides

When a setting is modified through the UI:

- An `ApplicationSetting` record is created/updated in the database
- The database value takes precedence over appsettings.json and defaults
- Resetting a setting deletes the database record, reverting to defaults

**Database Schema:**

```csharp
public class ApplicationSetting
{
    public string Key { get; set; }              // e.g., "General:DefaultTimezone"
    public string Value { get; set; }            // Stored as string, converted by type
    public SettingCategory Category { get; set; }
    public SettingDataType DataType { get; set; }
    public bool RequiresRestart { get; set; }
    public DateTime LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }  // User ID
}
```

### Configuration Merging

The `SettingsService` merges values from multiple sources:

```csharp
// Priority: database value > config value > definition default
var value = dbValues.GetValueOrDefault(definition.Key)
    ?? _configuration[definition.Key]
    ?? definition.DefaultValue;
```

This allows:
- Developers to provide defaults in code and configuration
- Administrators to override via database
- Resetting to delete overrides and restore defaults

---

## Available Settings

### General Category

| Setting Key | Display Name | Type | Default | Restart Required | Description |
|------------|--------------|------|---------|------------------|-------------|
| `General:DefaultTimezone` | Default Timezone | Dropdown | `UTC` | No | Timezone used for scheduled tasks and timestamp displays |
| `General:StatusMessage` | Bot Status Message | Text | `""` | No | Custom status message shown in Discord (empty for default) |
| `General:BotEnabled` | Bot Enabled | Boolean | `true` | No | Enable or disable the bot without stopping the service |

**Available Timezones:**
- UTC
- America/New_York
- America/Chicago
- America/Denver
- America/Los_Angeles
- Europe/London
- Europe/Paris
- Europe/Berlin
- Asia/Tokyo
- Asia/Shanghai
- Australia/Sydney

### Features Category

| Setting Key | Display Name | Type | Default | Restart Required | Description |
|------------|--------------|------|---------|------------------|-------------|
| `Features:MessageLoggingEnabled` | Message Logging | Boolean | `true` | No | Enable or disable Discord message logging globally |
| `Features:WelcomeMessagesEnabled` | Welcome Messages | Boolean | `true` | No | Enable or disable welcome messages globally (in addition to per-guild settings) |
| `Features:RatWatchEnabled` | Rat Watch | Boolean | `true` | No | Enable or disable the Rat Watch accountability feature globally |

### Advanced Category

| Setting Key | Display Name | Type | Default | Restart Required | Description |
|------------|--------------|------|---------|------------------|-------------|
| `Advanced:MessageLogRetentionDays` | Message Log Retention (Days) | Integer | `90` | No | Number of days to retain Discord message logs before deletion |
| `Advanced:AuditLogRetentionDays` | Audit Log Retention (Days) | Integer | `90` | No | Number of days to retain audit log entries before deletion |

**Validation Rules:**
- **MessageLogRetentionDays**: Min: 1, Max: 365
- **AuditLogRetentionDays**: Min: 1, Max: 365

---

## UI Components

### Page Layout

```
┌─────────────────────────────────────────────────┐
│ [Settings Header]                  [Reset All]  │
│                                    [Save All]    │
├─────────────────────────────────────────────────┤
│ [General] [Features] [Advanced]  <- Tabs        │
├─────────────────────────────────────────────────┤
│                                                  │
│  [Category Settings Card]                       │
│  ┌───────────────────────────────────────────┐  │
│  │ Setting 1  [Toggle/Input/Dropdown]        │  │
│  │ Setting 2  [Toggle/Input/Dropdown]        │  │
│  │ ...                                       │  │
│  └───────────────────────────────────────────┘  │
│                      [Reset Category] [Save]    │
│                                                  │
├─────────────────────────────────────────────────┤
│ [Danger Zone: Reset All Settings]               │
└─────────────────────────────────────────────────┘
```

### Form Field Types

Settings render different input controls based on their `DataType`:

#### Boolean Toggle

```html
<div class="flex items-start gap-4">
  <toggle-component>
    <label>Bot Enabled</label>
    <description>Enable or disable the bot...</description>
  </toggle-component>
  <badge>Restart Required</badge> <!-- if applicable -->
</div>
```

#### Dropdown Select

```html
<select name="FormSettings[General:DefaultTimezone]">
  <option value="UTC">UTC</option>
  <option value="America/New_York">America/New_York</option>
  ...
</select>
<help-text>Timezone used for scheduled tasks...</help-text>
```

#### Number Input

```html
<input type="number"
       name="FormSettings[Advanced:MessageLogRetentionDays]"
       min="1" max="365" step="1" />
<help-text>Number of days to retain Discord message logs...</help-text>
```

#### Text Input

```html
<input type="text"
       name="FormSettings[General:StatusMessage]"
       placeholder="Enter custom status..." />
<help-text>Custom status message shown in Discord...</help-text>
```

### Restart Required Badge

Settings that require restart display a warning badge:

```html
<span class="inline-flex items-center gap-1 px-2 py-1
             bg-warning/10 border border-warning text-warning
             text-xs font-medium rounded">
  <svg>...</svg>
  Restart Required
</span>
```

### Restart Pending Banner

When settings requiring restart are changed, a banner appears at the top of the page:

```html
<div class="bg-warning/10 border border-warning rounded-lg p-4">
  <div class="flex items-start gap-3">
    <svg class="text-warning">...</svg>
    <div>
      <h3>Restart Required</h3>
      <p>Some settings changes require a bot restart to take effect.</p>
      <a href="/Admin/Settings" onclick="window.settingsManager?.switchTab('BotControl'); return true;">Go to Bot Control →</a>
    </div>
  </div>
</div>
```

---

## Real-Time Updates Architecture

### SettingsChanged Event

The `ISettingsService` exposes a `SettingsChanged` event that fires when settings are updated:

```csharp
public interface ISettingsService
{
    event EventHandler<SettingsChangedEventArgs>? SettingsChanged;
}

public class SettingsChangedEventArgs : EventArgs
{
    public IReadOnlyList<string> UpdatedKeys { get; init; }
    public string UserId { get; init; }
}
```

### Event Flow

```
┌──────────────┐
│  Admin UI    │
│  Settings    │
│    Page      │
└──────┬───────┘
       │ POST /Admin/Settings?handler=SaveCategory
       ▼
┌──────────────────┐
│ SettingsService  │
│ UpdateSettingsAsync()
└──────┬───────────┘
       │ raises SettingsChanged event
       ▼
┌──────────────────┐
│ BotHostedService │
│ OnSettingsChangedAsync()
└──────┬───────────┘
       │ if General:StatusMessage changed
       ▼
┌──────────────────┐
│ Apply Status     │
│ Update (Discord) │
└──────────────────┘
```

### Subscriber Implementation

The `BotHostedService` subscribes to settings changes:

```csharp
// In StartAsync()
_settingsService.SettingsChanged += OnSettingsChangedAsync;

// Handler
private void OnSettingsChangedAsync(object? sender, SettingsChangedEventArgs e)
{
    if (e.UpdatedKeys.Contains("General:StatusMessage"))
    {
        _logger.LogInformation("Bot status message setting changed, applying update");
        _ = ApplyCustomStatusAsync(); // Update Discord status immediately
    }
}

// In StopAsync()
_settingsService.SettingsChanged -= OnSettingsChangedAsync;
```

### Real-Time vs Restart Required

| Setting | Real-Time Update | Restart Required |
|---------|------------------|------------------|
| `General:StatusMessage` | Yes (via SettingsChanged event) | No |
| `General:BotEnabled` | No | No |
| `General:DefaultTimezone` | No | No |
| `Features:MessageLoggingEnabled` | No | No |
| `Features:WelcomeMessagesEnabled` | No | No |
| `Advanced:MessageLogRetentionDays` | No | No |
| `Advanced:AuditLogRetentionDays` | No | No |

**Note:** Currently, only `General:StatusMessage` implements real-time updates. Other settings take effect on the next bot operation or restart.

---

## JavaScript API

The settings page uses the `settingsManager` JavaScript module for client-side interactions.

### Public Methods

```javascript
window.settingsManager = {
    switchTab(category),          // Switch between setting category tabs
    saveCategory(category),       // Save settings for a specific category
    saveAllSettings(),            // Save all settings across all categories
    showResetCategoryModal(cat),  // Show confirmation modal for category reset
    showResetAllModal()           // Show confirmation modal for reset all
}
```

### Form Data Handling

The module handles proper checkbox serialization (unchecked checkboxes don't submit):

```javascript
function buildFormData(form) {
    const formData = new FormData();

    // Add the anti-forgery token
    const token = form.querySelector('input[name="__RequestVerificationToken"]');
    if (token) {
        formData.append('__RequestVerificationToken', token.value);
    }

    // Process all toggle checkboxes - add their current state (true/false)
    const toggles = form.querySelectorAll('input[data-setting-toggle]');
    toggles.forEach(toggle => {
        formData.append(toggle.name, toggle.checked ? 'true' : 'false');
    });

    // Process all other form inputs (text, number, select, etc.)
    const inputs = form.querySelectorAll('input:not([type="checkbox"]):not([type="hidden"]), select, textarea');
    inputs.forEach(input => {
        if (input.name && !input.name.startsWith('__')) {
            formData.append(input.name, input.value);
        }
    });

    return formData;
}
```

### Dirty State Tracking

The module tracks unsaved changes and warns users:

```javascript
let isDirty = false;

// Track changes
form.addEventListener('input', () => { isDirty = true; });

// Warn before navigation
window.addEventListener('beforeunload', (e) => {
    if (isDirty) {
        e.returnValue = 'You have unsaved changes. Are you sure?';
    }
});
```

---

## Server-Side Handlers

The `SettingsModel` page model provides POST handlers for settings operations.

### Save Category

**Endpoint:** `POST /Admin/Settings?handler=SaveCategory&category={category}`

```csharp
public async Task<IActionResult> OnPostSaveCategoryAsync(string category)
{
    var userId = User.Identity?.Name ?? "Unknown";
    var updateDto = new SettingsUpdateDto { Settings = FormSettings };

    var result = await _settingsService.UpdateSettingsAsync(updateDto, userId);

    return new JsonResult(new
    {
        success = true,
        message = result.Changes.Count > 0
            ? $"Settings saved successfully. {result.Changes.Count} setting(s) updated."
            : "No changes detected.",
        restartRequired = result.RestartRequired
    });
}
```

### Save All

**Endpoint:** `POST /Admin/Settings?handler=SaveAll`

```csharp
public async Task<IActionResult> OnPostSaveAllAsync()
{
    var userId = User.Identity?.Name ?? "Unknown";
    var updateDto = new SettingsUpdateDto { Settings = FormSettings };

    var result = await _settingsService.UpdateSettingsAsync(updateDto, userId);

    return new JsonResult(new
    {
        success = true,
        message = result.Changes.Count > 0
            ? $"All settings saved successfully. {result.Changes.Count} setting(s) updated."
            : "No changes detected.",
        restartRequired = result.RestartRequired
    });
}
```

### Reset Category

**Endpoint:** `POST /Admin/Settings?handler=ResetCategory&category={category}`

```csharp
public async Task<IActionResult> OnPostResetCategoryAsync(string category)
{
    var userId = User.Identity?.Name ?? "Unknown";
    var categoryEnum = Enum.Parse<SettingCategory>(category);

    var result = await _settingsService.ResetCategoryAsync(categoryEnum, userId);

    return new JsonResult(new
    {
        success = result.Success,
        message = $"{category} settings have been reset to defaults.",
        restartRequired = result.RestartRequired
    });
}
```

### Reset All

**Endpoint:** `POST /Admin/Settings?handler=ResetAll`

```csharp
public async Task<IActionResult> OnPostResetAllAsync()
{
    var userId = User.Identity?.Name ?? "Unknown";
    var result = await _settingsService.ResetAllAsync(userId);

    return new JsonResult(new
    {
        success = result.Success,
        message = "All settings have been reset to defaults.",
        restartRequired = result.RestartRequired
    });
}
```

### Response Format

All handlers return JSON:

```json
{
    "success": true,
    "message": "Settings saved successfully. 2 setting(s) updated.",
    "restartRequired": false
}
```

---

## Settings Service Implementation

### Service Registration

The `SettingsService` is registered as a **singleton** to maintain the `IsRestartPending` flag across requests:

```csharp
// In Program.cs or ServiceExtensions
services.AddSingleton<ISettingsService, SettingsService>();
```

**Why singleton?**
- The `IsRestartPending` flag is in-memory state
- Must persist across multiple HTTP requests
- Cleared when bot restarts (via `BotHostedService`)

### Scoped Repository Access

Since repositories are scoped, the singleton service uses `IServiceScopeFactory`:

```csharp
public class SettingsService : ISettingsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private bool _restartPending;

    private ISettingsRepository GetRepository(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ISettingsRepository>();

    public async Task<IReadOnlyList<SettingDto>> GetSettingsByCategoryAsync(
        SettingCategory category,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = GetRepository(scope);

        // Use repository...
    }
}
```

### Validation

The service validates setting values before saving:

```csharp
private string? ValidateValue(SettingDefinition definition, string value)
{
    // Check allowed values
    if (definition.AllowedValues != null && !definition.AllowedValues.Contains(value))
    {
        return $"Value must be one of: {string.Join(", ", definition.AllowedValues)}";
    }

    // Type-specific validation
    switch (definition.DataType)
    {
        case SettingDataType.Integer:
            if (!int.TryParse(value, out var intValue))
                return "Value must be a valid integer";
            return ValidateNumericRange(intValue, definition.ValidationRules);

        case SettingDataType.Boolean:
            if (!bool.TryParse(value, out _))
                return "Value must be 'true' or 'false'";
            break;

        case SettingDataType.Json:
            try { JsonDocument.Parse(value); }
            catch { return "Value must be valid JSON"; }
            break;
    }

    return null; // Valid
}
```

### Type Conversion

The service converts string values to typed values:

```csharp
public async Task<T?> GetSettingValueAsync<T>(string key, CancellationToken ct = default)
{
    // Check database first
    var dbSetting = await repository.GetByKeyAsync(key, ct);
    string? valueStr = dbSetting?.Value;

    // Fall back to configuration
    if (valueStr == null)
        valueStr = _configuration[key];

    // Convert to type T
    return ConvertValue<T>(valueStr);
}

private T? ConvertValue<T>(string value)
{
    var targetType = typeof(T);
    var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

    if (underlyingType == typeof(string)) return (T)(object)value;
    if (underlyingType == typeof(int)) return (T)(object)int.Parse(value);
    if (underlyingType == typeof(bool)) return (T)(object)bool.Parse(value);
    if (underlyingType == typeof(decimal)) return (T)(object)decimal.Parse(value, CultureInfo.InvariantCulture);
    if (underlyingType == typeof(double)) return (T)(object)double.Parse(value, CultureInfo.InvariantCulture);

    return (T?)Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
}
```

---

## Adding New Settings

Follow these steps to add a new setting to the system.

### 1. Define the Setting

Add the setting definition to `SettingDefinitions.cs`:

```csharp
public static readonly IReadOnlyList<SettingDefinition> All = new List<SettingDefinition>
{
    // Existing settings...

    new(
        key: "Features:RateLimitEnabled",
        displayName: "Rate Limiting",
        category: SettingCategory.Features,
        dataType: SettingDataType.Boolean,
        defaultValue: "true",
        requiresRestart: false,
        description: "Enable rate limiting on commands to prevent abuse"
    ),

    new(
        key: "Advanced:MaxCommandsPerMinute",
        displayName: "Max Commands Per Minute",
        category: SettingCategory.Advanced,
        dataType: SettingDataType.Integer,
        defaultValue: "30",
        requiresRestart: false,
        description: "Maximum number of commands per user per minute",
        validation: new { min = 1, max = 100 }
    )
};
```

### 2. Add Default to appsettings.json (Optional)

Override the code-defined default in configuration:

```json
{
  "Features": {
    "RateLimitEnabled": true
  },
  "Advanced": {
    "MaxCommandsPerMinute": 30
  }
}
```

### 3. Apply Database Migration (if needed)

If this is the first setting added, ensure the `ApplicationSettings` table exists:

```bash
dotnet ef migrations add AddApplicationSettingsTable --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
dotnet ef database update --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot
```

### 4. Use the Setting in Code

Retrieve and use the setting value:

```csharp
public class MyService
{
    private readonly ISettingsService _settingsService;

    public async Task DoSomethingAsync()
    {
        var rateLimitEnabled = await _settingsService.GetSettingValueAsync<bool>(
            "Features:RateLimitEnabled");

        if (rateLimitEnabled == true)
        {
            var maxCommands = await _settingsService.GetSettingValueAsync<int>(
                "Advanced:MaxCommandsPerMinute");

            // Apply rate limiting...
        }
    }
}
```

### 5. Subscribe to Changes (Optional)

If your setting should apply in real-time without restart:

```csharp
public class MyService
{
    private readonly ISettingsService _settingsService;

    public MyService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _settingsService.SettingsChanged += OnSettingsChangedAsync;
    }

    private void OnSettingsChangedAsync(object? sender, SettingsChangedEventArgs e)
    {
        if (e.UpdatedKeys.Contains("Features:RateLimitEnabled") ||
            e.UpdatedKeys.Contains("Advanced:MaxCommandsPerMinute"))
        {
            // Reload settings and apply immediately
            _ = ReloadRateLimitSettingsAsync();
        }
    }
}
```

### 6. Test the Setting

1. Run the application
2. Navigate to `/Admin/Settings`
3. Verify the new setting appears in the correct category
4. Test saving, resetting, and validation
5. Verify the setting value is retrieved correctly in code

---

## Security Considerations

### Authorization

- Page requires `Admin` or `SuperAdmin` role
- Authorization checked on GET and all POST handlers
- Users without permission receive `403 Forbidden`

```csharp
[Authorize(Policy = "RequireAdmin")]
public class SettingsModel : PageModel
{
    public async Task<IActionResult> OnPostSaveCategoryAsync(string category)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
        {
            _logger.LogWarning("Non-admin user {UserId} attempted to save settings",
                User.Identity?.Name);
            return Forbid();
        }
        // ...
    }
}
```

### Anti-Forgery Protection

All POST requests require CSRF tokens:

```html
<form method="post" id="settingsForm">
    @Html.AntiForgeryToken()
    <!-- Settings inputs... -->
</form>
```

```javascript
const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

fetch('?handler=SaveCategory', {
    method: 'POST',
    headers: {
        'RequestVerificationToken': token
    },
    body: formData
});
```

### Input Validation

Settings are validated on the server before being saved:

1. **Type validation**: Values must match the setting's `DataType`
2. **Range validation**: Numeric values must fall within min/max constraints
3. **Allowed values**: Dropdown settings must use one of the allowed values
4. **JSON validation**: JSON settings must be valid JSON

### Audit Logging

All setting changes are logged with user attribution:

```csharp
_logger.LogInformation("Settings saved successfully for category {Category} by user {UserId}. Updated keys: {Keys}",
    category, userId, string.Join(", ", result.UpdatedKeys));
```

The `ApplicationSetting` entity tracks:
- `LastModifiedAt`: Timestamp of last change
- `LastModifiedBy`: User ID who made the change

---

## Error Handling

### Validation Errors

Validation errors are returned to the client:

```json
{
    "success": false,
    "message": "Failed to save settings.",
    "errors": [
        "Message Log Retention (Days): Value must be at least 1",
        "Audit Log Retention (Days): Value must be at most 365"
    ]
}
```

### Exception Handling

Exceptions are caught and logged:

```csharp
try
{
    var result = await _settingsService.UpdateSettingsAsync(updateDto, userId);
    // ...
}
catch (Exception ex)
{
    _logger.LogError(ex, "Exception occurred while saving settings for category {Category}", category);

    return new JsonResult(new
    {
        success = false,
        message = "An error occurred while saving settings. Please check logs."
    })
    {
        StatusCode = 500
    };
}
```

### User Feedback

Errors are displayed via toast notifications:

```javascript
if (response.ok && data.success) {
    window.quickActions?.showToast(data.message, 'success');
} else {
    const errorMsg = data.errors ? data.errors.join(', ') : data.message;
    window.quickActions?.showToast(errorMsg || 'Failed to save settings.', 'error');
}
```

---

## Performance Considerations

### Caching

The `SettingsService` does not implement caching because:
- Settings are infrequently read (primarily at startup and on-demand)
- Real-time updates require fresh data
- The database is the source of truth

If caching is added in the future:
- Invalidate cache on `UpdateSettingsAsync`, `ResetCategoryAsync`, `ResetAllAsync`
- Use distributed cache (Redis) for multi-instance deployments
- Short expiration (1-5 minutes) to balance performance and freshness

### Database Queries

Settings queries are efficient:
- Primary key lookups for single settings: `GetByKeyAsync(key)`
- Category index for category queries: `GetByCategoryAsync(category)`
- Small data set (typically < 50 settings total)

### Restart Pending Flag

The in-memory `IsRestartPending` flag is efficient but:
- Only works for single-instance deployments
- Cleared on app restart (expected behavior)
- For multi-instance deployments, use a distributed cache or database flag

---

## Testing

### Unit Tests

Test the `SettingsService` methods:

```csharp
[Fact]
public async Task GetSettingValueAsync_ReturnsDbValue_WhenExists()
{
    // Arrange
    var setting = new ApplicationSetting
    {
        Key = "General:BotEnabled",
        Value = "false"
    };
    _mockRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), default))
        .ReturnsAsync(setting);

    // Act
    var result = await _settingsService.GetSettingValueAsync<bool>("General:BotEnabled");

    // Assert
    Assert.False(result);
}

[Fact]
public async Task UpdateSettingsAsync_SetsRestartPending_WhenRequiresRestart()
{
    // Arrange: Setting with RequiresRestart = true
    var updates = new SettingsUpdateDto
    {
        Settings = new Dictionary<string, string>
        {
            ["General:SomeSetting"] = "value"
        }
    };

    // Act
    var result = await _settingsService.UpdateSettingsAsync(updates, "testuser");

    // Assert
    Assert.True(result.RestartRequired);
    Assert.True(_settingsService.IsRestartPending);
}
```

### Integration Tests

Test the Settings page handlers:

```csharp
[Fact]
public async Task SaveCategory_UpdatesSettings_ReturnsSuccess()
{
    // Arrange
    var formData = new Dictionary<string, string>
    {
        ["FormSettings[General:BotEnabled]"] = "false"
    };

    // Act
    var result = await _pageModel.OnPostSaveCategoryAsync("General");

    // Assert
    var jsonResult = Assert.IsType<JsonResult>(result);
    dynamic data = jsonResult.Value;
    Assert.True(data.success);
}
```

### UI Tests

Test JavaScript functionality:

```javascript
describe('settingsManager', () => {
    it('should build form data with checkbox values', () => {
        const formData = buildFormData(document.getElementById('settingsForm'));

        // Unchecked checkbox should still be present with 'false' value
        expect(formData.get('FormSettings[General:BotEnabled]')).toBe('false');
    });

    it('should warn before tab switch if dirty', () => {
        isDirty = true;
        window.confirm = jest.fn(() => false);

        switchTab('Features');

        expect(window.confirm).toHaveBeenCalled();
    });
});
```

---

## Related Documentation

- [Design System](design-system.md) - UI color palette, typography, components
- [Authorization Policies](authorization-policies.md) - Role-based access control
- [API Endpoints](api-endpoints.md) - REST API documentation (if settings exposed via API)
- [Database Schema](database-schema.md) - ApplicationSetting entity details
- [Environment Configuration](environment-configuration.md) - appsettings.json structure

---

## Change Log

| Version | Date | Changes |
|---------|------|---------|
| 1.1 | 2025-12-30 | Added RatWatchEnabled setting, updated code examples to match implementation |
| 1.0 | 2025-12-29 | Initial documentation for Settings page overhaul |
