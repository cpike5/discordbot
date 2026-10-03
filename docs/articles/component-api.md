# Component API Usage Guide

**Version:** 1.4
**Last Updated:** 2026-02-19
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
| [FormTextarea](#formtextarea-component) | Multi-line text | Messages, reasons, descriptions |
| [RadioCard](#radiocard-components) | Pick one by clicking a card | Modes, channels, presets |
| [FormToggle](#formtoggle-component) | On/off switch | Settings, flags |
| [Alert](#alert-component) | Persistent page-level state | Load failures, degraded services, validation summaries |
| [Toasts](#toasts-and-the-tempdata-bridge) | Action results | Saved, deleted, failed to save (JS or `TempData.Set*Toast`) |
| [ApiClient](#apiclient-javascript-api) | Requests from page scripts | Session expiry, plain-language errors, timeouts |
| [Double-submit guard](#double-submit-guard) | Pending state for posted forms | `data-submit-guard` on any server-posted form |
| [ConfirmationModal](#confirmationmodal-component) | Confirmation dialogs | Delete actions, destructive operations |
| [TypedConfirmationModal](#typedconfirmationmodal-component) | Text-verified confirmations | Irreversible destructive actions |
| [quickActions JS API](#quickactions-javascript-api) | Promise-based dialogs | AJAX-gated confirms, dynamic alerts |
| [LoadingSpinner](#loadingspinner-component) | Loading states | Async operations, page loads |
| [EmptyState](#emptystate-component) | No data feedback | Empty lists, search results, load errors (server and `EmptyState` JS twin) |
| [Skeletons](#skeleton-components) | Loading placeholders | Regions that load after the page (`Skeleton.show` waits 300ms) |
| [Unsaved changes](#unsaved-changes) | Leave-page protection | `data-unsaved-changes` on any editable form |
| [Pagination](#pagination-component) | Data navigation | Tables, lists, search results |
| [NavTabs](#navtabs-component) | Tabbed navigation | Page navigation, in-page tabs, AJAX content |
| [SortDropdown](#sortdropdown-component) | Sort selection dropdown | Table headers, list sorting |
| [FilterPanel](#filterpanel-javascript-utility) | Collapsible filter sections | Data filtering, date range selection |
| [VoiceChannelPanel](#voicechannelpanel-component) | Voice channel controls | Audio portals, admin voice pages |

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

Text input field with label, validation states, help text, icons, and character counting. A thin wrapper over the `.form-input` class (UX plan D3): the partial adds the label, the help and message paragraphs and the ARIA wiring; the look lives in `site.css`.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Id` | `string` | `""` | Input element ID (required) |
| `Name` | `string` | `""` | Input name attribute (required) |
| `Label` | `string?` | `null` | Field label text |
| `Type` | `string` | `"text"` | Input type: `text`, `email`, `password`, `search`, `url`, `tel`, `number`, `date`... |
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
| `ShowCharacterCount` | `bool` | `false` | Shows the character count as of page load (it does not update while typing) |
| `Autocomplete` | `string?` | `null` | `autocomplete` token: `email`, `username`, `current-password`, `new-password`, `one-time-code`, `off`... Passwords and sign-in names need one |
| `InputMode` | `string?` | `null` | `inputmode` hint for the phone keyboard: `numeric`, `decimal`, `email`, `tel`, `url`, `search`. Use `numeric` on `type="text"` for IDs (Discord snowflakes are too large for a number field) |
| `Min` / `Max` | `string?` | `null` | Bounds for `number` and date inputs (strings, so decimals and dates pass through) |
| `Step` | `string?` | `null` | `step` for `number` (`1`, `0.01`, `any`) |
| `Pattern` | `string?` | `null` | `pattern` regular expression |
| `DescribedBy` | `string?` | `null` | Extra element IDs for `aria-describedby`, after the component's own help or message ID |
| `AdditionalAttributes` | `Dictionary<string, string>?` | `null` | Custom HTML attributes (values are HTML-encoded) |

### Enums

#### InputSize

| Value | Padding | Font Size |
|-------|---------|-----------|
| `Small` | `.form-input-sm` | `text-xs` |
| `Medium` | `.form-input` | `text-sm` (16px under a coarse pointer) |
| `Large` | `.form-input-lg` | `text-base` |

#### ValidationState

| Value | Description | Border Color |
|-------|-------------|--------------|
| `None` | No validation | Default border |
| `Success` | Valid input | `.input-validation-success` (green border) |
| `Warning` | Warning state | `.input-validation-warning` (amber border) |
| `Error` | Invalid input | `.input-validation-error` (red border, red focus ring) |

**Validation classes.** `.input-validation-error`, `-warning` and `-success` are the names ASP.NET tag helpers and jQuery unobtrusive validation add on their own, so server-rendered and live validation look the same. The selectors name the element (`input.input-validation-error`), so a raw `<input asp-for>` that carries utility borders turns red too. `.field-validation-error` colours a `<span asp-validation-for>`, and `.validation-summary-errors` styles the summary list. All are token-based and safelisted.

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
- `aria-describedby` points at the help text while the field has no state, at the message while it has one, then at any `DescribedBy` IDs; `aria-invalid="true"` on errors
- The error message is `role="alert"`; the required asterisk is `aria-hidden` (the input has `required` and `aria-required`)

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

## FormTextarea Component

Multi-line text with the same label, help, validation and `aria-describedby` rules as `FormInput`, on `.form-textarea` (vertical resize, `min-height` of five rows' worth).

**Partial:** `Components/_FormTextarea` - **ViewModel:** `FormTextareaViewModel`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Id`, `Name`, `Label`, `Placeholder`, `Value`, `HelpText` | `string` | | As `FormInput` |
| `Rows` | `int` | `4` | Visible rows |
| `MaxLength` | `int?` | `null` | Browser-enforced limit |
| `ValidationState` / `ValidationMessage` | | `None` | Same classes and message paragraphs as `FormInput` |
| `IsRequired`, `IsDisabled`, `IsReadOnly` | `bool` | `false` | |
| `Autocomplete` | `string?` | `null` | Free text rarely wants one |
| `Dir` | `string?` | `null` | `auto` lets right-to-left text flow correctly in user-written content |
| `DescribedBy` | `string?` | `null` | Extra `aria-describedby` IDs |
| `AdditionalAttributes` | `Dictionary<string, string>?` | `null` | Encoded attributes |

```razor
<partial name="Components/_FormTextarea" model='new FormTextareaViewModel {
    Id = "reason", Name = "Input.Reason", Label = "Reason", IsRequired = true, Rows = 3, Dir = "auto",
    ValidationState = ModelState.IsValid ? ValidationState.None : ValidationState.Error,
    ValidationMessage = "Give a reason." }' />
```

---

## RadioCard Components

Choose one option by clicking a card. The `<input type="radio">` is visually hidden (`.radio-card-input`, clipped, not `display:none`) so it stays in the tab order; the card draws hover, focus (a 2px ring from `:focus-visible`), selected and disabled, and a dot marks the selection so colour is not the only signal. Tab enters the group once, arrow keys move between cards and skip disabled ones.

**Partials:** `Components/_RadioCardGroup` (a `<fieldset>` with a `<legend>`) and `Components/_RadioCard` (one card, for custom layouts) - **ViewModels:** `RadioCardGroupViewModel`, `RadioCardViewModel`

| `RadioCardGroupViewModel` | Type | Description |
|---------------------------|------|-------------|
| `Name` | `string` | Shared radio name (the bound property) |
| `Legend` | `string` | The group label; required for an accessible group |
| `HelpText` | `string?` | Shown under the legend and described by `aria-describedby` |
| `Options` | `List<RadioCardViewModel>` | `Value`, `Title`, `Description?`, `IconPath?`, `IsDisabled`, `Id?`, `AdditionalAttributes?` |
| `SelectedValue` | `string?` | The checked card's `Value` |
| `Columns` | `int` | 1-4 from `sm` up; one column on phones. Default 2 |
| `IsRequired` | `bool` | Adds an asterisk to the legend |
| `ValidationMessage` | `string?` | Turns the cards red (`.radio-card-group-invalid`) and announces the message |

```razor
<partial name="Components/_RadioCardGroup" model='new RadioCardGroupViewModel {
    Name = "Input.Mode", Legend = "Purge mode", SelectedValue = Model.Input.Mode,
    Options = new() {
        new() { Value = "recent", Title = "Recent messages", Description = "The last 100 in a channel." },
        new() { Value = "user",   Title = "By user" } } }' />
```

Each card's radio is named by its title (`aria-labelledby`) and described by its description. Use this instead of `display:none` radios (findings F-7).

---

## FormToggle Component

Switch on the canonical `.toggle` classes. The checkbox has `role="switch"`, is named by its label and described by the description, and shows a focus ring on the track.

**Partial:** `Components/_FormToggle` - **ViewModel:** `FormToggleViewModel` (`Id`, `Name`, `Label`, `Description`, `IsChecked`, `IsDisabled`, `PostsFalseWhenOff`, `AdditionalAttributes`)

**Unchecked posts `false`.** A browser omits an unchecked checkbox, so a form cannot tell "turned off" from "not on the form", and a bound `bool` never becomes false. With `PostsFalseWhenOff` (the default) a hidden `false` input with the same name follows the checkbox; a checked toggle posts `true,false` and the model binder reads the first. It is not rendered while the toggle is disabled (a disabled field posts nothing). Set `PostsFalseWhenOff = false` for scripts that read `checked` themselves: the Settings page does, with `AdditionalAttributes["data-setting-toggle"] = "true"`.

Legacy `.form-toggle*` markup (Settings command modules, `llm-models.js`, Privacy) is still styled from `site.css` until those screens move to the partial.

---

## Alert Component

Banner for **persistent page-level state**: a load failure, a degraded or unconfigured service, a validation summary. The result of an action (saved, deleted, failed to save) is a toast instead; see [Toasts and the TempData Bridge](#toasts-and-the-tempdata-bridge). Field errors stay inline next to the field.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Variant` | `AlertVariant` | `Info` | Alert type/severity |
| `Title` | `string?` | `null` | Optional alert title |
| `Message` | `string` | `""` | Alert message text (required) |
| `IsDismissible` | `bool` | `false` | Shows dismiss button |
| `ShowIcon` | `bool` | `true` | Shows variant icon |
| `DismissCallback` | `string?` | `null` | Name of a global JavaScript function called with the alert element after it is dismissed. A name, never code; dismissing works without it |

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
    DismissCallback = "onRestartBannerDismissed" // optional: window.onRestartBannerDismissed(alertElement)
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
- Dismiss button includes `aria-label="Dismiss"`; `toast.js` wires every `[data-alert-dismiss]` button, so no callback is needed
- `role="status"`: the alert is server-rendered and present on load, so it is announced politely rather than interrupting
- Icon provides visual reinforcement (not sole indicator)

---

## Toasts and the TempData Bridge

Toasts report the **result of an action**. Error toasts stay until dismissed; success closes after 4 seconds and info/warning after 6. A toast identical to one already on screen is not stacked again; the existing one restarts its timer. Timers pause while the toast is hovered, focused or touched. One live region per politeness announces them (errors assertively).

### From JavaScript

```javascript
toast.success('Settings saved.');
toast.error('Could not save the settings.', {
    action: { label: 'Retry', onClick: save }   // optional action button
});
toast.info('Copied to clipboard.', { title: 'Clipboard' });
toast.warning('Rate limit approaching.', { duration: 0 });   // 0 = stay until dismissed
toast.dismissAll();
```

Options: `title`, `duration` (ms), `action` (`{ label, onClick }`), `key` (de-duplication identity; defaults to type + title + message).

The older call shapes still work and route to the same implementation: `ToastManager.show(type, message, options)`, `quickActions.showToast(message, type)`, `showToast(type, message)` or `showToast(message, type)`, and `Toast.show(message, type)`. Use `toast.*` in new code.

### From a page handler

```csharp
using DiscordBot.Bot.Extensions;

public async Task<IActionResult> OnPostDeleteAsync(long guildId, Guid id)
{
    if (!await _service.DeleteAsync(id))
    {
        TempData.SetErrorToast("That message no longer exists.");
        return RedirectToPage(new { guildId });
    }

    TempData.SetSuccessToast("Message deleted.");
    return RedirectToPage(new { guildId });
}
```

`SetSuccessToast`, `SetErrorToast`, `SetWarningToast` and `SetInfoToast` (each with an optional title) queue the toast for the next page render, whether the handler redirects or returns `Page()`. `_ToastContainer`, rendered by `_Layout` and `_PortalLayout`, reads them with `TempData.TakeToasts()` and hands them to `toast.js` as a JSON data block, so each shows exactly once.

Rules:

- Do not add `[TempData] SuccessMessage` / `ErrorMessage` properties or render action results as `_Alert`s.
- A load failure set in `OnGet` is page state: a plain `ErrorMessage` property rendered with `_Alert` (`GuildPageModelBase.ErrorMessage` is that property for guild pages).
- A handler that returns `JsonResult` puts its message in the JSON. TempData written there would pop up on the next, unrelated page.

### Accessibility Notes

- Toast elements carry no live role; `#toastLiveRegion` (polite) and `#toastAlertRegion` (assertive, errors) announce them once
- Dismissing a toast that holds focus moves focus to `#main-content`
- Reduced motion turns off the slide and the progress animation

---

## ApiClient JavaScript API

`wwwroot/js/api-client.js`, loaded by both layouts as `window.ApiClient`. Use it for every request a page script makes.

```javascript
try {
    const data = await ApiClient.post(`/api/guilds/${guildId}/welcome`, payload);
    toast.success(data.message || 'Saved.');
} catch (err) {
    ApiClient.showErrorToast(err);   // plain-language message; skips session expiry (already shown)
}
```

| Helper | Behaviour |
|---|---|
| `get/post/put/del(url, [body], options)` | Resolve with the parsed body; throw `ApiClientError` for any failure |
| `getRaw/postRaw/putRaw/delRaw(...)` | Resolve with `{ ok, status, data, response }` for HTTP errors; reject only for network failure or timeout |

What it handles for every caller:

- **Expired session.** The server answers script requests with 401 problem JSON, not a redirect (see `IdentityServiceExtensions`). ApiClient also treats a redirect that lands on the sign-in page as expiry. Either way it shows one "Your session has expired" error toast with a **Sign in** action that returns to the current page, and fails with `err.kind === 'session-expired'`. Same-origin responses to raw `fetch()` calls are watched too, so older scripts get the same toast.
- **Messages.** `ApiClientError.message` is always plain language. For a 4xx: `detail`, then `message`, `errors`, `title`. For a 5xx `detail` and `title` are skipped (some controllers put exception text in `detail`), so it is `message` or a sentence for the status code. Never "HTTP 500", never HTML.
- **No HTML as data.** An HTML body (an error page) becomes `{ success: false, message }`.
- **Network failures and timeouts.** 30 seconds by default (`timeout: 0` to disable, `signal` to cancel). They reject with `kind` `'network'` or `'timeout'` and status 0.
- **Headers.** Anti-forgery token, `X-Requested-With: XMLHttpRequest`, `Accept: application/json`.

---

## Double-Submit Guard

Add `data-submit-guard` to a server-posted `<form>`. On submit, `loading-manager.js` disables the submit button, shows a spinner in place of its icon (keeping the label, or using the button's `data-loading-text`), sets `aria-busy` on the button and form, and ignores further submits. Validation that cancels the submit leaves the form untouched. The state is undone when the page is restored from the back/forward cache.

```cshtml
<form method="post" asp-page-handler="Purge" data-submit-guard>
    <button type="submit" class="btn btn-primary" data-loading-text="Purging…">
        <svg …></svg> Purge
    </button>
</form>
```

Use `data-submit-guard="download"` for a form that does not navigate (a file export or `target="_blank"`); it re-enables after a few seconds. For script-driven buttons call `LoadingManager.setButtonLoading(button, true, 'Saving…')` and `false` to restore.

---

## Theme Toggle and ThemeManager

`<partial name="_ThemeToggle" model="@("topbar-icon-btn")" />` renders the header button that switches between Graphite (dark) and Purple Dusk (light). The model is extra classes for the button. It is already in `_Navbar` and the portal header; a new layout needs `theme-root` on `<html>`, `<partial name="_ThemeHead" />` in `<head>` and `theme.js`.

`window.ThemeManager` (`wwwroot/js/theme.js`):

| Member | Does |
|--------|------|
| `applyTheme(key, persistToServer)` | Shows the theme and saves it (cookie, localStorage, and `PUT /api/theme/preference` when `persistToServer`) |
| `clearTheme(persistToServer)` | Forgets the saved choice and follows the OS again |
| `toggle(persistToServer)` | Dark ↔ light |
| `isSaved()`, `isLight()`, `getActiveTheme()`, `getSystemTheme()` | State |

Every change dispatches `themechange` on `window` with `detail: { themeKey, saved }`. Listen for it rather than polling `data-theme`. The toggle is named for the theme it switches to ("Switch to the light theme").

---

## Chart Theme

`wwwroot/js/chart-theme.js` (loaded by `_Layout`) points Chart.js at the design tokens: `Chart.defaults` text, grid, font and tooltip colours, and a plugin that repaints each chart's own axis, grid, legend, title and tooltip colours as it is created and again on `themechange`. Under `prefers-reduced-motion` chart animation is off. Charts therefore need no colours for their chrome; give them only series colours.

```javascript
const c = ChartTheme.colors();          // read fresh from the tokens
new Chart(canvas, {
    type: 'line',
    data: { labels, datasets: [{ data, borderColor: c.secondary, backgroundColor: c.alpha('accent-blue', 0.15) }] }
});

// Recolour token-based datasets when the theme changes
ChartTheme.onChange((chart, colors) => {
    chart.data.datasets[0].borderColor = colors.secondary;
});
```

`colors()` returns `text`, `textMuted`, `textSubtle`, `grid`, `border`, `surface`, `canvas`, `track`, the inks `primary` (ember), `secondary` (blue), `purple`, `success`, `warning`, `error`, `info`, a `series` array, `fills` for solid bars, and `alpha(token, a)`. A script that creates charts after loading Chart.js itself should call `ChartTheme.ensureRegistered()` first (`Performance.ChartUtils` does).

---

## Formatting: `Format` and `DisplayFormat`

One way to write dates, relative time, plurals, numbers, durations and money (UX plan D6). `wwwroot/js/format.js` (loaded by both layouts, before `timezone.js`) is `window.Format`; `Helpers/DisplayFormat.cs` is its server twin. Do not write another `formatDate`, `timeAgo`, `toLocaleString('en-US')` or "N item(s)".

Rules: dates arrive from the server as UTC and show in the viewer's time zone, language and 12/24-hour setting (the locale is never hard-coded and `hour12` is never forced). A timestamp with no zone designator is read as UTC. CSV exports stay UTC with "UTC" in the header. Every function takes an optional `{ locale, timeZone, now }` for tests.

| `Format.` | Returns |
|---|---|
| `formatDate(value, style)` | `style` is `date`, `date-short`, `datetime` (default), `datetime-short`, `datetime-seconds`, `time` or `full`: "Oct 3, 2026, 2:05 PM" (en-US), "3 Oct 2026, 14:05" (en-GB) |
| `relativeTime(value)` | "now", "5 minutes ago", "yesterday", "in 2 hours"; after 30 days a date |
| `plural(count, one, [other])` | "1 server", "2 servers", "1,234 entries": count and word together |
| `number(n, { maximumFractionDigits })` | Locale grouping and decimal marks |
| `duration(ms, { maxUnits })` | "2d 5h", "5h 30m", "45s", "<1s" |
| `currency(amount, symbol, { iso })` | Virtual currency "1,250 🪙" (whole units, symbol after); `{ iso: true }` for an ISO code like `USD` |
| `parseUtc(value)` | A `Date`, or `null` |
| `scan(root)` | Binds relative-time elements under `root` (see below) |

**Relative time in markup.** `<time data-relative-time="2026-10-03T12:00:00Z"></time>` renders the wording, refreshes every 30 seconds (and when the tab becomes visible), and shows the absolute time with zone in a tooltip on hover and on keyboard focus. A lone element gets `tabindex="0"` so keyboard users can reach the tooltip. One inside a link, button or other focusable control, or inside a table, gets no Tab stop of its own and carries the absolute time as its `title` instead (focusing the enclosing control shows the tooltip too). `data-utc="…" data-format="relative"` does the same through `timezone.js`.

**Absolute dates in markup.** `<span data-utc="2026-10-03T12:00:00Z" data-format="datetime-short"></span>`. `timezone.js` converts them on load and, through a `MutationObserver`, anything inserted later (AJAX tabs, row templates) before it is painted. For markup you build by hand and need converted at once, call `timezoneUtils.scan(root)`. From Razor, `@DisplayFormat.Time(value, "datetime-short")` renders the element with a labelled UTC fallback inside, so the page reads correctly before script and the swap barely changes the width; `relative: true` gives the relative form. Put `Iso(value)` (always ends in `Z`) in a hand-written `data-utc`, not `ToString("o")`: an `Unspecified` `DateTime` serializes without the `Z`.

| `DisplayFormat.` (C#) | Notes |
|---|---|
| `Time(DateTime?, style, relative, empty)` | `<time>` element, upgraded by script |
| `Iso(DateTime)`, `ToUtc(DateTime)` | `Unspecified` is treated as UTC |
| `Date(value, style)`, `RelativeTime(value, now)` | UTC / English fallback text; the script replaces it in the viewer's language |
| `Plural`, `Number`, `Duration`, `Currency` | Final as rendered; pass a `CultureInfo` in tests |

**Date presets.** `wwwroot/js/date-range-filter.js` is the only preset helper. `DateRangeFilter.presetRange('today' | 'yesterday' | '7days' | '30days' | '90days')` returns `{ start, end }` as `YYYY-MM-DD` in the viewer's local calendar (`toISOString()` is UTC and gives the wrong day in the local evening); `applyPreset(startInput, endInput, preset)` fills two date inputs and `detectPreset(start, end)` names a matching range. Pages that still compute presets with `toISOString()` should switch as they are reworked.

---

## Live Connection: Hub States, Banner and Stale Badge

`DashboardHub` (`wwwroot/js/dashboard-hub.js`) never gives up. Its states, from `getConnectionState()` and `onStateChange(({ state, previousState }) => …)`, are `connecting` (the first attempt), `connected`, `reconnecting` (lost and still trying; this includes a first attempt that failed, because a page opened while the server is down keeps retrying too) and `disconnected` (only after `disconnect()`). Retries go at 0, 2, 5 and 10 seconds, then every 25 to 35 seconds forever; the browser coming back online or the tab becoming visible retries at once, and `DashboardHub.retryNow()` does the same on demand. When a first attempt that failed finally gets through the hub raises `connected` only; when a connection that was up and dropped comes back it raises `connected` and `reconnected`. The online and tab-visible retries are skipped after a 401/403 (only `retryNow()` from code or a reload starts over). Group memberships do not survive a new connection, so a page that joined groups rejoins them in a `DashboardHub.on('reconnected', …)` handler.

The one thing it does not retry is a 401 or 403: that means the session ended, not that the server is away. The hub goes to `disconnected` with `getDisconnectReason() === 'auth'` (also `reason` on the state change), and `retryNow()` tries once more in case the user signed in elsewhere. If the SignalR client library did not load, `connect()` resolves `false` and nothing retries.

`_ConnectionBanner` (in `_Layout`; driven by `connection-banner.js`) floats under the top bar while the hub is down for more than 1.5 seconds: "Reconnecting…", a "Retry now" button, then "Live updates restored." for 3 seconds. An ended session shows at once as "Signed out" with a Sign in link instead of Retry. It reports the **hub**, not the bot: in offline mode the hub is connected and the banner stays hidden. One polite live region (`#connection-announcer`) carries the announcements.

Any element with `data-stale-badge` and the `hidden` attribute is shown while live updates are paused and hidden again on recovery. Put one beside anything labelled "Live" (the sidebar footer has one):

```cshtml
<h2>Recent Activity <span class="badge badge-warning" data-stale-badge hidden>Stale</span></h2>
```

`<partial name="Components/_ConnectionStatus" />` takes `ConnectionStatusViewModel(State, CustomText, Id = "connection-status", Live = true)`. Pass a different `Id` when the default is already on the page, and `Live = false` when something else announces the change. No page script reads the element: connection state belongs to the layout's `_ConnectionBanner`, which `dashboard-realtime.js` no longer duplicates.

**Sidebar bot status.** The sidebar footer shows the bot's own state: "Bot online", "Bot connecting…", "Bot offline" (with "Offline mode" in the detail line under `Discord:OfflineMode`) or "Status unknown" when `/api/bot/status` does not answer. It is rendered by the server from `IBotService`, then kept current by `bot-status-refresh.js` from the `BotStatusUpdated` hub event and a 30-second poll. `BotStatus.apply({ connectionState: 'Connected' })` applies a payload by hand (handy in a browser test: an offline-mode bot cannot be switched on); on the dashboard it redraws the status banner too. `BotStatus.watchRestart()` marks the banner "Restarting" and resolves `true` once the bot reports Connected (polling `/api/bot/status`), or `false` after 90 seconds.

---

## Row Actions

Put `row-actions` on the group of buttons in a table row, list item or card. They fade in on hover, but are never hover-only: they show whenever anything in the row has keyboard focus, and always on devices that cannot hover (touch). The row is a `tr`, `li`, `.table-row`, `.group` or `[data-row]`.

```cshtml
<tr class="table-row">
    …
    <td><div class="flex items-center justify-end gap-1 row-actions">…</div></td>
</tr>
```

Do not hand-roll `opacity-0 group-hover:opacity-100` for actions.

---

## Status and Severity Badges

`.status-badge` and `.severity-badge` (in `site.css`) are rounded tint pills. Status variants: `status-pending`, `status-acknowledged`, `status-actioned`, `status-dismissed` (flagged events), `online` / `offline` (portal header, with `status-badge-lg`), and `status-badge-connected|reconnecting|success|warning|error|secondary` (performance incidents). Severity variants: `severity-low|medium|high|critical` (moderation) and `severity-info|warning|critical` (alerts). `_StatusBadge` and `_SeverityBadge` render the flagged-event ones. `.btn-error` is an alias of `.btn-danger`.

---

## ConfirmationModal Component

Modal dialog for confirming user intent before executing an action. It carries a `<form>` with an anti-forgery token, posted over `fetch` by `quick-actions.js`. Open and close it with [`quickActions`](#quickactions-javascript-api); it gets motion, scroll lock, an `inert` background, a focus trap, Escape and focus return from the same layer as every other dialog.

**Partial:** `Components/_ConfirmationModal`

**Namespace:** `DiscordBot.Bot.ViewModels.Components`

> For JavaScript-driven confirmations that do not require a form POST, use the [`quickActions` JS API](#quickactions-javascript-api) instead.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Id` | `string` | `""` | Unique modal element ID — required; used to open/close the modal via JS |
| `Title` | `string` | `""` | Modal heading text |
| `Message` | `string` | `""` | Body message explaining the action to confirm |
| `ConfirmText` | `string` | `"Confirm"` | Label for the confirm button |
| `CancelText` | `string` | `"Cancel"` | Label for the cancel button |
| `Variant` | `ConfirmationVariant` | `Warning` | Visual severity of the dialog |
| `FormAction` | `string?` | `null` | URL to POST to, with its own query if it needs one (`/Admin/Users/Edit?userId=...`). Null or empty posts to the current page, query included |
| `FormHandler` | `string?` | `null` | Razor Pages handler name (e.g., `"Delete"` maps to `OnPostDelete`). The partial writes it into the form's `action` as `handler=...`, because Razor Pages reads the handler from the query string, not the body |
| `CustomIconPath` | `string?` | `null` | SVG path override for the dialog icon |

### Enums

#### ConfirmationVariant

| Value | Description | Accent Color |
|-------|-------------|--------------|
| `Info` | Informational dialog | Blue (`accent-blue`) |
| `Warning` | Caution; reversible but significant action | Amber (`warning`) |
| `Danger` | Destructive or irreversible action | Red (`error`) |

### Basic Usage

**Delete Confirmation (form POST):**

```csharp
// PageModel
public ConfirmationModalViewModel DeleteModal { get; set; } = new()
{
    Id = "delete-server-modal",
    Title = "Delete Server",
    Message = "Are you sure you want to delete this server? This action cannot be undone.",
    ConfirmText = "Delete Server",
    CancelText = "Keep Server",
    Variant = ConfirmationVariant.Danger,
    FormAction = $"/guild/{GuildId}/settings",
    FormHandler = "Delete"
};
```

```cshtml
@* Render the modal (hidden by default) *@
<partial name="Shared/Components/_ConfirmationModal" model="Model.DeleteModal" />

@* Trigger button *@
<partial name="Shared/Components/_Button" model="@(new ButtonViewModel
{
    Text = "Delete Server",
    Variant = ButtonVariant.Danger,
    OnClick = "window.quickActions.showConfirmationModal('delete-server-modal')"
})" />
```

**Warning Confirmation (custom icon):**

```csharp
var restartModal = new ConfirmationModalViewModel
{
    Id = "restart-bot-modal",
    Title = "Restart Bot",
    Message = "Restarting the bot will temporarily disconnect all voice channels and clear the playback queue.",
    ConfirmText = "Restart Now",
    Variant = ConfirmationVariant.Warning,
    FormAction = "/admin/bot/restart",
    FormHandler = "Restart",
    CustomIconPath = "M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"
};
```

### What happens on confirm

The form is submitted by one delegated listener in `quick-actions.js` (B-8). It posts to the form's own `action` (handler included), with `redirect: 'manual'`, once per confirmation:

| The handler answers | The modal |
|---------------------|-----------|
| JSON `{ success: true, message }` | Closes, shows a success toast, and fires a bubbling `quickactions:confirmed` event (`detail: { modalId, form, data }`) |
| JSON error or a 4xx/5xx | Stays open, re-enables the button, shows the server's message (or plain text for the status) as an error toast |
| A redirect (`RedirectToPage`) | Stays busy while the browser loads the current page once. The redirect is **not followed** by script: following it would run the target's GET and spend the TempData toast or one-time value (Users/Edit's generated password) meant for the page the user lands on |
| Session expired | The "Sign in" toast from `ApiClient`; the modal stays open |

Opt-outs on the `<form>`: `data-custom-submit` (a page script handles submit itself, as `settings.js` does for reset-category; the generic handler also stands down whenever a script already called `preventDefault`) and `data-submit-mode="navigate"` (a plain browser submit, with the busy state).

### Accessibility Notes

- `role="alertdialog"`, `aria-modal="true"`, `aria-labelledby` the title, `aria-describedby` the message
- The page behind is `inert` and cannot scroll while it is open; Tab and Shift+Tab stay inside
- The first focusable control (Cancel) receives focus; `Escape` closes (not while a request is in flight) and returns focus to the opener
- Cancel and the backdrop are `data-modal-dismiss` controls; there are no inline handlers
- Enter and exit use class toggles (`.qa-open`), skipped under `prefers-reduced-motion`

---

## TypedConfirmationModal Component

Confirmation dialog that requires the user to type a specific phrase before the confirm button is enabled. Use this for irreversible destructive actions where accidental confirmation must be prevented.

**Partial:** `Components/_TypedConfirmationModal`

**Namespace:** `DiscordBot.Bot.ViewModels.Components`

> For a lighter confirmation without text entry, use [`ConfirmationModal`](#confirmationmodal-component). For JavaScript-only typed confirms, use [`quickActions.typedConfirm()`](#quickactions-javascript-api).

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Id` | `string` | `""` | Unique modal element ID — required |
| `Title` | `string` | `""` | Modal heading text |
| `Message` | `string` | `""` | Body message explaining the action |
| `RequiredText` | `string` | `""` | Exact phrase the user must type to enable confirm |
| `InputLabel` | `string` | `""` | Label displayed above the text input |
| `ConfirmText` | `string` | `"Confirm"` | Label for the confirm button |
| `CancelText` | `string` | `"Cancel"` | Label for the cancel button |
| `Variant` | `ConfirmationVariant` | `Danger` | Visual severity (defaults to `Danger` for typed confirms) |
| `FormAction` | `string?` | `null` | Form `action` attribute (URL to POST to on confirm) |
| `FormHandler` | `string?` | `null` | Razor Pages handler name |

### Basic Usage

**Purge Data (typed confirmation):**

```csharp
// PageModel
public TypedConfirmationModalViewModel PurgeModal { get; set; } = new()
{
    Id = "purge-data-modal",
    Title = "Purge All Data",
    Message = "This will permanently delete all messages, logs, and settings for this guild. " +
              "This action cannot be undone.",
    RequiredText = "delete everything",
    InputLabel = "Type \"delete everything\" to confirm",
    ConfirmText = "Permanently Delete",
    CancelText = "Cancel",
    Variant = ConfirmationVariant.Danger,
    FormAction = $"/guild/{GuildId}/data",
    FormHandler = "Purge"
};
```

```cshtml
@* Render the modal (hidden by default) *@
<partial name="Shared/Components/_TypedConfirmationModal" model="Model.PurgeModal" />

@* Trigger button *@
<partial name="Shared/Components/_Button" model="@(new ButtonViewModel
{
    Text = "Purge All Data",
    Variant = ButtonVariant.Danger,
    IconLeft = "M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16",
    OnClick = "window.quickActions.showConfirmationModal('purge-data-modal')"
})" />
```

**Remove Member (typed with member name):**

```csharp
var removeModal = new TypedConfirmationModalViewModel
{
    Id = "remove-member-modal",
    Title = "Remove Member",
    Message = $"You are about to permanently remove {memberName} from this guild. " +
               "All of their data, roles, and history will be deleted.",
    RequiredText = memberName,
    InputLabel = $"Type \"{memberName}\" to confirm removal",
    ConfirmText = "Remove Member",
    Variant = ConfirmationVariant.Danger,
    FormAction = $"/guild/{GuildId}/members/{memberId}",
    FormHandler = "Remove"
};
```

### Accessibility Notes

- Confirm button is disabled until `RequiredText` matches exactly (case-sensitive)
- Input field receives focus on modal open; the phrase is cleared and the button locked again whenever the modal reopens
- No submit is possible until text matches
- `Escape` dismisses and clears the typed input
- It needs only `quick-actions.js` (both layouts load it). The partial no longer calls `settingsManager`; `settings.js` forwards its old `showTypedModal`/`hideTypedModal` to `quickActions`
- Submission and outcomes are the same as [`ConfirmationModal`](#confirmationmodal-component)

---

## quickActions JavaScript API

Global utility object (`window.quickActions`) providing two categories of dialog control:

1. **Modal helpers** — open/close server-rendered `_ConfirmationModal` or `_TypedConfirmationModal` partials
2. **Promise-based dynamic dialogs** — create and await confirmation/alert dialogs entirely in JavaScript, with no Razor partial required

The object is registered globally and available on all pages that include the shared layout.

### When to Use Which Approach

| Scenario | Recommended Approach |
|----------|----------------------|
| Confirm before a form POST (delete, update) | Server-rendered partial + `showConfirmationModal()` |
| Confirm before an AJAX call | `quickActions.confirm()` Promise API |
| Display a non-dismissible alert before proceeding | `quickActions.alert()` Promise API |
| Require text entry before an AJAX destructive action | `quickActions.typedConfirm()` Promise API |
| Anti-forgery token required on the resulting request | Server-rendered partial (form includes token automatically) |

### Dialog behaviour

Every dialog, static or dynamic, goes through one core in `quick-actions.js`:

- **Motion.** `hidden` is removed, then `.qa-open` is added, so `.qa-modal-backdrop` fades and `.qa-modal-panel` rises in; closing reverses it and adds `hidden` after about 180ms. Under `prefers-reduced-motion` both are immediate. Give the backdrop `qa-modal-backdrop` and the panel `qa-modal-panel` to get the motion.
- **Scroll lock.** `html.qa-scroll-lock` (counted, so stacked dialogs lock once).
- **`inert` background.** The siblings of the dialog and of each of its ancestors are made `inert` (the toast region is left live). Closing lifts `inert` before focus returns, because an element inside an inert subtree cannot take focus.
- **Focus.** The trigger is remembered before anything becomes inert; focus goes to `initialFocus`, `[data-modal-initial-focus]` or the first control, is trapped, and returns to the trigger on close.
- **Stacking.** A dialog opened from inside another gets a higher `z-index`; Escape closes only the top one, and closing it hands focus back inside the one below. Reopening a dialog that is still fading out cancels its removal; closing twice is harmless.

#### openDialog(element, options) / closeDialog(element)

Give any dialog element the same behaviour (a hand-rolled modal moves onto this instead of keeping its own trap).

```typescript
openDialog(element: HTMLElement, options?: {
    initialFocus?: string | HTMLElement;   // selector or element
    onClose?: () => void;                  // called once, as it starts closing
    dismissOnEscape?: boolean;             // default true
}): object | null
closeDialog(element: HTMLElement): boolean // false when it was not open
```

The element should be `hidden`, carry `role="dialog"` or `role="alertdialog"` with `aria-modal="true"`, and mark its Cancel/close controls and backdrop with `data-modal-dismiss`.

### Modal Helpers

Use these functions to open and close server-rendered confirmation modals that were rendered with `_ConfirmationModal` or `_TypedConfirmationModal`.

#### showConfirmationModal(modalId)

Opens a server-rendered confirmation modal by its element ID.

```typescript
showConfirmationModal(modalId: string): void
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `modalId` | `string` | The `Id` property value used when rendering the partial |

```javascript
// Open the modal rendered with Id = "delete-server-modal"
window.quickActions.showConfirmationModal('delete-server-modal');
```

#### hideConfirmationModal(modalId)

Closes a server-rendered confirmation modal.

```typescript
hideConfirmationModal(modalId: string): void
```

```javascript
window.quickActions.hideConfirmationModal('delete-server-modal');
```

### Promise-Based Dialog API

These methods create modals dynamically in JavaScript and return Promises. No Razor partial is needed. The returned Promise resolves when the user dismisses the dialog.

#### confirm(options)

Displays a yes/no confirmation dialog. Resolves `true` if confirmed, `false` if cancelled.

```typescript
confirm(options: {
    title: string;
    message: string;
    variant?: 'info' | 'warning' | 'danger';  // default: 'warning'
    confirmText?: string;                       // default: 'Confirm'
    cancelText?: string;                        // default: 'Cancel'
}): Promise<boolean>
```

**Example — confirm before AJAX delete:**

```javascript
async function deleteSound(soundId) {
    const confirmed = await window.quickActions.confirm({
        title: 'Delete Sound',
        message: 'Are you sure you want to delete this sound? It cannot be recovered.',
        variant: 'danger',
        confirmText: 'Delete',
        cancelText: 'Keep'
    });

    if (!confirmed) return;

    await fetch(`/api/sounds/${soundId}`, { method: 'DELETE' });
    location.reload();
}
```

#### alert(options)

Displays an informational or warning alert that the user must acknowledge. Resolves `void` when dismissed.

```typescript
alert(options: {
    title: string;
    message: string;
    variant?: 'info' | 'warning' | 'danger';  // default: 'info'
    okText?: string;                            // default: 'OK'
}): Promise<void>
```

**Example — notify before a long operation:**

```javascript
async function exportData() {
    await window.quickActions.alert({
        title: 'Export Started',
        message: 'Your data export has been queued. You will receive a notification when it is ready.',
        variant: 'info',
        okText: 'Got It'
    });

    // Optionally navigate or continue after acknowledgement
}
```

#### typedConfirm(options)

Displays a confirmation dialog requiring the user to type a specific phrase. Resolves `true` when the exact phrase is entered and confirmed, `false` if cancelled.

```typescript
typedConfirm(options: {
    title: string;
    message: string;
    requiredText: string;          // Phrase user must type exactly
    inputLabel?: string;           // Label for the input field
    variant?: 'info' | 'warning' | 'danger';  // default: 'danger'
    confirmText?: string;          // default: 'Confirm'
    cancelText?: string;           // default: 'Cancel'
}): Promise<boolean>
```

**Example — typed confirm before irreversible AJAX action:**

```javascript
async function resetGuildSettings(guildId) {
    const confirmed = await window.quickActions.typedConfirm({
        title: 'Reset All Settings',
        message: 'This will reset all guild settings to their defaults. ' +
                 'Custom configurations will be permanently lost.',
        requiredText: 'reset settings',
        inputLabel: 'Type "reset settings" to confirm',
        variant: 'danger',
        confirmText: 'Reset Everything'
    });

    if (!confirmed) return;

    await fetch(`/api/guilds/${guildId}/settings/reset`, { method: 'POST' });
    location.reload();
}
```

### Complete Example: Mixed Server-Rendered and JS API

A page that uses a server-rendered modal for a form POST and JS API for an AJAX action:

```cshtml
@* Server-rendered modal for the ban action (needs anti-forgery token) *@
<partial name="Shared/Components/_ConfirmationModal" model="@(new ConfirmationModalViewModel
{
    Id = "ban-member-modal",
    Title = "Ban Member",
    Message = $"Are you sure you want to ban {Model.MemberName}? They will be removed from the guild.",
    ConfirmText = "Ban Member",
    Variant = ConfirmationVariant.Danger,
    FormAction = $"/guild/{Model.GuildId}/members/{Model.MemberId}",
    FormHandler = "Ban"
})" />

@* Trigger for the server-rendered modal *@
<partial name="Shared/Components/_Button" model="@(new ButtonViewModel
{
    Text = "Ban Member",
    Variant = ButtonVariant.Danger,
    OnClick = "window.quickActions.showConfirmationModal('ban-member-modal')"
})" />

@* AJAX action — uses JS Promise API directly *@
<button type="button" onclick="sendWarning('@Model.MemberId')">Send Warning</button>

<script>
    async function sendWarning(memberId) {
        const confirmed = await window.quickActions.confirm({
            title: 'Send Warning',
            message: 'Send an official warning to this member?',
            variant: 'warning',
            confirmText: 'Send Warning'
        });

        if (!confirmed) return;

        const resp = await fetch(`/api/members/${memberId}/warn`, { method: 'POST' });
        if (resp.ok) {
            await window.quickActions.alert({
                title: 'Warning Sent',
                message: 'The member has been notified.',
                variant: 'info'
            });
        }
    }
</script>
```

### Accessibility Notes

- Promise-based dialogs use the same accessible structure as server-rendered modals (`role="alertdialog"`, `aria-modal`, `aria-labelledby`, `aria-describedby`)
- Focus is trapped within the dialog while open, the page behind is `inert`, and focus returns to the opener
- `Escape` and a backdrop click cancel (resolve `false` / resolve `void`); the Promise settles once
- `typedConfirm` also accepts Enter once the phrase matches

---

## Skeleton Components

Placeholders for a region that loads after the page, on the `.skeleton` class (a shimmer that stops under `prefers-reduced-motion`).

| Partial | ViewModel | Use |
|---------|-----------|-----|
| `Components/_Skeleton` | `SkeletonViewModel` (`Type`: `Text`, `Title`, `Avatar`, `AvatarSmall`, `AvatarLarge`, `Button`, `Card`, `Rectangle`; `Width`, `Height`, `Rounded`, `Animate`, `CssClass`) | One shape |
| `Components/_SkeletonCard` | `SkeletonCardViewModel` (`Type`: `Stats`, `Server`, `Activity`, `Table`, `List`, `Form`; `ShowHeader`, `Label`) | A card-shaped placeholder |
| `Components/_SkeletonTable` | `SkeletonTableViewModel` (`Rows`, `Columns`, `Label`) | Table or list rows |
| `Components/_SkeletonLines` | `SkeletonLinesViewModel` (`Lines`, `Label`) | Text lines, the last shorter |

The shapes are `aria-hidden`. `Label` adds a visually hidden `role="status"` text ("Loading users"); how reliably a screen reader announces it varies, so pair it with the page's own status line. Leave it off when several cards share a region that announces itself.

### Skeleton JavaScript

`wwwroot/js/skeleton.js` (both layouts). A skeleton that flashes for 80ms is worse than none, so `Skeleton.show` draws nothing for 300ms and nothing at all if the data arrives first.

```javascript
const loading = Skeleton.show(panel, { kind: 'table', rows: 6, columns: 4, label: 'Loading users' });
try {
    const data = await ApiClient.get(url);
    loading.hide();
    render(panel, data);
} catch (err) {
    loading.hide();
    EmptyState.error(panel, { onRetry: load });
}
```

`show` marks the container `aria-busy="true"` while loading; once drawn, the skeleton replaces the container's content (stale content from an earlier load must not stay on screen) and adds visually hidden "Loading" text. Announcement of text inserted into a busy region varies between screen readers, so do not rely on it alone. Kinds: `lines` (`count`), `table` (`rows`, `columns`), `list` (`rows`), `card` (`type`, `showHeader`). Options: `delay` (ms, default 300, 0 draws at once) and `label`. `Skeleton.build(options)` makes the element without the delay.

---

## Bulk Selection

`wwwroot/js/bulk-selection.js` keeps one selection for a list that renders the same rows twice (a table from `md` up and cards below it). Both layouts stay in the DOM, so counting checked boxes counts every row twice; the module keys the selection by the row's id and mirrors it onto every checkbox with that id.

```html
<input type="checkbox" data-select-item="<id>" aria-label="Select …">   <!-- one per row per layout -->
<input type="checkbox" data-select-all aria-label="Select all …">       <!-- any number; indeterminate when partial -->
<div data-bulk-toolbar hidden class="hidden …"> <span data-selected-count></span> <button data-bulk-clear>…</button> </div>
<p class="sr-only" role="status" data-selection-status></p>             <!-- polite announcement -->
```

```javascript
const selection = BulkSelection.init({ noun: ['member', 'members'], onChange(ids) { … } });
selection.ids();      // distinct ids, as strings (snowflakes and GUIDs stay exact)
selection.clear();
```

The count text uses `Format.plural`. Toggle the toolbar with both the `hidden` attribute and the `hidden` class (the module does), because a utility such as `flex` outranks the attribute. `createSelection` has no DOM dependency and is covered by `wwwroot/js/__tests__/bulk-selection.test.js`. Used by Members and FlaggedEvents.

---

## Unsaved Changes

`wwwroot/js/unsaved-changes.js` (both layouts) warns before a page is left with edits not saved. Opt in with one attribute:

```html
<form method="post" data-unsaved-changes> ... </form>
```

A form is dirty when its controls differ from how they were at load, so typing the old value back clears it. Checkboxes and radios count only when checked; buttons, file inputs, disabled controls and the antiforgery token are ignored. Submitting is not a loss: a submit that nothing cancelled does not warn, while a fetch-based save (which calls `preventDefault`) keeps its protection.

| Attribute | On | Effect |
|-----------|----|--------|
| `data-unsaved-changes` | `<form>` | Track it |
| `data-unsaved-dirty-on-load` | `<form>` | Start dirty. Put it on a form the server re-rendered after failed validation: the input on screen is not saved yet |
| `data-unsaved-ignore` | control or container | Do not count it (a search box beside the fields) |
| `data-unsaved-indicator` | element inside the form | Shown (its `hidden` class removed) only while dirty |

The form gets `data-dirty="true|false"` and fires a bubbling `unsavedchange` event (`detail.dirty`). API: `UnsavedChanges.markClean(form)` after a successful fetch save (takes the saved values as the new baseline), `isDirty(form?)`, `track(form)` / `untrack(form)` for forms inserted later, `init(scope)`. The `beforeunload` listener exists only while a tracked form is dirty, so clean pages keep the back/forward cache. The browser decides the wording of the leave-page prompt; a script cannot set it. The tracker core (`serialize`, `createTracker`, `createRegistry`, `handleBeforeUnload`) has no DOM dependency and is covered by `wwwroot/js/__tests__/unsaved-changes.test.js`.

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

Placeholder component for empty lists, no search results, error states, and first-time user experiences. Buttons are `.btn btn-primary` and the icon tile is `.empty-state-icon`. For a region a script fills, use the [`EmptyState` JS twin](#emptystate-javascript-twin).

An empty result has three different states, and each needs its own wording: **empty** ("No servers yet", an action to add one), **filtered-empty** ("No users match your filters" with a Clear filters action, never the first-time copy) and **error** ("Could not load this" with Retry, in plain language, never exception text).

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
| `PrimaryActionIconPath` | `string?` | `null` | SVG path for the action's icon. Null keeps the plus sign; `""` shows no icon (right for Retry and Clear filters) |
| `PrimaryActionAttributes` | `Dictionary<string, string>?` | `null` | Extra attributes for the action (`data-*`, `id`), encoded. Prefer `data-action="..."` and a delegated listener to `PrimaryActionOnClick` |
| `Size` | `EmptyStateSize` | `Default` | Component size |
| `HeadingLevel` | `int` | `3` | Title tag `h1`-`h6`; follow the page's outline rather than skip a level |
| `Announce` | `bool` | `false` | `role="status"`, for an empty state that appears after load (a filter result, a failed fetch) |
| `Id` | `string?` | `null` | Element ID, so a script can replace or clear it |

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
- `Announce` makes a late-appearing state a polite live region

### EmptyState JavaScript twin

`wwwroot/js/empty-state.js` (both layouts) draws the same markup for regions a script fills. Text goes in as text, never as HTML; an action's `onClick` is a listener, and event-handler attribute names are refused.

```javascript
EmptyState.render(container, { type: 'noResults', title: 'No matches', description: '...',
    action: { text: 'Add one', url: '/Admin/Users/Create' } });
EmptyState.filtered(container, { noun: 'users', onClear: resetFilters });   // "No users match your filters" + Clear filters
EmptyState.error(container, { onRetry: load });                             // plain text + Retry, role="status"
const node = EmptyState.create(options);                                    // build without inserting
```

Options: `type` (`noData`, `noResults`, `firstTime`, `error`, `noPermission`, `offline`), `title`, `description`, `icon` (SVG path), `size` (`compact`, `default`, `large`), `headingLevel`, `announce`, `id`, and `action` / `secondary` as `{ text, url, onClick, iconPath, attributes }`. The partial and the twin share icons, size classes and structure; change both together.

---

## Pagination Component

Navigation component for paginated data with page numbers, item counts, and page size selection. The state rules live on `PaginationViewModel` (`EffectivePage`, `IsEmpty`, `HasMultiplePages`, `FirstItem`, `LastItem`, `VisiblePages()`) and are unit tested; the partial only draws them.

| State | What shows |
|-------|------------|
| Several pages | First, Previous, numbers with ellipses, Next, Last. On phones only the current page sits between Previous and Next |
| First / last page | Previous (or Next) is a `<span aria-disabled="true">`, not a link |
| One page | The item count stays ("Showing 1-7 of 7"); no navigation |
| No items | "No results" (with `ShowItemCount`); never "Showing 1-0 of 0" |
| `CurrentPage` past the end | Clamped to the last page; never "Showing 201 to 90" |
| `TotalItems` left at 0 with several pages | "Page X of Y" instead of an invented range |

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
| `PageParameterName` | `string` | `"page"` | Query string parameter for page. Pass `"pageNumber"` when the base URL comes from route values: Razor Pages reserves `page` for the page name |
| `AriaLabel` | `string` | `"Pagination"` | Landmark name; give each pagination on a page its own |
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

- `<nav>` with `aria-label` (`AriaLabel`)
- Current page is a `<span aria-current="page" aria-label="Page 3">`; other pages are `<a aria-label="Page 4">`
- Previous/Next carry `rel="prev"` / `rel="next"` and an `aria-label`; at the ends they are `<span aria-disabled="true">` (not tabbable, not links)
- Links are 36px, 44px under a coarse pointer
- The page-size selector is a labelled `<select>` in a GET form that carries the other query values as hidden inputs and resets to page 1. It replaces an inline `onchange` that wrote the base URL (which holds filter values) into script text

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
| `IconPathOutline` | `string?` | `null` | SVG path for outline icon (used for all states) |
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
            IconPathOutline = "M9 19V6l12-3v13M9 19c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zm12-3c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zM9 10l12-3"
        },
        new()
        {
            Id = "queue",
            Label = "Queue",
            Href = "/audio/queue",
            IconPathOutline = "M4 6h16M4 10h16M4 14h16M4 18h16"
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

**In-place sorting.** With `UseAjax = true`, `TargetSelector` and `PartialUrl`, the dropdown fires `sortchange` and `wwwroot/js/ajax-sort.js` fetches the partial and swaps it into the target. The old list stays on screen (dimmed, `aria-busy`) while it loads; a failure keeps it and shows an error toast with Retry, and the partial handler should answer an error status rather than 200 HTML. Each sort adds a history entry, the first entry is stamped, so Back and Forward re-render the list and the dropdown (`wrapper.sortDropdown.setSelected`). Anything that depends on the new markup listens for `ajaxsort:loaded` on the target (or `AjaxSort.configure({ onAfterLoad })`); render values the script would otherwise fill in (such as a select's options) on the server so they survive the swap. The partial is HTML, which `ApiClient` refuses as data, so this one request uses `fetch()`.

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

## VoiceChannelPanel Component

Voice channel control panel with connection status, channel selection, now playing display, and queue management. Supports both full-featured and compact layouts for different use cases.

**Location:** `Pages/Shared/Components/_VoiceChannelPanel.cshtml`

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `GuildId` | `ulong` | (required) | Discord guild ID |
| `IsCompact` | `bool` | `false` | Compact mode for sidebar/widget use |
| `ShowNowPlaying` | `bool` | `true` | Controls Now Playing section visibility |
| `ShowProgress` | `bool` | `true` | Progress bar (true) vs "Playing..." text (false) |
| `IsConnected` | `bool` | `false` | Whether bot is connected to voice |
| `ConnectedChannelName` | `string?` | `null` | Connected channel name |
| `ConnectedChannelId` | `ulong?` | `null` | Connected channel ID |
| `ChannelMemberCount` | `int?` | `null` | Members in connected channel |
| `AvailableChannels` | `IReadOnlyList<VoiceChannelInfo>` | `[]` | Available voice channels |
| `NowPlaying` | `NowPlayingInfo?` | `null` | Currently playing audio info |
| `Queue` | `IReadOnlyList<QueueItemInfo>` | `[]` | Queued audio items |
| `ApiBase` | `string?` | `null` | Member portal voice endpoints for this guild (`/api/portal/soundboard/{guildId}`). Set on portal pages: join, leave, stop and status use them, the queue section is left out, and the panel polls `GET {ApiBase}/status` instead of relying on SignalR. `null` keeps the Viewer-gated `/api/guilds/{id}/audio` endpoints the admin pages use |
| `IsPortal` | `bool` | (derived) | `ApiBase` is set |

### Client script (`wwwroot/js/voice-channel-panel.js`)

The panel keeps what the server last said (connected, channel, head count, playing, busy) and renders everything
from that. "Connected" is the bot being in a voice channel; the state of the page's own SignalR connection only
decides whether a "Live updates are paused" note shows. After a successful join or leave the panel applies the
answer at once instead of waiting for an event, and on portal pages it re-reads `/status`.

- Below 1024px the panel shows a **voice bar** (channel name, state chip, Stop while something plays) that opens the controls; on portal pages the bar is sticky.
- `window.VoiceChannelPanel`: `refresh()` (portal: read status now), `notePlaying(name, source)`, `reveal()` (open the controls and focus the picker: what a page does when someone tries to play without a voice channel), `toggle(force)`, `getState()`.
- Events on `document`: `voicepanel:change` (`detail: { isConnected, channelId, isPlaying, busy }`) after every state change, and `voicepanel:playbackended`. `#voice-channel-panel` also carries `data-connected`, `data-channel-id`, `data-playing` and `data-busy`.
- `resolveVoiceEndpoint` and `voiceJoinBody` are exported for tests (`__tests__/voice-channel-panel.test.js`). A channel ID is sent as JSON text so its digits survive.

### Supporting Types

#### NowPlayingInfo

Information about the currently playing audio track.

```csharp
public record NowPlayingInfo
{
    public string Name { get; init; } = string.Empty;              // Track name (required)
    public double DurationSeconds { get; init; }                   // Total track duration
    public double PositionSeconds { get; init; }                   // Current playback position
    public int ProgressPercent => (int)((PositionSeconds / DurationSeconds) * 100);
}
```

#### VoiceChannelInfo

Information about an available voice channel.

```csharp
public record VoiceChannelInfo
{
    public ulong Id { get; init; }                                 // Discord channel ID (required)
    public string Name { get; init; } = string.Empty;              // Channel name (required)
    public int MemberCount { get; init; }                          // Current member count
}
```

#### QueueItemInfo

Information about a queued audio item.

```csharp
public record QueueItemInfo
{
    public int Position { get; init; }                             // Queue position (1-indexed)
    public string Name { get; init; } = string.Empty;              // Item name (required)
    public double DurationSeconds { get; init; }                   // Item duration
}
```

### Basic Usage

**Full Mode (Admin Pages):**

Used on admin pages with all controls and information displayed.

```csharp
var voicePanel = new VoiceChannelPanelViewModel
{
    GuildId = Model.GuildId,
    IsCompact = false,  // Full mode
    IsConnected = Model.IsConnected,
    ConnectedChannelId = Model.ConnectedChannelId,
    ConnectedChannelName = Model.ConnectedChannelName,
    ChannelMemberCount = Model.ChannelMemberCount,
    AvailableChannels = Model.AvailableChannels
};
```

```cshtml
<partial name="Shared/Components/_VoiceChannelPanel" model="voicePanel" />
```

**Compact Mode with Now Playing and Progress (Soundboard Portal):**

Used in audio portals where playback duration is known.

```csharp
var voicePanel = new VoiceChannelPanelViewModel
{
    GuildId = Model.GuildId,
    IsCompact = true,
    ShowNowPlaying = true,
    ShowProgress = true,              // Progress bar shown (duration known)
    IsConnected = Model.IsConnected,
    ConnectedChannelId = Model.ConnectedChannelId,
    ConnectedChannelName = Model.ConnectedChannelName,
    AvailableChannels = Model.AvailableChannels,
    NowPlaying = Model.NowPlaying
};
```

**Compact Mode with Now Playing but No Progress (TTS/VOX Portal):**

Used for TTS and VOX portals where audio duration is unknown until playback.

```csharp
var voicePanel = new VoiceChannelPanelViewModel
{
    GuildId = Model.GuildId,
    IsCompact = true,
    ShowNowPlaying = true,
    ShowProgress = false,             // Shows "Playing..." text instead of progress bar
    IsConnected = Model.IsConnected,
    ConnectedChannelId = Model.ConnectedChannelId,
    ConnectedChannelName = Model.ConnectedChannelName,
    AvailableChannels = Model.AvailableChannels,
    NowPlaying = Model.NowPlaying
};
```

**Compact Mode without Now Playing (Admin Pages with Compact Layout):**

Used when sidebar space is limited and now playing info is not needed.

```csharp
var voicePanel = new VoiceChannelPanelViewModel
{
    GuildId = Model.GuildId,
    IsCompact = true,
    ShowNowPlaying = false,           // Hide now playing section
    IsConnected = Model.IsConnected,
    ConnectedChannelId = Model.ConnectedChannelId,
    ConnectedChannelName = Model.ConnectedChannelName,
    AvailableChannels = Model.AvailableChannels
};
```

### Feature Details

#### Connection Status Display

- Shows bot connection state with visual indicator
- Displays connected channel name and member count
- Channel selection dropdown for connecting/disconnecting

#### Now Playing Display

When `ShowNowPlaying` is true and `NowPlaying` is populated:

**With Progress Bar (`ShowProgress = true`):**
- Displays track name
- Shows progress bar with ARIA attributes
- Displays current time and duration
- Stop button to stop playback

**With "Playing..." Text (`ShowProgress = false`):**
- Displays track name
- Shows "Playing..." indicator (no progress bar)
- Stop button to stop playback

#### Queue Management

When queue items are present, displays upcoming tracks in playback order.

### Accessibility Notes

- Progress bar uses `role="progressbar"` with `aria-valuenow`, `aria-valuemin`, `aria-valuemax`
- Connection status includes `aria-label` for screen readers
- Stop button includes `title` attribute with keyboard shortcut
- All buttons are keyboard navigable with visible focus indicators
- Channel selection dropdown properly labeled

### Common Patterns

**Checking Audio Portal State:**

```csharp
// In page model
public VoiceChannelPanelViewModel GetVoicePanel()
{
    var isAudioPortal = Model.AudioMode == AudioPortalMode.Soundboard;

    return new VoiceChannelPanelViewModel
    {
        GuildId = Model.GuildId,
        IsCompact = true,
        ShowNowPlaying = true,
        ShowProgress = isAudioPortal,  // Only for soundboard
        IsConnected = Model.IsConnected,
        ConnectedChannelId = Model.ConnectedChannelId,
        ConnectedChannelName = Model.ConnectedChannelName,
        AvailableChannels = Model.AvailableChannels,
        NowPlaying = isAudioPortal ? Model.NowPlaying : null
    };
}
```

**Responsive Layout Strategy:**

- Full mode on desktop admin pages with space for details
- Compact mode on sidebar and mobile layouts
- Hide now playing when screen space is critical
- Show progress only when duration information is available

### Related Documentation

- **[Unified Now Playing](unified-now-playing.md)** - Architecture and real-time state management
- **[Voice Channel Integration](voice-channels.md)** - Discord voice channel connectivity
- **[Soundboard](soundboard.md)** - Soundboard feature and audio playback

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

### Version 1.6 (2026-10-03)
- Added Bulk Selection (`bulk-selection.js`)

### Version 1.5 (2026-10-02)
- Alert is for persistent page state; `role="status"`; dismiss works without a callback; `DismissCallback` is a function name, never evaluated code
- Added Toasts and the TempData Bridge: the `toast.*` API, legacy aliases, `TempData.Set*Toast`, de-duplication and pause rules
- Added ApiClient: session-expiry toast, plain-language errors, timeout, no HTML as data
- Added the `data-submit-guard` double-submit guard

### Version 1.4 (2026-02-19)
- Added ConfirmationModal component documentation with all ViewModel properties
- Added TypedConfirmationModal component documentation with all ViewModel properties
- Documented ConfirmationVariant enum values and visual meaning
- Added quickActions JavaScript API section covering server-rendered modal helpers and Promise-based dialog API (`confirm`, `alert`, `typedConfirm`)
- Added guidance table on when to use server-rendered partials vs the JS Promise API
- Updated Quick Reference table with entries for new components and JS utility

### Version 1.3 (2026-02-05)
- Added VoiceChannelPanel component documentation
- Documented ShowNowPlaying and ShowProgress properties for Now Playing control
- Added usage examples for portal and admin page configurations
- Included accessibility guidelines for progress bar and audio controls

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
