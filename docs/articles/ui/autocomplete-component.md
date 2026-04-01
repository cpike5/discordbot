# Autocomplete Component

**Version:** 1.0
**Last Updated:** 2026-01-02

---

## Overview

The autocomplete component provides type-ahead search functionality for filter inputs across the admin UI. It consists of three parts:

1. **JavaScript Module** (`autocomplete.js`) - Client-side behavior and API integration
2. **Razor Partial View** (`_AutocompleteInput.cshtml`) - HTML structure and initialization
3. **ViewModel** (`AutocompleteInputViewModel`) - Configuration options

The component is designed to be accessible (WCAG 2.1 AA compliant), performant (debounced API calls), and reusable across different entity types.

---

## Quick Start

### Basic Usage in Razor Pages

```razor
@using DiscordBot.Bot.ViewModels.Components
@{
    var userAutocomplete = new AutocompleteInputViewModel
    {
        Id = "UserId",
        Name = "UserId",
        Label = "User",
        Placeholder = "Search by username...",
        Endpoint = "/api/autocomplete/users",
        NoResultsMessage = "No users found"
    };
}

<partial name="Shared/Components/_AutocompleteInput" model="userAutocomplete" />
```

### With Pre-populated Value

```razor
var userAutocomplete = new AutocompleteInputViewModel
{
    Id = "UserId",
    Name = "UserId",
    Label = "User",
    Endpoint = "/api/autocomplete/users",
    InitialValue = Model.UserId?.ToString(),        // The ID to submit
    InitialDisplayText = Model.Username             // The text to show
};
```

### Guild-Scoped Search (e.g., Channels)

```razor
var channelAutocomplete = new AutocompleteInputViewModel
{
    Id = "ChannelId",
    Name = "ChannelId",
    Label = "Channel",
    Placeholder = "Select a guild first...",
    Endpoint = "/api/autocomplete/channels",
    GuildIdSourceElement = "GuildId",  // ID of element containing guild ID
    InitialValue = Model.ChannelId?.ToString(),
    InitialDisplayText = Model.ChannelName
};
```

---

## ViewModel Reference

### AutocompleteInputViewModel

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Id` | string | Required | ID for the hidden input (stores selected value) |
| `Name` | string | Required | Form name for submission |
| `Label` | string? | null | Label text displayed above input |
| `Placeholder` | string? | "Search..." | Placeholder text in search input |
| `Endpoint` | string | Required | API endpoint for search (e.g., "/api/autocomplete/users") |
| `InitialValue` | string? | null | Pre-selected value (ID) |
| `InitialDisplayText` | string? | null | Pre-selected display text |
| `GuildIdSourceElement` | string? | null | Element ID containing guild ID for scoped searches |
| `IsRequired` | bool | false | Whether field is required for form submission |
| `MinChars` | int | 2 | Minimum characters before triggering search |
| `DebounceMs` | int | 300 | Delay in milliseconds before API call |
| `MaxResults` | int | 25 | Maximum suggestions to display |
| `NoResultsMessage` | string | "No results found" | Message when search returns empty |
| `HelpText` | string? | null | Help text displayed below input |

---

## JavaScript API

### AutocompleteManager

The `AutocompleteManager` is a singleton that manages all autocomplete instances on the page.

#### Initialization

The component auto-initializes when using the Razor partial. For manual initialization:

```javascript
const instance = AutocompleteManager.init({
    inputId: 'UserId-search',      // Visible search input ID
    hiddenInputId: 'UserId',       // Hidden input ID for form value
    endpoint: '/api/autocomplete/users',
    guildIdSource: 'GuildId',      // Optional: element ID for guild filter
    minChars: 2,
    debounceMs: 300,
    maxResults: 25,
    placeholder: 'Search by username...',
    noResultsMessage: 'No users found'
});
```

#### Instance Methods

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `setValue(id, displayText)` | id: string, displayText: string | void | Programmatically set the value |
| `getValue()` | - | `{id, displayText}` or null | Get current selection |
| `clear()` | - | void | Clear the selection |
| `destroy()` | - | void | Remove the instance and cleanup |

#### Static Methods

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `AutocompleteManager.init(config)` | config object | instance | Create new instance |
| `AutocompleteManager.get(inputId)` | inputId: string | instance or undefined | Get existing instance |
| `AutocompleteManager.destroy(inputId)` | inputId: string | void | Destroy specific instance |
| `AutocompleteManager.destroyAll()` | - | void | Destroy all instances |

#### Events

The component dispatches custom events on the visible search input:

| Event | Detail | Description |
|-------|--------|-------------|
| `autocomplete:select` | `{ item: { id, displayText } }` | Fired when user selects an item |
| `autocomplete:clear` | - | Fired when selection is cleared |

The hidden input also fires standard `change` events when the value changes.

```javascript
// Listen for selection
document.getElementById('UserId-search').addEventListener('autocomplete:select', (e) => {
    console.log('Selected:', e.detail.item);
});

// Listen for value changes on hidden input
document.getElementById('UserId').addEventListener('change', (e) => {
    console.log('Value changed:', e.target.value);
});
```

---

## API Response Format

### Standard Suggestion

Most endpoints return suggestions in this format:

```json
{
    "id": "123456789012345678",
    "displayText": "Display Name"
}
```

### Channel Suggestion (Extended)

The channels endpoint includes additional metadata:

```json
{
    "id": "123456789012345678",
    "displayText": "general",
    "channelType": "Text"
}
```

Channel types are rendered with appropriate icons (text channel `#`, voice channel speaker icon).

---

## Available Endpoints

| Endpoint | Use Case | Required Parameters | Optional Parameters |
|----------|----------|---------------------|---------------------|
| `/api/autocomplete/users` | User filter inputs | `search` | `guildId` |
| `/api/autocomplete/guilds` | Guild filter inputs | `search` | - |
| `/api/autocomplete/channels` | Channel filter inputs | `search`, `guildId` | - |
| `/api/autocomplete/commands` | Command filter inputs | `search` | - |

See [API Endpoints Reference](api-endpoints.md#autocomplete-api) for detailed documentation.

---

## Accessibility

The component implements ARIA best practices for accessible autocomplete:

### ARIA Attributes

| Attribute | Element | Value | Purpose |
|-----------|---------|-------|---------|
| `role="combobox"` | Input | - | Identifies as combobox pattern |
| `aria-autocomplete="list"` | Input | - | Indicates list-based autocomplete |
| `aria-expanded` | Input | true/false | Indicates dropdown state |
| `aria-haspopup="listbox"` | Input | - | Indicates popup type |
| `aria-controls` | Input | listbox ID | Links to dropdown |
| `aria-activedescendant` | Input | option ID | Current focused option |
| `role="listbox"` | Dropdown | - | Identifies dropdown role |
| `role="option"` | Items | - | Identifies option role |
| `aria-selected` | Items | true/false | Indicates selection state |

### Keyboard Navigation

| Key | Action |
|-----|--------|
| `ArrowDown` | Open dropdown / move to next item |
| `ArrowUp` | Move to previous item |
| `Enter` | Select highlighted item |
| `Escape` | Close dropdown |
| `Tab` | Close dropdown and move focus |

### Screen Reader Announcements

The component announces results to screen readers via a live region:
- "5 results available" when results load
- "No results found" when search returns empty

---

## Styling

The component uses CSS classes defined in `site.css`. All styles follow the design system.

### CSS Classes

| Class | Purpose |
|-------|---------|
| `.autocomplete-wrapper` | Container for positioning |
| `.autocomplete-dropdown` | Dropdown container |
| `.autocomplete-dropdown.active` | Visible dropdown state |
| `.autocomplete-dropdown.above` | Positioned above input |
| `.autocomplete-item` | Individual suggestion |
| `.autocomplete-item.selected` | Highlighted/selected item |
| `.autocomplete-item-icon` | Icon container |
| `.autocomplete-item-text` | Display text |
| `.autocomplete-item-meta` | Secondary info (e.g., channel type) |
| `.autocomplete-clear` | Clear button |
| `.autocomplete-loading` | Loading state (shows spinner) |
| `.autocomplete-no-results` | No results message |
| `.autocomplete-error` | Error message |

### Customizing Appearance

The component inherits from the design system. To customize:

```css
/* Example: Change selected item background */
.autocomplete-item.selected {
    background-color: rgba(203, 78, 27, 0.15); /* Orange tint */
}

/* Example: Change dropdown shadow */
.autocomplete-dropdown {
    box-shadow: 0 4px 20px rgba(0, 0, 0, 0.2);
}
```

---

## Usage Examples

### Message Logs Page

Three autocomplete inputs with channel depending on guild:

```razor
@{
    var authorAutocomplete = new AutocompleteInputViewModel
    {
        Id = "AuthorId",
        Name = "AuthorId",
        Label = "User",
        Placeholder = "Search by username...",
        Endpoint = "/api/autocomplete/users",
        InitialValue = Model.AuthorId?.ToString(),
        InitialDisplayText = Model.AuthorUsername,
        NoResultsMessage = "No users found"
    };

    var guildAutocomplete = new AutocompleteInputViewModel
    {
        Id = "GuildId",
        Name = "GuildId",
        Label = "Guild",
        Placeholder = "Search by guild name...",
        Endpoint = "/api/autocomplete/guilds",
        InitialValue = Model.GuildId?.ToString(),
        InitialDisplayText = Model.GuildName,
        NoResultsMessage = "No guilds found"
    };

    var channelAutocomplete = new AutocompleteInputViewModel
    {
        Id = "ChannelId",
        Name = "ChannelId",
        Label = "Channel",
        Placeholder = "Select a guild first...",
        Endpoint = "/api/autocomplete/channels",
        GuildIdSourceElement = "GuildId",
        InitialValue = Model.ChannelId?.ToString(),
        InitialDisplayText = Model.ChannelName,
        NoResultsMessage = "No channels found"
    };
}

<div class="grid grid-cols-1 md:grid-cols-3 gap-4">
    <partial name="Shared/Components/_AutocompleteInput" model="authorAutocomplete" />
    <partial name="Shared/Components/_AutocompleteInput" model="guildAutocomplete" />
    <partial name="Shared/Components/_AutocompleteInput" model="channelAutocomplete" />
</div>

@section Scripts {
    <script src="~/js/autocomplete.js" asp-append-version="true"></script>
    <script src="~/js/message-logs.js" asp-append-version="true"></script>
}
```

### Handling Channel/Guild Dependency

When channels should be disabled until a guild is selected:

```javascript
// message-logs.js
document.addEventListener('DOMContentLoaded', function() {
    const GUILD_HIDDEN_INPUT_ID = 'GuildId';
    const CHANNEL_SEARCH_INPUT_ID = 'ChannelId-search';
    const CHANNEL_HIDDEN_INPUT_ID = 'ChannelId';

    function updateChannelInputState() {
        const guildInput = document.getElementById(GUILD_HIDDEN_INPUT_ID);
        const channelSearch = document.getElementById(CHANNEL_SEARCH_INPUT_ID);

        if (!channelSearch) return;

        const hasGuild = guildInput && guildInput.value;

        channelSearch.disabled = !hasGuild;
        channelSearch.placeholder = hasGuild
            ? 'Search by channel name...'
            : 'Select a guild first...';

        // Clear channel when guild changes
        if (!hasGuild) {
            const channelHidden = document.getElementById(CHANNEL_HIDDEN_INPUT_ID);
            if (channelHidden) channelHidden.value = '';
            channelSearch.value = '';
        }
    }

    // Initial state
    updateChannelInputState();

    // Listen for guild changes
    const guildInput = document.getElementById(GUILD_HIDDEN_INPUT_ID);
    if (guildInput) {
        guildInput.addEventListener('change', updateChannelInputState);
    }
});
```

### Audit Logs Page

Single autocomplete for actor ID:

```razor
@{
    var actorAutocomplete = new AutocompleteInputViewModel
    {
        Id = "ActorId",
        Name = "ActorId",
        Label = "Actor",
        Placeholder = "Search by username...",
        Endpoint = "/api/autocomplete/users",
        InitialValue = Model.ActorId,
        InitialDisplayText = Model.ActorDisplayName,
        NoResultsMessage = "No users found"
    };
}

<partial name="Shared/Components/_AutocompleteInput" model="actorAutocomplete" />

@section Scripts {
    <script src="~/js/autocomplete.js" asp-append-version="true"></script>
}
```

---

## Populating Display Names in PageModel

When a page loads with pre-selected filter values, the PageModel must populate the display text:

```csharp
public class IndexModel : PageModel
{
    private readonly IMessageLogRepository _messageLogRepository;
    private readonly IGuildService _guildService;

    [BindProperty(SupportsGet = true)]
    public ulong? AuthorId { get; set; }

    public string? AuthorUsername { get; set; }  // Display text

    public async Task<IActionResult> OnGetAsync()
    {
        // Populate display name from AuthorId
        if (AuthorId.HasValue)
        {
            var messages = await _messageLogRepository.GetUserMessagesAsync(
                AuthorId.Value,
                limit: 1);
            AuthorUsername = messages.FirstOrDefault()?.User?.Username;
        }

        // ... rest of page logic
        return Page();
    }
}
```

---

## Integration Checklist

When adding autocomplete to a new page:

- [ ] Add `AutocompleteInputViewModel` definitions in Razor code block
- [ ] Replace text inputs with `<partial name="Shared/Components/_AutocompleteInput" />` calls
- [ ] Add display name properties to PageModel (e.g., `AuthorUsername`)
- [ ] Inject required services to PageModel for display name lookup
- [ ] Populate display names in `OnGetAsync` when filter values are present
- [ ] Add `autocomplete.js` script reference in `@section Scripts`
- [ ] Add page-specific JS for dependencies (e.g., channel disabled until guild selected)
- [ ] Test keyboard navigation and screen reader announcements

---

## Related Documentation

- [API Endpoints - Autocomplete API](api-endpoints.md#autocomplete-api) - Backend endpoint reference
- [Design System](design-system.md) - Visual design specifications
- [Form Implementation Standards](form-implementation-standards.md) - Form patterns and validation

---

*Last Updated: January 2, 2026*
