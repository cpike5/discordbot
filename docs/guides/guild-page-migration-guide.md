# Guild Page Migration Guide

This guide walks through migrating Razor Pages to use the new guild layout infrastructure (issue #1191). The guild layout system provides consistent breadcrumb navigation, guild headers with action buttons, and guild-level tab navigation across all guild management pages.

## Overview

The guild layout infrastructure consists of three components:

1. **Guild Breadcrumb** - Hierarchical navigation showing path from Home → Servers → [Guild Name] → Current Page
2. **Guild Header** - Displays guild name, icon, page title, description, and action buttons
3. **Guild Navigation Bar** - Tabs for navigating between guild management sections (Overview, Members, Moderation, Messages, Audio, Rat Watch, Reminders, Welcome, Assistant)

All pages using the guild layout inherit from `_GuildLayout.cshtml`, which automatically renders these components based on ViewModels you populate in your PageModel.

## Architecture

The three-component system works as follows:

```
_GuildLayout.cshtml (shared layout)
├── _GuildBreadcrumb.cshtml (rendered with GuildBreadcrumbViewModel)
├── _GuildHeader.cshtml (rendered with GuildHeaderViewModel)
├── _GuildNavBar.cshtml (rendered with GuildNavBarViewModel)
└── @RenderBody() (your page content)
```

## Step-by-Step Migration Checklist

### 1. Change Layout Reference

In your page's `.cshtml` file, update the Layout reference:

```razor
@{
    Layout = "_GuildLayout.cshtml";
}
```

### 2. Add ViewModel Properties to PageModel

In your `.cshtml.cs` file, add these properties to your PageModel class:

```csharp
/// <summary>
/// Guild layout breadcrumb ViewModel.
/// </summary>
public GuildBreadcrumbViewModel Breadcrumb { get; set; } = new();

/// <summary>
/// Guild layout header ViewModel.
/// </summary>
public GuildHeaderViewModel Header { get; set; } = new();

/// <summary>
/// Guild layout navigation ViewModel.
/// </summary>
public GuildNavBarViewModel Navigation { get; set; } = new();
```

Add the using statement:

```csharp
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.Configuration;
```

### 3. Populate ViewModels in OnGetAsync

In your `OnGetAsync` method, after fetching the guild and building your page ViewModel, populate the layout ViewModels.

### 4. Test Navigation and Rendering

- Verify breadcrumbs display correctly with proper URLs
- Verify the correct navigation tab is highlighted
- Verify header actions render (if applicable)
- Check that page content displays below the navigation

## Breadcrumb Patterns

### 2-Level Breadcrumb (Root Guild Pages)

For pages directly under guild navigation (e.g., Guild Details, Members, Moderation):

```csharp
Breadcrumb = new GuildBreadcrumbViewModel
{
    Items = new List<BreadcrumbItem>
    {
        new() { Label = "Home", Url = "/" },
        new() { Label = "Servers", Url = "/Guilds" },
        new() { Label = guild.Name, IsCurrent = true }
    }
};
```

**Examples**: Guild Details, Members, Moderation, Scheduled Messages, Rat Watch

### 3-Level Breadcrumb (Child Pages)

For pages nested under a parent section (e.g., Soundboard under Audio, Scheduled Message Edit):

```csharp
Breadcrumb = new GuildBreadcrumbViewModel
{
    Items = new List<BreadcrumbItem>
    {
        new() { Label = "Home", Url = "/" },
        new() { Label = "Servers", Url = "/Guilds" },
        new() { Label = guild.Name, Url = $"/Guilds/Details?id={guild.Id}" },
        new() { Label = "Audio", IsCurrent = true }
    }
};
```

**Examples**: Soundboard, Audio Settings, Welcome Settings, Text-to-Speech, Assistant Settings

**Key points:**
- The last item should have `IsCurrent = true` (no URL)
- Only the current page is marked as current
- Guild name should link to `/Guilds/Details?id={guild.Id}`
- URLs use query parameters, not route parameters

## Header ViewModel Configuration

### Required Fields

```csharp
Header = new GuildHeaderViewModel
{
    GuildId = guild.Id,
    GuildName = guild.Name,
    GuildIconUrl = guild.IconUrl,
    PageTitle = "Audio",
    PageDescription = $"Manage audio settings for {guild.Name}"
};
```

### Optional Action Buttons

Add action buttons conditionally. Common patterns:

#### Single Action Button

```csharp
Header = new GuildHeaderViewModel
{
    // ... required fields ...
    Actions = new List<HeaderAction>
    {
        new()
        {
            Label = "Edit Settings",
            Url = $"/Guilds/Edit?id={guild.Id}",
            Icon = "M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z",
            Style = HeaderActionStyle.Primary
        }
    }
};
```

#### Multiple Action Buttons

```csharp
Actions = new List<HeaderAction>
{
    new()
    {
        Label = "Active",
        Url = "#",
        Style = HeaderActionStyle.Secondary
    },
    new()
    {
        Label = "Sync",
        Url = "#",
        Icon = "M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15",
        Style = HeaderActionStyle.Secondary
    },
    new()
    {
        Label = "Members",
        Url = $"/Guilds/Members?guildId={guild.Id}",
        Icon = "M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z",
        Style = HeaderActionStyle.Secondary
    },
    new()
    {
        Label = "Edit",
        Url = $"/Guilds/Edit?id={guild.Id}",
        Icon = "M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z",
        Style = HeaderActionStyle.Primary
    }
}
```

#### Conditional Action Buttons

```csharp
Actions = ViewModel.CanEdit ? new List<HeaderAction>
{
    new()
    {
        Label = "Edit Settings",
        Url = $"/Guilds/Edit?id={guild.Id}",
        Icon = "M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z",
        Style = HeaderActionStyle.Primary
    }
} : null
```

### Action Button Properties

- **Label** (required): Button text
- **Url** (required): Navigation target (can be "#" for non-navigating buttons)
- **Icon** (optional): SVG path for button icon
- **Style** (required): `Primary` (orange), `Secondary` (bordered), or `Link` (text-only)
- **OpenInNewTab** (optional): Set `true` for external links or member portal links

### Common SVG Icons

| Icon | SVG Path |
|------|----------|
| Edit/Pencil | `M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z` |
| Refresh/Sync | `M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15` |
| Users/Members | `M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z` |
| External Link | `M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14` |
| Play/Open Portal | `M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14` |

## Guild Navigation Tab Setup

All guild pages use the same tab configuration from `GuildNavigationConfig`:

```csharp
Navigation = new GuildNavBarViewModel
{
    GuildId = guild.Id,
    ActiveTab = "overview",  // Must match a tab Id
    Tabs = GuildNavigationConfig.GetTabs().ToList()
};
```

### Available Tabs and Tab IDs

| Tab ID | Label | Page | Order |
|--------|-------|------|-------|
| `overview` | Overview | /Guilds/Details | 1 |
| `members` | Members | /Guilds/Members | 2 |
| `moderation` | Moderation | /Guilds/ModerationSettings | 3 |
| `messages` | Messages | /Guilds/ScheduledMessages | 4 |
| `audio` | Audio | /Guilds/Soundboard | 5 |
| `ratwatch` | Rat Watch | /Guilds/RatWatch | 6 |
| `reminders` | Reminders | /Guilds/Reminders | 7 |
| `welcome` | Welcome | /Guilds/Welcome | 8 |
| `assistant` | Assistant | /Guilds/AssistantSettings | 9 |

### Setting the Active Tab

Always use the exact tab ID that matches the current page:

```csharp
// For Welcome page
Navigation = new GuildNavBarViewModel
{
    GuildId = guild.Id,
    ActiveTab = "welcome",  // Matches GuildNavigationConfig
    Tabs = GuildNavigationConfig.GetTabs().ToList()
};
```

## Special Cases

### Pages with Internal Tabs (e.g., Soundboard)

Some pages like Soundboard have their own internal tab system PLUS the guild-level navigation. The guild layout handles this cleanly:

1. Set the guild navigation to the parent section's tab ID
2. Keep your page's internal `_AudioTabs` partial or other internal navigation

```csharp
// In Soundboard page
Navigation = new GuildNavBarViewModel
{
    GuildId = guild.Id,
    ActiveTab = "audio",  // Parent section tab
    Tabs = GuildNavigationConfig.GetTabs().ToList()
};

// Your page content still has internal tabs:
// @await Html.PartialAsync("_AudioTabs", viewModel)
```

The guild layout breadcrumb should reflect the parent section:

```csharp
Breadcrumb = new GuildBreadcrumbViewModel
{
    Items = new List<BreadcrumbItem>
    {
        new() { Label = "Home", Url = "/" },
        new() { Label = "Servers", Url = "/Guilds" },
        new() { Label = guild.Name, Url = $"/Guilds/Details?id={guild.Id}" },
        new() { Label = "Audio", IsCurrent = true }  // Parent section, not specific sub-page
    }
};
```

### Pages with No Action Buttons

Simply set `Actions` to `null` or omit it:

```csharp
Header = new GuildHeaderViewModel
{
    GuildId = guild.Id,
    GuildName = guild.Name,
    GuildIconUrl = guild.IconUrl,
    PageTitle = "Welcome Settings",
    PageDescription = $"Configure welcome messages for {guild.Name}"
    // Actions is null by default
};
```

### Pages with Conditional Navigation

Some pages may not need the full guild navigation. This is rare but handle it with an empty list:

```csharp
Navigation = new GuildNavBarViewModel
{
    GuildId = guild.Id,
    ActiveTab = string.Empty,
    Tabs = new List<GuildNavItem>()  // Empty list hides navigation
};
```

## Code Examples

### Simple Page Example: Welcome Settings

Complete implementation for a simple page with breadcrumb, header, and navigation:

```csharp
public async Task<IActionResult> OnGetAsync(ulong id, CancellationToken cancellationToken)
{
    _logger.LogInformation("User accessing welcome configuration for guild {GuildId}", id);

    var guild = await _guildService.GetGuildByIdAsync(id, cancellationToken);
    if (guild == null)
    {
        _logger.LogWarning("Guild {GuildId} not found", id);
        return NotFound();
    }

    // Fetch and populate your page ViewModel
    var welcomeConfig = await _welcomeService.GetConfigurationAsync(id, cancellationToken);
    ViewModel = WelcomeConfigurationViewModel.FromDto(welcomeConfig, guild.Name, guild.IconUrl);

    // Populate guild layout ViewModels
    Breadcrumb = new GuildBreadcrumbViewModel
    {
        Items = new List<BreadcrumbItem>
        {
            new() { Label = "Home", Url = "/" },
            new() { Label = "Servers", Url = "/Guilds" },
            new() { Label = guild.Name, Url = $"/Guilds/Details?id={guild.Id}" },
            new() { Label = "Welcome Settings", IsCurrent = true }
        }
    };

    Header = new GuildHeaderViewModel
    {
        GuildId = guild.Id,
        GuildName = guild.Name,
        GuildIconUrl = guild.IconUrl,
        PageTitle = "Welcome Settings",
        PageDescription = $"Configure automatic welcome messages for {guild.Name}"
    };

    Navigation = new GuildNavBarViewModel
    {
        GuildId = guild.Id,
        ActiveTab = "welcome",
        Tabs = GuildNavigationConfig.GetTabs().ToList()
    };

    return Page();
}
```

### Complex Page Example: Guild Details

Complete implementation with multiple action buttons:

```csharp
public async Task<IActionResult> OnGetAsync(ulong id, CancellationToken cancellationToken)
{
    _logger.LogInformation("User accessing guild details for guild {GuildId}", id);

    var guild = await _guildService.GetGuildByIdAsync(id, cancellationToken);
    if (guild == null)
    {
        _logger.LogWarning("Guild {GuildId} not found", id);
        return NotFound();
    }

    // Fetch all required data for the detail page
    var commandLogs = await _commandLogService.GetLogsAsync(
        new CommandLogQueryDto { GuildId = id, Page = 1, PageSize = 10 },
        cancellationToken);

    // Build page ViewModel
    ViewModel = GuildDetailViewModel.FromDto(guild, commandLogs.Items);

    // 2-level breadcrumb for root page
    Breadcrumb = new GuildBreadcrumbViewModel
    {
        Items = new List<BreadcrumbItem>
        {
            new() { Label = "Home", Url = "/" },
            new() { Label = "Servers", Url = "/Guilds" },
            new() { Label = guild.Name, IsCurrent = true }
        }
    };

    // Header with multiple actions
    Header = new GuildHeaderViewModel
    {
        GuildId = guild.Id,
        GuildName = guild.Name,
        GuildIconUrl = guild.IconUrl,
        PageTitle = guild.Name,
        PageDescription = $"ID: {guild.Id}",
        Actions = new List<HeaderAction>
        {
            new()
            {
                Label = "Active",
                Url = "#",
                Style = HeaderActionStyle.Secondary
            },
            new()
            {
                Label = "Sync",
                Url = "#",
                Icon = "M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15",
                Style = HeaderActionStyle.Secondary
            },
            new()
            {
                Label = "Members",
                Url = $"/Guilds/Members?guildId={guild.Id}",
                Icon = "M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z",
                Style = HeaderActionStyle.Secondary
            },
            new()
            {
                Label = "Edit Settings",
                Url = $"/Guilds/Edit?id={guild.Id}",
                Icon = "M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z",
                Style = HeaderActionStyle.Primary
            }
        }
    };

    // Standard navigation with "overview" as active tab
    Navigation = new GuildNavBarViewModel
    {
        GuildId = guild.Id,
        ActiveTab = "overview",
        Tabs = GuildNavigationConfig.GetTabs().ToList()
    };

    return Page();
}
```

### Page with Internal Tabs Example: Soundboard

```csharp
public async Task<IActionResult> OnGetAsync(ulong guildId, CancellationToken cancellationToken)
{
    _logger.LogInformation("User accessing Soundboard for guild {GuildId}", guildId);

    var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
    if (guild == null)
    {
        _logger.LogWarning("Guild {GuildId} not found", guildId);
        return NotFound();
    }

    // Fetch sounds and build page ViewModel
    var sounds = await _soundService.GetAllByGuildAsync(guildId, cancellationToken);
    ViewModel = SoundboardIndexViewModel.Create(guildId, guild.Name, guild.IconUrl, sounds);

    // 3-level breadcrumb with parent section
    Breadcrumb = new GuildBreadcrumbViewModel
    {
        Items = new List<BreadcrumbItem>
        {
            new() { Label = "Home", Url = "/" },
            new() { Label = "Servers", Url = "/Guilds" },
            new() { Label = guild.Name, Url = $"/Guilds/Details?id={guild.Id}" },
            new() { Label = "Audio", IsCurrent = true }
        }
    };

    // Header with conditional action for member portal
    Header = new GuildHeaderViewModel
    {
        GuildId = guild.Id,
        GuildName = guild.Name,
        GuildIconUrl = guild.IconUrl,
        PageTitle = "Audio",
        PageDescription = $"Manage audio settings and soundboard for {guild.Name}",
        Actions = ViewModel.IsMemberPortalEnabled ? new List<HeaderAction>
        {
            new()
            {
                Label = "Open Member Portal",
                Url = $"/Portal/Soundboard/{guildId}",
                Icon = "M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14",
                Style = HeaderActionStyle.Secondary,
                OpenInNewTab = true
            }
        } : null
    };

    // Active tab is "audio" for the parent section
    Navigation = new GuildNavBarViewModel
    {
        GuildId = guild.Id,
        ActiveTab = "audio",
        Tabs = GuildNavigationConfig.GetTabs().ToList()
    };

    return Page();
}
```

## Troubleshooting

### Issue: Active Tab Not Highlighting

**Symptom**: The current page's tab doesn't appear highlighted/active.

**Solution**: Verify the `ActiveTab` ID exactly matches a tab ID from `GuildNavigationConfig.GetTabs()`:

```csharp
// Wrong - "audio-settings" doesn't exist
Navigation = new GuildNavBarViewModel
{
    ActiveTab = "audio-settings"  // ERROR: use "audio" instead
};

// Correct
Navigation = new GuildNavBarViewModel
{
    ActiveTab = "audio"  // Matches GuildNavigationConfig
};
```

### Issue: Breadcrumb URLs Not Working

**Symptom**: Clicking breadcrumb items doesn't navigate.

**Solution**: Verify URL format uses query parameters and correct page names:

```csharp
// Wrong - missing query parameter prefix
new() { Label = "Servers", Url = "Guilds" }  // ERROR

// Correct - include leading slash and query parameter for pages
new() { Label = "Guild Name", Url = $"/Guilds/Details?id={guild.Id}" }
```

### Issue: Header Actions Not Appearing

**Symptom**: No buttons appear in the header.

**Solution**:
1. Verify `Actions` is not null and has items
2. Check `Label` and `Url` are populated
3. Verify style is one of: `Primary`, `Secondary`, `Link`

```csharp
// Wrong - no actions
Header = new GuildHeaderViewModel
{
    // ... other fields ...
    Actions = null  // or not set
};

// Correct
Header = new GuildHeaderViewModel
{
    // ... other fields ...
    Actions = new List<HeaderAction>
    {
        new()
        {
            Label = "Action",
            Url = "...",
            Style = HeaderActionStyle.Primary
        }
    }
};
```

### Issue: Guild Icon Not Displaying

**Symptom**: Only initials show instead of guild icon.

**Solution**: Verify `GuildIconUrl` is set to a valid URL from the guild service:

```csharp
// Ensure guild has icon URL
Header = new GuildHeaderViewModel
{
    GuildIconUrl = guild.IconUrl,  // Must not be null
    // ...
};
```

### Issue: Custom ViewModels Not Rendering

**Symptom**: Page content doesn't appear or shows wrong data.

**Solution**: Verify you're still populating your page's custom ViewModel:

```csharp
// Before layout changes, populate page ViewModel
ViewModel = WelcomeConfigurationViewModel.FromDto(
    welcomeConfig,
    guild.Name,
    guild.IconUrl);

// Then populate layout ViewModels
Breadcrumb = new GuildBreadcrumbViewModel { ... };
Header = new GuildHeaderViewModel { ... };
Navigation = new GuildNavBarViewModel { ... };
```

### Issue: Query Parameter Encoding in URLs

**Symptom**: Guild IDs show as "123456" in breadcrumb but API gets wrong value.

**Solution**: Remember that Discord IDs are `ulong` (64-bit integers). URL encoding handles this automatically - no special formatting needed:

```csharp
// This works fine - {guild.Id} as ulong automatically formats correctly
new() { Label = guild.Name, Url = $"/Guilds/Details?id={guild.Id}" }
```

## Testing Checklist

Before marking a page migration as complete:

- [ ] Layout renders without exceptions
- [ ] Breadcrumbs display with correct hierarchy
- [ ] Current breadcrumb item marked as "current" (no link)
- [ ] Breadcrumb URLs are clickable and navigate correctly
- [ ] Guild header displays guild name and icon
- [ ] Page title displays correctly in header
- [ ] Header description (if provided) displays correctly
- [ ] Action buttons appear (if configured)
- [ ] Action buttons navigate to correct URLs
- [ ] Correct tab is highlighted in guild navigation
- [ ] Guild navigation tabs are clickable
- [ ] Page content renders below layout components
- [ ] No console errors or 404s for CSS/JS resources
- [ ] Layout responsive on mobile (breadcrumbs wrap, buttons stack)
- [ ] Form submission works (if applicable)
- [ ] TempData messages display correctly

## Related Documentation

- [Guild Layout Design Spec](../designs/guild-layout-design-spec.md) - UI component specifications
- [Form Implementation Standards](../articles/form-implementation-standards.md) - Form patterns for guild pages
- [Authorization Policies](../articles/authorization-policies.md) - Permission checks for guild access

## Common Patterns Reference

### Redirect After Form Submission

Preserve success messages when redirecting:

```csharp
public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
{
    // ... process form ...

    _logger.LogInformation("Configuration saved for guild {GuildId}", Input.GuildId);
    SuccessMessage = "Configuration saved successfully.";

    return RedirectToPage("Welcome", new { id = Input.GuildId });
}
```

### Loading ViewModel After Validation Error

Re-populate layout ViewModels in a helper method after validation fails:

```csharp
private async Task LoadViewModelAsync(ulong guildId, CancellationToken cancellationToken)
{
    var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
    if (guild != null)
    {
        // Reload page ViewModel
        var data = await _service.GetConfigAsync(guildId, cancellationToken);
        ViewModel = YourViewModel.FromDto(data);

        // Reload layout ViewModels
        Breadcrumb = new GuildBreadcrumbViewModel { /* ... */ };
        Header = new GuildHeaderViewModel { /* ... */ };
        Navigation = new GuildNavBarViewModel { /* ... */ };
    }
}
```

### Conditional Authorization on Header Actions

Only show certain actions based on user permissions:

```csharp
var canManageSoundboard = User.IsInRole("Admin");  // or check guild-specific permissions

Actions = canManageSoundboard ? new List<HeaderAction>
{
    new()
    {
        Label = "Edit",
        Url = $"/Guilds/Soundboard/Edit?id={guild.Id}",
        Style = HeaderActionStyle.Primary
    }
} : null
```
