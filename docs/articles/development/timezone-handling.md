# Timezone Handling

**Last Updated:** 2025-12-27
**Feature Reference:** Issue #319
**Status:** Implemented

---

## Overview

The Discord Bot Management System implements robust timezone handling to ensure that all time-based data is displayed accurately to users regardless of their geographic location. The system automatically detects the user's browser timezone and converts UTC-stored timestamps to local time for display and user input.

### Key Features

- **Automatic timezone detection** using browser `Intl.DateTimeFormat` API
- **UTC storage** for all timestamps in the database
- **Client-side conversion** for display using JavaScript
- **Server-side conversion** for user input using C# TimezoneHelper
- **Transparent handling** requiring minimal developer intervention
- **IANA timezone support** (e.g., "America/New_York", "Europe/London")
- **Daylight Saving Time (DST) aware** conversions

### Why Timezone Handling Matters

Without proper timezone handling:

- Users in different timezones see incorrect times
- Scheduled operations execute at wrong times for users
- Audit logs and timestamps are confusing
- Data comparisons across timezones are inaccurate

With proper timezone handling:

- All users see times in their local timezone
- Server stores all times in UTC (standard practice)
- Timezone conversions are automatic and transparent
- Daylight Saving Time transitions are handled correctly

---

## Architecture

The timezone handling system consists of three main components working together:

### 1. UTC Storage (Database)

All timestamps are stored in UTC in the database.

**Benefits:**
- **Consistency:** Single source of truth for all timestamps
- **Portability:** Easy to migrate servers across timezones
- **Calculations:** Simplified time math and comparisons
- **Standards:** Follows industry best practices

**Example Database Values:**

```sql
-- All stored as UTC
CreatedAt:     2025-12-27 15:30:00 UTC
UpdatedAt:     2025-12-27 16:45:00 UTC
ScheduledFor:  2025-12-28 09:00:00 UTC
```

### 2. Browser Detection (JavaScript)

The browser automatically detects the user's timezone using the JavaScript `Intl.DateTimeFormat` API.

**Detection Process:**

1. Page loads with `timezone.js` script
2. Script detects timezone: `Intl.DateTimeFormat().resolvedOptions().timeZone`
3. Populates hidden form fields with detected timezone
4. Converts `[data-utc]` elements to local time for display

**Detected Values:**

```javascript
// Examples of detected timezone identifiers
"America/New_York"      // Eastern Time (US)
"Europe/London"         // British Time
"Asia/Tokyo"            // Japan Standard Time
"Australia/Sydney"      // Australian Eastern Time
```

### 3. Server Conversion (C# TimezoneHelper)

The server converts between UTC and user's local timezone when processing form submissions.

**Conversion Flow:**

```
User Input (Local Time)
    ↓
JavaScript sends: DateTime + Timezone
    ↓
Server receives: "2025-12-27T10:30" + "America/New_York"
    ↓
TimezoneHelper.ConvertToUtc()
    ↓
Database stores: 2025-12-27 15:30:00 UTC
```

---

## TimezoneHelper Utility

**Location:** `src/DiscordBot.Core/Utilities/TimezoneHelper.cs`

The `TimezoneHelper` static class provides timezone conversion utilities using IANA timezone names.

### Methods

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `ConvertToUtc` | `DateTime localDateTime`<br/>`string? ianaTimezoneName` | `DateTime` | Converts local DateTime to UTC |
| `ConvertFromUtc` | `DateTime utcDateTime`<br/>`string? ianaTimezoneName` | `DateTime` | Converts UTC DateTime to local time |
| `IsValidTimezone` | `string? ianaTimezoneName` | `bool` | Validates IANA timezone identifier |
| `GetTimeZoneInfo` | `string? ianaTimezoneName` | `TimeZoneInfo` | Gets TimeZoneInfo, fallback to UTC |
| `GetTimezoneAbbreviation` | `string? ianaTimezoneName`<br/>`DateTime forDateTime` | `string` | Gets timezone abbreviation (e.g., "EST") |

### ConvertToUtc

Converts a local DateTime to UTC using the specified IANA timezone.

**Signature:**

```csharp
public static DateTime ConvertToUtc(DateTime localDateTime, string? ianaTimezoneName)
```

**Parameters:**

- `localDateTime`: The local datetime in the specified timezone. The `DateTimeKind` will be ignored.
- `ianaTimezoneName`: The IANA timezone identifier (e.g., "America/New_York")

**Returns:**

- UTC datetime with `DateTimeKind.Utc`

**Behavior:**

- If `ianaTimezoneName` is null or empty, assumes input is already UTC
- If timezone is invalid, treats input as UTC (fallback)
- Properly handles Daylight Saving Time transitions

**Usage Example:**

```csharp
// User in New York selects: 2025-12-27 10:30 AM
var localTime = new DateTime(2025, 12, 27, 10, 30, 0);
var timezone = "America/New_York";

var utc = TimezoneHelper.ConvertToUtc(localTime, timezone);
// Result: 2025-12-27 15:30:00 UTC (EST is UTC-5)

// Save to database
entity.ScheduledAt = utc;
```

**DST Example:**

```csharp
// During DST (summer): EDT is UTC-4
var summerTime = new DateTime(2025, 7, 15, 10, 30, 0);
var utc = TimezoneHelper.ConvertToUtc(summerTime, "America/New_York");
// Result: 2025-07-15 14:30:00 UTC (EDT is UTC-4)

// During standard time (winter): EST is UTC-5
var winterTime = new DateTime(2025, 12, 27, 10, 30, 0);
utc = TimezoneHelper.ConvertToUtc(winterTime, "America/New_York");
// Result: 2025-12-27 15:30:00 UTC (EST is UTC-5)
```

### ConvertFromUtc

Converts a UTC DateTime to local time in the specified IANA timezone.

**Signature:**

```csharp
public static DateTime ConvertFromUtc(DateTime utcDateTime, string? ianaTimezoneName)
```

**Parameters:**

- `utcDateTime`: The UTC datetime
- `ianaTimezoneName`: The IANA timezone identifier

**Returns:**

- Local datetime in the specified timezone

**Behavior:**

- If `ianaTimezoneName` is null or empty, returns UTC unchanged
- If timezone is invalid, returns UTC unchanged (fallback)
- Properly handles Daylight Saving Time

**Usage Example:**

```csharp
// Load from database (stored as UTC)
var utcTime = entity.ScheduledAt; // 2025-12-27 15:30:00 UTC

// Convert for user in New York
var timezone = "America/New_York";
var localTime = TimezoneHelper.ConvertFromUtc(utcTime, timezone);
// Result: 2025-12-27 10:30:00 (EST)

// Display to user
var formatted = localTime.ToString("yyyy-MM-dd HH:mm");
// Output: "2025-12-27 10:30"
```

### IsValidTimezone

Validates whether a string is a valid IANA timezone identifier.

**Signature:**

```csharp
public static bool IsValidTimezone(string? ianaTimezoneName)
```

**Parameters:**

- `ianaTimezoneName`: The timezone identifier to validate

**Returns:**

- `true` if valid, `false` otherwise

**Usage Example:**

```csharp
// Valid timezones
TimezoneHelper.IsValidTimezone("America/New_York");     // true
TimezoneHelper.IsValidTimezone("Europe/London");        // true
TimezoneHelper.IsValidTimezone("UTC");                  // true

// Invalid timezones
TimezoneHelper.IsValidTimezone(null);                   // false
TimezoneHelper.IsValidTimezone("");                     // false
TimezoneHelper.IsValidTimezone("Invalid/Timezone");     // false
TimezoneHelper.IsValidTimezone("PST");                  // false (abbreviations not valid)
```

**Use Cases:**

- Validate user input before processing
- Check timezone from external APIs
- Fallback validation in form handlers

```csharp
if (!TimezoneHelper.IsValidTimezone(userTimezone))
{
    // Use default or show error
    userTimezone = "UTC";
}
```

### GetTimezoneAbbreviation

Gets the timezone abbreviation for display, considering Daylight Saving Time.

**Signature:**

```csharp
public static string GetTimezoneAbbreviation(string? ianaTimezoneName, DateTime forDateTime)
```

**Parameters:**

- `ianaTimezoneName`: The IANA timezone identifier
- `forDateTime`: The datetime to get the abbreviation for (determines DST status)

**Returns:**

- Timezone abbreviation string (e.g., "EST", "EDT", "PST")
- "UTC" if timezone is null, empty, or invalid

**Usage Example:**

```csharp
var timezone = "America/New_York";

// During standard time
var winterDate = new DateTime(2025, 12, 27, 10, 0, 0);
var abbr = TimezoneHelper.GetTimezoneAbbreviation(timezone, winterDate);
// Result: "Eastern Standard Time" (full name, not abbreviation in current implementation)

// During daylight saving time
var summerDate = new DateTime(2025, 7, 15, 10, 0, 0);
abbr = TimezoneHelper.GetTimezoneAbbreviation(timezone, summerDate);
// Result: "Eastern Daylight Time" (full name)
```

**Note:** The current implementation returns the full timezone name (e.g., "Eastern Standard Time") rather than the short abbreviation (e.g., "EST"). This is due to limitations in .NET's `TimeZoneInfo` API. For short abbreviations, use the JavaScript `timezoneUtils.getTimezoneAbbreviation()` function.

---

## JavaScript Module (timezone.js)

**Location:** `src/DiscordBot.Bot/wwwroot/js/timezone.js`

The JavaScript timezone utilities module provides client-side timezone detection and display formatting.

### Global Object: window.timezoneUtils

All functions are accessible via the global `timezoneUtils` object.

```javascript
// Available globally after script loads
window.timezoneUtils.getTimezone()
window.timezoneUtils.formatLocalTime(utcString)
// etc.
```

### Functions

| Function | Parameters | Returns | Description |
|----------|------------|---------|-------------|
| `getTimezone()` | None | `string` | Gets user's IANA timezone |
| `getTimezoneAbbreviation()` | None | `string` | Gets timezone abbreviation |
| `formatLocalTime(utcIsoString, options)` | `string`, `object?` | `string` | Formats UTC time to local |
| `initTimezoneFields()` | None | `void` | Initializes hidden timezone fields |
| `convertDisplayTimes()` | None | `void` | Converts `[data-utc]` elements |
| `setDateTimeLocalFromUtc(inputId, utcIsoString)` | `string`, `string` | `void` | Sets datetime-local input from UTC |
| `setDefaultDateTime(inputId, offsetMinutes)` | `string`, `number` | `void` | Sets default datetime value |

### getTimezone()

Gets the user's IANA timezone name from the browser.

**Signature:**

```javascript
getTimezone: function()
```

**Returns:**

- IANA timezone identifier string (e.g., "America/New_York")
- "UTC" if detection fails

**Usage Example:**

```javascript
var userTimezone = timezoneUtils.getTimezone();
console.log(userTimezone);
// Output: "America/New_York" (or user's actual timezone)

// Use in form submission
document.getElementById('userTimezoneField').value = userTimezone;
```

### getTimezoneAbbreviation()

Gets a display-friendly timezone abbreviation (e.g., "EST", "PST").

**Signature:**

```javascript
getTimezoneAbbreviation: function()
```

**Returns:**

- Timezone abbreviation string (e.g., "EST", "EDT")
- Empty string if detection fails

**Usage Example:**

```javascript
var abbr = timezoneUtils.getTimezoneAbbreviation();
console.log(abbr);
// Output: "EST" or "EDT" depending on current date

// Display to user
document.querySelector('.timezone-label').textContent = `Times shown in ${abbr}`;
```

### formatLocalTime()

Converts a UTC ISO string to a formatted local time string.

**Signature:**

```javascript
formatLocalTime: function(utcIsoString, options)
```

**Parameters:**

- `utcIsoString`: UTC timestamp in ISO format (e.g., "2025-12-27T15:30:00Z")
- `options`: Optional `Intl.DateTimeFormat` options object

**Returns:**

- Formatted local time string

**Default Options:**

```javascript
{
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    hour12: true
}
```

**Usage Examples:**

```javascript
// Default format
var utc = "2025-12-27T15:30:00Z";
var local = timezoneUtils.formatLocalTime(utc);
// Output: "Dec 27, 2025, 10:30 AM" (for EST user)

// Custom format (date only)
local = timezoneUtils.formatLocalTime(utc, {
    year: 'numeric',
    month: 'long',
    day: 'numeric'
});
// Output: "December 27, 2025"

// Custom format (time only)
local = timezoneUtils.formatLocalTime(utc, {
    hour: 'numeric',
    minute: '2-digit',
    hour12: true
});
// Output: "10:30 AM"
```

### initTimezoneFields()

Initializes all timezone hidden fields on the page with the user's detected timezone.

**Signature:**

```javascript
initTimezoneFields: function()
```

**Behavior:**

- Finds all `input[name$="UserTimezone"]` elements
- Sets their `value` to the detected timezone
- Updates `.timezone-indicator` elements with timezone display name and abbreviation

**Auto-execution:** This function runs automatically on page load via `DOMContentLoaded` event.

**Usage Example:**

```html
<!-- HTML form with timezone field -->
<form>
    <input type="hidden" name="Input.UserTimezone" />
    <div class="timezone-indicator"></div>
</form>

<!-- JavaScript auto-populates on page load -->
<!-- Result: Input value = "America/New_York" -->
<!-- Result: Indicator text = "America/New_York (EST)" -->
```

**Manual Call:**

```javascript
// Re-initialize after dynamically adding forms
timezoneUtils.initTimezoneFields();
```

### convertDisplayTimes()

Converts all elements with `data-utc` attribute to local time for display.

**Signature:**

```javascript
convertDisplayTimes: function()
```

**Behavior:**

- Finds all elements with `data-utc` attribute
- Reads UTC timestamp from attribute
- Converts to local time based on `data-format` attribute
- Updates element's `textContent` with formatted local time

**Auto-execution:** This function runs automatically on page load.

**Supported Formats (via `data-format` attribute):**

| Format | Output Example |
|--------|----------------|
| `datetime` (default) | "Dec 27, 2025, 10:30 AM" |
| `date` | "Dec 27, 2025" |
| `time` | "10:30 AM" |

**Usage Example:**

```html
<!-- UTC timestamp in attribute -->
<span data-utc="2025-12-27T15:30:00Z">Loading...</span>

<!-- After convertDisplayTimes() runs -->
<span data-utc="2025-12-27T15:30:00Z">Dec 27, 2025, 10:30 AM</span>

<!-- Date only -->
<span data-utc="2025-12-27T15:30:00Z" data-format="date">Dec 27, 2025</span>

<!-- Time only -->
<span data-utc="2025-12-27T15:30:00Z" data-format="time">10:30 AM</span>
```

**Manual Call:**

```javascript
// Re-convert after adding new elements with data-utc
timezoneUtils.convertDisplayTimes();
```

### setDateTimeLocalFromUtc()

Sets a `datetime-local` input value from a UTC timestamp.

**Signature:**

```javascript
setDateTimeLocalFromUtc: function(inputId, utcIsoString)
```

**Parameters:**

- `inputId`: The ID of the `<input type="datetime-local">` element
- `utcIsoString`: UTC timestamp in ISO format

**Usage Example:**

```html
<input type="datetime-local" id="scheduledTime" />

<script>
// Set input to UTC time converted to user's local time
var utc = "2025-12-27T15:30:00Z";
timezoneUtils.setDateTimeLocalFromUtc('scheduledTime', utc);
// Input now shows: 2025-12-27T10:30 (for EST user)
</script>
```

**Use Cases:**

- Pre-filling edit forms with existing timestamps
- Setting initial values from server-side data
- Updating inputs after AJAX responses

### setDefaultDateTime()

Sets a default datetime value for an input, offset by minutes from now.

**Signature:**

```javascript
setDefaultDateTime: function(inputId, offsetMinutes)
```

**Parameters:**

- `inputId`: The ID of the `<input type="datetime-local">` element
- `offsetMinutes`: Minutes to add to current time (default: 5)

**Behavior:**

- Only sets value if input is currently empty
- Rounds to next 5-minute interval
- Clears seconds and milliseconds

**Usage Example:**

```html
<input type="datetime-local" id="scheduledTime" />

<script>
// Set default to 30 minutes from now (rounded to 5-min interval)
timezoneUtils.setDefaultDateTime('scheduledTime', 30);

// If current time is 10:23, input shows 10:55
// If current time is 10:55, input shows 11:25
</script>
```

---

## Usage Examples

### Complete Form Example

This example shows a complete Razor Page form with timezone handling for a scheduled message feature.

**Razor Page (CreateScheduledMessage.cshtml):**

```cshtml
@page
@model CreateScheduledMessageModel
@{
    ViewData["Title"] = "Schedule Message";
}

<h1>Schedule Message</h1>

<form method="post">
    <!-- Message content -->
    <div class="form-group">
        <label asp-for="Input.Message" class="form-label"></label>
        <textarea asp-for="Input.Message" class="form-input" rows="5"></textarea>
        <span asp-validation-for="Input.Message" class="form-error"></span>
    </div>

    <!-- Scheduled time (datetime-local input) -->
    <div class="form-group">
        <label asp-for="Input.ScheduledAt" class="form-label">Schedule For</label>
        <input asp-for="Input.ScheduledAt" type="datetime-local" class="form-input" id="scheduledTimeInput" />
        <span asp-validation-for="Input.ScheduledAt" class="form-error"></span>

        <!-- Timezone indicator shows user's timezone -->
        <span class="form-help timezone-indicator"></span>
    </div>

    <!-- Hidden timezone field (auto-populated by JavaScript) -->
    <input asp-for="Input.UserTimezone" type="hidden" />

    <button type="submit" class="btn btn-primary">Schedule Message</button>
</form>

@section Scripts {
    <!-- Load timezone utilities -->
    <script src="~/js/timezone.js"></script>
    <script>
        // Set default time to 5 minutes from now
        timezoneUtils.setDefaultDateTime('scheduledTimeInput', 5);
    </script>
}
```

**PageModel (CreateScheduledMessage.cshtml.cs):**

```csharp
using DiscordBot.Core.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class CreateScheduledMessageModel : PageModel
{
    private readonly IScheduledMessageService _messageService;

    public CreateScheduledMessageModel(IScheduledMessageService messageService)
    {
        _messageService = messageService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = default!;

    public class InputModel
    {
        [Required]
        [StringLength(2000)]
        public string Message { get; set; } = string.Empty;

        [Required]
        public DateTime ScheduledAt { get; set; }

        // User's timezone from browser (auto-populated by JS)
        public string? UserTimezone { get; set; }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Convert user's local time to UTC for storage
        var scheduledAtUtc = TimezoneHelper.ConvertToUtc(
            Input.ScheduledAt,
            Input.UserTimezone
        );

        // Create scheduled message with UTC timestamp
        var message = new ScheduledMessage
        {
            Content = Input.Message,
            ScheduledAt = scheduledAtUtc, // Stored in UTC
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _messageService.CreateAsync(message);

        TempData["SuccessMessage"] = "Message scheduled successfully!";
        return RedirectToPage("./Index");
    }
}
```

### Display Existing Timestamps

Show existing timestamps from the database in the user's local timezone.

**Razor Page (ViewScheduledMessages.cshtml):**

```cshtml
@page
@model ViewScheduledMessagesModel

<h1>Scheduled Messages</h1>

<table class="table">
    <thead>
        <tr>
            <th>Message</th>
            <th>Scheduled For</th>
            <th>Created</th>
            <th>Actions</th>
        </tr>
    </thead>
    <tbody>
        @foreach (var msg in Model.ScheduledMessages)
        {
            <tr>
                <td>@msg.Content</td>
                <td>
                    <!-- UTC value in data-utc, JavaScript converts to local time -->
                    <span data-utc="@msg.ScheduledAt.ToString("o")">
                        @msg.ScheduledAt.ToString("yyyy-MM-dd HH:mm")
                    </span>
                </td>
                <td>
                    <span data-utc="@msg.CreatedAt.ToString("o")" data-format="datetime">
                        @msg.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                    </span>
                </td>
                <td>
                    <a asp-page="./Edit" asp-route-id="@msg.Id" class="btn btn-sm btn-secondary">Edit</a>
                </td>
            </tr>
        }
    </tbody>
</table>

@section Scripts {
    <script src="~/js/timezone.js"></script>
}
```

**PageModel:**

```csharp
public class ViewScheduledMessagesModel : PageModel
{
    private readonly IScheduledMessageService _messageService;

    public ViewScheduledMessagesModel(IScheduledMessageService messageService)
    {
        _messageService = messageService;
    }

    public List<ScheduledMessage> ScheduledMessages { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Load messages (stored in UTC)
        ScheduledMessages = await _messageService.GetActiveMessagesAsync();
    }
}
```

**Result:**

- User in New York sees: "Dec 27, 2025, 10:30 AM"
- User in London sees: "Dec 27, 2025, 3:30 PM"
- User in Tokyo sees: "Dec 28, 2025, 12:30 AM"

All showing the same moment in time, just in their local timezone.

### Edit Form with Existing Value

Pre-fill an edit form with an existing UTC timestamp.

**Razor Page (EditScheduledMessage.cshtml):**

```cshtml
@page "{id:guid}"
@model EditScheduledMessageModel

<h1>Edit Scheduled Message</h1>

<form method="post">
    <div class="form-group">
        <label asp-for="Input.Message" class="form-label"></label>
        <textarea asp-for="Input.Message" class="form-input" rows="5"></textarea>
    </div>

    <div class="form-group">
        <label asp-for="Input.ScheduledAt" class="form-label">Schedule For</label>
        <input asp-for="Input.ScheduledAt" type="datetime-local" class="form-input" id="scheduledTimeInput" />
        <span class="form-help timezone-indicator"></span>
    </div>

    <input asp-for="Input.UserTimezone" type="hidden" />

    <button type="submit" class="btn btn-primary">Save Changes</button>
</form>

@section Scripts {
    <script src="~/js/timezone.js"></script>
    <script>
        // Pre-fill with existing UTC timestamp (converted to local)
        @if (Model.Message?.ScheduledAt != null)
        {
            <text>
            var utcTime = '@Model.Message.ScheduledAt.ToString("o")';
            timezoneUtils.setDateTimeLocalFromUtc('scheduledTimeInput', utcTime);
            </text>
        }
    </script>
}
```

**PageModel:**

```csharp
public class EditScheduledMessageModel : PageModel
{
    private readonly IScheduledMessageService _messageService;

    public EditScheduledMessageModel(IScheduledMessageService messageService)
    {
        _messageService = messageService;
    }

    public ScheduledMessage? Message { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = default!;

    public class InputModel
    {
        public string Message { get; set; } = string.Empty;
        public DateTime ScheduledAt { get; set; }
        public string? UserTimezone { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Message = await _messageService.GetByIdAsync(id);
        if (Message == null)
        {
            return NotFound();
        }

        // Pre-populate form
        Input = new InputModel
        {
            Message = Message.Content,
            // ScheduledAt will be set by JavaScript to local time
            ScheduledAt = Message.ScheduledAt
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var message = await _messageService.GetByIdAsync(id);
        if (message == null)
        {
            return NotFound();
        }

        // Convert user's local time to UTC
        var scheduledAtUtc = TimezoneHelper.ConvertToUtc(
            Input.ScheduledAt,
            Input.UserTimezone
        );

        message.Content = Input.Message;
        message.ScheduledAt = scheduledAtUtc;
        message.UpdatedAt = DateTime.UtcNow;

        await _messageService.UpdateAsync(message);

        return RedirectToPage("./Index");
    }
}
```

---

## Troubleshooting

### Common Issues and Solutions

#### Issue: Times showing as UTC instead of local time

**Symptoms:**
- Timestamps display in UTC instead of user's local timezone
- All times show the same regardless of user location

**Causes:**
1. JavaScript `timezone.js` not loaded
2. `[data-utc]` attribute missing or incorrect
3. `convertDisplayTimes()` not called

**Solutions:**

1. **Verify Script is Loaded:**

```cshtml
@section Scripts {
    <script src="~/js/timezone.js"></script>
}
```

2. **Check data-utc Attribute:**

```html
<!-- Correct -->
<span data-utc="2025-12-27T15:30:00Z">Placeholder</span>

<!-- Incorrect (missing 'Z' for UTC) -->
<span data-utc="2025-12-27T15:30:00">Placeholder</span>
```

3. **Verify Auto-Initialization:**

Open browser console and check:

```javascript
console.log(window.timezoneUtils); // Should be defined
timezoneUtils.convertDisplayTimes(); // Manually trigger conversion
```

#### Issue: Form submission uses wrong timezone

**Symptoms:**
- Scheduled times execute at wrong time
- Database stores incorrect UTC timestamps

**Causes:**
1. Hidden timezone field not populated
2. Server not using TimezoneHelper.ConvertToUtc()
3. User's browser timezone is incorrect

**Solutions:**

1. **Check Hidden Field:**

```cshtml
<!-- Must have name ending with "UserTimezone" -->
<input asp-for="Input.UserTimezone" type="hidden" />
```

Browser console:
```javascript
console.log(document.querySelector('[name$="UserTimezone"]').value);
// Should output: "America/New_York" or similar
```

2. **Verify Server Conversion:**

```csharp
// WRONG - stores user's local time as UTC
entity.ScheduledAt = Input.ScheduledAt;

// CORRECT - converts to UTC first
entity.ScheduledAt = TimezoneHelper.ConvertToUtc(
    Input.ScheduledAt,
    Input.UserTimezone
);
```

3. **Check User's System Timezone:**

Ask user to verify their system timezone settings are correct.

#### Issue: Timezone indicator not showing

**Symptoms:**
- `.timezone-indicator` element is empty
- No timezone abbreviation displayed

**Causes:**
1. Element with class `.timezone-indicator` doesn't exist
2. JavaScript failed to detect timezone
3. `initTimezoneFields()` not called

**Solutions:**

1. **Add Indicator Element:**

```cshtml
<div class="form-group">
    <label>Scheduled Time</label>
    <input type="datetime-local" />
    <span class="form-help timezone-indicator"></span>
</div>
```

2. **Check Browser Compatibility:**

```javascript
// Test timezone detection
console.log(Intl.DateTimeFormat().resolvedOptions().timeZone);
// Should output timezone name
```

3. **Manually Trigger:**

```javascript
timezoneUtils.initTimezoneFields();
```

#### Issue: Daylight Saving Time transitions incorrect

**Symptoms:**
- Times are off by 1 hour during DST transitions
- Scheduled operations execute at wrong time near DST boundaries

**Causes:**
1. Using fixed UTC offset instead of timezone
2. Not accounting for DST in calculations

**Solutions:**

**NEVER use fixed offsets:**

```csharp
// WRONG - breaks during DST
var utc = localTime.AddHours(5); // Assumes EST always UTC-5

// CORRECT - handles DST automatically
var utc = TimezoneHelper.ConvertToUtc(localTime, "America/New_York");
```

**TimezoneHelper handles DST automatically:**

```csharp
// March 10, 2:30 AM doesn't exist (DST spring forward)
var localTime = new DateTime(2025, 3, 10, 2, 30, 0);
var utc = TimezoneHelper.ConvertToUtc(localTime, "America/New_York");
// Handles ambiguous time correctly

// November 3, 1:30 AM occurs twice (DST fall back)
localTime = new DateTime(2025, 11, 3, 1, 30, 0);
utc = TimezoneHelper.ConvertToUtc(localTime, "America/New_York");
// Uses standard time by default
```

#### Issue: Invalid timezone error

**Symptoms:**
- TimezoneHelper throws exception
- Fallback to UTC unexpectedly

**Causes:**
1. Using timezone abbreviation instead of IANA name
2. Typo in timezone name
3. Old/deprecated timezone identifier

**Solutions:**

**Use IANA timezone identifiers:**

```csharp
// WRONG - abbreviations not valid
var tz = "EST";
var tz = "PST";

// CORRECT - IANA identifiers
var tz = "America/New_York";
var tz = "America/Los_Angeles";
```

**Validate before using:**

```csharp
if (!TimezoneHelper.IsValidTimezone(userTimezone))
{
    logger.LogWarning("Invalid timezone: {Timezone}, using UTC", userTimezone);
    userTimezone = "UTC";
}
```

**Common valid timezones:**
- North America: `America/New_York`, `America/Chicago`, `America/Denver`, `America/Los_Angeles`
- Europe: `Europe/London`, `Europe/Paris`, `Europe/Berlin`
- Asia: `Asia/Tokyo`, `Asia/Shanghai`, `Asia/Kolkata`
- Australia: `Australia/Sydney`, `Australia/Melbourne`

---

## Testing Timezone Handling

### Manual Testing Checklist

Test timezone handling across different scenarios:

#### Test 1: Browser Detection

1. Load page with timezone form
2. Open browser console
3. Verify timezone detected:
   ```javascript
   console.log(timezoneUtils.getTimezone());
   // Should output your timezone (e.g., "America/New_York")
   ```
4. Check hidden field populated:
   ```javascript
   console.log(document.querySelector('[name$="UserTimezone"]').value);
   ```

#### Test 2: Display Conversion

1. Create element with UTC timestamp:
   ```html
   <span data-utc="2025-12-27T15:30:00Z">Loading...</span>
   ```
2. Refresh page
3. Verify element shows local time (e.g., "Dec 27, 2025, 10:30 AM" for EST)

#### Test 3: Form Submission

1. Fill out form with datetime-local input
2. Select time: "2025-12-27 10:30"
3. Submit form
4. Check database value is UTC:
   ```sql
   -- For EST user selecting 10:30 AM:
   -- Database should show: 2025-12-27 15:30:00
   ```

#### Test 4: Edit Form Pre-fill

1. Load edit form for existing record
2. Verify datetime-local input shows local time
3. Database has: `2025-12-27 15:30:00 UTC`
4. Input should show: `2025-12-27T10:30` (for EST user)

#### Test 5: DST Transition

1. Test date during DST (e.g., July 15)
   - EST offset: UTC-4 (EDT)
2. Test date during standard time (e.g., December 27)
   - EST offset: UTC-5 (EST)
3. Verify conversion uses correct offset

### Automated Testing

**Unit Test Example (TimezoneHelper):**

```csharp
[Fact]
public void ConvertToUtc_WithValidTimezone_ConvertsCorrectly()
{
    // Arrange
    var localTime = new DateTime(2025, 12, 27, 10, 30, 0);
    var timezone = "America/New_York";

    // Act
    var utc = TimezoneHelper.ConvertToUtc(localTime, timezone);

    // Assert
    Assert.Equal(new DateTime(2025, 12, 27, 15, 30, 0, DateTimeKind.Utc), utc);
    Assert.Equal(DateTimeKind.Utc, utc.Kind);
}

[Fact]
public void ConvertToUtc_WithDST_HandlesCorrectly()
{
    // Arrange - Summer time (EDT, UTC-4)
    var summerTime = new DateTime(2025, 7, 15, 10, 30, 0);
    var timezone = "America/New_York";

    // Act
    var utc = TimezoneHelper.ConvertToUtc(summerTime, timezone);

    // Assert - Should be UTC-4, not UTC-5
    Assert.Equal(new DateTime(2025, 7, 15, 14, 30, 0, DateTimeKind.Utc), utc);
}

[Fact]
public void ConvertToUtc_WithInvalidTimezone_FallsBackToUtc()
{
    // Arrange
    var localTime = new DateTime(2025, 12, 27, 10, 30, 0);
    var invalidTimezone = "Invalid/Timezone";

    // Act
    var utc = TimezoneHelper.ConvertToUtc(localTime, invalidTimezone);

    // Assert - Should treat as UTC (no conversion)
    Assert.Equal(new DateTime(2025, 12, 27, 10, 30, 0, DateTimeKind.Utc), utc);
}
```

**Integration Test Example:**

```csharp
[Fact]
public async Task CreateScheduledMessage_WithTimezone_StoresUtcCorrectly()
{
    // Arrange
    var client = _factory.CreateClient();
    var localTime = "2025-12-27T10:30";
    var timezone = "America/New_York";

    var formData = new Dictionary<string, string>
    {
        ["Input.Message"] = "Test message",
        ["Input.ScheduledAt"] = localTime,
        ["Input.UserTimezone"] = timezone
    };

    // Act
    var response = await client.PostAsync("/ScheduledMessages/Create",
        new FormUrlEncodedContent(formData));

    // Assert
    response.EnsureSuccessStatusCode();

    var message = await _dbContext.ScheduledMessages.FirstAsync();
    var expectedUtc = new DateTime(2025, 12, 27, 15, 30, 0, DateTimeKind.Utc);
    Assert.Equal(expectedUtc, message.ScheduledAt);
}
```

---

## Best Practices

### For Developers

1. **Always Store UTC in Database**

```csharp
// DO
entity.CreatedAt = DateTime.UtcNow;
entity.ScheduledAt = TimezoneHelper.ConvertToUtc(localTime, userTimezone);

// DON'T
entity.CreatedAt = DateTime.Now; // Uses server timezone
entity.ScheduledAt = localTime;  // Stores local time as if UTC
```

2. **Use TimezoneHelper for All Conversions**

```csharp
// DO
var utc = TimezoneHelper.ConvertToUtc(localTime, timezone);
var local = TimezoneHelper.ConvertFromUtc(utcTime, timezone);

// DON'T
var utc = localTime.ToUniversalTime(); // Uses server timezone
var utc = localTime.AddHours(5);       // Breaks during DST
```

3. **Include Timezone Field in Forms**

```cshtml
<!-- DO -->
<input asp-for="Input.ScheduledAt" type="datetime-local" />
<input asp-for="Input.UserTimezone" type="hidden" />

<!-- DON'T -->
<input asp-for="Input.ScheduledAt" type="datetime-local" />
<!-- Missing timezone - will use server timezone or fail -->
```

4. **Use data-utc for Display**

```cshtml
<!-- DO -->
<span data-utc="@timestamp.ToString("o")">@timestamp</span>

<!-- DON'T -->
<span>@timestamp.ToString("yyyy-MM-dd HH:mm")</span>
<!-- Shows UTC time, not user's local time -->
```

5. **Validate Timezone Input**

```csharp
// DO
if (!TimezoneHelper.IsValidTimezone(Input.UserTimezone))
{
    ModelState.AddModelError(nameof(Input.UserTimezone), "Invalid timezone");
    return Page();
}

// DON'T
var utc = TimezoneHelper.ConvertToUtc(localTime, Input.UserTimezone);
// May silently fall back to UTC without warning
```

### For Users

1. **Verify System Timezone**
   - Ensure your operating system timezone is set correctly
   - Browser uses system timezone for detection

2. **Check Timezone Indicator**
   - Look for timezone abbreviation near time inputs
   - Verify it matches your expected timezone

3. **Test with Known Time**
   - When scheduling, verify the time makes sense
   - If scheduling for 10:00 AM your time, confirm it's not 10:00 AM UTC

4. **Report Incorrect Times**
   - If times seem off by hours, report to administrator
   - Include your location and browser information

---

## Related Documentation

- [Design System - Timezone-Aware Inputs](design-system.md#timezone-aware-inputs)
- [API Endpoints](api-endpoints.md) - REST API timezone handling
- [Database Schema](database-schema.md) - UTC timestamp storage

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-12-27 | Initial documentation for Issue #319 |

---

**Document Status:** Complete
**Review Required:** No
**Implementation Status:** Implemented (Issue #319)
