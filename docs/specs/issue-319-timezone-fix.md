# Issue #319: Date/Time Locale Issues - Implementation Plan

**Version:** 1.0
**Created:** 2025-12-27
**Issue:** [#319 - Date/Time Locale Issues](https://github.com/cpike5/discordbot/issues/319)
**Status:** Draft

---

## 1. Requirement Summary

When the Discord bot is deployed to a VPS in a different region than the user, scheduled messages execute at incorrect times due to timezone handling issues. The core problem is that:

1. The HTML `datetime-local` input sends no timezone information
2. The server assumes local times are in the server's local timezone (`DateTimeKind.Local`)
3. Default times use `DateTime.Now` (server's local time)
4. Display times convert to server's local timezone, not user's timezone
5. Audit log date filtering has the same issue

**Goal:** Implement client-side timezone detection to ensure scheduled messages execute at the time the user intended, regardless of server location.

---

## 2. Architectural Considerations

### 2.1 Existing System Components

| Component | Location | Current Behavior |
|-----------|----------|------------------|
| `Create.cshtml.cs` | `src/DiscordBot.Bot/Pages/Guilds/ScheduledMessages/` | Uses `DateTime.Now` for defaults, converts via `DateTimeKind.Local` |
| `Edit.cshtml.cs` | `src/DiscordBot.Bot/Pages/Guilds/ScheduledMessages/` | Converts UTC to local using `ToLocalTime()` |
| `ScheduledMessageListViewModel.cs` | `src/DiscordBot.Bot/ViewModels/Pages/` | Uses `ToLocalTime()` for display |
| `Index.cshtml.cs` (AuditLogs) | `src/DiscordBot.Bot/Pages/Admin/AuditLogs/` | Uses `ToUniversalTime()` on date filters |
| `ScheduledMessageService.cs` | `src/DiscordBot.Bot/Services/` | Stores/processes times in UTC |
| `ScheduledMessage.cs` | `src/DiscordBot.Core/Entities/` | Entity with `DateTime` properties |

### 2.2 Integration Requirements

- **No database schema changes** - All timestamps remain stored as UTC
- **JavaScript timezone detection** via `Intl.DateTimeFormat` API
- **Hidden form field** to transmit IANA timezone name (e.g., `America/New_York`)
- **Server-side conversion** using `TimeZoneInfo` with IANA timezone names (.NET 6+ ICU support)
- **Reusable utility class** for timezone conversion operations
- **Shared JavaScript module** for timezone detection and display conversion

### 2.3 Architectural Patterns

- Follow existing `IOptions<T>` pattern for any timezone-related configuration
- Follow existing shared component pattern (`Pages/Shared/Components/`)
- Follow existing JavaScript module pattern (`wwwroot/js/`)
- Maintain backward compatibility with existing data

### 2.4 Security Considerations

- Validate timezone names against known IANA timezone identifiers
- Sanitize timezone input to prevent injection attacks
- Fallback to UTC if timezone is invalid or missing

### 2.5 Performance Considerations

- Timezone detection happens once on page load (cached in hidden field)
- Use static `TimeZoneInfo` lookups (thread-safe, cached by runtime)
- No additional database queries required

---

## 3. Subagent Task Plan

### 3.1 design-specialist

**Objective:** Define UI patterns for timezone-aware datetime inputs.

**Deliverables:**

1. **Update `docs/articles/design-system.md`**
   - Add section for "Timezone-Aware Inputs"
   - Document the hidden timezone field pattern
   - Document the timezone display indicator pattern (showing detected timezone)

2. **Component Specifications**
   - Timezone indicator badge showing user's detected timezone (e.g., "Your timezone: America/New_York (EST)")
   - Styling for timezone helper text under datetime inputs
   - Color: Use `text-text-tertiary` (#7a7876) for timezone indicator

**Acceptance Criteria:**
- Design tokens defined for timezone display elements
- Accessibility notes for screen readers (timezone should be announced)
- Consistent with existing form component patterns in `docs/prototypes/forms/components/08-date-time.html`

---

### 3.2 html-prototyper

**Objective:** Create prototype for timezone-aware scheduled message form.

**Deliverables:**

1. **Update `docs/prototypes/features/v0.3.0/scheduled-messages/create.html`**
   - Add hidden `userTimezone` input field
   - Add timezone indicator below datetime-local input
   - Add JavaScript for timezone detection using `Intl.DateTimeFormat().resolvedOptions().timeZone`

2. **Update `docs/prototypes/features/v0.3.0/scheduled-messages/edit.html`**
   - Same changes as create page
   - Show stored timezone vs. current detected timezone if different

3. **Create timezone display component prototype**
   - Location: `docs/prototypes/features/v0.3.0/components/timezone-indicator.html`
   - Shows detected timezone name and abbreviation
   - Optional warning if browser timezone differs from stored preference

**Acceptance Criteria:**
- Prototypes demonstrate timezone detection working in browser
- Prototypes show graceful fallback when `Intl` API unavailable
- Matches existing design system styling

---

### 3.3 dotnet-specialist

**Objective:** Implement server-side timezone handling and update page models.

**Deliverables:**

#### 3.3.1 Create TimezoneHelper Utility

**File:** `src/DiscordBot.Core/Utilities/TimezoneHelper.cs`

```csharp
namespace DiscordBot.Core.Utilities;

/// <summary>
/// Provides timezone conversion utilities using IANA timezone names.
/// </summary>
public static class TimezoneHelper
{
    /// <summary>
    /// Converts a local DateTime to UTC using the specified IANA timezone.
    /// </summary>
    public static DateTime ConvertToUtc(DateTime localDateTime, string ianaTimezoneName);

    /// <summary>
    /// Converts a UTC DateTime to local time in the specified IANA timezone.
    /// </summary>
    public static DateTime ConvertFromUtc(DateTime utcDateTime, string ianaTimezoneName);

    /// <summary>
    /// Validates whether the provided string is a valid IANA timezone identifier.
    /// </summary>
    public static bool IsValidTimezone(string ianaTimezoneName);

    /// <summary>
    /// Gets TimeZoneInfo from IANA timezone name, with fallback to UTC.
    /// </summary>
    public static TimeZoneInfo GetTimeZoneInfo(string ianaTimezoneName);

    /// <summary>
    /// Gets the timezone abbreviation for display (e.g., "EST", "PST").
    /// </summary>
    public static string GetTimezoneAbbreviation(string ianaTimezoneName, DateTime forDateTime);
}
```

**Implementation Notes:**
- Use `TimeZoneInfo.FindSystemTimeZoneById()` which supports IANA names on .NET 6+ with ICU
- Handle Windows timezone ID format as fallback for older deployments
- Return UTC and log warning if timezone lookup fails
- Thread-safe static methods

#### 3.3.2 Update Create.cshtml.cs

**File:** `src/DiscordBot.Bot/Pages/Guilds/ScheduledMessages/Create.cshtml.cs`

Changes:
1. Add `UserTimezone` property to `InputModel`:
   ```csharp
   [Display(Name = "User Timezone")]
   public string? UserTimezone { get; set; }
   ```

2. Update `OnGetAsync`:
   - Remove `DateTime.Now` usage for defaults
   - Return null/empty `NextExecutionAt` to let JavaScript set it

3. Update `OnPostAsync`:
   - Use `TimezoneHelper.ConvertToUtc()` with `Input.UserTimezone`
   - Fallback to UTC if timezone invalid or missing
   - Log timezone used for debugging

#### 3.3.3 Update Edit.cshtml.cs

**File:** `src/DiscordBot.Bot/Pages/Guilds/ScheduledMessages/Edit.cshtml.cs`

Changes:
1. Add `UserTimezone` property to `InputModel`

2. Update `OnGetAsync`:
   - Convert `NextExecutionAt` from UTC to user's timezone for display
   - Note: Use JavaScript-detected timezone for display

3. Update `OnPostAsync`:
   - Use `TimezoneHelper.ConvertToUtc()` for the submitted time

#### 3.3.4 Update ScheduledMessageListViewModel.cs

**File:** `src/DiscordBot.Bot/ViewModels/Pages/ScheduledMessageListViewModel.cs`

Changes:
1. Store timestamps as UTC in the view model
2. Use JavaScript client-side conversion for display (via `local-time` CSS class pattern)
3. Alternatively: Add optional timezone parameter to `NextRunDisplay` property

#### 3.3.5 Update AuditLogs Index.cshtml.cs

**File:** `src/DiscordBot.Bot/Pages/Admin/AuditLogs/Index.cshtml.cs`

Changes:
1. Add `UserTimezone` bind property
2. Convert `StartDate` and `EndDate` from user timezone to UTC for querying
3. Use proper date boundary handling (start of day, end of day in user's timezone)

#### 3.3.6 Create Shared JavaScript Module

**File:** `src/DiscordBot.Bot/wwwroot/js/timezone.js`

```javascript
/**
 * Timezone Utilities Module
 * Detects user timezone and provides conversion helpers
 */
(function() {
    'use strict';

    const timezoneUtils = {
        /**
         * Gets the user's IANA timezone name
         * @returns {string} IANA timezone identifier (e.g., "America/New_York")
         */
        getTimezone: function() {
            try {
                return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
            } catch (e) {
                console.warn('Timezone detection failed, defaulting to UTC', e);
                return 'UTC';
            }
        },

        /**
         * Gets a display-friendly timezone abbreviation
         * @returns {string} Timezone abbreviation (e.g., "EST", "PST")
         */
        getTimezoneAbbreviation: function() {
            try {
                const date = new Date();
                const formatter = new Intl.DateTimeFormat('en-US', { timeZoneName: 'short' });
                const parts = formatter.formatToParts(date);
                const tzPart = parts.find(p => p.type === 'timeZoneName');
                return tzPart ? tzPart.value : '';
            } catch (e) {
                return '';
            }
        },

        /**
         * Converts a UTC ISO string to local time display
         * @param {string} utcIsoString - UTC timestamp in ISO format
         * @param {Object} options - Intl.DateTimeFormat options
         * @returns {string} Formatted local time string
         */
        formatLocalTime: function(utcIsoString, options) {
            const date = new Date(utcIsoString);
            const defaultOptions = {
                year: 'numeric',
                month: 'short',
                day: 'numeric',
                hour: 'numeric',
                minute: '2-digit',
                hour12: true
            };
            return date.toLocaleString('en-US', options || defaultOptions);
        },

        /**
         * Initializes timezone hidden fields on the page
         */
        initTimezoneFields: function() {
            const tz = this.getTimezone();
            document.querySelectorAll('input[name$="UserTimezone"]').forEach(input => {
                input.value = tz;
            });

            // Update timezone indicator elements
            document.querySelectorAll('.timezone-indicator').forEach(el => {
                el.textContent = `${tz} (${this.getTimezoneAbbreviation()})`;
            });
        },

        /**
         * Converts all elements with data-utc attribute to local time
         */
        convertDisplayTimes: function() {
            document.querySelectorAll('[data-utc]').forEach(el => {
                const utc = el.getAttribute('data-utc');
                if (utc) {
                    const format = el.getAttribute('data-format') || 'datetime';
                    let options;
                    switch (format) {
                        case 'date':
                            options = { year: 'numeric', month: 'short', day: 'numeric' };
                            break;
                        case 'time':
                            options = { hour: 'numeric', minute: '2-digit', hour12: true };
                            break;
                        default:
                            options = {
                                year: 'numeric',
                                month: 'short',
                                day: 'numeric',
                                hour: 'numeric',
                                minute: '2-digit',
                                hour12: true
                            };
                    }
                    el.textContent = this.formatLocalTime(utc, options);
                }
            });
        },

        /**
         * Sets datetime-local input value from UTC
         * @param {string} inputId - The input element ID
         * @param {string} utcIsoString - UTC timestamp in ISO format
         */
        setDateTimeLocalFromUtc: function(inputId, utcIsoString) {
            const input = document.getElementById(inputId);
            if (input && utcIsoString) {
                const date = new Date(utcIsoString);
                // Format as YYYY-MM-DDTHH:mm for datetime-local input
                const local = new Date(date.getTime() - (date.getTimezoneOffset() * 60000));
                input.value = local.toISOString().slice(0, 16);
            }
        },

        /**
         * Sets the default datetime-local value to now + offset minutes
         * @param {string} inputId - The input element ID
         * @param {number} offsetMinutes - Minutes to add to current time
         */
        setDefaultDateTime: function(inputId, offsetMinutes) {
            const input = document.getElementById(inputId);
            if (input && !input.value) {
                const now = new Date();
                now.setMinutes(now.getMinutes() + (offsetMinutes || 5));
                // Round to next 5 minutes
                now.setMinutes(Math.ceil(now.getMinutes() / 5) * 5);
                now.setSeconds(0);
                now.setMilliseconds(0);
                input.value = now.toISOString().slice(0, 16);
            }
        }
    };

    // Expose globally
    window.timezoneUtils = timezoneUtils;

    // Auto-initialize on DOMContentLoaded
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() {
            timezoneUtils.initTimezoneFields();
            timezoneUtils.convertDisplayTimes();
        });
    } else {
        timezoneUtils.initTimezoneFields();
        timezoneUtils.convertDisplayTimes();
    }
})();
```

#### 3.3.7 Update Layout to Include Timezone Script

**File:** `src/DiscordBot.Bot/Pages/Shared/_Layout.cshtml`

Add reference to timezone.js:
```html
<script src="~/js/timezone.js" asp-append-version="true"></script>
```

#### 3.3.8 Update Razor Views

**Create.cshtml Changes:**
```html
<!-- Add hidden timezone field inside form -->
<input type="hidden" name="Input.UserTimezone" id="Input_UserTimezone" value="" />

<!-- Add timezone indicator below datetime-local input -->
<p class="text-xs text-text-tertiary mt-1">
    Your timezone: <span class="timezone-indicator font-medium">Detecting...</span>
</p>
```

**Edit.cshtml Changes:**
Same as Create.cshtml, plus update display times to use `data-utc` attribute pattern.

**AuditLogs/Index.cshtml Changes:**
```html
<!-- Add hidden timezone field inside filter form -->
<input type="hidden" name="UserTimezone" id="UserTimezone" value="" />
```

**Acceptance Criteria:**
- `TimezoneHelper.IsValidTimezone()` returns true for valid IANA names
- `TimezoneHelper.ConvertToUtc()` correctly converts across DST boundaries
- Page models use timezone from form submission, not server local time
- JavaScript module initializes on page load and populates hidden fields
- All existing tests continue to pass
- New unit tests cover timezone conversion edge cases

---

### 3.4 docs-writer

**Objective:** Document the timezone handling approach and update relevant documentation.

**Deliverables:**

1. **Create `docs/articles/timezone-handling.md`**
   - Overview of the timezone architecture
   - How IANA timezone names are used
   - Server-side conversion patterns
   - Client-side detection and display patterns
   - Troubleshooting common issues

2. **Update `docs/articles/design-system.md`**
   - Add timezone-aware input patterns section
   - Reference the new timezone module

3. **Update CLAUDE.md if needed**
   - Add any new development patterns
   - Document the timezone helper utility

4. **Add inline code documentation**
   - XML docs for all public methods in `TimezoneHelper`
   - JSDoc comments in `timezone.js`

**Acceptance Criteria:**
- Documentation explains why UTC storage + user timezone conversion is used
- Examples show common timezone conversion scenarios
- Troubleshooting section addresses DST edge cases

---

## 4. Timeline / Dependency Map

```
Phase 1: Foundation (Can be parallel)
├── design-specialist: Update design system docs
├── docs-writer: Create timezone-handling.md skeleton
└── html-prototyper: Create timezone indicator prototype

Phase 2: Implementation (Sequential)
├── dotnet-specialist: Create TimezoneHelper utility
│   └── dotnet-specialist: Write unit tests for TimezoneHelper
├── dotnet-specialist: Create timezone.js module
└── dotnet-specialist: Update _Layout.cshtml

Phase 3: Integration (Sequential, depends on Phase 2)
├── dotnet-specialist: Update Create.cshtml.cs + Create.cshtml
├── dotnet-specialist: Update Edit.cshtml.cs + Edit.cshtml
├── dotnet-specialist: Update ScheduledMessageListViewModel
└── dotnet-specialist: Update AuditLogs Index

Phase 4: Documentation & Testing (Parallel, depends on Phase 3)
├── docs-writer: Complete all documentation
└── dotnet-specialist: Integration testing
```

**Estimated Effort:**
- Phase 1: 2-3 hours
- Phase 2: 4-6 hours
- Phase 3: 4-6 hours
- Phase 4: 2-3 hours
- **Total: 12-18 hours**

---

## 5. Acceptance Criteria

### 5.1 TimezoneHelper Utility
- [ ] `IsValidTimezone("America/New_York")` returns `true`
- [ ] `IsValidTimezone("Invalid/Zone")` returns `false`
- [ ] `ConvertToUtc(localTime, "America/New_York")` correctly handles EST/EDT
- [ ] `ConvertFromUtc(utcTime, "Europe/London")` correctly handles GMT/BST
- [ ] Fallback to UTC when invalid timezone provided (with warning log)

### 5.2 Create Scheduled Message Page
- [ ] Hidden `UserTimezone` field populated by JavaScript on page load
- [ ] Timezone indicator shows detected timezone below datetime input
- [ ] Submitted times converted to UTC using user's timezone
- [ ] Server log shows timezone used: "Converting NextExecutionAt from America/New_York to UTC"
- [ ] Scheduled message executes at user's intended local time

### 5.3 Edit Scheduled Message Page
- [ ] Existing `NextExecutionAt` displayed in user's local timezone
- [ ] Updates convert back to UTC using submitted timezone
- [ ] Timezone indicator matches Create page design

### 5.4 Scheduled Messages List
- [ ] Times displayed in user's local timezone (via JavaScript conversion)
- [ ] `data-utc` attribute contains UTC ISO string for accessibility

### 5.5 Audit Logs Page
- [ ] Date filters use user's timezone for boundary calculations
- [ ] Filtering by "2025-12-27" in EST correctly queries UTC range

### 5.6 Backward Compatibility
- [ ] Existing scheduled messages continue to work (UTC times unchanged)
- [ ] Missing timezone in form submission falls back to UTC with warning
- [ ] Pages work (with degraded experience) if JavaScript disabled

### 5.7 Timezone Storage
- [ ] All timestamps stored in database as UTC (no schema changes)
- [ ] Timezone conversion happens only at display and input layers

---

## 6. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| IANA timezone not recognized on Windows Server | Medium | High | Use TimeZoneConverter NuGet package as fallback, test on Windows deployment |
| DST transition causes off-by-one-hour errors | Medium | Medium | Test with dates during DST transition (March/November) |
| Browser `Intl` API unavailable (old browsers) | Low | Medium | Fallback to UTC, show warning message |
| User expects server-local time (legacy behavior) | Low | Low | Document change in release notes, add help text |
| Timezone spoofing/manipulation | Low | Low | Validate against known IANA list, no security-sensitive operations depend on timezone |

### 6.1 Recommended Testing Scenarios

1. **User in EST, server in UTC**: Schedule message for 9:00 AM EST, verify executes at 14:00 UTC
2. **User in PST, server in EST**: Schedule message for 5:00 PM PST, verify executes correctly
3. **DST transition (Spring forward)**: Schedule message for 2:30 AM on March 9, 2025 (time doesn't exist in EST)
4. **DST transition (Fall back)**: Schedule message for 1:30 AM on November 2, 2025 (time exists twice in EST)
5. **No JavaScript**: Verify form still submits, falls back to UTC
6. **Invalid timezone submitted**: Verify graceful fallback to UTC with warning

---

## 7. Navigation Integration Checklist

This fix does not introduce new pages. Navigation updates are not required.

---

## 8. Date/Time Handling Specification

### 8.1 Storage Layer
- All `DateTime` values stored in UTC
- Entity properties remain `DateTime` (not `DateTimeOffset`)
- No database migrations required

### 8.2 Display Layer
- JavaScript `timezone.js` module converts UTC to user's local timezone
- Use `data-utc` attribute pattern for automatic conversion:
  ```html
  <span data-utc="2025-12-27T14:00:00Z" data-format="datetime">Dec 27, 2025 9:00 AM</span>
  ```
- Fallback text is server-rendered UTC time for no-JS users

### 8.3 Input Layer
- `datetime-local` input captures user's local time (no timezone info)
- Hidden `UserTimezone` field transmits IANA timezone name
- Server combines datetime + timezone to calculate correct UTC

### 8.4 Expected Display Format
- Dates: "Dec 27, 2025"
- Times: "9:00 AM"
- DateTime: "Dec 27, 2025 9:00 AM"
- With timezone indicator: "Dec 27, 2025 9:00 AM (EST)"

---

## 9. Files to Modify Summary

| File | Action | Description |
|------|--------|-------------|
| `src/DiscordBot.Core/Utilities/TimezoneHelper.cs` | Create | Timezone conversion utility |
| `src/DiscordBot.Bot/wwwroot/js/timezone.js` | Create | Client-side timezone detection |
| `src/DiscordBot.Bot/Pages/Shared/_Layout.cshtml` | Update | Add timezone.js reference |
| `src/DiscordBot.Bot/Pages/Guilds/ScheduledMessages/Create.cshtml` | Update | Add hidden field, timezone indicator |
| `src/DiscordBot.Bot/Pages/Guilds/ScheduledMessages/Create.cshtml.cs` | Update | Use TimezoneHelper for conversion |
| `src/DiscordBot.Bot/Pages/Guilds/ScheduledMessages/Edit.cshtml` | Update | Add hidden field, timezone indicator |
| `src/DiscordBot.Bot/Pages/Guilds/ScheduledMessages/Edit.cshtml.cs` | Update | Use TimezoneHelper for conversion |
| `src/DiscordBot.Bot/Pages/Guilds/ScheduledMessages/Index.cshtml` | Update | Use data-utc pattern for times |
| `src/DiscordBot.Bot/ViewModels/Pages/ScheduledMessageListViewModel.cs` | Update | Remove ToLocalTime() calls |
| `src/DiscordBot.Bot/Pages/Admin/AuditLogs/Index.cshtml` | Update | Add hidden timezone field |
| `src/DiscordBot.Bot/Pages/Admin/AuditLogs/Index.cshtml.cs` | Update | Use timezone for date filtering |
| `tests/DiscordBot.Tests/Utilities/TimezoneHelperTests.cs` | Create | Unit tests for TimezoneHelper |
| `docs/articles/timezone-handling.md` | Create | Timezone handling documentation |
| `docs/articles/design-system.md` | Update | Add timezone input patterns |
| `docs/prototypes/features/v0.3.0/scheduled-messages/create.html` | Update | Add timezone detection prototype |
| `docs/prototypes/features/v0.3.0/scheduled-messages/edit.html` | Update | Add timezone detection prototype |

---

## 10. Implementation Notes

### 10.1 .NET Timezone Resolution

.NET 6+ uses ICU on Linux/macOS which natively supports IANA timezone names. On Windows, it maps to Windows timezone IDs automatically. For maximum compatibility:

```csharp
// This works cross-platform in .NET 6+
var tz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
```

If running on older Windows without ICU, consider adding the `TimeZoneConverter` NuGet package:
```bash
dotnet add package TimeZoneConverter
```

### 10.2 Browser Compatibility

The `Intl.DateTimeFormat` API is supported in:
- Chrome 24+
- Firefox 29+
- Safari 10+
- Edge 12+

This covers 99%+ of modern browsers. For older browsers, the fallback is UTC.

### 10.3 Testing Tips

Use browser DevTools to simulate different timezones:
```javascript
// In browser console
Intl.DateTimeFormat().resolvedOptions().timeZone // Shows detected timezone
```

Or use environment variables when running the bot:
```bash
# Linux
TZ=America/Los_Angeles dotnet run --project src/DiscordBot.Bot

# Windows PowerShell
$env:TZ = "America/Los_Angeles"; dotnet run --project src/DiscordBot.Bot
```

---

## Appendix A: Related Issues

- Issue #319 - Date/Time Locale Issues (this issue)

## Appendix B: References

- [IANA Time Zone Database](https://www.iana.org/time-zones)
- [.NET TimeZoneInfo and IANA](https://docs.microsoft.com/en-us/dotnet/standard/datetime/finding-the-time-zones-on-local-system)
- [MDN Intl.DateTimeFormat](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Intl/DateTimeFormat)
- [Handling Time Zones in Web Applications](https://www.w3.org/International/articles/time-zones/)
