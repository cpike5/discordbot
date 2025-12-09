# Implementation Plan: Issue #61 - Reusable UI Components

**GitHub Issue:** #61 - Feature 1.4: Reusable UI Components
**Created:** 2025-12-09
**Status:** Ready for Implementation

---

## 1. Requirement Summary

Create 10 reusable Razor partial views for the Discord Bot Admin UI. These components will be located in `src/DiscordBot.Bot/Pages/Shared/Components/` and will follow the design system patterns established in `docs/articles/design-system.md` and HTML prototypes in `docs/prototypes/`.

### Required Components

| # | Component | File Name | Purpose |
|---|-----------|-----------|---------|
| 1 | Button | `_Button.cshtml` | Primary, secondary, accent, danger buttons with variants |
| 2 | Card | `_Card.cshtml` | Content containers with header, body, footer sections |
| 3 | Form Input | `_FormInput.cshtml` | Text inputs with validation support |
| 4 | Form Select | `_FormSelect.cshtml` | Dropdown select components |
| 5 | Alert | `_Alert.cshtml` | Notification/alert banners |
| 6 | Badge | `_Badge.cshtml` | Status badges and labels |
| 7 | Status Indicator | `_StatusIndicator.cshtml` | Online/offline/idle status display |
| 8 | Pagination | `_Pagination.cshtml` | Page navigation controls |
| 9 | Empty State | `_EmptyState.cshtml` | No-data placeholder displays |
| 10 | Loading Spinner | `_LoadingSpinner.cshtml` | Loading state indicators |

---

## 2. Architectural Considerations

### 2.1 Existing System Components

- **Current Shared Partials:** `_Layout.cshtml`, `_Navbar.cshtml`, `_Sidebar.cshtml`, `_Breadcrumb.cshtml` exist in `src/DiscordBot.Bot/Pages/Shared/`
- **CSS Framework:** Tailwind CSS compiled locally (not CDN) - see `src/DiscordBot.Bot/wwwroot/css/main.css`
- **Design System:** Comprehensive tokens defined in `docs/articles/design-system.md`
- **Prototypes:** Reference implementations in `docs/prototypes/` folder

### 2.2 Integration Requirements

- All components must use Tailwind utility classes aligned with design tokens
- Components receive parameters via `@model` or ViewData dictionary
- Must support accessibility (WCAG 2.1 AA compliance)
- Components should be composable and nestable where appropriate

### 2.3 View Model Strategy

Create a shared view models namespace: `DiscordBot.Bot.ViewModels.Components`

Each component model should be:
- Immutable where possible (use `init` setters)
- Nullable for optional parameters with sensible defaults
- Validated using data annotations where applicable

### 2.4 CSS Class Conventions

All components should use Tailwind classes following design system tokens:
- Background: `bg-bg-primary`, `bg-bg-secondary`, `bg-bg-tertiary`
- Text: `text-text-primary`, `text-text-secondary`, `text-text-tertiary`
- Border: `border-border-primary`, `border-border-focus`
- Accent: `bg-accent-orange`, `bg-accent-blue`, `text-accent-orange`
- Semantic: `bg-success`, `bg-warning`, `bg-error`, `bg-info`

---

## 3. Component Specifications

### 3.1 `_Button.cshtml`

**Source Prototype:** `docs/articles/design-system.md` (Section 4: Buttons)

#### Model Definition

```csharp
// src/DiscordBot.Bot/ViewModels/Components/ButtonViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record ButtonViewModel
{
    public string Text { get; init; } = string.Empty;
    public ButtonVariant Variant { get; init; } = ButtonVariant.Primary;
    public ButtonSize Size { get; init; } = ButtonSize.Medium;
    public string? Type { get; init; } = "button"; // button, submit, reset
    public string? IconLeft { get; init; }  // SVG path or icon name
    public string? IconRight { get; init; }
    public bool IsDisabled { get; init; } = false;
    public bool IsLoading { get; init; } = false;
    public bool IsIconOnly { get; init; } = false;
    public string? AriaLabel { get; init; }
    public string? OnClick { get; init; } // JavaScript handler
    public Dictionary<string, string>? AdditionalAttributes { get; init; }
}

public enum ButtonVariant
{
    Primary,    // Orange accent - main CTAs
    Secondary,  // Outline - cancel/secondary actions
    Accent,     // Blue - informational actions
    Danger,     // Red - destructive actions
    Ghost       // Transparent - subtle actions
}

public enum ButtonSize
{
    Small,      // py-1.5 px-3 text-xs
    Medium,     // py-2.5 px-5 text-sm (default)
    Large       // py-3 px-6 text-base
}
```

#### HTML Pattern

```html
<!-- Primary Button -->
<button class="inline-flex items-center justify-center gap-2 px-5 py-2.5 text-sm font-semibold text-white bg-accent-orange hover:bg-accent-orange-hover active:bg-accent-orange-active rounded-md transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-blue disabled:opacity-50 disabled:cursor-not-allowed">
    <!-- Optional Icon Left -->
    <svg class="w-4 h-4">...</svg>
    <span>Button Text</span>
    <!-- Optional Icon Right -->
</button>

<!-- Secondary Button -->
<button class="inline-flex items-center justify-center gap-2 px-5 py-2.5 text-sm font-semibold text-text-primary bg-transparent border border-border-primary hover:bg-bg-hover rounded-md transition-colors">
    <span>Cancel</span>
</button>

<!-- Accent Button -->
<button class="inline-flex items-center justify-center gap-2 px-5 py-2.5 text-sm font-semibold text-white bg-accent-blue hover:bg-accent-blue-hover active:bg-accent-blue-active rounded-md transition-colors">
    <span>View Details</span>
</button>

<!-- Danger Button -->
<button class="inline-flex items-center justify-center gap-2 px-5 py-2.5 text-sm font-semibold text-white bg-error hover:bg-error/80 rounded-md transition-colors">
    <span>Delete</span>
</button>

<!-- Size Variants -->
<!-- Small: px-3 py-1.5 text-xs -->
<!-- Medium: px-5 py-2.5 text-sm (default) -->
<!-- Large: px-6 py-3 text-base -->

<!-- Icon Only -->
<button class="p-2.5 text-text-secondary hover:text-text-primary hover:bg-bg-hover rounded-md transition-colors" aria-label="Settings">
    <svg class="w-5 h-5">...</svg>
</button>

<!-- Loading State -->
<button class="... opacity-75 cursor-not-allowed" disabled>
    <svg class="w-4 h-4 animate-spin"><!-- spinner --></svg>
    <span>Saving...</span>
</button>
```

---

### 3.2 `_Card.cshtml`

**Source Prototype:** `docs/prototypes/components/data-display/cards.html`

#### Model Definition

```csharp
// src/DiscordBot.Bot/ViewModels/Components/CardViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record CardViewModel
{
    public string? Title { get; init; }
    public string? Subtitle { get; init; }
    public RenderFragment? HeaderContent { get; init; }  // For custom header
    public RenderFragment? HeaderActions { get; init; }  // Buttons in header
    public RenderFragment? BodyContent { get; init; }    // Main content
    public RenderFragment? FooterContent { get; init; }  // Footer content
    public CardVariant Variant { get; init; } = CardVariant.Default;
    public bool IsInteractive { get; init; } = false;
    public bool IsCollapsible { get; init; } = false;
    public bool IsExpanded { get; init; } = true;
    public string? OnClick { get; init; }
    public string? CssClass { get; init; }
}

public enum CardVariant
{
    Default,    // Standard bordered card
    Flat,       // No border, subtle background
    Elevated    // With shadow
}
```

#### HTML Pattern

```html
<!-- Standard Card with Header + Body + Footer -->
<div class="bg-bg-secondary border border-border-primary rounded-lg overflow-hidden">
    <!-- Header -->
    <div class="flex items-center justify-between px-6 py-4 border-b border-border-primary">
        <h3 class="text-lg font-semibold text-text-primary">Card Title</h3>
        <!-- Optional Header Actions -->
        <button class="px-3 py-1.5 text-sm font-medium bg-accent-orange hover:bg-accent-orange-hover text-white rounded-md transition-colors">
            Action
        </button>
    </div>
    <!-- Body -->
    <div class="p-6">
        <p class="text-text-secondary">Card body content...</p>
    </div>
    <!-- Footer (optional) -->
    <div class="flex items-center justify-between px-6 py-4 border-t border-border-primary bg-bg-primary/50">
        <span class="text-xs text-text-tertiary">Footer info</span>
    </div>
</div>

<!-- Body-Only Card -->
<div class="bg-bg-secondary border border-border-primary rounded-lg p-6">
    <p class="text-text-secondary">Simple content card</p>
</div>

<!-- Elevated Card -->
<div class="bg-bg-secondary border border-border-primary rounded-lg shadow-lg overflow-hidden">
    ...
</div>

<!-- Interactive Card (hover effect) -->
<div class="bg-bg-secondary border border-border-primary rounded-lg overflow-hidden cursor-pointer transition-all hover:border-accent-blue hover:-translate-y-0.5 hover:shadow-lg hover:shadow-accent-blue/15">
    ...
</div>

<!-- Collapsible Card -->
<div class="bg-bg-secondary border border-border-primary rounded-lg overflow-hidden">
    <button class="w-full flex items-center justify-between px-6 py-4 border-b border-border-primary hover:bg-bg-hover transition-colors">
        <h3 class="text-lg font-semibold text-text-primary">Collapsible Title</h3>
        <svg class="w-5 h-5 text-text-secondary transition-transform" class:rotate-180="expanded">
            <path d="M19 9l-7 7-7-7" />
        </svg>
    </button>
    <div class="p-6" style="max-height: 0; overflow: hidden; transition: max-height 0.3s">
        Content
    </div>
</div>
```

---

### 3.3 `_FormInput.cshtml`

**Source Prototype:** `docs/prototypes/forms/components/01-text-input.html`

#### Model Definition

```csharp
// src/DiscordBot.Bot/ViewModels/Components/FormInputViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record FormInputViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Label { get; init; }
    public string Type { get; init; } = "text"; // text, email, password, search, url, tel
    public string? Placeholder { get; init; }
    public string? Value { get; init; }
    public string? HelpText { get; init; }
    public InputSize Size { get; init; } = InputSize.Medium;
    public ValidationState ValidationState { get; init; } = ValidationState.None;
    public string? ValidationMessage { get; init; }
    public bool IsRequired { get; init; } = false;
    public bool IsDisabled { get; init; } = false;
    public bool IsReadOnly { get; init; } = false;
    public string? IconLeft { get; init; }   // SVG icon path
    public string? IconRight { get; init; }
    public int? MaxLength { get; init; }
    public bool ShowCharacterCount { get; init; } = false;
    public Dictionary<string, string>? AdditionalAttributes { get; init; }
}

public enum InputSize
{
    Small,      // py-1.5 px-3 text-xs
    Medium,     // py-2.5 px-3.5 text-sm (default)
    Large       // py-3 px-4 text-base
}

public enum ValidationState
{
    None,
    Success,
    Warning,
    Error
}
```

#### HTML Pattern

```html
<!-- Form Group Structure -->
<div class="space-y-1.5">
    <!-- Label -->
    <label for="input-id" class="block text-sm font-medium text-text-primary">
        Label Text
        <span class="text-error">*</span> <!-- if required -->
    </label>

    <!-- Input Wrapper (for icons) -->
    <div class="relative">
        <!-- Icon Left -->
        <div class="absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none">
            <svg class="w-5 h-5 text-text-tertiary">...</svg>
        </div>

        <!-- Input -->
        <input
            type="text"
            id="input-id"
            name="input-name"
            class="w-full px-3.5 py-2.5 text-sm text-text-primary bg-bg-primary border border-border-primary rounded-md placeholder-text-tertiary transition-colors focus:outline-none focus:border-accent-blue focus:ring-[3px] focus:ring-accent-blue/15"
            placeholder="Enter text..."
        />

        <!-- Icon Right -->
        <div class="absolute right-3 top-1/2 -translate-y-1/2">
            <svg class="w-5 h-5 text-text-tertiary">...</svg>
        </div>
    </div>

    <!-- Help Text -->
    <p class="text-xs text-text-secondary">Help text description</p>

    <!-- Error Message -->
    <p class="flex items-center gap-1.5 text-xs text-error">
        <svg class="w-4 h-4"><!-- error icon --></svg>
        Error message text
    </p>

    <!-- Success Message -->
    <p class="flex items-center gap-1.5 text-xs text-success">
        <svg class="w-4 h-4"><!-- check icon --></svg>
        Success message
    </p>
</div>

<!-- Validation State Classes -->
<!-- Error: border-error focus:border-error focus:ring-error/15 -->
<!-- Success: border-success focus:border-success focus:ring-success/15 -->

<!-- Size Variants -->
<!-- Small: py-1.5 px-3 text-xs -->
<!-- Medium: py-2.5 px-3.5 text-sm -->
<!-- Large: py-3 px-4 text-base -->

<!-- With Icon Left: pl-10 -->
<!-- With Icon Right: pr-10 -->
<!-- With Both Icons: pl-10 pr-10 -->

<!-- Disabled State -->
<input class="... bg-bg-secondary text-text-tertiary cursor-not-allowed opacity-60" disabled />

<!-- Character Counter -->
<div class="flex justify-end">
    <span class="text-xs text-text-tertiary">45/100</span>
</div>
```

---

### 3.4 `_FormSelect.cshtml`

**Source Prototype:** `docs/prototypes/forms/components/03-select-dropdown.html`

#### Model Definition

```csharp
// src/DiscordBot.Bot/ViewModels/Components/FormSelectViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record FormSelectViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Label { get; init; }
    public string? Placeholder { get; init; } = "Select an option";
    public string? SelectedValue { get; init; }
    public List<SelectOption> Options { get; init; } = new();
    public List<SelectOptionGroup>? OptionGroups { get; init; }
    public string? HelpText { get; init; }
    public InputSize Size { get; init; } = InputSize.Medium;
    public ValidationState ValidationState { get; init; } = ValidationState.None;
    public string? ValidationMessage { get; init; }
    public bool IsRequired { get; init; } = false;
    public bool IsDisabled { get; init; } = false;
    public bool AllowMultiple { get; init; } = false;
    public Dictionary<string, string>? AdditionalAttributes { get; init; }
}

public record SelectOption
{
    public string Value { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public bool IsDisabled { get; init; } = false;
}

public record SelectOptionGroup
{
    public string Label { get; init; } = string.Empty;
    public List<SelectOption> Options { get; init; } = new();
}
```

#### HTML Pattern

```html
<!-- Native Select -->
<div class="space-y-1.5">
    <label for="select-id" class="block text-sm font-medium text-text-primary">
        Select Label
        <span class="text-error">*</span>
    </label>

    <div class="relative">
        <select
            id="select-id"
            name="select-name"
            class="w-full px-3.5 py-2.5 pr-10 text-sm text-text-primary bg-bg-primary border border-border-primary rounded-md appearance-none transition-colors focus:outline-none focus:border-accent-blue focus:ring-[3px] focus:ring-accent-blue/15"
        >
            <option value="" disabled selected>Select an option</option>
            <option value="1">Option 1</option>
            <option value="2">Option 2</option>
            <option value="3">Option 3</option>
        </select>

        <!-- Chevron Icon -->
        <div class="absolute right-3 top-1/2 -translate-y-1/2 pointer-events-none">
            <svg class="w-5 h-5 text-text-tertiary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
            </svg>
        </div>
    </div>

    <p class="text-xs text-text-secondary">Help text</p>
</div>

<!-- With Option Groups -->
<select class="...">
    <optgroup label="Group 1">
        <option value="1a">Option 1A</option>
        <option value="1b">Option 1B</option>
    </optgroup>
    <optgroup label="Group 2">
        <option value="2a">Option 2A</option>
        <option value="2b">Option 2B</option>
    </optgroup>
</select>

<!-- Validation States -->
<!-- Error: border-error focus:border-error focus:ring-error/15 -->
<!-- Success: border-success focus:border-success focus:ring-success/15 -->

<!-- Size Variants -->
<!-- Small: py-1.5 px-3 text-xs -->
<!-- Medium: py-2.5 px-3.5 text-sm -->
<!-- Large: py-3 px-4 text-base -->
```

---

### 3.5 `_Alert.cshtml`

**Source Prototype:** `docs/prototypes/feedback-alerts.html`

#### Model Definition

```csharp
// src/DiscordBot.Bot/ViewModels/Components/AlertViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record AlertViewModel
{
    public AlertVariant Variant { get; init; } = AlertVariant.Info;
    public string? Title { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool IsDismissible { get; init; } = false;
    public bool ShowIcon { get; init; } = true;
    public string? DismissCallback { get; init; } // JavaScript function name
}

public enum AlertVariant
{
    Info,       // Cyan/blue - informational
    Success,    // Green - success/confirmation
    Warning,    // Amber - caution
    Error       // Red - error/danger
}
```

#### HTML Pattern

```html
<!-- Info Alert -->
<div class="flex items-start gap-3 p-4 rounded-lg border bg-info/10 border-info/30 text-info" role="alert" aria-live="polite">
    <svg class="w-5 h-5 flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
    </svg>
    <div class="flex-1">
        <h3 class="text-sm font-semibold">Alert Title</h3>
        <p class="text-sm opacity-90 mt-1">Alert message content goes here.</p>
    </div>
    <!-- Dismissible -->
    <button type="button" class="p-1 hover:opacity-70 transition-opacity" aria-label="Dismiss">
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
        </svg>
    </button>
</div>

<!-- Success Alert -->
<div class="... bg-success/10 border-success/30 text-success" role="alert">
    <svg><!-- check-circle icon --></svg>
    ...
</div>

<!-- Warning Alert -->
<div class="... bg-warning/10 border-warning/30 text-warning" role="alert">
    <svg><!-- exclamation-triangle icon --></svg>
    ...
</div>

<!-- Error Alert -->
<div class="... bg-error/10 border-error/30 text-error" role="alert">
    <svg><!-- x-circle icon --></svg>
    ...
</div>

<!-- Icons by Variant -->
<!-- Info: M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z -->
<!-- Success: M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z -->
<!-- Warning: M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z -->
<!-- Error: M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z -->
```

---

### 3.6 `_Badge.cshtml`

**Source Prototype:** `docs/prototypes/components/data-display/primitives.html`

#### Model Definition

```csharp
// src/DiscordBot.Bot/ViewModels/Components/BadgeViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record BadgeViewModel
{
    public string Text { get; init; } = string.Empty;
    public BadgeVariant Variant { get; init; } = BadgeVariant.Default;
    public BadgeSize Size { get; init; } = BadgeSize.Medium;
    public BadgeStyle Style { get; init; } = BadgeStyle.Filled;
    public string? IconLeft { get; init; }
    public bool IsRemovable { get; init; } = false;
    public string? OnRemove { get; init; }
}

public enum BadgeVariant
{
    Default,    // Gray
    Orange,     // Primary accent
    Blue,       // Secondary accent
    Success,    // Green
    Warning,    // Amber
    Error,      // Red
    Info        // Cyan
}

public enum BadgeSize
{
    Small,      // px-2 py-0.5 text-[10px]
    Medium,     // px-3 py-1 text-xs
    Large       // px-4 py-1.5 text-sm
}

public enum BadgeStyle
{
    Filled,     // Solid background
    Outline     // Border only
}
```

#### HTML Pattern

```html
<!-- Filled Badges -->
<span class="inline-flex items-center px-3 py-1 text-xs font-semibold rounded-full bg-accent-orange text-white">
    Admin
</span>

<span class="inline-flex items-center px-3 py-1 text-xs font-semibold rounded-full bg-accent-blue text-white">
    Moderator
</span>

<span class="inline-flex items-center px-3 py-1 text-xs font-semibold rounded-full bg-bg-tertiary text-text-secondary">
    Member
</span>

<span class="inline-flex items-center px-3 py-1 text-xs font-semibold rounded-full bg-success text-white">
    Active
</span>

<span class="inline-flex items-center px-3 py-1 text-xs font-semibold rounded-full bg-warning text-white">
    Pending
</span>

<span class="inline-flex items-center px-3 py-1 text-xs font-semibold rounded-full bg-error text-white">
    Banned
</span>

<!-- Outline Badges -->
<span class="inline-flex items-center px-3 py-1 text-xs font-semibold rounded-full border border-accent-orange text-accent-orange bg-transparent">
    Outline Badge
</span>

<!-- Size Variants -->
<!-- Small: px-2 py-0.5 text-[10px] -->
<!-- Medium: px-3 py-1 text-xs -->
<!-- Large: px-4 py-1.5 text-sm -->

<!-- With Icon -->
<span class="inline-flex items-center gap-1.5 px-3 py-1 text-xs font-semibold rounded-full bg-success text-white">
    <svg class="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
        <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />
    </svg>
    Verified
</span>

<!-- Removable Badge -->
<span class="inline-flex items-center gap-1.5 px-3 py-1 text-xs font-semibold rounded-full bg-accent-blue text-white">
    Tag Name
    <button type="button" class="hover:bg-white/20 rounded-full p-0.5 transition-colors" aria-label="Remove">
        <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
        </svg>
    </button>
</span>
```

---

### 3.7 `_StatusIndicator.cshtml`

**Source Prototype:** `docs/prototypes/components/data-display/primitives.html`

#### Model Definition

```csharp
// src/DiscordBot.Bot/ViewModels/Components/StatusIndicatorViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record StatusIndicatorViewModel
{
    public StatusType Status { get; init; } = StatusType.Offline;
    public string? Text { get; init; }  // Optional text label
    public StatusDisplayStyle DisplayStyle { get; init; } = StatusDisplayStyle.DotWithText;
    public bool IsPulsing { get; init; } = false;
    public StatusSize Size { get; init; } = StatusSize.Medium;
}

public enum StatusType
{
    Online,     // Green
    Idle,       // Yellow/Amber
    Busy,       // Red (Do Not Disturb)
    Offline     // Gray
}

public enum StatusDisplayStyle
{
    DotOnly,        // Just the colored dot
    DotWithText,    // Dot + status text
    BadgeStyle      // Pill badge with dot
}

public enum StatusSize
{
    Small,      // w-1.5 h-1.5
    Medium,     // w-2 h-2
    Large       // w-3 h-3
}
```

#### HTML Pattern

```html
<!-- Dot with Text -->
<span class="inline-flex items-center gap-2 text-sm font-medium text-success">
    <span class="w-2 h-2 bg-success rounded-full"></span>
    Online
</span>

<span class="inline-flex items-center gap-2 text-sm font-medium text-warning">
    <span class="w-2 h-2 bg-warning rounded-full"></span>
    Idle
</span>

<span class="inline-flex items-center gap-2 text-sm font-medium text-error">
    <span class="w-2 h-2 bg-error rounded-full"></span>
    Do Not Disturb
</span>

<span class="inline-flex items-center gap-2 text-sm font-medium text-text-tertiary">
    <span class="w-2 h-2 bg-text-tertiary rounded-full"></span>
    Offline
</span>

<!-- Dot Only -->
<span class="w-2 h-2 bg-success rounded-full"></span>

<!-- Pulsing Dot -->
<span class="relative">
    <span class="w-2 h-2 bg-success rounded-full"></span>
    <span class="absolute inset-0 w-2 h-2 bg-success rounded-full animate-ping opacity-75"></span>
</span>

<!-- Badge Style -->
<span class="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-semibold rounded-full bg-success/20 text-success">
    <span class="w-1.5 h-1.5 bg-success rounded-full"></span>
    Online
</span>

<span class="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-semibold rounded-full bg-warning/20 text-warning">
    <span class="w-1.5 h-1.5 bg-warning rounded-full"></span>
    Idle
</span>

<span class="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-semibold rounded-full bg-error/20 text-error">
    <span class="w-1.5 h-1.5 bg-error rounded-full"></span>
    Busy
</span>

<span class="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-semibold rounded-full bg-border-primary text-text-tertiary">
    <span class="w-1.5 h-1.5 bg-text-tertiary rounded-full"></span>
    Offline
</span>

<!-- Size Variants -->
<!-- Small: w-1.5 h-1.5 -->
<!-- Medium: w-2 h-2 -->
<!-- Large: w-3 h-3 -->
```

---

### 3.8 `_Pagination.cshtml`

**Source Prototype:** `docs/prototypes/components/data-display/lists.html` (Section 5)

#### Model Definition

```csharp
// src/DiscordBot.Bot/ViewModels/Components/PaginationViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record PaginationViewModel
{
    public int CurrentPage { get; init; } = 1;
    public int TotalPages { get; init; } = 1;
    public int TotalItems { get; init; } = 0;
    public int PageSize { get; init; } = 10;
    public int[] PageSizeOptions { get; init; } = new[] { 10, 25, 50, 100 };
    public PaginationStyle Style { get; init; } = PaginationStyle.Full;
    public bool ShowPageSizeSelector { get; init; } = false;
    public bool ShowItemCount { get; init; } = false;
    public bool ShowFirstLast { get; init; } = true;
    public string BaseUrl { get; init; } = string.Empty;
    public string PageParameterName { get; init; } = "page";
    public string PageSizeParameterName { get; init; } = "pageSize";
}

public enum PaginationStyle
{
    Full,       // First, Prev, page numbers, Next, Last
    Simple,     // Just Prev/Next buttons
    Compact,    // Prev, Page X of Y, Next
    Bordered    // Connected button group style
}
```

#### HTML Pattern

```html
<!-- Full Pagination -->
<nav aria-label="Pagination" class="flex items-center gap-1">
    <!-- First Page -->
    <button class="px-2 py-2 text-sm text-text-secondary hover:bg-bg-hover rounded-md transition-colors disabled:opacity-50 disabled:cursor-not-allowed" aria-label="First page">
        <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 19l-7-7 7-7m8 14l-7-7 7-7" />
        </svg>
    </button>

    <!-- Previous -->
    <button class="px-2 py-2 text-sm text-text-secondary hover:bg-bg-hover rounded-md transition-colors disabled:opacity-50 disabled:cursor-not-allowed" aria-label="Previous page">
        <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
        </svg>
    </button>

    <!-- Page Numbers -->
    <button class="px-3 py-2 text-sm font-medium text-white bg-accent-orange rounded-md">1</button>
    <button class="px-3 py-2 text-sm text-text-secondary hover:bg-bg-hover rounded-md transition-colors">2</button>
    <button class="px-3 py-2 text-sm text-text-secondary hover:bg-bg-hover rounded-md transition-colors">3</button>
    <span class="px-2 py-2 text-text-tertiary">...</span>
    <button class="px-3 py-2 text-sm text-text-secondary hover:bg-bg-hover rounded-md transition-colors">10</button>

    <!-- Next -->
    <button class="px-2 py-2 text-sm text-text-secondary hover:bg-bg-hover rounded-md transition-colors" aria-label="Next page">
        <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
        </svg>
    </button>

    <!-- Last Page -->
    <button class="px-2 py-2 text-sm text-text-secondary hover:bg-bg-hover rounded-md transition-colors" aria-label="Last page">
        <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 5l7 7-7 7M5 5l7 7-7 7" />
        </svg>
    </button>
</nav>

<!-- Simple Pagination -->
<nav aria-label="Pagination" class="flex items-center gap-3">
    <button class="inline-flex items-center gap-2 px-4 py-2 text-sm font-medium text-text-secondary bg-bg-secondary border border-border-primary rounded-md hover:bg-bg-hover transition-colors">
        <svg class="w-4 h-4"><!-- chevron-left --></svg>
        Previous
    </button>
    <button class="inline-flex items-center gap-2 px-4 py-2 text-sm font-medium text-text-secondary bg-bg-secondary border border-border-primary rounded-md hover:bg-bg-hover transition-colors">
        Next
        <svg class="w-4 h-4"><!-- chevron-right --></svg>
    </button>
</nav>

<!-- Compact/Mobile Pagination -->
<nav aria-label="Pagination" class="flex items-center justify-between gap-4">
    <button class="px-3 py-2 text-sm text-text-secondary bg-bg-secondary border border-border-primary rounded-md hover:bg-bg-hover transition-colors">
        <svg class="w-4 h-4"><!-- chevron-left --></svg>
    </button>
    <span class="text-sm text-text-secondary">
        Page <span class="font-medium text-text-primary">1</span> of <span class="font-medium text-text-primary">10</span>
    </span>
    <button class="px-3 py-2 text-sm text-text-secondary bg-bg-secondary border border-border-primary rounded-md hover:bg-bg-hover transition-colors">
        <svg class="w-4 h-4"><!-- chevron-right --></svg>
    </button>
</nav>

<!-- With Info and Page Size -->
<div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
    <div class="flex items-center gap-3">
        <label for="pageSize" class="text-sm text-text-secondary">Show:</label>
        <select id="pageSize" class="px-3 py-1.5 text-sm bg-bg-primary border border-border-primary rounded-md text-text-primary focus:border-accent-blue">
            <option value="10">10</option>
            <option value="25">25</option>
            <option value="50">50</option>
        </select>
        <span class="text-sm text-text-secondary">
            Showing <span class="font-medium text-text-primary">1-10</span> of <span class="font-medium text-text-primary">100</span>
        </span>
    </div>
    <nav><!-- pagination buttons --></nav>
</div>

<!-- Bordered Style -->
<nav class="inline-flex items-center bg-bg-secondary border border-border-primary rounded-lg overflow-hidden">
    <button class="px-3 py-2 text-sm text-text-secondary hover:bg-bg-hover border-r border-border-primary">
        <svg class="w-4 h-4"><!-- chevron-left --></svg>
    </button>
    <button class="px-4 py-2 text-sm font-medium text-white bg-accent-orange border-r border-border-primary">1</button>
    <button class="px-4 py-2 text-sm text-text-secondary hover:bg-bg-hover border-r border-border-primary">2</button>
    <button class="px-4 py-2 text-sm text-text-secondary hover:bg-bg-hover border-r border-border-primary">3</button>
    <button class="px-3 py-2 text-sm text-text-secondary hover:bg-bg-hover">
        <svg class="w-4 h-4"><!-- chevron-right --></svg>
    </button>
</nav>
```

---

### 3.9 `_EmptyState.cshtml`

**Source Prototype:** `docs/prototypes/feedback-empty-states.html`

#### Model Definition

```csharp
// src/DiscordBot.Bot/ViewModels/Components/EmptyStateViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record EmptyStateViewModel
{
    public EmptyStateType Type { get; init; } = EmptyStateType.NoData;
    public string Title { get; init; } = "No Data";
    public string Description { get; init; } = "There are no items to display.";
    public string? IconSvgPath { get; init; }  // Custom SVG path override
    public string? PrimaryActionText { get; init; }
    public string? PrimaryActionUrl { get; init; }
    public string? PrimaryActionOnClick { get; init; }
    public string? SecondaryActionText { get; init; }
    public string? SecondaryActionUrl { get; init; }
    public EmptyStateSize Size { get; init; } = EmptyStateSize.Default;
}

public enum EmptyStateType
{
    NoData,         // Folder icon - generic empty
    NoResults,      // Search icon with X - no search results
    FirstTime,      // Rocket/stars icon - onboarding
    Error,          // Warning icon - error loading
    NoPermission,   // Lock icon - access restricted
    Offline         // Wifi-off icon - no connection
}

public enum EmptyStateSize
{
    Compact,    // Smaller padding, icon, text
    Default,    // Standard size
    Large       // For full-page empty states
}
```

#### HTML Pattern

```html
<!-- Default Empty State -->
<div class="flex flex-col items-center text-center max-w-[400px] mx-auto space-y-4 py-8">
    <!-- Icon -->
    <div class="p-4 bg-bg-tertiary rounded-full">
        <svg class="w-16 h-16 text-text-tertiary" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
            <!-- Icon path based on type -->
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
        </svg>
    </div>

    <!-- Text Content -->
    <div>
        <h3 class="text-lg font-semibold text-text-primary mb-2">No Data Available</h3>
        <p class="text-sm text-text-secondary">There are no items to display. Data will appear here once it becomes available.</p>
    </div>

    <!-- Actions (optional) -->
    <div class="flex flex-col sm:flex-row items-center gap-3">
        <button class="inline-flex items-center gap-2 px-5 py-2.5 bg-accent-orange hover:bg-accent-orange-hover text-white font-semibold text-sm rounded-md transition-colors">
            <svg class="w-4 h-4"><!-- plus icon --></svg>
            Add Item
        </button>
        <a href="#" class="text-sm text-accent-blue hover:text-accent-blue-hover transition-colors">
            Learn more
        </a>
    </div>
</div>

<!-- Compact Empty State -->
<div class="flex flex-col items-center text-center max-w-[320px] mx-auto space-y-3 py-5">
    <div class="p-3 bg-bg-tertiary rounded-full">
        <svg class="w-10 h-10 text-text-tertiary">...</svg>
    </div>
    <div>
        <h3 class="text-base font-semibold text-text-primary mb-1">No Data</h3>
        <p class="text-xs text-text-secondary">Nothing to display yet.</p>
    </div>
    <button class="inline-flex items-center gap-1.5 px-4 py-2 bg-accent-orange text-white font-semibold text-xs rounded-md">
        <svg class="w-3.5 h-3.5">...</svg>
        Add Item
    </button>
</div>

<!-- Icon Paths by Type -->
<!-- NoData (Folder): M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z -->
<!-- NoResults (Search+X): M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z + M15 9l-6 6m0-6l6 6 -->
<!-- FirstTime (Stars): M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813... -->
<!-- Error (Warning): M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z -->
<!-- NoPermission (Lock): M16.5 10.5V6.75a4.5 4.5 0 10-9 0v3.75m-.75 11.25h10.5a2.25 2.25... -->
<!-- Offline (Wifi-off): M8.288 15.038a5.25 5.25 0 017.424 0M5.106 11.856c3.807-3.808... + M4 4l16 16 -->
```

---

### 3.10 `_LoadingSpinner.cshtml`

**Source Prototype:** `docs/prototypes/feedback-loading.html`

#### Model Definition

```csharp
// src/DiscordBot.Bot/ViewModels/Components/LoadingSpinnerViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record LoadingSpinnerViewModel
{
    public SpinnerVariant Variant { get; init; } = SpinnerVariant.Simple;
    public SpinnerSize Size { get; init; } = SpinnerSize.Medium;
    public string? Message { get; init; }
    public string? SubMessage { get; init; }
    public SpinnerColor Color { get; init; } = SpinnerColor.Blue;
    public bool IsOverlay { get; init; } = false;  // Full container overlay
}

public enum SpinnerVariant
{
    Simple,     // Rotating circle
    Dots,       // Three bouncing dots
    Pulse       // Pulsing circle with ring
}

public enum SpinnerSize
{
    Small,      // 24px
    Medium,     // 40px
    Large       // 64px
}

public enum SpinnerColor
{
    Blue,       // accent-blue (default)
    Orange,     // accent-orange
    White       // For dark backgrounds
}
```

#### HTML Pattern

```html
<!-- Simple Spinner -->
<div class="w-10 h-10 border-[3px] border-white/20 border-t-accent-blue rounded-full animate-spin"></div>

<!-- Simple Spinner with Color Variants -->
<!-- Blue: border-t-accent-blue -->
<!-- Orange: border-t-accent-orange -->
<!-- White: border-white/20 border-t-white -->

<!-- Size Variants -->
<!-- Small: w-6 h-6 border-2 -->
<!-- Medium: w-10 h-10 border-[3px] -->
<!-- Large: w-16 h-16 border-4 -->

<!-- Dots Spinner -->
<div class="flex items-center justify-center gap-1.5">
    <div class="w-2.5 h-2.5 bg-accent-blue rounded-full animate-bounce" style="animation-delay: -0.32s"></div>
    <div class="w-2.5 h-2.5 bg-accent-blue rounded-full animate-bounce" style="animation-delay: -0.16s"></div>
    <div class="w-2.5 h-2.5 bg-accent-blue rounded-full animate-bounce"></div>
</div>

<!-- Dots Size Variants -->
<!-- Small: w-1.5 h-1.5 -->
<!-- Medium: w-2.5 h-2.5 -->
<!-- Large: w-4 h-4 -->

<!-- Pulse Spinner -->
<div class="relative flex items-center justify-center">
    <div class="absolute w-10 h-10 border-[3px] border-accent-blue rounded-full animate-ping opacity-75"></div>
    <div class="w-5 h-5 bg-accent-blue rounded-full animate-pulse"></div>
</div>

<!-- Pulse Size Variants -->
<!-- Small: outer w-6, inner w-3 -->
<!-- Medium: outer w-10, inner w-5 -->
<!-- Large: outer w-16, inner w-8 -->

<!-- Spinner with Message -->
<div class="flex flex-col items-center justify-center gap-3">
    <div class="w-10 h-10 border-[3px] border-white/20 border-t-accent-blue rounded-full animate-spin"></div>
    <p class="text-sm text-text-primary">Loading servers...</p>
    <p class="text-xs text-text-tertiary">This may take a moment</p>
</div>

<!-- Inline Spinner (for buttons) -->
<button class="inline-flex items-center gap-2 px-5 py-2.5 bg-accent-orange text-white rounded-md opacity-75 cursor-not-allowed" disabled>
    <svg class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
        <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
        <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
    </svg>
    Saving...
</button>

<!-- Container Overlay -->
<div class="relative">
    <!-- Content that's loading -->
    <div class="p-6 bg-bg-secondary rounded-lg">
        Content here...
    </div>
    <!-- Overlay -->
    <div class="absolute inset-0 flex flex-col items-center justify-center gap-3 bg-bg-primary/80 rounded-lg">
        <div class="w-10 h-10 border-[3px] border-white/20 border-t-accent-blue rounded-full animate-spin"></div>
        <p class="text-sm text-text-primary">Loading...</p>
    </div>
</div>

<!-- Progress Bar (Indeterminate) -->
<div class="h-1 bg-border-primary rounded-full overflow-hidden">
    <div class="h-full w-1/2 bg-accent-blue rounded-full animate-[indeterminate_1.5s_ease-in-out_infinite]"></div>
</div>

<!-- Progress Bar (Determinate) -->
<div class="space-y-2">
    <div class="flex justify-between text-sm">
        <span class="text-text-primary">Uploading...</span>
        <span class="text-accent-blue font-mono">75%</span>
    </div>
    <div class="h-1 bg-border-primary rounded-full overflow-hidden">
        <div class="h-full bg-accent-blue rounded-full transition-all duration-300" style="width: 75%"></div>
    </div>
</div>
```

---

## 4. File Structure

### 4.1 Directory Layout

```
src/DiscordBot.Bot/
├── Pages/
│   └── Shared/
│       ├── _Layout.cshtml           # (existing)
│       ├── _Navbar.cshtml           # (existing)
│       ├── _Sidebar.cshtml          # (existing)
│       ├── _Breadcrumb.cshtml       # (existing)
│       └── Components/              # NEW DIRECTORY
│           ├── _Button.cshtml
│           ├── _Card.cshtml
│           ├── _FormInput.cshtml
│           ├── _FormSelect.cshtml
│           ├── _Alert.cshtml
│           ├── _Badge.cshtml
│           ├── _StatusIndicator.cshtml
│           ├── _Pagination.cshtml
│           ├── _EmptyState.cshtml
│           └── _LoadingSpinner.cshtml
└── ViewModels/
    └── Components/                  # NEW DIRECTORY
        ├── ButtonViewModel.cs
        ├── CardViewModel.cs
        ├── FormInputViewModel.cs
        ├── FormSelectViewModel.cs
        ├── AlertViewModel.cs
        ├── BadgeViewModel.cs
        ├── StatusIndicatorViewModel.cs
        ├── PaginationViewModel.cs
        ├── EmptyStateViewModel.cs
        └── LoadingSpinnerViewModel.cs
```

---

## 5. Implementation Order

### Phase 1: Foundation (No Dependencies)
1. **ButtonViewModel.cs** + **_Button.cshtml** - Most fundamental, used everywhere
2. **BadgeViewModel.cs** + **_Badge.cshtml** - Simple, standalone
3. **StatusIndicatorViewModel.cs** + **_StatusIndicator.cshtml** - Simple, standalone
4. **LoadingSpinnerViewModel.cs** + **_LoadingSpinner.cshtml** - Simple, standalone

### Phase 2: Form Components (May use Button)
5. **FormInputViewModel.cs** + **_FormInput.cshtml** - Core form element
6. **FormSelectViewModel.cs** + **_FormSelect.cshtml** - Core form element
7. **AlertViewModel.cs** + **_Alert.cshtml** - Uses similar patterns

### Phase 3: Complex Components (May use multiple Phase 1-2 components)
8. **CardViewModel.cs** + **_Card.cshtml** - Container, may include buttons
9. **EmptyStateViewModel.cs** + **_EmptyState.cshtml** - May include buttons
10. **PaginationViewModel.cs** + **_Pagination.cshtml** - Uses button patterns

---

## 6. Acceptance Criteria

### General Criteria (All Components)
- [ ] Component renders correctly with default parameters
- [ ] Component renders correctly with all parameter combinations
- [ ] Tailwind CSS classes match design system tokens
- [ ] ARIA attributes present for accessibility
- [ ] Keyboard navigation works for interactive elements
- [ ] Focus states are visible (blue outline)
- [ ] Component is responsive (mobile-friendly)

### Per-Component Criteria

#### _Button.cshtml
- [ ] All 5 variants render correctly (primary, secondary, accent, danger, ghost)
- [ ] All 3 sizes render correctly
- [ ] Icon-left, icon-right, and icon-only configurations work
- [ ] Loading state shows spinner and disables button
- [ ] Disabled state applies correct styling

#### _Card.cshtml
- [ ] Header, body, and footer sections render conditionally
- [ ] Elevated and interactive variants apply correct styles
- [ ] Collapsible functionality works with JavaScript

#### _FormInput.cshtml
- [ ] Label, input, help text, and validation messages render correctly
- [ ] All validation states (none, success, warning, error) apply correct styles
- [ ] Icon left/right positioning works
- [ ] Disabled and readonly states work
- [ ] Character counter displays when enabled

#### _FormSelect.cshtml
- [ ] Options render correctly
- [ ] Option groups render correctly
- [ ] Validation states apply correct styles
- [ ] Placeholder text shows for empty selection

#### _Alert.cshtml
- [ ] All 4 variants render with correct colors and icons
- [ ] Title and message display correctly
- [ ] Dismissible button shows when enabled
- [ ] Correct ARIA attributes for screen readers

#### _Badge.cshtml
- [ ] All color variants render correctly
- [ ] All sizes render correctly
- [ ] Outline style renders correctly
- [ ] Icon support works
- [ ] Removable badge functionality works

#### _StatusIndicator.cshtml
- [ ] All 4 status types render with correct colors
- [ ] All 3 display styles work (dot-only, dot-with-text, badge)
- [ ] Pulsing animation works for online status

#### _Pagination.cshtml
- [ ] All 4 styles render correctly (full, simple, compact, bordered)
- [ ] Current page is highlighted
- [ ] Disabled states for first/last page work
- [ ] Page size selector works when enabled
- [ ] Item count displays when enabled

#### _EmptyState.cshtml
- [ ] All 6 types render with correct icons
- [ ] Title and description display correctly
- [ ] Primary and secondary actions render when provided
- [ ] Compact size reduces padding and icon size

#### _LoadingSpinner.cshtml
- [ ] All 3 variants animate correctly
- [ ] All 3 sizes render correctly
- [ ] Message and sub-message display when provided
- [ ] Overlay mode covers parent container

---

## 7. Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| CSS class name inconsistencies | Medium | Use exact classes from design-system.md; create a CSS class mapping document |
| Animation performance on mobile | Low | Use CSS transforms and opacity only; test on real devices |
| Accessibility gaps | High | Run accessibility audit with axe-core; test with screen reader |
| Component API too complex | Medium | Start with minimal required parameters; add optional parameters incrementally |
| Tailwind purge removes classes | High | Ensure all component classes are included in Tailwind content paths |

---

## 8. Testing Strategy

### Unit Tests
- Test view model validation and defaults
- Test enum value handling

### Integration Tests
- Render components with TagHelper test utilities
- Verify HTML output structure

### Visual Tests
- Compare rendered components against prototype screenshots
- Test responsive behavior at breakpoints

### Accessibility Tests
- Run axe-core on each component
- Keyboard navigation testing
- Screen reader compatibility

---

## 9. Documentation Requirements

After implementation, update:
1. `docs/articles/design-system.md` - Add component usage examples
2. Create `docs/articles/razor-components.md` - API documentation for all components
3. Update README with component library information

---

## 10. References

- Design System: `docs/articles/design-system.md`
- Button patterns: Design System Section 4
- Card patterns: `docs/prototypes/components/data-display/cards.html`
- Form patterns: `docs/prototypes/forms/components/01-text-input.html`, `03-select-dropdown.html`
- Alert patterns: `docs/prototypes/feedback-alerts.html`
- Badge/Status patterns: `docs/prototypes/components/data-display/primitives.html`
- Pagination patterns: `docs/prototypes/components/data-display/lists.html`
- Empty State patterns: `docs/prototypes/feedback-empty-states.html`
- Loading patterns: `docs/prototypes/feedback-loading.html`
