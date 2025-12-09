# Razor UI Components

This document provides technical reference for the reusable Razor partial components in the DiscordBot admin UI. Each component is implemented as a strongly-typed partial view with a corresponding ViewModel.

## Table of Contents

- [Overview](#overview)
- [Usage Pattern](#usage-pattern)
- [Components](#components)
  - [Button](#button)
  - [Badge](#badge)
  - [StatusIndicator](#statusindicator)
  - [LoadingSpinner](#loadingspinner)
  - [FormInput](#forminput)
  - [FormSelect](#formselect)
  - [Alert](#alert)
  - [Card](#card)
  - [EmptyState](#emptystate)
  - [Pagination](#pagination)

## Overview

All UI components are located in `src/DiscordBot.Bot/Pages/Shared/Components/` and use ViewModels from `src/DiscordBot.Bot/ViewModels/Components/`. Components are designed to match the design system specifications defined in [design-system.md](design-system.md).

## Usage Pattern

Components are rendered using ASP.NET Core partial views:

```cshtml
@await Html.PartialAsync("Components/_ComponentName", new ComponentViewModel
{
    Property1 = value1,
    Property2 = value2
})
```

All ViewModels are C# records with `init` properties, allowing for concise initialization syntax.

---

## Components

### Button

**Location:** `Pages/Shared/Components/_Button.cshtml`
**ViewModel:** `ButtonViewModel`

Interactive button component supporting multiple variants, sizes, loading states, and icons.

#### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Text` | `string` | `""` | Button text content |
| `Variant` | `ButtonVariant` | `Primary` | Visual style variant |
| `Size` | `ButtonSize` | `Medium` | Button size |
| `Type` | `string?` | `"button"` | HTML button type (`button`, `submit`, `reset`) |
| `IconLeft` | `string?` | `null` | SVG path for left icon |
| `IconRight` | `string?` | `null` | SVG path for right icon |
| `IsDisabled` | `bool` | `false` | Whether button is disabled |
| `IsLoading` | `bool` | `false` | Show loading spinner instead of icon |
| `IsIconOnly` | `bool` | `false` | Icon-only mode (no text) |
| `AriaLabel` | `string?` | `null` | Accessibility label |
| `OnClick` | `string?` | `null` | JavaScript click handler |
| `AdditionalAttributes` | `Dictionary<string, string>?` | `null` | Custom HTML attributes |

#### Enums

**ButtonVariant:**
- `Primary` - Orange accent, main CTAs
- `Secondary` - Outline style, cancel/secondary actions
- `Accent` - Blue accent, informational actions
- `Danger` - Red, destructive actions
- `Ghost` - Transparent, subtle actions

**ButtonSize:**
- `Small` - `py-1.5 px-3 text-xs`
- `Medium` - `py-2.5 px-5 text-sm` (default)
- `Large` - `py-3 px-6 text-base`

#### Examples

**Primary Submit Button:**
```cshtml
@await Html.PartialAsync("Components/_Button", new ButtonViewModel
{
    Text = "Save Changes",
    Variant = ButtonVariant.Primary,
    Type = "submit"
})
```

**Danger Button with Loading State:**
```cshtml
@await Html.PartialAsync("Components/_Button", new ButtonViewModel
{
    Text = "Delete Server",
    Variant = ButtonVariant.Danger,
    IsLoading = Model.IsDeleting,
    OnClick = "confirmDelete()"
})
```

**Icon-Only Ghost Button:**
```cshtml
@await Html.PartialAsync("Components/_Button", new ButtonViewModel
{
    Text = "Settings",
    Variant = ButtonVariant.Ghost,
    IsIconOnly = true,
    IconLeft = "M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z",
    AriaLabel = "Open settings"
})
```

---

### Badge

**Location:** `Pages/Shared/Components/_Badge.cshtml`
**ViewModel:** `BadgeViewModel`

Small label component for tags, status indicators, and counts.

#### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Text` | `string` | `""` | Badge text content |
| `Variant` | `BadgeVariant` | `Default` | Color variant |
| `Size` | `BadgeSize` | `Medium` | Badge size |
| `Style` | `BadgeStyle` | `Filled` | Filled or outline style |
| `IconLeft` | `string?` | `null` | SVG path for left icon |
| `IsRemovable` | `bool` | `false` | Show remove/close button |
| `OnRemove` | `string?` | `null` | JavaScript handler for remove action |

#### Enums

**BadgeVariant:**
- `Default` - Gray
- `Orange` - Primary accent
- `Blue` - Secondary accent
- `Success` - Green
- `Warning` - Amber
- `Error` - Red
- `Info` - Cyan

**BadgeSize:**
- `Small` - `px-2 py-0.5 text-[10px]`
- `Medium` - `px-3 py-1 text-xs` (default)
- `Large` - `px-4 py-1.5 text-sm`

**BadgeStyle:**
- `Filled` - Solid background
- `Outline` - Border only

#### Examples

**Status Badge:**
```cshtml
@await Html.PartialAsync("Components/_Badge", new BadgeViewModel
{
    Text = "Active",
    Variant = BadgeVariant.Success,
    Style = BadgeStyle.Filled
})
```

**Removable Tag:**
```cshtml
@await Html.PartialAsync("Components/_Badge", new BadgeViewModel
{
    Text = "JavaScript",
    Variant = BadgeVariant.Blue,
    IsRemovable = true,
    OnRemove = "removeTag('javascript')"
})
```

**Warning Badge with Icon:**
```cshtml
@await Html.PartialAsync("Components/_Badge", new BadgeViewModel
{
    Text = "2 Warnings",
    Variant = BadgeVariant.Warning,
    Size = BadgeSize.Large,
    IconLeft = "M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
})
```

---

### StatusIndicator

**Location:** `Pages/Shared/Components/_StatusIndicator.cshtml`
**ViewModel:** `StatusIndicatorViewModel`

Visual indicator for online/offline/busy status, commonly used for Discord bot status or user presence.

#### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Status` | `StatusType` | `Offline` | Status type determining color |
| `Text` | `string?` | `null` | Optional text label |
| `DisplayStyle` | `StatusDisplayStyle` | `DotWithText` | Display variant |
| `IsPulsing` | `bool` | `false` | Enable pulse animation |
| `Size` | `StatusSize` | `Medium` | Indicator size |

#### Enums

**StatusType:**
- `Online` - Green
- `Idle` - Yellow/Amber
- `Busy` - Red (Do Not Disturb)
- `Offline` - Gray

**StatusDisplayStyle:**
- `DotOnly` - Just the colored dot
- `DotWithText` - Dot + status text
- `BadgeStyle` - Pill badge with dot

**StatusSize:**
- `Small` - `w-1.5 h-1.5`
- `Medium` - `w-2 h-2` (default)
- `Large` - `w-3 h-3`

#### Examples

**Bot Status Indicator:**
```cshtml
@await Html.PartialAsync("Components/_StatusIndicator", new StatusIndicatorViewModel
{
    Status = StatusType.Online,
    Text = "Bot Online",
    DisplayStyle = StatusDisplayStyle.DotWithText,
    IsPulsing = true
})
```

**Minimal Status Dot:**
```cshtml
@await Html.PartialAsync("Components/_StatusIndicator", new StatusIndicatorViewModel
{
    Status = StatusType.Idle,
    DisplayStyle = StatusDisplayStyle.DotOnly,
    Size = StatusSize.Small
})
```

---

### LoadingSpinner

**Location:** `Pages/Shared/Components/_LoadingSpinner.cshtml`
**ViewModel:** `LoadingSpinnerViewModel`

Loading indicator with multiple animation styles and optional overlay mode.

#### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Variant` | `SpinnerVariant` | `Simple` | Animation style |
| `Size` | `SpinnerSize` | `Medium` | Spinner size |
| `Message` | `string?` | `null` | Primary loading message |
| `SubMessage` | `string?` | `null` | Secondary message |
| `Color` | `SpinnerColor` | `Blue` | Color theme |
| `IsOverlay` | `bool` | `false` | Full container overlay mode |

#### Enums

**SpinnerVariant:**
- `Simple` - Rotating circle
- `Dots` - Three bouncing dots
- `Pulse` - Pulsing circle with ring

**SpinnerSize:**
- `Small` - 24px
- `Medium` - 40px (default)
- `Large` - 64px

**SpinnerColor:**
- `Blue` - accent-blue (default)
- `Orange` - accent-orange
- `White` - For dark backgrounds

#### Examples

**Simple Loading Spinner:**
```cshtml
@await Html.PartialAsync("Components/_LoadingSpinner", new LoadingSpinnerViewModel
{
    Variant = SpinnerVariant.Simple,
    Message = "Loading servers..."
})
```

**Full-Page Overlay:**
```cshtml
@if (Model.IsLoading)
{
    @await Html.PartialAsync("Components/_LoadingSpinner", new LoadingSpinnerViewModel
    {
        Variant = SpinnerVariant.Pulse,
        Size = SpinnerSize.Large,
        Message = "Connecting to Discord",
        SubMessage = "This may take a moment",
        IsOverlay = true
    })
}
```

---

### FormInput

**Location:** `Pages/Shared/Components/_FormInput.cshtml`
**ViewModel:** `FormInputViewModel`

Text input field with validation states, icons, help text, and character counting.

#### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Id` | `string` | `""` | Input element ID |
| `Name` | `string` | `""` | Form field name |
| `Label` | `string?` | `null` | Field label |
| `Type` | `string` | `"text"` | Input type (`text`, `email`, `password`, `search`, `url`, `tel`) |
| `Placeholder` | `string?` | `null` | Placeholder text |
| `Value` | `string?` | `null` | Current value |
| `HelpText` | `string?` | `null` | Help text below input |
| `Size` | `InputSize` | `Medium` | Input size |
| `ValidationState` | `ValidationState` | `None` | Validation state |
| `ValidationMessage` | `string?` | `null` | Validation message |
| `IsRequired` | `bool` | `false` | Required field marker |
| `IsDisabled` | `bool` | `false` | Disabled state |
| `IsReadOnly` | `bool` | `false` | Read-only state |
| `IconLeft` | `string?` | `null` | SVG path for left icon |
| `IconRight` | `string?` | `null` | SVG path for right icon |
| `MaxLength` | `int?` | `null` | Maximum character length |
| `ShowCharacterCount` | `bool` | `false` | Show X/Y character counter |
| `AdditionalAttributes` | `Dictionary<string, string>?` | `null` | Custom HTML attributes |

#### Enums

**InputSize:**
- `Small` - `py-1.5 px-3 text-xs`
- `Medium` - `py-2.5 px-3.5 text-sm` (default)
- `Large` - `py-3 px-4 text-base`

**ValidationState:**
- `None` - No validation styling
- `Success` - Green border/message
- `Warning` - Amber border/message
- `Error` - Red border/message

#### Examples

**Basic Text Input:**
```cshtml
@await Html.PartialAsync("Components/_FormInput", new FormInputViewModel
{
    Id = "serverName",
    Name = "ServerName",
    Label = "Server Name",
    Placeholder = "Enter server name",
    IsRequired = true,
    HelpText = "The display name for your Discord server"
})
```

**Email Input with Validation:**
```cshtml
@await Html.PartialAsync("Components/_FormInput", new FormInputViewModel
{
    Id = "email",
    Name = "Email",
    Label = "Email Address",
    Type = "email",
    Value = Model.Email,
    ValidationState = Model.EmailValid ? ValidationState.Success : ValidationState.Error,
    ValidationMessage = Model.EmailValid ? "Email is valid" : "Please enter a valid email address",
    IconLeft = "M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"
})
```

**Password with Character Counter:**
```cshtml
@await Html.PartialAsync("Components/_FormInput", new FormInputViewModel
{
    Id = "password",
    Name = "Password",
    Label = "Password",
    Type = "password",
    IsRequired = true,
    MaxLength = 64,
    ShowCharacterCount = true,
    HelpText = "Must be at least 8 characters"
})
```

---

### FormSelect

**Location:** `Pages/Shared/Components/_FormSelect.cshtml`
**ViewModel:** `FormSelectViewModel`

Dropdown select input with support for option groups and validation.

#### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Id` | `string` | `""` | Select element ID |
| `Name` | `string` | `""` | Form field name |
| `Label` | `string?` | `null` | Field label |
| `Placeholder` | `string?` | `"Select an option"` | Placeholder text |
| `SelectedValue` | `string?` | `null` | Currently selected value |
| `Options` | `List<SelectOption>` | `[]` | List of options |
| `OptionGroups` | `List<SelectOptionGroup>?` | `null` | Grouped options |
| `HelpText` | `string?` | `null` | Help text below select |
| `Size` | `InputSize` | `Medium` | Select size |
| `ValidationState` | `ValidationState` | `None` | Validation state |
| `ValidationMessage` | `string?` | `null` | Validation message |
| `IsRequired` | `bool` | `false` | Required field marker |
| `IsDisabled` | `bool` | `false` | Disabled state |
| `AllowMultiple` | `bool` | `false` | Multiple selection mode |
| `AdditionalAttributes` | `Dictionary<string, string>?` | `null` | Custom HTML attributes |

#### Supporting Types

**SelectOption:**
- `Value` (`string`) - Option value
- `Text` (`string`) - Display text
- `IsDisabled` (`bool`) - Disabled option

**SelectOptionGroup:**
- `Label` (`string`) - Group label
- `Options` (`List<SelectOption>`) - Options in group

#### Examples

**Simple Dropdown:**
```cshtml
@await Html.PartialAsync("Components/_FormSelect", new FormSelectViewModel
{
    Id = "region",
    Name = "Region",
    Label = "Server Region",
    SelectedValue = Model.Region,
    Options = new List<SelectOption>
    {
        new() { Value = "us-east", Text = "US East" },
        new() { Value = "us-west", Text = "US West" },
        new() { Value = "eu", Text = "Europe" },
        new() { Value = "asia", Text = "Asia Pacific" }
    },
    IsRequired = true
})
```

**Grouped Options:**
```cshtml
@await Html.PartialAsync("Components/_FormSelect", new FormSelectViewModel
{
    Id = "channel",
    Name = "ChannelId",
    Label = "Select Channel",
    OptionGroups = new List<SelectOptionGroup>
    {
        new()
        {
            Label = "Text Channels",
            Options = new List<SelectOption>
            {
                new() { Value = "123", Text = "#general" },
                new() { Value = "124", Text = "#announcements" }
            }
        },
        new()
        {
            Label = "Voice Channels",
            Options = new List<SelectOption>
            {
                new() { Value = "456", Text = "Voice Chat" },
                new() { Value = "457", Text = "Music" }
            }
        }
    }
})
```

---

### Alert

**Location:** `Pages/Shared/Components/_Alert.cshtml`
**ViewModel:** `AlertViewModel`

Notification banner for info, success, warning, and error messages.

#### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Variant` | `AlertVariant` | `Info` | Alert type/color |
| `Title` | `string?` | `null` | Optional title |
| `Message` | `string` | `""` | Alert message content |
| `IsDismissible` | `bool` | `false` | Show dismiss button |
| `ShowIcon` | `bool` | `true` | Show variant icon |
| `DismissCallback` | `string?` | `null` | JavaScript dismiss handler |

#### Enums

**AlertVariant:**
- `Info` - Cyan/blue, informational
- `Success` - Green, success/confirmation
- `Warning` - Amber, caution
- `Error` - Red, error/danger

#### Examples

**Success Message:**
```cshtml
@await Html.PartialAsync("Components/_Alert", new AlertViewModel
{
    Variant = AlertVariant.Success,
    Title = "Settings Saved",
    Message = "Your changes have been successfully saved.",
    IsDismissible = true
})
```

**Error Alert:**
```cshtml
@await Html.PartialAsync("Components/_Alert", new AlertViewModel
{
    Variant = AlertVariant.Error,
    Message = "Failed to connect to Discord API. Please check your bot token.",
    ShowIcon = true
})
```

**Warning Banner:**
```cshtml
@await Html.PartialAsync("Components/_Alert", new AlertViewModel
{
    Variant = AlertVariant.Warning,
    Title = "Action Required",
    Message = "Your bot token expires in 7 days. Please renew it in the Discord Developer Portal.",
    IsDismissible = true,
    DismissCallback = "dismissWarning"
})
```

---

### Card

**Location:** `Pages/Shared/Components/_Card.cshtml`
**ViewModel:** `CardViewModel`

Container component with optional header, body, and footer sections. Supports interactive and collapsible modes.

#### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Title` | `string?` | `null` | Header title text |
| `Subtitle` | `string?` | `null` | Header subtitle text |
| `HeaderContent` | `string?` | `null` | Custom header HTML |
| `HeaderActions` | `string?` | `null` | Buttons/actions in header |
| `BodyContent` | `string?` | `null` | Main content HTML |
| `FooterContent` | `string?` | `null` | Footer content HTML |
| `Variant` | `CardVariant` | `Default` | Visual style variant |
| `IsInteractive` | `bool` | `false` | Hover effects, clickable |
| `IsCollapsible` | `bool` | `false` | Allow collapse/expand |
| `IsExpanded` | `bool` | `true` | Initial expanded state |
| `OnClick` | `string?` | `null` | JavaScript click handler |
| `CssClass` | `string?` | `null` | Additional CSS classes |

#### Enums

**CardVariant:**
- `Default` - Standard bordered card
- `Flat` - No border, subtle background
- `Elevated` - With shadow

#### Examples

**Basic Card:**
```cshtml
@await Html.PartialAsync("Components/_Card", new CardViewModel
{
    Title = "Server Statistics",
    BodyContent = "<p>Total Messages: 1,234</p><p>Active Users: 56</p>",
    Variant = CardVariant.Default
})
```

**Interactive Card with Actions:**
```cshtml
@await Html.PartialAsync("Components/_Card", new CardViewModel
{
    Title = "My Discord Server",
    Subtitle = "1,234 members",
    HeaderActions = "<button class='btn-secondary'>Manage</button>",
    BodyContent = "<p>Last activity: 5 minutes ago</p>",
    IsInteractive = true,
    OnClick = "navigateToServer('123')",
    Variant = CardVariant.Elevated
})
```

**Collapsible Card:**
```cshtml
@await Html.PartialAsync("Components/_Card", new CardViewModel
{
    Title = "Advanced Settings",
    IsCollapsible = true,
    IsExpanded = false,
    BodyContent = @"
        <div class='space-y-4'>
            <label>Debug Mode: <input type='checkbox' /></label>
            <label>Log Level: <select><option>Info</option></select></label>
        </div>
    "
})
```

---

### EmptyState

**Location:** `Pages/Shared/Components/_EmptyState.cshtml`
**ViewModel:** `EmptyStateViewModel`

Placeholder component for empty data scenarios with optional call-to-action buttons.

#### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Type` | `EmptyStateType` | `NoData` | Icon/message type |
| `Title` | `string` | `"No Data"` | Main heading |
| `Description` | `string` | `"There are no items to display."` | Description text |
| `IconSvgPath` | `string?` | `null` | Custom SVG path override |
| `PrimaryActionText` | `string?` | `null` | Primary button text |
| `PrimaryActionUrl` | `string?` | `null` | Primary button URL |
| `PrimaryActionOnClick` | `string?` | `null` | Primary button JS handler |
| `SecondaryActionText` | `string?` | `null` | Secondary button text |
| `SecondaryActionUrl` | `string?` | `null` | Secondary button URL |
| `Size` | `EmptyStateSize` | `Default` | Component size |

#### Enums

**EmptyStateType:**
- `NoData` - Folder icon, generic empty
- `NoResults` - Search icon with X, no search results
- `FirstTime` - Rocket/stars icon, onboarding
- `Error` - Warning icon, error loading
- `NoPermission` - Lock icon, access restricted
- `Offline` - Wifi-off icon, no connection

**EmptyStateSize:**
- `Compact` - Smaller padding, icon, text
- `Default` - Standard size
- `Large` - For full-page empty states

#### Examples

**No Servers Found:**
```cshtml
@await Html.PartialAsync("Components/_EmptyState", new EmptyStateViewModel
{
    Type = EmptyStateType.NoData,
    Title = "No Servers Configured",
    Description = "Get started by adding your first Discord server.",
    PrimaryActionText = "Add Server",
    PrimaryActionUrl = "/servers/add"
})
```

**Search Results Empty:**
```cshtml
@await Html.PartialAsync("Components/_EmptyState", new EmptyStateViewModel
{
    Type = EmptyStateType.NoResults,
    Title = "No Results Found",
    Description = $"No servers match '{Model.SearchQuery}'",
    Size = EmptyStateSize.Compact,
    PrimaryActionText = "Clear Search",
    PrimaryActionOnClick = "clearSearch()"
})
```

**First-Time Setup:**
```cshtml
@await Html.PartialAsync("Components/_EmptyState", new EmptyStateViewModel
{
    Type = EmptyStateType.FirstTime,
    Title = "Welcome to DiscordBot Manager",
    Description = "Configure your bot settings and add your first server to get started.",
    PrimaryActionText = "Configure Bot",
    PrimaryActionUrl = "/settings/bot",
    SecondaryActionText = "View Documentation",
    SecondaryActionUrl = "/docs",
    Size = EmptyStateSize.Large
})
```

---

### Pagination

**Location:** `Pages/Shared/Components/_Pagination.cshtml`
**ViewModel:** `PaginationViewModel`

Navigation component for paginated data with multiple display styles and optional page size selector.

#### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CurrentPage` | `int` | `1` | Current page number (1-indexed) |
| `TotalPages` | `int` | `1` | Total number of pages |
| `TotalItems` | `int` | `0` | Total item count |
| `PageSize` | `int` | `10` | Items per page |
| `PageSizeOptions` | `int[]` | `[10, 25, 50, 100]` | Available page sizes |
| `Style` | `PaginationStyle` | `Full` | Display style variant |
| `ShowPageSizeSelector` | `bool` | `false` | Show page size dropdown |
| `ShowItemCount` | `bool` | `false` | Show "X-Y of Z items" |
| `ShowFirstLast` | `bool` | `true` | Show first/last buttons |
| `BaseUrl` | `string` | `""` | Base URL for page links |
| `PageParameterName` | `string` | `"page"` | Query param for page |
| `PageSizeParameterName` | `string` | `"pageSize"` | Query param for page size |

#### Enums

**PaginationStyle:**
- `Full` - First, Prev, page numbers, Next, Last
- `Simple` - Just Prev/Next buttons
- `Compact` - Prev, "Page X of Y", Next
- `Bordered` - Connected button group style

#### Examples

**Full Pagination:**
```cshtml
@await Html.PartialAsync("Components/_Pagination", new PaginationViewModel
{
    CurrentPage = Model.Page,
    TotalPages = Model.TotalPages,
    TotalItems = Model.TotalServers,
    PageSize = 25,
    BaseUrl = "/servers",
    ShowItemCount = true
})
```

**Simple Navigation:**
```cshtml
@await Html.PartialAsync("Components/_Pagination", new PaginationViewModel
{
    CurrentPage = Model.CurrentPage,
    TotalPages = Model.TotalPages,
    Style = PaginationStyle.Simple,
    BaseUrl = "/logs"
})
```

**With Page Size Selector:**
```cshtml
@await Html.PartialAsync("Components/_Pagination", new PaginationViewModel
{
    CurrentPage = Model.Page,
    TotalPages = Model.TotalPages,
    TotalItems = Model.TotalMembers,
    PageSize = Model.PageSize,
    PageSizeOptions = new[] { 10, 25, 50, 100 },
    ShowPageSizeSelector = true,
    ShowItemCount = true,
    BaseUrl = "/members"
})
```

---

## Best Practices

### Component Initialization

Use object initializer syntax for cleaner code:

```cshtml
@await Html.PartialAsync("Components/_Button", new ButtonViewModel
{
    Text = "Save",
    Variant = ButtonVariant.Primary
})
```

### Conditional Rendering

Wrap components in conditional blocks when needed:

```cshtml
@if (Model.HasError)
{
    @await Html.PartialAsync("Components/_Alert", new AlertViewModel
    {
        Variant = AlertVariant.Error,
        Message = Model.ErrorMessage
    })
}
```

### Combining Components

Components can be combined to create complex UIs:

```cshtml
@await Html.PartialAsync("Components/_Card", new CardViewModel
{
    Title = "User Profile",
    HeaderActions = Html.Partial("Components/_Button", new ButtonViewModel
    {
        Text = "Edit",
        Variant = ButtonVariant.Secondary,
        Size = ButtonSize.Small
    }).ToString(),
    BodyContent = $@"
        {Html.Partial("Components/_StatusIndicator", new StatusIndicatorViewModel
        {
            Status = StatusType.Online,
            Text = ""Online""
        })}
        <p class='mt-2'>Member since: {Model.JoinDate}</p>
    "
})
```

### Accessibility

Always provide `AriaLabel` for icon-only buttons and ensure proper semantic HTML:

```cshtml
@await Html.PartialAsync("Components/_Button", new ButtonViewModel
{
    IconLeft = "...",
    IsIconOnly = true,
    AriaLabel = "Close dialog" // Required for screen readers
})
```

### Validation Feedback

Use `ValidationState` and `ValidationMessage` for real-time form feedback:

```cshtml
@await Html.PartialAsync("Components/_FormInput", new FormInputViewModel
{
    Id = "username",
    Name = "Username",
    ValidationState = Model.IsValid ? ValidationState.Success : ValidationState.Error,
    ValidationMessage = Model.ValidationMessage
})
```

## Related Documentation

- [Design System](design-system.md) - Visual design specifications
- [MVP Plan](mvp-plan.md) - Implementation phases
- [API Endpoints](api-endpoints.md) - Backend API reference
