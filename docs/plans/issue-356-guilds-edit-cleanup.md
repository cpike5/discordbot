# Issue #356: Review and Improve Guilds/Edit Page Settings - Implementation Plan

**Version:** 1.0
**Created:** 2025-12-29
**Status:** Planning
**Target Framework:** ASP.NET Core Razor Pages (.NET 8)
**Scope:** Small-Medium

---

## 1. Requirement Summary

The Guilds/Edit page currently contains several settings that need cleanup:
- **Obsolete settings** that no longer apply to the slash-command-only bot
- **Duplicate settings** that are better handled by dedicated pages (Welcome page)
- **Placeholder settings** with no backend implementation
- **Unverified behavior** for the "Bot Active" toggle

This plan identifies which settings to remove, recommends cleanup actions, and ensures the page accurately reflects implemented functionality.

---

## 2. Current State Analysis

### 2.1 Settings Inventory

| Setting | UI Location | Storage | Status | Recommendation |
|---------|-------------|---------|--------|----------------|
| **Command Prefix** | General Settings | `Guild.Prefix` | **OBSOLETE** | Remove - bot uses slash commands only |
| **Bot Active** | General Settings | `Guild.IsActive` | **NOT ENFORCED** | Keep, but add enforcement |
| **Welcome Messages Toggle** | Notifications | Settings JSON | **DUPLICATE** | Remove - Welcome page has full controls |
| **Leave Messages** | Notifications | Settings JSON | **PLACEHOLDER** | Remove - not implemented |
| **Moderation Alerts** | Notifications | Settings JSON | **PLACEHOLDER** | Remove - not implemented |
| **Command Logging** | Notifications | Settings JSON | **PLACEHOLDER** | Remove - not implemented |
| **Log Channel** | Advanced Settings | Settings JSON | **PLACEHOLDER** | Remove - no backend service |
| **Welcome Channel** | Advanced Settings | Settings JSON | **DUPLICATE** | Remove - Welcome page has proper selector |
| **Auto-Moderation** | Advanced Settings | Settings JSON | **PLACEHOLDER** | Remove - not implemented |

### 2.2 "Bot Active" Toggle Analysis

**Current Behavior:**
- The `Guild.IsActive` property exists in the database entity
- It is saved and retrieved correctly
- It is displayed on the dashboard (active vs. total guilds count)
- **It is NOT enforced in bot behavior** - commands execute regardless of this setting

**Evidence:**
- `InteractionHandler.OnInteractionCreatedAsync()` does not check `Guild.IsActive`
- No precondition attribute exists for guild active status
- No event handlers check this flag before processing

### 2.3 Welcome Page Comparison

The dedicated Welcome page (`Pages/Guilds/Welcome.cshtml`) provides:
- Proper enable/disable toggle with form section disabling
- Channel selector from live Discord API (not text input)
- Welcome message template with token insertion
- Embed configuration (color, avatar, etc.)
- Live preview functionality

The Edit page's duplicate settings are inferior and confusing to users.

---

## 3. Architectural Considerations

### 3.1 Settings JSON Schema

**Current Structure:**
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

**Proposed Structure (after cleanup):**
```json
{}
```

Since the dedicated Welcome page uses `WelcomeConfiguration` table (not Settings JSON), and all placeholder settings are being removed, the Settings JSON field may become empty. This is acceptable - the field can be used for future guild-specific settings.

### 3.2 Database Implications

| Entity | Changes |
|--------|---------|
| `Guild.Prefix` | Keep column, but stop using it (backward compatible) |
| `Guild.IsActive` | Keep and implement enforcement |
| `Guild.Settings` | Will contain empty JSON or null after cleanup |

### 3.3 Migration Strategy

No database migration required. Existing settings data can remain in the database but will no longer be read/written by the Edit page.

---

## 4. Implementation Recommendations

### 4.1 Settings to Remove from UI

**General Settings Section:**
1. **Command Prefix** - Remove entirely
   - Reason: Bot uses slash commands exclusively (see CLAUDE.md: "Slash commands only")
   - The `Guild.Prefix` property comment even says "for text commands"

**Notifications Section:**
2. **Welcome Messages Toggle** - Remove
   - Reason: Duplicate of Welcome page functionality
   - Users should use the dedicated Welcome page instead

3. **Leave Messages** - Remove
   - Reason: No backend implementation exists
   - No handlers listen for `GuildMemberRemoved` event

4. **Moderation Alerts** - Remove
   - Reason: No backend implementation exists
   - No moderation event handlers exist

5. **Command Logging** - Remove
   - Reason: Misleading - command logging to DB exists but this toggle controls nothing
   - The CommandExecutionLogger runs unconditionally

**Advanced Settings Section:**
6. **Log Channel** - Remove
   - Reason: No backend service uses this value
   - No channel logging functionality exists

7. **Welcome Channel** - Remove
   - Reason: Duplicate of Welcome page functionality
   - Welcome page has proper channel selector from Discord API

8. **Auto-Moderation** - Remove
   - Reason: No auto-moderation implementation exists

### 4.2 Settings to Keep

**General Settings Section:**
1. **Bot Active Toggle** - Keep and enhance
   - Currently the only meaningful setting on this page
   - Requires enforcement implementation (see section 4.3)

### 4.3 Bot Active Enforcement Options

**Option A: Precondition Attribute (Recommended)**
Create a `RequireGuildActiveAttribute` precondition that checks `Guild.IsActive` before command execution.

Pros:
- Clean separation of concerns
- Reusable across all commands
- Follows existing precondition pattern (RequireAdminAttribute)

Cons:
- Requires database lookup per command
- Need to consider caching for performance

**Option B: InteractionHandler Check**
Add check in `InteractionHandler.OnInteractionCreatedAsync()` before executing commands.

Pros:
- Single implementation point
- Can reject interaction early

Cons:
- Couples database logic to handler
- Less flexible for per-command exceptions

**Recommendation:** Option A with caching. Create precondition attribute and apply it globally or per-module as needed.

---

## 5. Subagent Task Plan

### 5.1 design-specialist

**Not required for this issue.** No new design tokens or component specifications needed. The page will use fewer existing components.

### 5.2 html-prototyper

**Not required for this issue.** The simplified page uses existing component patterns.

### 5.3 dotnet-specialist

| Task ID | Description | Priority | Files |
|---------|-------------|----------|-------|
| DS-1 | Remove obsolete/placeholder settings from Edit.cshtml | High | `src/DiscordBot.Bot/Pages/Guilds/Edit.cshtml` |
| DS-2 | Update InputModel to remove unused properties | High | `src/DiscordBot.Bot/Pages/Guilds/Edit.cshtml.cs` |
| DS-3 | Update GuildEditViewModel to remove unused properties | High | `src/DiscordBot.Bot/ViewModels/Pages/GuildEditViewModel.cs` |
| DS-4 | Remove GuildSettingsData class (no longer needed) | Medium | `src/DiscordBot.Bot/ViewModels/Pages/GuildEditViewModel.cs` |
| DS-5 | Create RequireGuildActiveAttribute precondition | Medium | `src/DiscordBot.Bot/Preconditions/RequireGuildActiveAttribute.cs` |
| DS-6 | Apply RequireGuildActive to InteractionHandler or global module | Medium | `src/DiscordBot.Bot/Handlers/InteractionHandler.cs` or command modules |
| DS-7 | Update unit tests for GuildEditViewModel | Medium | `tests/DiscordBot.Tests/ViewModels/GuildEditViewModelTests.cs` |
| DS-8 | Add unit tests for RequireGuildActiveAttribute | Medium | `tests/DiscordBot.Tests/Preconditions/RequireGuildActiveAttributeTests.cs` |

### 5.4 docs-writer

| Task ID | Description | Priority | Files |
|---------|-------------|----------|-------|
| DW-1 | Update issue-76-guild-settings-edit.md with cleanup notes | Low | `docs/plans/issue-76-guild-settings-edit.md` |

---

## 6. Detailed Implementation Specifications

### 6.1 Simplified Edit.cshtml

The page should contain only:
1. Breadcrumb navigation
2. Page header with guild icon/name
3. Success/error alerts
4. **General Settings Section** with only "Bot Active" toggle
5. Form actions (Save/Cancel)

### 6.2 Simplified Edit.cshtml.cs InputModel

```csharp
public class InputModel
{
    public ulong GuildId { get; set; }

    [Display(Name = "Bot Active")]
    public bool IsActive { get; set; } = true;
}
```

### 6.3 Simplified GuildEditViewModel

```csharp
public class GuildEditViewModel
{
    public ulong Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public static GuildEditViewModel FromDto(GuildDto dto)
    {
        return new GuildEditViewModel
        {
            Id = dto.Id,
            Name = dto.Name,
            IconUrl = dto.IconUrl,
            IsActive = dto.IsActive
        };
    }
}
```

Note: The `ToSettingsJson()` method and `GuildSettingsData` class can be removed entirely since no settings are being saved to the JSON field.

### 6.4 Simplified OnPostAsync

```csharp
public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
{
    if (!ModelState.IsValid)
    {
        await LoadViewModelAsync(Input.GuildId, cancellationToken);
        return Page();
    }

    var updateRequest = new GuildUpdateRequestDto
    {
        IsActive = Input.IsActive
        // Prefix and Settings no longer updated
    };

    var result = await _guildService.UpdateGuildAsync(Input.GuildId, updateRequest, cancellationToken);

    if (result == null)
    {
        ErrorMessage = "Guild not found.";
        await LoadViewModelAsync(Input.GuildId, cancellationToken);
        return Page();
    }

    SuccessMessage = "Guild settings saved successfully.";
    return RedirectToPage("Details", new { id = Input.GuildId });
}
```

### 6.5 RequireGuildActiveAttribute

```csharp
using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that requires the guild to be active (bot not disabled for this guild).
/// </summary>
public class RequireGuildActiveAttribute : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        if (context.Guild == null)
        {
            // Allow DM commands to pass through
            return PreconditionResult.FromSuccess();
        }

        var guildService = services.GetRequiredService<IGuildService>();
        var guild = await guildService.GetGuildByIdAsync(context.Guild.Id, CancellationToken.None);

        if (guild == null)
        {
            // Guild not in database - allow (will be added on first interaction)
            return PreconditionResult.FromSuccess();
        }

        if (!guild.IsActive)
        {
            return PreconditionResult.FromError(
                "The bot has been disabled for this server by an administrator.");
        }

        return PreconditionResult.FromSuccess();
    }
}
```

---

## 7. Timeline / Dependency Map

```
Phase 1: UI Cleanup (Can run in parallel)
  [DS-1] Edit.cshtml cleanup ─────────────┐
  [DS-2] Edit.cshtml.cs InputModel cleanup ┴─► Phase 2
  [DS-3] GuildEditViewModel cleanup ───────┤
  [DS-4] Remove GuildSettingsData ─────────┘

Phase 2: Testing Updates (Sequential after Phase 1)
  [DS-7] Update GuildEditViewModelTests

Phase 3: Bot Active Enforcement (Parallel to Phase 1-2)
  [DS-5] Create RequireGuildActiveAttribute
  [DS-6] Apply to InteractionHandler or modules
  [DS-8] Add precondition tests

Phase 4: Documentation (After all implementation)
  [DW-1] Update docs
```

### Estimated Effort

| Phase | Tasks | Estimated Time |
|-------|-------|----------------|
| Phase 1 | UI/ViewModel Cleanup | 1-2 hours |
| Phase 2 | Test Updates | 30 minutes |
| Phase 3 | Bot Active Enforcement | 1-2 hours |
| Phase 4 | Documentation | 30 minutes |
| **Total** | | **3-5 hours** |

---

## 8. Acceptance Criteria

### 8.1 UI Cleanup

| # | Criterion | Validation Method |
|---|-----------|-------------------|
| 1 | Command Prefix field removed from Edit page | Visual inspection |
| 2 | All Notifications section settings removed | Visual inspection |
| 3 | All Advanced Settings section settings removed | Visual inspection |
| 4 | Only "Bot Active" toggle remains in General Settings | Visual inspection |
| 5 | Save button still works with simplified form | Manual test |
| 6 | Cancel button still navigates to Details page | Manual test |

### 8.2 Bot Active Enforcement

| # | Criterion | Validation Method |
|---|-----------|-------------------|
| 7 | When IsActive=false, slash commands return error message | Manual test with Discord |
| 8 | When IsActive=true, slash commands execute normally | Manual test with Discord |
| 9 | Error message is user-friendly | Visual inspection |
| 10 | DM commands are not affected by guild active status | Manual test |

### 8.3 Code Quality

| # | Criterion | Validation Method |
|---|-----------|-------------------|
| 11 | GuildEditViewModel no longer contains notification/advanced properties | Code review |
| 12 | GuildSettingsData class removed | Code review |
| 13 | Unit tests pass | `dotnet test` |
| 14 | No TypeScript/C# compiler errors | `dotnet build` |

---

## 9. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Users confused by missing settings | Medium | Low | Add info text explaining dedicated pages for Welcome settings |
| Existing Settings JSON data causes issues | Low | Low | Settings are read from JSON with null-safe parsing; empty JSON is valid |
| Bot Active enforcement impacts performance | Medium | Medium | Consider caching guild active status; evaluate with metrics |
| Breaking change for GuildUpdateRequestDto | Low | Medium | Keep Prefix and Settings properties on DTO but make them nullable/optional |

---

## 10. Files to Modify

### Primary Changes

| File | Change Type | Description |
|------|-------------|-------------|
| `src/DiscordBot.Bot/Pages/Guilds/Edit.cshtml` | Modify | Remove all settings except Bot Active toggle |
| `src/DiscordBot.Bot/Pages/Guilds/Edit.cshtml.cs` | Modify | Simplify InputModel and OnPostAsync |
| `src/DiscordBot.Bot/ViewModels/Pages/GuildEditViewModel.cs` | Modify | Remove notification/advanced properties, remove GuildSettingsData |
| `src/DiscordBot.Bot/Preconditions/RequireGuildActiveAttribute.cs` | Create | New precondition for guild active check |
| `tests/DiscordBot.Tests/ViewModels/GuildEditViewModelTests.cs` | Modify | Update tests for simplified ViewModel |
| `tests/DiscordBot.Tests/Preconditions/RequireGuildActiveAttributeTests.cs` | Create | Tests for new precondition |

### Optional/Future Changes

| File | Change Type | Description |
|------|-------------|-------------|
| `src/DiscordBot.Bot/Handlers/InteractionHandler.cs` | Modify | Optional: Add global guild active check |
| `docs/plans/issue-76-guild-settings-edit.md` | Modify | Add note about cleanup performed |

---

## 11. Alternative Approaches Considered

### 11.1 Mark Placeholder Features as "Coming Soon"

Instead of removing placeholder settings, display them with a "Coming Soon" badge.

**Rejected because:**
- Creates expectation that features are being actively developed
- Clutters the UI with non-functional controls
- Users may toggle settings expecting behavior changes

### 11.2 Keep Welcome Messages Toggle for Quick Enable/Disable

Keep the simple toggle on Edit page as a convenience shortcut.

**Rejected because:**
- Creates confusion about which page is the "source of truth"
- Welcome page provides the enable/disable toggle already
- Inconsistent UX - toggle here vs. full config on Welcome page

### 11.3 Keep All Settings, Implement Later

Keep the UI and implement backend features later.

**Rejected because:**
- Placeholder UI creates false expectations
- Technical debt accumulates
- Users may report "bugs" for unimplemented features

---

## 12. Future Considerations

After this cleanup, if new guild-specific settings are needed:
1. First determine if a dedicated page (like Welcome) is more appropriate
2. If simple toggle settings are needed, re-add the Notifications or Advanced sections
3. The `Guild.Settings` JSON field remains available for storage
4. Consider adding a "Features" or "Modules" section for enabling/disabling bot features per-guild

---

## Changelog

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-12-29 | Systems Architect | Initial plan creation |
