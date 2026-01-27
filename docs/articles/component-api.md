# Component API Usage Guide

**Version:** 1.2
**Last Updated:** 2026-01-26
**Target Framework:** .NET 8 Razor Pages with Tailwind CSS

---

## Overview

This guide provides comprehensive documentation for the Discord Bot Admin UI component library. All components are built as reusable Razor partial views with strongly-typed ViewModels, designed to maintain consistency with the [Design System](design-system.md) and provide an accessible, maintainable developer experience.

### Key Features

- **Strongly-typed ViewModels**: All components use C# record types for compile-time safety
- **Tailwind CSS Integration**: Components leverage the design system's utility classes
- **Accessibility-first**: WCAG 2.1 AA compliant with proper ARIA attributes
- **Flexible Configuration**: Support for variants, sizes, states, and custom styling
- **Partial View Pattern**: Easy integration with Razor Pages and Blazor

### Basic Usage Pattern

All components follow the same rendering pattern:

```cshtml
@using DiscordBot.Bot.ViewModels.Components

<partial name="Shared/Components/_ComponentName" model="viewModel" />
```

Or in code-behind (PageModel):

```csharp
using DiscordBot.Bot.ViewModels.Components;

public class MyPageModel : PageModel
{
    public ButtonViewModel MyButton { get; set; } = new()
    {
        Text = "Click Me",
        Variant = ButtonVariant.Primary,
        Size = ButtonSize.Medium
    };
}
```

Then in the view:

```cshtml
<partial name="Shared/Components/_Button" model="Model.MyButton" />
```

---

## Quick Reference

| Component | Purpose | Common Use Cases |
|-----------|---------|------------------|
| [Button](#button-component) | Interactive actions | Forms, CTAs, navigation triggers |
| [Badge](#badge-component) | Status labels and tags | Roles, statuses, counts |
| [StatusIndicator](#statusindicator-component) | Real-time status display | Bot status, user presence |
| [Card](#card-component) | Content containers | Dashboard widgets, grouped content |
| [FormInput](#forminput-component) | Text input fields | Forms, search bars |
| [FormSelect](#formselect-component) | Dropdown selection | Forms, filters |
| [Alert](#alert-component) | User notifications | Success/error messages, warnings |
| [LoadingSpinner](#loadingspinner-component) | Loading states | Async operations, page loads |
| [EmptyState](#emptystate-component) | No data feedback | Empty lists, search results |
| [Pagination](#pagination-component) | Data navigation | Tables, lists, search results |
| [NavTabs](#navtabs-component) | Tabbed navigation | Page navigation, in-page tabs, AJAX content |
| [SortDropdown](#sortdropdown-component) | Sort selection dropdown | Table headers, list sorting |
| [FilterPanel](#filterpanel-javascript-utility) | Collapsible filter sections | Data filtering, date range selection |

---

## Button Component

Interactive button element with multiple variants, sizes, and states including loading and icon support.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Text` | `string` | `""` | Button text content (required unless `IsIconOnly`) |
| `Variant` | `ButtonVariant` | `Primary` | Visual style variant |
| `Size` | `ButtonSize` | `Medium` | Button size |
| `Type` | `string?` | `"button"` | HTML button type: `button`, `submit`, `reset` |
| `IconLeft` | `string?` | `null` | SVG path for left icon |
| `IconRight` | `string?` | `null` | SVG path for right icon |
| `IsDisabled` | `bool` | `false` | Disables the button |
| `IsLoading` | `bool` | `false` | Shows loading spinner instead of icon |
| `IsIconOnly` | `bool` | `false` | Renders as icon-only button (hides text) |
| `AriaLabel` | `string?` | `null` | Accessibility label (required for icon-only buttons) |
| `OnClick` | `string?` | `null` | JavaScript click handler function name |
| `AdditionalAttributes` | `Dictionary<string, string>?` | `null` | Custom HTML attributes |

### Enums

#### ButtonVariant

| Value | Description | Visual Style |
|-------|-------------|--------------|
| `Primary` | Main call-to-action | Orange background, white text |
| `Secondary` | Secondary actions | Transparent with border, hover effect |
| `Accent` | Informational actions | Blue background, white text |
| `Danger` | Destructive actions | Red background, white text |
| `Ghost` | Subtle actions | Transparent, minimal styling |

#### ButtonSize

| Value | Description | Padding | Font Size |
|-------|-------------|---------|-----------|
| `Small` | Compact button | `py-1.5 px-3` | `text-xs` |
| `Medium` | Default size | `py-2.5 px-5` | `text-sm` |
| `Large` | Prominent button | `py-3 px-6` | `text-base` |

### Basic Usage

**Simple Primary Button:**

```csharp
var button = new ButtonViewModel
{
    Text = "Save Changes",
    Variant = ButtonVariant.Primary,
    Type = "submit"
};
```

```cshtml
<partial name="Shared/Components/_Button" model="button" />
```

**Button with Icon:**

```csharp
var addButton = new ButtonViewModel
{
    Text = "Add Server",
    Variant = ButtonVariant.Primary,
    IconLeft = "M12 4v16m8-8H4" // Plus icon path
};
```

**Icon-Only Button:**

```csharp
var settingsButton = new ButtonViewModel
{
    Text = "Settings", // Used for accessibility
    IsIconOnly = true,
    IconLeft = "M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z",
    AriaLabel = "Open Settings"
};
```

### Common Patterns

**Loading State:**

```csharp
var submitButton = new ButtonViewModel
{
    Text = "Processing...",
    Variant = ButtonVariant.Primary,
    IsLoading = true,
    IsDisabled = true
};
```

**Button Group:**

```cshtml
<div class="flex gap-3">
    <partial name="Shared/Components/_Button" model="@(new ButtonViewModel
    {
        Text = "Save",
        Variant = ButtonVariant.Primary,
        Type = "submit"
    })" />
    <partial name="Shared/Components/_Button" model="@(new ButtonViewModel
    {
        Text = "Cancel",
        Variant = ButtonVariant.Secondary,
        OnClick = "closeModal()"
    })" />
</div>
```

**Danger Action with Confirmation:**

```csharp
var deleteButton = new ButtonViewModel
{
    Text = "Delete Server",
    Variant = ButtonVariant.Danger,
    IconLeft = "M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16",
    OnClick = "confirmDelete()"
};
```

### Accessibility Notes

- All buttons include `focus-visible:outline` for keyboard navigation
- Icon-only buttons automatically use `Text` property for `aria-label` if `AriaLabel` is not provided
- Disabled state applies `disabled` attribute and reduces opacity
- Loading state shows spinner with implicit "loading" indication

---

## Badge Component

Small labeled element for displaying statuses, tags, roles, or counts with support for icons and removable badges.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Text` | `string` | `""` | Badge label text (required) |
| `Variant` | `BadgeVariant` | `Default` | Color scheme variant |
| `Size` | `BadgeSize` | `Medium` | Badge size |
| `Style` | `BadgeStyle` | `Filled` | Filled or outline style |
| `IconLeft` | `string?` | `null` | SVG path for left icon |
| `IsRemovable` | `bool` | `false` | Shows remove button |
| `OnRemove` | `string?` | `null` | JavaScript function for remove action |

### Enums

#### BadgeVariant

| Value | Description | Color |
|-------|-------------|-------|
| `Default` | Neutral/gray | `bg-bg-tertiary` |
| `Orange` | Primary accent | `bg-accent-orange` |
| `Blue` | Secondary accent | `bg-accent-blue` |
| `Success` | Positive state | `bg-success` (green) |
| `Warning` | Caution state | `bg-warning` (amber) |
| `Error` | Error state | `bg-error` (red) |
| `Info` | Informational | `bg-info` (cyan) |

#### BadgeSize

| Value | Padding | Font Size |
|-------|---------|-----------|
| `Small` | `px-2 py-0.5` | `text-[10px]` |
| `Medium` | `px-3 py-1` | `text-xs` |
| `Large` | `px-4 py-1.5` | `text-sm` |

#### BadgeStyle

| Value | Description |
|-------|-------------|
| `Filled` | Solid background color |
| `Outline` | Border only, transparent background |

### Basic Usage

**Simple Status Badge:**

```csharp
var badge = new BadgeViewModel
{
    Text = "Online",
    Variant = BadgeVariant.Success
};
```

**Role Badge with Icon:**

```csharp
var adminBadge = new BadgeViewModel
{
    Text = "Admin",
    Variant = BadgeVariant.Orange,
    IconLeft = "M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"
};
```

**Outline Badge:**

```csharp
var outlineBadge = new BadgeViewModel
{
    Text = "Moderator",
    Variant = BadgeVariant.Blue,
    Style = BadgeStyle.Outline
};
```

### Common Patterns

**Removable Tag:**

```csharp
var tag = new BadgeViewModel
{
    Text = "JavaScript",
    Variant = BadgeVariant.Info,
    IsRemovable = true,
    OnRemove = "removeTag('javascript')"
};
```

**Count Badge:**

```csharp
var countBadge = new BadgeViewModel
{
    Text = "12",
    Variant = BadgeVariant.Default,
    Size = BadgeSize.Small
};
```

**Status in Table:**

```cshtml
<td class="table-cell">
    <partial name="Shared/Components/_Badge" model="@(new BadgeViewModel
    {
        Text = user.IsActive ? "Active" : "Inactive",
        Variant = user.IsActive ? BadgeVariant.Success : BadgeVariant.Default
    })" />
</td>
```

### Accessibility Notes

- Badges use `<span>` element with semantic color classes
- Removable badges include `aria-label="Remove"` on close button
- Remove button has hover state for keyboard/mouse interaction

---

## StatusIndicator Component

Displays real-time status with colored dot indicator and optional text label, supporting pulsing animation.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Status` | `StatusType` | `Offline` | Status type (determines color) |
| `Text` | `string?` | `null` | Optional status label text |
| `DisplayStyle` | `StatusDisplayStyle` | `DotWithText` | Display variant |
| `IsPulsing` | `bool` | `false` | Enables pulsing animation |
| `Size` | `StatusSize` | `Medium` | Indicator size |

### Enums

#### StatusType

| Value | Description | Color |
|-------|-------------|-------|
| `Online` | Active/connected | Green (`#10b981`) |
| `Idle` | Away/inactive | Amber (`#f59e0b`) |
| `Busy` | Do Not Disturb | Red (`#ef4444`) |
| `Offline` | Disconnected | Gray (`#7a7876`) |

#### StatusDisplayStyle

| Value | Description |
|-------|-------------|
| `DotOnly` | Shows only the colored dot |
| `DotWithText` | Dot with text label inline |
| `BadgeStyle` | Pill-shaped badge with dot and text |

#### StatusSize

| Value | Dimensions |
|-------|------------|
| `Small` | `w-1.5 h-1.5` (6px) |
| `Medium` | `w-2 h-2` (8px) |
| `Large` | `w-3 h-3` (12px) |

### Basic Usage

**Simple Online Indicator:**

```csharp
var status = new StatusIndicatorViewModel
{
    Status = StatusType.Online,
    Text = "Connected"
};
```

**Pulsing Live Indicator:**

```csharp
var liveStatus = new StatusIndicatorViewModel
{
    Status = StatusType.Online,
    Text = "Live",
    IsPulsing = true
};
```

**Dot Only (Compact):**

```csharp
var dotOnly = new StatusIndicatorViewModel
{
    Status = StatusType.Idle,
    DisplayStyle = StatusDisplayStyle.DotOnly,
    Size = StatusSize.Small
};
```

### Common Patterns

**Bot Status Display:**

```csharp
var botStatus = new StatusIndicatorViewModel
{
    Status = botIsOnline ? StatusType.Online : StatusType.Offline,
    Text = botIsOnline ? "Bot Online" : "Bot Offline",
    IsPulsing = botIsOnline,
    Size = StatusSize.Large
};
```

**User Presence:**

```cshtml
<div class="flex items-center gap-2">
    <img src="@user.AvatarUrl" class="w-10 h-10 rounded-full" />
    <div>
        <div class="font-medium">@user.Username</div>
        <partial name="Shared/Components/_StatusIndicator" model="@(new StatusIndicatorViewModel
        {
            Status = user.Status,
            Text = user.StatusText,
            DisplayStyle = StatusDisplayStyle.DotWithText,
            Size = StatusSize.Small
        })" />
    </div>
</div>
```

### Accessibility Notes

- Uses semantic HTML with proper color contrast
- Text labels provide context for screen readers
- Pulsing animation respects `prefers-reduced-motion` preference

---

## Card Component

Flexible container component for grouping related content with optional header, body, footer, and interactive states.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Title` | `string?` | `null` | Card header title |
| `Subtitle` | `string?` | `null` | Card header subtitle |
| `HeaderContent` | `string?` | `null` | Custom header HTML |
| `HeaderActions` | `string?` | `null` | Header action buttons HTML |
| `BodyContent` | `string?` | `null` | Main content HTML |
| `FooterContent` | `string?` | `null` | Footer content HTML |
| `Variant` | `CardVariant` | `Default` | Visual style variant |
| `IsInteractive` | `bool` | `false` | Adds hover effect and pointer cursor |
| `IsCollapsible` | `bool` | `false` | Enables collapse/expand functionality |
| `IsExpanded` | `bool` | `true` | Initial expanded state (if collapsible) |
| `OnClick` | `string?` | `null` | JavaScript click handler |
| `CssClass` | `string?` | `null` | Additional CSS classes |

### Enums

#### CardVariant

| Value | Description | Styling |
|-------|-------------|---------|
| `Default` | Standard card | Border, secondary background |
| `Flat` | Subtle card | No border, minimal background |
| `Elevated` | Raised card | Shadow effect |

### Basic Usage

**Simple Card:**

```csharp
var card = new CardViewModel
{
    Title = "Server Statistics",
    BodyContent = "<p class='text-text-secondary'>Content goes here</p>"
};
```

**Card with Actions:**

```csharp
var actionCard = new CardViewModel
{
    Title = "Recent Activity",
    Subtitle = "Last 24 hours",
    HeaderActions = "<button class='btn btn-sm btn-secondary'>View All</button>",
    BodyContent = activityHtml
};
```

**Interactive Card:**

```csharp
var clickableCard = new CardViewModel
{
    Title = "Server: Main Guild",
    BodyContent = serverDetailsHtml,
    IsInteractive = true,
    OnClick = "navigateToServer('12345')",
    Variant = CardVariant.Elevated
};
```

### Common Patterns

**Dashboard Widget:**

```csharp
var statsCard = new CardViewModel
{
    Title = "Total Members",
    BodyContent = @"
        <div class='text-4xl font-bold text-text-primary'>1,234</div>
        <p class='text-sm text-success mt-2'>↑ 12% from last month</p>
    ",
    Variant = CardVariant.Default
};
```

**Card with Footer:**

```csharp
var dataCard = new CardViewModel
{
    Title = "Command Usage",
    BodyContent = chartHtml,
    FooterContent = @"
        <div class='flex items-center justify-between text-xs text-text-tertiary'>
            <span>Last updated: 2 minutes ago</span>
            <button class='text-accent-blue hover:underline'>Refresh</button>
        </div>
    "
};
```

**Grid of Cards:**

```cshtml
<div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
    @foreach (var stat in Model.Stats)
    {
        <partial name="Shared/Components/_Card" model="@(new CardViewModel
        {
            Title = stat.Title,
            BodyContent = stat.Content,
            Variant = CardVariant.Default
        })" />
    }
</div>
```

### Accessibility Notes

- Proper heading hierarchy with `<h3>` for card titles
- Interactive cards use `role="button"` when clickable
- Collapsible cards implement `aria-expanded` state

---

## FormInput Component

Text input field with label, validation states, help text, icons, and character counting.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Id` | `string` | `""` | Input element ID (required) |
| `Name` | `string` | `""` | Input name attribute (required) |
| `Label` | `string?` | `null` | Field label text |
| `Type` | `string` | `"text"` | Input type: `text`, `email`, `password`, `search`, `url`, `tel` |
| `Placeholder` | `string?` | `null` | Placeholder text |
| `Value` | `string?` | `null` | Input value |
| `HelpText` | `string?` | `null` | Help text below input |
| `Size` | `InputSize` | `Medium` | Input size |
| `ValidationState` | `ValidationState` | `None` | Validation state |
| `ValidationMessage` | `string?` | `null` | Validation message text |
| `IsRequired` | `bool` | `false` | Adds required attribute |
| `IsDisabled` | `bool` | `false` | Disables input |
| `IsReadOnly` | `bool` | `false` | Makes input read-only |
| `IconLeft` | `string?` | `null` | SVG path for left icon |
| `IconRight` | `string?` | `null` | SVG path for right icon |
| `MaxLength` | `int?` | `null` | Maximum character length |
| `ShowCharacterCount` | `bool` | `false` | Shows character counter |
| `AdditionalAttributes` | `Dictionary<string, string>?` | `null` | Custom HTML attributes |

### Enums

#### InputSize

| Value | Padding | Font Size |
|-------|---------|-----------|
| `Small` | `py-1.5 px-3` | `text-xs` |
| `Medium` | `py-2.5 px-3.5` | `text-sm` |
| `Large` | `py-3 px-4` | `text-base` |

#### ValidationState

| Value | Description | Border Color |
|-------|-------------|--------------|
| `None` | No validation | Default border |
| `Success` | Valid input | Green border |
| `Warning` | Warning state | Amber border |
| `Error` | Invalid input | Red border |

### Basic Usage

**Simple Text Input:**

```csharp
var nameInput = new FormInputViewModel
{
    Id = "server-name",
    Name = "serverName",
    Label = "Server Name",
    Placeholder = "Enter server name",
    IsRequired = true
};
```

**Email Input with Validation:**

```csharp
var emailInput = new FormInputViewModel
{
    Id = "email",
    Name = "email",
    Label = "Email Address",
    Type = "email",
    ValidationState = isValid ? ValidationState.Success : ValidationState.Error,
    ValidationMessage = isValid ? "" : "Please enter a valid email address",
    IsRequired = true
};
```

**Search Input with Icon:**

```csharp
var searchInput = new FormInputViewModel
{
    Id = "search",
    Name = "query",
    Type = "search",
    Placeholder = "Search servers...",
    IconLeft = "M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
};
```

### Common Patterns

**Password Input with Toggle:**

```csharp
var passwordInput = new FormInputViewModel
{
    Id = "password",
    Name = "password",
    Label = "Password",
    Type = "password",
    HelpText = "Must be at least 8 characters",
    IconRight = "M15 12a3 3 0 11-6 0 3 3 0 016 0z",
    IsRequired = true
};
```

**Character-Limited Input:**

```csharp
var bioInput = new FormInputViewModel
{
    Id = "bio",
    Name = "bio",
    Label = "Bio",
    Placeholder = "Tell us about yourself",
    MaxLength = 200,
    ShowCharacterCount = true,
    HelpText = "A brief description for your profile"
};
```

**Disabled/Read-Only Input:**

```csharp
var idInput = new FormInputViewModel
{
    Id = "user-id",
    Name = "userId",
    Label = "User ID",
    Value = "123456789",
    IsReadOnly = true,
    HelpText = "This value cannot be changed"
};
```

### Accessibility Notes

- All inputs have associated `<label>` elements
- Required inputs include `required` attribute
- Validation messages use proper ARIA attributes
- Focus states use blue outline for visibility
- Help text uses `aria-describedby` association

---

## FormSelect Component

Dropdown selection component with support for option groups, validation states, and multiple selection.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Id` | `string` | `""` | Select element ID (required) |
| `Name` | `string` | `""` | Select name attribute (required) |
| `Label` | `string?` | `null` | Field label text |
| `Placeholder` | `string?` | `"Select an option"` | Placeholder option text |
| `SelectedValue` | `string?` | `null` | Pre-selected value |
| `Options` | `List<SelectOption>` | `new()` | List of options |
| `OptionGroups` | `List<SelectOptionGroup>?` | `null` | Grouped options |
| `HelpText` | `string?` | `null` | Help text below select |
| `Size` | `InputSize` | `Medium` | Select size |
| `ValidationState` | `ValidationState` | `None` | Validation state |
| `ValidationMessage` | `string?` | `null` | Validation message text |
| `IsRequired` | `bool` | `false` | Adds required attribute |
| `IsDisabled` | `bool` | `false` | Disables select |
| `AllowMultiple` | `bool` | `false` | Enables multiple selection |
| `AdditionalAttributes` | `Dictionary<string, string>?` | `null` | Custom HTML attributes |

### Supporting Types

#### SelectOption

```csharp
public record SelectOption
{
    public string Value { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public bool IsDisabled { get; init; } = false;
}
```

#### SelectOptionGroup

```csharp
public record SelectOptionGroup
{
    public string Label { get; init; } = string.Empty;
    public List<SelectOption> Options { get; init; } = new();
}
```

### Basic Usage

**Simple Dropdown:**

```csharp
var roleSelect = new FormSelectViewModel
{
    Id = "role",
    Name = "role",
    Label = "User Role",
    Options = new List<SelectOption>
    {
        new() { Value = "admin", Text = "Administrator" },
        new() { Value = "mod", Text = "Moderator" },
        new() { Value = "member", Text = "Member" }
    },
    IsRequired = true
};
```

**With Placeholder and Selection:**

```csharp
var regionSelect = new FormSelectViewModel
{
    Id = "region",
    Name = "region",
    Label = "Server Region",
    Placeholder = "Choose a region",
    SelectedValue = "us-east",
    Options = new List<SelectOption>
    {
        new() { Value = "us-east", Text = "US East" },
        new() { Value = "us-west", Text = "US West" },
        new() { Value = "eu-west", Text = "EU West" },
        new() { Value = "asia", Text = "Asia Pacific" }
    }
};
```

**With Option Groups:**

```csharp
var channelSelect = new FormSelectViewModel
{
    Id = "channel",
    Name = "channelId",
    Label = "Target Channel",
    OptionGroups = new List<SelectOptionGroup>
    {
        new()
        {
            Label = "Text Channels",
            Options = new List<SelectOption>
            {
                new() { Value = "1", Text = "#general" },
                new() { Value = "2", Text = "#announcements" }
            }
        },
        new()
        {
            Label = "Voice Channels",
            Options = new List<SelectOption>
            {
                new() { Value = "3", Text = "General Voice" },
                new() { Value = "4", Text = "Gaming" }
            }
        }
    }
};
```

### Common Patterns

**Multiple Selection:**

```csharp
var permissionsSelect = new FormSelectViewModel
{
    Id = "permissions",
    Name = "permissions",
    Label = "Permissions",
    AllowMultiple = true,
    Options = new List<SelectOption>
    {
        new() { Value = "read", Text = "Read Messages" },
        new() { Value = "write", Text = "Send Messages" },
        new() { Value = "manage", Text = "Manage Channels" }
    }
};
```

**With Validation:**

```csharp
var validatedSelect = new FormSelectViewModel
{
    Id = "category",
    Name = "category",
    Label = "Category",
    ValidationState = string.IsNullOrEmpty(selectedValue)
        ? ValidationState.Error
        : ValidationState.None,
    ValidationMessage = "Please select a category",
    Options = categories
};
```

### Accessibility Notes

- All selects have associated `<label>` elements
- Placeholder option has empty value
- Required selects include `required` attribute
- Option groups use `<optgroup>` for semantic grouping
- Disabled options use `disabled` attribute

---

## Alert Component

Notification banner for displaying informational, success, warning, or error messages with optional dismiss functionality.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Variant` | `AlertVariant` | `Info` | Alert type/severity |
| `Title` | `string?` | `null` | Optional alert title |
| `Message` | `string` | `""` | Alert message text (required) |
| `IsDismissible` | `bool` | `false` | Shows dismiss button |
| `ShowIcon` | `bool` | `true` | Shows variant icon |
| `DismissCallback` | `string?` | `null` | JavaScript function for dismiss |

### Enums

#### AlertVariant

| Value | Description | Color | Icon |
|-------|-------------|-------|------|
| `Info` | Informational message | Cyan/Blue | Info circle |
| `Success` | Success confirmation | Green | Check circle |
| `Warning` | Warning/caution | Amber | Exclamation triangle |
| `Error` | Error/danger | Red | X circle |

### Basic Usage

**Simple Info Alert:**

```csharp
var alert = new AlertViewModel
{
    Variant = AlertVariant.Info,
    Message = "Your changes have been saved successfully."
};
```

**Success Alert with Title:**

```csharp
var successAlert = new AlertViewModel
{
    Variant = AlertVariant.Success,
    Title = "Server Created",
    Message = "Your new server has been created and is now active."
};
```

**Dismissible Warning:**

```csharp
var warningAlert = new AlertViewModel
{
    Variant = AlertVariant.Warning,
    Title = "Limited Functionality",
    Message = "Some features are unavailable while the bot is restarting.",
    IsDismissible = true,
    DismissCallback = "dismissAlert('warning-1')"
};
```

### Common Patterns

**Error with Details:**

```csharp
var errorAlert = new AlertViewModel
{
    Variant = AlertVariant.Error,
    Title = "Connection Failed",
    Message = "Unable to connect to Discord API. Please check your internet connection and try again.",
    IsDismissible = true
};
```

**Icon-less Alert:**

```csharp
var subtleAlert = new AlertViewModel
{
    Variant = AlertVariant.Info,
    Message = "This is a subtle informational message.",
    ShowIcon = false
};
```

**Form Validation Summary:**

```cshtml
@if (!ModelState.IsValid)
{
    <partial name="Shared/Components/_Alert" model="@(new AlertViewModel
    {
        Variant = AlertVariant.Error,
        Title = "Validation Errors",
        Message = "Please correct the errors below and try again.",
        IsDismissible = true
    })" />
}
```

### Accessibility Notes

- Uses semantic colors with sufficient contrast
- Dismiss button includes `aria-label="Dismiss"`
- Alert uses appropriate ARIA role implicitly
- Icon provides visual reinforcement (not sole indicator)

---

## LoadingSpinner Component

Loading indicator with multiple visual styles, sizes, and optional message text for async operations.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Variant` | `SpinnerVariant` | `Simple` | Visual style |
| `Size` | `SpinnerSize` | `Medium` | Spinner size |
| `Message` | `string?` | `null` | Loading message text |
| `SubMessage` | `string?` | `null` | Secondary message text |
| `Color` | `SpinnerColor` | `Blue` | Spinner color |
| `IsOverlay` | `bool` | `false` | Full container overlay with backdrop |

### Enums

#### SpinnerVariant

| Value | Description |
|-------|-------------|
| `Simple` | Rotating circle (default) |
| `Dots` | Three bouncing dots |
| `Pulse` | Pulsing circle with ring |

#### SpinnerSize

| Value | Dimensions |
|-------|------------|
| `Small` | 24px |
| `Medium` | 40px |
| `Large` | 64px |

#### SpinnerColor

| Value | Color |
|-------|-------|
| `Blue` | Accent blue (default) |
| `Orange` | Accent orange |
| `White` | White (for dark backgrounds) |

### Basic Usage

**Simple Spinner:**

```csharp
var spinner = new LoadingSpinnerViewModel
{
    Variant = SpinnerVariant.Simple,
    Size = SpinnerSize.Medium
};
```

**With Message:**

```csharp
var loadingSpinner = new LoadingSpinnerViewModel
{
    Variant = SpinnerVariant.Simple,
    Message = "Loading servers...",
    Color = SpinnerColor.Blue
};
```

**Overlay Loading:**

```csharp
var overlaySpinner = new LoadingSpinnerViewModel
{
    Variant = SpinnerVariant.Pulse,
    Size = SpinnerSize.Large,
    Message = "Processing request...",
    SubMessage = "This may take a few moments",
    IsOverlay = true
};
```

### Common Patterns

**Inline Button Loading:**

```csharp
// In button
var button = new ButtonViewModel
{
    Text = "Saving...",
    IsLoading = true // Built-in spinner
};
```

**Page Loading State:**

```cshtml
@if (Model.IsLoading)
{
    <partial name="Shared/Components/_LoadingSpinner" model="@(new LoadingSpinnerViewModel
    {
        Variant = SpinnerVariant.Dots,
        Size = SpinnerSize.Large,
        Message = "Loading dashboard...",
        IsOverlay = true
    })" />
}
else
{
    <!-- Page content -->
}
```

**Card Loading State:**

```csharp
var cardContent = isLoading
    ? "<div class='flex items-center justify-center py-12'>" +
      "  <partial name='Shared/Components/_LoadingSpinner' model='spinner' />" +
      "</div>"
    : actualContent;
```

### Accessibility Notes

- Respects `prefers-reduced-motion` for animations
- Overlay includes backdrop for focus trapping
- Loading messages provide context for screen readers
- Spinner animations are CSS-based (no JavaScript required)

---

## EmptyState Component

Placeholder component for empty lists, no search results, error states, and first-time user experiences.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Type` | `EmptyStateType` | `NoData` | Empty state type (determines icon) |
| `Title` | `string` | `"No Data"` | Main heading text |
| `Description` | `string` | `"There are no items to display."` | Descriptive text |
| `IconSvgPath` | `string?` | `null` | Custom SVG path (overrides type icon) |
| `PrimaryActionText` | `string?` | `null` | Primary button text |
| `PrimaryActionUrl` | `string?` | `null` | Primary button URL |
| `PrimaryActionOnClick` | `string?` | `null` | Primary button JavaScript handler |
| `SecondaryActionText` | `string?` | `null` | Secondary button/link text |
| `SecondaryActionUrl` | `string?` | `null` | Secondary link URL |
| `Size` | `EmptyStateSize` | `Default` | Component size |

### Enums

#### EmptyStateType

| Value | Description | Default Icon |
|-------|-------------|--------------|
| `NoData` | Generic empty state | Folder icon |
| `NoResults` | No search results | Search with X icon |
| `FirstTime` | Onboarding/welcome | Rocket/stars icon |
| `Error` | Error loading data | Warning icon |
| `NoPermission` | Access restricted | Lock icon |
| `Offline` | No connection | Wifi-off icon |

#### EmptyStateSize

| Value | Use Case |
|-------|----------|
| `Compact` | Small containers, cards |
| `Default` | Standard empty states |
| `Large` | Full-page empty states |

### Basic Usage

**Simple Empty List:**

```csharp
var emptyState = new EmptyStateViewModel
{
    Type = EmptyStateType.NoData,
    Title = "No Servers Found",
    Description = "You haven't added any servers yet."
};
```

**With Primary Action:**

```csharp
var emptyServers = new EmptyStateViewModel
{
    Type = EmptyStateType.FirstTime,
    Title = "Welcome to Your Dashboard",
    Description = "Get started by adding your first server to the bot.",
    PrimaryActionText = "Add Server",
    PrimaryActionUrl = "/servers/add"
};
```

**Search Results Empty:**

```csharp
var noResults = new EmptyStateViewModel
{
    Type = EmptyStateType.NoResults,
    Title = "No Results Found",
    Description = $"No servers match your search for '{searchQuery}'.",
    SecondaryActionText = "Clear Search",
    SecondaryActionUrl = "/servers"
};
```

### Common Patterns

**Error State:**

```csharp
var errorState = new EmptyStateViewModel
{
    Type = EmptyStateType.Error,
    Title = "Failed to Load Data",
    Description = "An error occurred while loading the server list. Please try again.",
    PrimaryActionText = "Retry",
    PrimaryActionOnClick = "location.reload()",
    Size = EmptyStateSize.Default
};
```

**No Permission:**

```csharp
var noAccess = new EmptyStateViewModel
{
    Type = EmptyStateType.NoPermission,
    Title = "Access Restricted",
    Description = "You don't have permission to view this content. Contact an administrator for access.",
    Size = EmptyStateSize.Large
};
```

**Conditional Rendering:**

```cshtml
@if (!Model.Servers.Any())
{
    <partial name="Shared/Components/_EmptyState" model="@(new EmptyStateViewModel
    {
        Type = EmptyStateType.NoData,
        Title = "No Servers",
        Description = "Add your first server to get started.",
        PrimaryActionText = "Add Server",
        PrimaryActionUrl = "/servers/add"
    })" />
}
else
{
    <!-- Server list -->
}
```

### Accessibility Notes

- Uses semantic heading hierarchy
- Buttons/links have proper focus states
- Icon uses decorative `aria-hidden="true"`
- Text content is fully accessible to screen readers

---

## Pagination Component

Navigation component for paginated data with page numbers, item counts, and page size selection.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CurrentPage` | `int` | `1` | Active page number (1-indexed) |
| `TotalPages` | `int` | `1` | Total number of pages |
| `TotalItems` | `int` | `0` | Total number of items |
| `PageSize` | `int` | `10` | Items per page |
| `PageSizeOptions` | `int[]` | `[10, 25, 50, 100]` | Available page sizes |
| `Style` | `PaginationStyle` | `Full` | Visual style variant |
| `ShowPageSizeSelector` | `bool` | `false` | Shows page size dropdown |
| `ShowItemCount` | `bool` | `false` | Shows "Showing X-Y of Z" text |
| `ShowFirstLast` | `bool` | `true` | Shows First/Last buttons |
| `BaseUrl` | `string` | `""` | Base URL for page links |
| `PageParameterName` | `string` | `"page"` | Query string parameter for page |
| `PageSizeParameterName` | `string` | `"pageSize"` | Query string parameter for page size |

### Enums

#### PaginationStyle

| Value | Description |
|-------|-------------|
| `Full` | First, Prev, page numbers, Next, Last buttons |
| `Simple` | Only Previous/Next buttons |
| `Compact` | Previous, "Page X of Y", Next |
| `Bordered` | Connected button group style |

### Basic Usage

**Simple Pagination:**

```csharp
var pagination = new PaginationViewModel
{
    CurrentPage = 2,
    TotalPages = 10,
    BaseUrl = "/servers"
};
```

**With Item Count:**

```csharp
var paginationWithCount = new PaginationViewModel
{
    CurrentPage = 1,
    TotalPages = 5,
    TotalItems = 47,
    PageSize = 10,
    ShowItemCount = true,
    BaseUrl = "/users"
};
```

**With Page Size Selector:**

```csharp
var customPagination = new PaginationViewModel
{
    CurrentPage = Model.Page,
    TotalPages = Model.TotalPages,
    TotalItems = Model.TotalCount,
    PageSize = Model.PageSize,
    PageSizeOptions = new[] { 10, 25, 50, 100 },
    ShowPageSizeSelector = true,
    ShowItemCount = true,
    BaseUrl = "/servers",
    Style = PaginationStyle.Full
};
```

### Common Patterns

**Table Pagination:**

```cshtml
<div class="space-y-4">
    <!-- Table -->
    <table class="table">
        <!-- Table content -->
    </table>

    <!-- Pagination -->
    <partial name="Shared/Components/_Pagination" model="@(new PaginationViewModel
    {
        CurrentPage = Model.CurrentPage,
        TotalPages = Model.TotalPages,
        TotalItems = Model.TotalItems,
        PageSize = Model.PageSize,
        ShowItemCount = true,
        BaseUrl = Request.Path
    })" />
</div>
```

**Compact Mobile Pagination:**

```csharp
var mobilePagination = new PaginationViewModel
{
    CurrentPage = currentPage,
    TotalPages = totalPages,
    Style = PaginationStyle.Compact,
    ShowFirstLast = false,
    BaseUrl = "/search"
};
```

**Custom Query Parameters:**

```csharp
var customPagination = new PaginationViewModel
{
    CurrentPage = Model.CurrentPage,
    TotalPages = Model.TotalPages,
    BaseUrl = "/api/data",
    PageParameterName = "pageNumber",
    PageSizeParameterName = "itemsPerPage"
};
// Generates: /api/data?pageNumber=2&itemsPerPage=25
```

### Accessibility Notes

- Uses `<nav>` with `aria-label="Pagination"`
- Current page marked with `aria-current="page"`
- Disabled buttons have `disabled` attribute
- Page links use semantic `<a>` elements
- Keyboard navigable

---

## NavTabs Component

Unified tabbed navigation component with support for page navigation, in-page panels, and AJAX content loading with full accessibility.

### Properties

#### NavTabsViewModel

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ContainerId` | `string` | `""` | Unique identifier for the tab container (required) |
| `Tabs` | `List<NavTabItem>` | `new()` | Collection of tab items |
| `ActiveTabId` | `string?` | `null` | ID of the currently active tab |
| `StyleVariant` | `NavTabStyle` | `Underline` | Visual style variant |
| `NavigationMode` | `NavMode` | `Page` | How tab navigation works |
| `PersistenceMode` | `NavPersistence` | `None` | How to persist active tab state |

#### NavTabItem

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Id` | `string` | `""` | Unique tab identifier (required) |
| `Label` | `string` | `""` | Display text for the tab (required) |
| `ShortLabel` | `string?` | `null` | Shorter label for mobile displays |
| `Href` | `string?` | `null` | Navigation URL (required for Page mode) |
| `IconPathOutline` | `string?` | `null` | SVG path for outline icon |
| `IconPathSolid` | `string?` | `null` | SVG path for solid icon (active state) |
| `Disabled` | `bool` | `false` | Whether the tab is disabled |

### Enums

#### NavTabStyle

| Value | Description | Use Case |
|-------|-------------|----------|
| `Underline` | Bottom border indicator | Standard page sections |
| `Pills` | Rounded pill background | Compact/grouped options |
| `Bordered` | Full border container | Portal-style navigation |

#### NavMode

| Value | Description | Requires |
|-------|-------------|----------|
| `Page` | Full page navigation | `Href` on each tab |
| `InPage` | Show/hide pre-rendered panels | Panels with `data-nav-panel-for` |
| `Ajax` | Fetch content dynamically | `data-ajax-url` on tabs |

#### NavPersistence

| Value | Description |
|-------|-------------|
| `None` | No persistence |
| `Hash` | URL hash (e.g., `#settings`) |
| `LocalStorage` | Browser localStorage |

### Basic Usage

**Page Navigation (Default):**

```csharp
var navTabs = new NavTabsViewModel
{
    ContainerId = "guild-nav",
    Tabs = new List<NavTabItem>
    {
        new() { Id = "overview", Label = "Overview", Href = "/guild/123/overview" },
        new() { Id = "members", Label = "Members", Href = "/guild/123/members" },
        new() { Id = "settings", Label = "Settings", Href = "/guild/123/settings" }
    },
    ActiveTabId = "overview",
    StyleVariant = NavTabStyle.Underline,
    NavigationMode = NavMode.Page
};
```

```cshtml
<partial name="Shared/Components/_NavTabs" model="navTabs" />
```

**With Icons:**

```csharp
var navTabs = new NavTabsViewModel
{
    ContainerId = "audio-nav",
    Tabs = new List<NavTabItem>
    {
        new()
        {
            Id = "soundboard",
            Label = "Soundboard",
            ShortLabel = "Sounds",
            Href = "/audio/soundboard",
            IconPathOutline = "M9 19V6l12-3v13M9 19c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zm12-3c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zM9 10l12-3",
            IconPathSolid = "M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"
        },
        new()
        {
            Id = "queue",
            Label = "Queue",
            Href = "/audio/queue",
            IconPathOutline = "M4 6h16M4 10h16M4 14h16M4 18h16",
            IconPathSolid = "M3 5h18v2H3V5zm0 4h18v2H3V9zm0 4h18v2H3v-2zm0 4h18v2H3v-2z"
        }
    },
    ActiveTabId = "soundboard",
    StyleVariant = NavTabStyle.Pills
};
```

**In-Page Tabs:**

```csharp
var inPageTabs = new NavTabsViewModel
{
    ContainerId = "profile-tabs",
    Tabs = new List<NavTabItem>
    {
        new() { Id = "details", Label = "Details" },
        new() { Id = "activity", Label = "Activity" },
        new() { Id = "permissions", Label = "Permissions" }
    },
    ActiveTabId = "details",
    NavigationMode = NavMode.InPage,
    PersistenceMode = NavPersistence.Hash
};
```

```cshtml
<partial name="Shared/Components/_NavTabs" model="inPageTabs" />

<div data-nav-panel-for="profile-tabs" data-tab-id="details">
    <!-- Details content -->
</div>
<div data-nav-panel-for="profile-tabs" data-tab-id="activity" hidden>
    <!-- Activity content -->
</div>
<div data-nav-panel-for="profile-tabs" data-tab-id="permissions" hidden>
    <!-- Permissions content -->
</div>
```

**AJAX Tabs:**

```csharp
var ajaxTabs = new NavTabsViewModel
{
    ContainerId = "performance-tabs",
    Tabs = new List<NavTabItem>
    {
        new() { Id = "overview", Label = "Overview" },
        new() { Id = "health", Label = "Health" },
        new() { Id = "metrics", Label = "Metrics" }
    },
    ActiveTabId = "overview",
    NavigationMode = NavMode.Ajax,
    PersistenceMode = NavPersistence.Hash
};
```

```cshtml
<!-- Tabs with data-ajax-url handled by partial -->
<partial name="Shared/Components/_NavTabs" model="ajaxTabs" />

<!-- Content panels created dynamically by JavaScript -->
```

### JavaScript API

The NavTabs JavaScript module provides programmatic control. It auto-initializes on `DOMContentLoaded`.

**Public Methods:**

| Method | Description |
|--------|-------------|
| `NavTabs.init(containerId, options)` | Initialize a specific container |
| `NavTabs.switchTo(containerId, tabId)` | Programmatically switch tabs |
| `NavTabs.getActiveTab(containerId)` | Get active tab ID |
| `NavTabs.retry(containerId, tabId)` | Retry failed AJAX load |
| `NavTabs.destroy(containerId)` | Clean up instance |
| `NavTabs.announce(message)` | Announce to screen readers |

**Events:**

```javascript
// Listen for tab changes
document.addEventListener('navtabchange', function(e) {
    console.log('Tab changed:', e.detail.containerId, e.detail.tabId);
});
```

**Manual Initialization:**

```javascript
// For dynamically added tabs
NavTabs.init('dynamic-tabs', {
    requestTimeout: 15000,
    loadingDelay: 200
});
```

**Programmatic Tab Switch:**

```javascript
// Switch to a specific tab
NavTabs.switchTo('guild-nav', 'settings');

// Get current active tab
const activeTab = NavTabs.getActiveTab('guild-nav');
```

### Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `containerSelector` | `[data-nav-tabs]` | Container element selector |
| `tabSelector` | `.nav-tabs-item` | Tab element selector |
| `panelSelector` | `[data-nav-panel-for]` | Panel element selector |
| `tablistSelector` | `.nav-tabs-list` | Tablist element selector |
| `activeClass` | `active` | CSS class for active state |
| `loadingClass` | `loading` | CSS class for loading state |
| `requestTimeout` | `10000` | AJAX timeout in ms |
| `loadingDelay` | `150` | Delay before showing spinner |
| `scrollThreshold` | `5` | Pixels for scroll indicators |

### Keyboard Navigation

| Key | Action |
|-----|--------|
| `←` `→` | Navigate between tabs (wraps around) |
| `Home` | Focus first tab |
| `End` | Focus last tab |
| `Enter` / `Space` | Activate focused tab |
| `Tab` | Move focus to active tab / exit tablist |

### Common Patterns

**Guild Navigation:**

```csharp
public NavTabsViewModel GetGuildNavTabs(ulong guildId, string activeTab)
{
    return new NavTabsViewModel
    {
        ContainerId = $"guild-nav-{guildId}",
        Tabs = new List<NavTabItem>
        {
            new() { Id = "overview", Label = "Overview", ShortLabel = "Home",
                    Href = $"/guild/{guildId}" },
            new() { Id = "members", Label = "Members",
                    Href = $"/guild/{guildId}/members" },
            new() { Id = "commands", Label = "Commands",
                    Href = $"/guild/{guildId}/commands" },
            new() { Id = "settings", Label = "Settings",
                    Href = $"/guild/{guildId}/settings" }
        },
        ActiveTabId = activeTab,
        StyleVariant = NavTabStyle.Underline,
        NavigationMode = NavMode.Page
    };
}
```

**Dashboard with AJAX Refresh:**

```cshtml
@{
    var dashTabs = new NavTabsViewModel
    {
        ContainerId = "dashboard-tabs",
        Tabs = new List<NavTabItem>
        {
            new() { Id = "stats", Label = "Statistics" },
            new() { Id = "activity", Label = "Activity" },
            new() { Id = "alerts", Label = "Alerts" }
        },
        ActiveTabId = "stats",
        NavigationMode = NavMode.Ajax,
        PersistenceMode = NavPersistence.LocalStorage
    };
}

<partial name="Shared/Components/_NavTabs" model="dashTabs" />

<script>
    // Refresh content periodically
    setInterval(() => {
        const activeTab = NavTabs.getActiveTab('dashboard-tabs');
        if (activeTab) {
            NavTabs.retry('dashboard-tabs', activeTab);
        }
    }, 60000);
</script>
```

**Disabled Tab:**

```csharp
var tabs = new NavTabsViewModel
{
    ContainerId = "feature-tabs",
    Tabs = new List<NavTabItem>
    {
        new() { Id = "basic", Label = "Basic" },
        new() { Id = "advanced", Label = "Advanced", Disabled = true },
        new() { Id = "premium", Label = "Premium", Disabled = !user.IsPremium }
    },
    ActiveTabId = "basic"
};
```

### Accessibility Notes

- Uses proper ARIA roles: `tablist`, `tab`, `tabpanel`
- `aria-selected` indicates active tab state
- `aria-controls` links tabs to panels
- `aria-labelledby` links panels to tabs
- Roving `tabindex` for keyboard focus management
- Screen reader announcements for tab changes and loading states
- Respects `prefers-reduced-motion` for animations
- Visible focus indicators for keyboard navigation

### Related Documentation

- **[Navigation Tabs Component Guide](nav-tabs-component.md)** - Comprehensive usage guide
- **[Navigation Tabs Migration Guide](nav-tabs-migration.md)** - Migrating from legacy components
- **[Design System](design-system.md)** - Style tokens and variants

---

## SortDropdown Component

Dropdown component for selecting sort options with keyboard navigation and accessibility support. Commonly used in table headers and list views.

**Location:** `Pages/Shared/_SortDropdown.cshtml`

### Properties

#### SortDropdownViewModel

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Id` | `string` | `"sortDropdown"` | Unique identifier for element IDs (supports multiple dropdowns) |
| `SortOptions` | `List<SortOption>` | `new()` | Collection of sort options to display |
| `CurrentSort` | `string` | `""` | Value of the currently selected sort option |
| `ParameterName` | `string` | `"sort"` | Query parameter name for URL construction |

#### SortOption

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Value` | `string` | `""` | Query parameter value when selected |
| `Label` | `string` | `""` | Display text shown to user |

### Basic Usage

**Standard Sort Dropdown:**

```csharp
var sortDropdown = new SortDropdownViewModel
{
    Id = "userSort",
    SortOptions = new List<SortOption>
    {
        new() { Value = "name-asc", Label = "Name (A-Z)" },
        new() { Value = "name-desc", Label = "Name (Z-A)" },
        new() { Value = "newest", Label = "Newest First" },
        new() { Value = "oldest", Label = "Oldest First" }
    },
    CurrentSort = Model.CurrentSort,
    ParameterName = "sort"
};
```

```cshtml
<partial name="Shared/_SortDropdown" model="sortDropdown" />
```

**Inline with Table Header:**

```cshtml
<div class="flex items-center gap-4">
    <h2 class="text-lg font-semibold text-text-primary">Sounds</h2>
    <partial name="Shared/_SortDropdown" model='new SortDropdownViewModel {
        Id = "soundSort",
        SortOptions = new List<SortOption>
        {
            new SortOption { Value = "name-asc", Label = "Name (A-Z)" },
            new SortOption { Value = "name-desc", Label = "Name (Z-A)" },
            new SortOption { Value = "newest", Label = "Newest First" },
            new SortOption { Value = "oldest", Label = "Oldest First" }
        },
        CurrentSort = Model.ViewModel.CurrentSort,
        ParameterName = "sort"
    }' />
</div>
```

### Common Patterns

**Multiple Dropdowns on Same Page:**

Use unique `Id` values to prevent element ID conflicts:

```cshtml
<!-- Primary sort -->
<partial name="Shared/_SortDropdown" model='new SortDropdownViewModel {
    Id = "primarySort",
    SortOptions = primaryOptions,
    CurrentSort = Model.PrimarySort,
    ParameterName = "sort"
}' />

<!-- Secondary sort -->
<partial name="Shared/_SortDropdown" model='new SortDropdownViewModel {
    Id = "categorySort",
    SortOptions = categoryOptions,
    CurrentSort = Model.CategorySort,
    ParameterName = "category"
}' />
```

**With Custom Parameter Names:**

```csharp
var sortDropdown = new SortDropdownViewModel
{
    Id = "memberSort",
    SortOptions = new List<SortOption>
    {
        new() { Value = "joined", Label = "Join Date" },
        new() { Value = "activity", Label = "Last Active" },
        new() { Value = "messages", Label = "Message Count" }
    },
    CurrentSort = Model.SortBy,
    ParameterName = "sortBy"  // Results in ?sortBy=joined
};
```

### Keyboard Navigation

The component supports full keyboard navigation:

| Key | Action |
|-----|--------|
| `Enter` / `Space` | Toggle dropdown open/closed |
| `↓` / `↑` | Navigate between options |
| `Home` / `End` | Jump to first/last option |
| `Enter` | Select focused option |
| `Escape` | Close dropdown |

### Accessibility Notes

- Uses `role="listbox"` and `role="option"` ARIA patterns
- `aria-selected` indicates current selection
- `aria-expanded` reflects dropdown state
- `aria-haspopup="listbox"` on trigger button
- Visible checkmark indicator for selected option
- Focus management maintains keyboard accessibility

### Styling

The dropdown uses design system tokens:
- `bg-bg-tertiary` for background
- `border-border-primary` for borders
- `text-text-primary` for text
- `bg-bg-hover` for hover states
- `accent-green` for selection checkmark

---

## FilterPanel JavaScript Utility

JavaScript utility functions for collapsible filter panels with date presets. Unlike Razor components, FilterPanel uses conventional element IDs and inline JavaScript.

**Location:** `wwwroot/js/shared/filter-panel.js`

### Required HTML Structure

The filter panel expects specific element IDs:

```html
<!-- Filter Panel Container -->
<div class="bg-bg-secondary border border-border-primary rounded-lg mb-6">
    <!-- Toggle Button -->
    <button type="button"
            id="filterToggle"
            class="w-full flex items-center justify-between px-5 py-4 text-left"
            aria-expanded="true"
            aria-controls="filterContent"
            onclick="toggleFilterPanel()">
        <div class="flex items-center gap-3">
            <svg class="w-5 h-5 text-text-secondary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z" />
            </svg>
            <span class="text-lg font-semibold text-text-primary">Filters</span>
        </div>
        <svg id="filterChevron"
             class="w-5 h-5 text-text-secondary transition-transform duration-200"
             fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
        </svg>
    </button>

    <!-- Collapsible Content -->
    <div id="filterContent" class="overflow-hidden transition-all duration-300 max-h-[1000px]">
        <div class="border-t border-border-primary p-5">
            <form method="get" id="filterForm">
                <!-- Filter fields go here -->
            </form>
        </div>
    </div>
</div>
```

### JavaScript Functions

#### toggleFilterPanel()

Toggles the filter panel visibility with smooth animation.

```javascript
// Automatically bound via onclick
onclick="toggleFilterPanel()"
```

**Required Elements:**
- `#filterToggle` - The toggle button
- `#filterContent` - The collapsible content container
- `#filterChevron` - The chevron icon for rotation

#### setDatePreset(preset)

Sets date range inputs to common presets and auto-submits the form.

```javascript
// Available presets
onclick="setDatePreset('today')"   // Today only
onclick="setDatePreset('7days')"   // Last 7 days
onclick="setDatePreset('30days')"  // Last 30 days
```

**Required Elements:**
- `#StartDate` - Start date input field
- `#EndDate` - End date input field
- `#filterForm` - Form to auto-submit

### Complete Example

```cshtml
@section Scripts {
    <script src="~/js/shared/filter-panel.js"></script>
}

<!-- Filter Panel -->
<div class="bg-bg-secondary border border-border-primary rounded-lg mb-6">
    <button type="button" id="filterToggle"
            class="w-full flex items-center justify-between px-5 py-4 text-left"
            aria-expanded="true" aria-controls="filterContent"
            onclick="toggleFilterPanel()">
        <div class="flex items-center gap-3">
            <svg class="w-5 h-5 text-text-secondary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z" />
            </svg>
            <span class="text-lg font-semibold text-text-primary">Filters</span>
            @if (Model.HasActiveFilters)
            {
                <partial name="Shared/Components/_Badge" model="@(new BadgeViewModel {
                    Text = "Active",
                    Variant = BadgeVariant.Orange,
                    Size = BadgeSize.Small
                })" />
            }
        </div>
        <svg id="filterChevron" class="w-5 h-5 text-text-secondary transition-transform duration-200"
             fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
        </svg>
    </button>

    <div id="filterContent" class="overflow-hidden transition-all duration-300 max-h-[1000px]">
        <div class="border-t border-border-primary p-5">
            <form method="get" id="filterForm">
                <!-- Quick Date Presets -->
                <div class="mb-4">
                    <label class="block text-sm font-medium text-text-primary mb-2">Quick Date Range</label>
                    <div class="flex gap-2">
                        <button type="button" onclick="setDatePreset('today')"
                                class="px-3 py-1.5 text-sm bg-bg-tertiary border border-border-primary rounded-lg
                                       text-text-primary hover:bg-bg-hover transition-colors">
                            Today
                        </button>
                        <button type="button" onclick="setDatePreset('7days')"
                                class="px-3 py-1.5 text-sm bg-bg-tertiary border border-border-primary rounded-lg
                                       text-text-primary hover:bg-bg-hover transition-colors">
                            Last 7 Days
                        </button>
                        <button type="button" onclick="setDatePreset('30days')"
                                class="px-3 py-1.5 text-sm bg-bg-tertiary border border-border-primary rounded-lg
                                       text-text-primary hover:bg-bg-hover transition-colors">
                            Last 30 Days
                        </button>
                    </div>
                </div>

                <!-- Date Range Inputs -->
                <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
                    <partial name="Shared/Components/_FormInput" model='new FormInputViewModel {
                        Id = "StartDate",
                        Name = "StartDate",
                        Label = "Start Date",
                        Type = "date",
                        Value = Model.StartDate?.ToString("yyyy-MM-dd")
                    }' />
                    <partial name="Shared/Components/_FormInput" model='new FormInputViewModel {
                        Id = "EndDate",
                        Name = "EndDate",
                        Label = "End Date",
                        Type = "date",
                        Value = Model.EndDate?.ToString("yyyy-MM-dd")
                    }' />
                </div>

                <!-- Apply Button -->
                <div class="flex justify-end">
                    <partial name="Shared/Components/_Button" model='new ButtonViewModel {
                        Text = "Apply Filters",
                        Variant = ButtonVariant.Primary,
                        Type = "submit"
                    }' />
                </div>
            </form>
        </div>
    </div>
</div>
```

### Best Practices

1. **Include the Script**: Add the script reference in your page's Scripts section
2. **Use Exact IDs**: Element IDs must match exactly (`filterToggle`, `filterContent`, `filterChevron`, etc.)
3. **Show Active State**: Display a Badge when filters are active to indicate non-default state
4. **Auto-Submit on Presets**: Date presets automatically submit the form for immediate feedback
5. **Combine with Other Components**: Use FormInput, FormSelect, and Button components within the filter panel

### Pages Using FilterPanel

- Analytics pages (`Analytics/Index.cshtml`, `Analytics/Engagement.cshtml`, `Analytics/Moderation.cshtml`)
- RatWatch Analytics (`RatWatch/Analytics.cshtml`, `RatWatch/Incidents.cshtml`)
- Member Directory (`Members/Index.cshtml`)
- Notifications (`Admin/Notifications/Index.cshtml`)

---

## Integration Examples

### Form with Validation

Complete form example combining FormInput, FormSelect, Button, and Alert components.

```cshtml
@page
@model CreateServerModel
@using DiscordBot.Bot.ViewModels.Components

<!-- Success Alert -->
@if (TempData["SuccessMessage"] != null)
{
    <div class="mb-6">
        <partial name="Shared/Components/_Alert" model="@(new AlertViewModel
        {
            Variant = AlertVariant.Success,
            Message = TempData["SuccessMessage"]!.ToString()!,
            IsDismissible = true
        })" />
    </div>
}

<!-- Error Alert -->
@if (!ModelState.IsValid)
{
    <div class="mb-6">
        <partial name="Shared/Components/_Alert" model="@(new AlertViewModel
        {
            Variant = AlertVariant.Error,
            Title = "Validation Errors",
            Message = "Please correct the errors below and try again."
        })" />
    </div>
}

<form method="post" class="space-y-6 max-w-2xl">
    <h1 class="text-h2 mb-6">Create New Server</h1>

    <!-- Server Name Input -->
    <partial name="Shared/Components/_FormInput" model="@(new FormInputViewModel
    {
        Id = "server-name",
        Name = "ServerName",
        Label = "Server Name",
        Placeholder = "Enter server name",
        Value = Model.ServerName,
        ValidationState = ModelState.GetValidationState("ServerName") == ModelValidationState.Invalid
            ? ValidationState.Error
            : ValidationState.None,
        ValidationMessage = ModelState["ServerName"]?.Errors.FirstOrDefault()?.ErrorMessage,
        IsRequired = true,
        HelpText = "This will be the display name for your server"
    })" />

    <!-- Region Select -->
    <partial name="Shared/Components/_FormSelect" model="@(new FormSelectViewModel
    {
        Id = "region",
        Name = "Region",
        Label = "Server Region",
        SelectedValue = Model.Region,
        Options = new List<SelectOption>
        {
            new() { Value = "us-east", Text = "US East" },
            new() { Value = "us-west", Text = "US West" },
            new() { Value = "eu-west", Text = "Europe West" },
            new() { Value = "asia", Text = "Asia Pacific" }
        },
        ValidationState = ModelState.GetValidationState("Region") == ModelValidationState.Invalid
            ? ValidationState.Error
            : ValidationState.None,
        ValidationMessage = ModelState["Region"]?.Errors.FirstOrDefault()?.ErrorMessage,
        IsRequired = true
    })" />

    <!-- Description Input -->
    <partial name="Shared/Components/_FormInput" model="@(new FormInputViewModel
    {
        Id = "description",
        Name = "Description",
        Label = "Description",
        Placeholder = "Brief description of your server",
        Value = Model.Description,
        MaxLength = 200,
        ShowCharacterCount = true,
        HelpText = "Optional description visible to members"
    })" />

    <!-- Form Actions -->
    <div class="flex gap-3 pt-4">
        <partial name="Shared/Components/_Button" model="@(new ButtonViewModel
        {
            Text = "Create Server",
            Variant = ButtonVariant.Primary,
            Type = "submit",
            IconLeft = "M12 4v16m8-8H4"
        })" />
        <partial name="Shared/Components/_Button" model="@(new ButtonViewModel
        {
            Text = "Cancel",
            Variant = ButtonVariant.Secondary,
            OnClick = "window.location.href='/servers'"
        })" />
    </div>
</form>
```

### Data Table with Pagination and Empty States

```cshtml
@page
@model ServersListModel
@using DiscordBot.Bot.ViewModels.Components

<div class="space-y-6">
    <div class="flex items-center justify-between">
        <h1 class="text-h2">Servers</h1>
        <partial name="Shared/Components/_Button" model="@(new ButtonViewModel
        {
            Text = "Add Server",
            Variant = ButtonVariant.Primary,
            IconLeft = "M12 4v16m8-8H4"
        })" />
    </div>

    @if (!Model.Servers.Any())
    {
        <!-- Empty State -->
        <partial name="Shared/Components/_EmptyState" model="@(new EmptyStateViewModel
        {
            Type = EmptyStateType.NoData,
            Title = "No Servers Found",
            Description = "Get started by adding your first server to the bot.",
            PrimaryActionText = "Add Server",
            PrimaryActionUrl = "/servers/add",
            Size = EmptyStateSize.Large
        })" />
    }
    else
    {
        <!-- Table -->
        <div class="table-container">
            <table class="table">
                <thead class="table-header">
                    <tr>
                        <th class="table-cell-header">Server Name</th>
                        <th class="table-cell-header">Region</th>
                        <th class="table-cell-header">Members</th>
                        <th class="table-cell-header">Status</th>
                        <th class="table-cell-header">Actions</th>
                    </tr>
                </thead>
                <tbody class="table-body">
                    @foreach (var server in Model.Servers)
                    {
                        <tr class="table-row">
                            <td class="table-cell font-medium">@server.Name</td>
                            <td class="table-cell">
                                <partial name="Shared/Components/_Badge" model="@(new BadgeViewModel
                                {
                                    Text = server.Region,
                                    Variant = BadgeVariant.Blue,
                                    Size = BadgeSize.Small
                                })" />
                            </td>
                            <td class="table-cell">@server.MemberCount.ToString("N0")</td>
                            <td class="table-cell">
                                <partial name="Shared/Components/_StatusIndicator" model="@(new StatusIndicatorViewModel
                                {
                                    Status = server.IsOnline ? StatusType.Online : StatusType.Offline,
                                    Text = server.IsOnline ? "Online" : "Offline"
                                })" />
                            </td>
                            <td class="table-cell">
                                <div class="flex gap-2">
                                    <partial name="Shared/Components/_Button" model="@(new ButtonViewModel
                                    {
                                        Text = "Edit",
                                        Variant = ButtonVariant.Secondary,
                                        Size = ButtonSize.Small
                                    })" />
                                </div>
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>

        <!-- Pagination -->
        <partial name="Shared/Components/_Pagination" model="@(new PaginationViewModel
        {
            CurrentPage = Model.CurrentPage,
            TotalPages = Model.TotalPages,
            TotalItems = Model.TotalItems,
            PageSize = Model.PageSize,
            ShowItemCount = true,
            ShowPageSizeSelector = true,
            BaseUrl = "/servers"
        })" />
    }
</div>
```

### Dashboard Card Grid Layout

```cshtml
@page
@model DashboardModel
@using DiscordBot.Bot.ViewModels.Components

<!-- Stats Card Grid -->
<div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
    <!-- Total Servers Card -->
    <partial name="Shared/Components/_Card" model="@(new CardViewModel
    {
        Title = "Total Servers",
        BodyContent = $@"
            <div class='text-4xl font-bold text-text-primary'>{Model.TotalServers}</div>
            <div class='flex items-center gap-2 mt-2 text-sm'>
                <span class='text-success'>↑ 12%</span>
                <span class='text-text-tertiary'>from last month</span>
            </div>
        ",
        Variant = CardVariant.Default
    })" />

    <!-- Active Users Card -->
    <partial name="Shared/Components/_Card" model="@(new CardViewModel
    {
        Title = "Active Users",
        BodyContent = $@"
            <div class='text-4xl font-bold text-text-primary'>{Model.ActiveUsers:N0}</div>
            <div class='flex items-center gap-2 mt-2'>
                <partial name='Shared/Components/_StatusIndicator' model='@(new StatusIndicatorViewModel
                {
                    Status = StatusType.Online,
                    Text = ""Online Now"",
                    Size = StatusSize.Small
                })' />
            </div>
        ",
        Variant = CardVariant.Default
    })" />

    <!-- Commands Today Card -->
    <partial name="Shared/Components/_Card" model="@(new CardViewModel
    {
        Title = "Commands Today",
        BodyContent = $@"
            <div class='text-4xl font-bold text-text-primary'>{Model.CommandsToday:N0}</div>
            <div class='text-sm text-text-tertiary mt-2'>
                Avg: {Model.AvgCommandsPerDay:N0}/day
            </div>
        ",
        Variant = CardVariant.Default
    })" />

    <!-- Bot Status Card -->
    <partial name="Shared/Components/_Card" model="@(new CardViewModel
    {
        Title = "Bot Status",
        BodyContent = $@"
            <div class='space-y-3'>
                <partial name='Shared/Components/_StatusIndicator' model='@(new StatusIndicatorViewModel
                {
                    Status = Model.BotStatus,
                    Text = Model.BotStatusText,
                    DisplayStyle = StatusDisplayStyle.BadgeStyle,
                    IsPulsing = Model.BotStatus == StatusType.Online
                })' />
                <div class='text-sm text-text-tertiary'>
                    Uptime: {Model.Uptime}
                </div>
            </div>
        ",
        Variant = CardVariant.Elevated
    })" />
</div>
```

### Loading States Pattern

```cshtml
@page
@model DataPageModel
@using DiscordBot.Bot.ViewModels.Components

@if (Model.IsLoading)
{
    <!-- Full Page Loading -->
    <partial name="Shared/Components/_LoadingSpinner" model="@(new LoadingSpinnerViewModel
    {
        Variant = SpinnerVariant.Pulse,
        Size = SpinnerSize.Large,
        Message = "Loading data...",
        SubMessage = "This may take a few moments",
        IsOverlay = true
    })" />
}
else if (Model.HasError)
{
    <!-- Error State -->
    <partial name="Shared/Components/_EmptyState" model="@(new EmptyStateViewModel
    {
        Type = EmptyStateType.Error,
        Title = "Failed to Load Data",
        Description = Model.ErrorMessage,
        PrimaryActionText = "Retry",
        PrimaryActionOnClick = "location.reload()"
    })" />
}
else
{
    <!-- Loaded Content -->
    <partial name="Shared/Components/_Card" model="@(new CardViewModel
    {
        Title = "Data Overview",
        BodyContent = Model.ContentHtml,
        FooterContent = $@"
            <div class='text-xs text-text-tertiary'>
                Last updated: {Model.LastUpdated:g}
            </div>
        "
    })" />
}
```

---

## Patterns & Best Practices

### Form Validation Patterns

**Client-Side Validation States:**

```csharp
// In PageModel
public ValidationState GetInputValidationState(string fieldName)
{
    if (!ModelState.ContainsKey(fieldName))
        return ValidationState.None;

    var state = ModelState.GetValidationState(fieldName);
    return state == ModelValidationState.Invalid
        ? ValidationState.Error
        : ValidationState.None;
}

public string? GetValidationMessage(string fieldName)
{
    return ModelState[fieldName]?.Errors.FirstOrDefault()?.ErrorMessage;
}
```

**Success State After Save:**

```csharp
// After successful save
TempData["SuccessMessage"] = "Server created successfully!";
return RedirectToPage("/Servers/Index");

// In target page
@if (TempData["SuccessMessage"] != null)
{
    <partial name="Shared/Components/_Alert" model="@(new AlertViewModel
    {
        Variant = AlertVariant.Success,
        Message = TempData["SuccessMessage"]!.ToString()!,
        IsDismissible = true
    })" />
}
```

### Button Groups and Loading States

**Action Button Group:**

```cshtml
<div class="flex items-center gap-3">
    <partial name="Shared/Components/_Button" model="@(new ButtonViewModel
    {
        Text = "Save",
        Variant = ButtonVariant.Primary,
        Type = "submit",
        IsLoading = Model.IsSaving
    })" />
    <partial name="Shared/Components/_Button" model="@(new ButtonViewModel
    {
        Text = "Cancel",
        Variant = ButtonVariant.Secondary,
        IsDisabled = Model.IsSaving,
        OnClick = "history.back()"
    })" />
    <partial name="Shared/Components/_Button" model="@(new ButtonViewModel
    {
        Text = "Delete",
        Variant = ButtonVariant.Danger,
        IsDisabled = Model.IsSaving,
        OnClick = "confirmDelete()"
    })" />
</div>
```

### Card Layouts with Actions

**Card with Header Actions:**

```csharp
var card = new CardViewModel
{
    Title = "Recent Activity",
    Subtitle = "Last 24 hours",
    HeaderActions = @"
        <div class='flex gap-2'>
            <partial name='Shared/Components/_Button' model='@(new ButtonViewModel
            {
                Text = ""Refresh"",
                Variant = ButtonVariant.Ghost,
                Size = ButtonSize.Small,
                IconLeft = ""M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15""
            })' />
        </div>
    ",
    BodyContent = activityListHtml
};
```

### Empty State Fallbacks

**Conditional Content Pattern:**

```csharp
public string GetCardContent()
{
    if (IsLoading)
    {
        return @"
            <div class='flex justify-center py-12'>
                <partial name='Shared/Components/_LoadingSpinner'
                         model='@(new LoadingSpinnerViewModel { Size = SpinnerSize.Medium })' />
            </div>
        ";
    }

    if (!Items.Any())
    {
        return @"
            <partial name='Shared/Components/_EmptyState'
                     model='@(new EmptyStateViewModel
                     {
                         Type = EmptyStateType.NoData,
                         Title = ""No Items"",
                         Description = ""Add your first item to get started."",
                         Size = EmptyStateSize.Compact
                     })' />
        ";
    }

    return RenderItemsList();
}
```

---

## Cross-References

### Related Documentation

- **[Design System](design-system.md)** - Color palette, typography, spacing tokens used by components
- **[User Management](user-management.md)** - Examples of components in user CRUD pages
- **[Interactive Components](interactive-components.md)** - Discord bot button/component patterns

### Component Showcase

For live examples of all components with interactive demos, visit the component showcase page at `/components` when running the application locally.

---

## Changelog

### Version 1.2 (2026-01-26)
- Added SortDropdown component documentation with ViewModel properties
- Added FilterPanel JavaScript utility documentation
- Documented keyboard navigation and accessibility for SortDropdown
- Included complete examples for filter panel with date presets

### Version 1.1 (2026-01-26)
- Added NavTabs component documentation
- Documented JavaScript API for NavTabs
- Added navigation modes, persistence, and keyboard navigation
- Cross-referenced with dedicated component and migration guides

### Version 1.0 (2025-12-22)
- Initial component API documentation
- Documented all 10 core components
- Added integration examples and patterns
- Included accessibility guidelines

---

## Support & Contributions

For questions, bug reports, or feature requests related to components:

1. Check this documentation first
2. Review the [Design System](design-system.md) for styling questions
3. Create an issue in the project repository with the `component` label

**Maintained by:** UI Development Team
**Last Review:** 2025-12-22
**Next Review:** Quarterly
