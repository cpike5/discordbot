# VOX UI/UX Design Specification

**Version:** 1.1
**Last Updated:** 2026-02-02
**Target Framework:** .NET 8 Razor Pages with Tailwind CSS
**Related Systems:** [Design System](design-system.md) | [Component API](component-api.md)

---

## Table of Contents

1. [Overview](#overview)
2. [Design Principles](#design-principles)
3. [Color & Typography](#color--typography)
4. [Page Specifications](#page-specifications)
5. [Component Specifications](#component-specifications)
6. [Animation & Transitions](#animation--transitions)
7. [Accessibility](#accessibility)
8. [API Requirements](#api-requirements)
9. [Implementation Checklist](#implementation-checklist)

---

## Overview

### What is VOX?

VOX is a Half-Life-style concatenated word-clip announcement system that synthesizes speech by:

1. **Breaking down sentences** into individual words
2. **Generating individual audio clips** for each word using Azure TTS
3. **Caching clips** in a word bank for reuse
4. **Concatenating clips** with configurable gaps between words
5. **Applying PA-system audio filters** (bandpass, compression, distortion) for the iconic Half-Life sound

### How VOX Differs from TTS

| Feature | TTS Portal | VOX Portal |
|---------|-----------|-----------|
| Input | Free-form text | Words parsed into tokens |
| Generation | Real-time synthesis | Word-by-word with caching |
| Audio Output | Smooth, natural speech | Robotic, concatenated clips |
| Audio Processing | None | PA-system filters (bandpass, compression, distortion) |
| Word Gap Control | N/A | 20-200ms configurable gap |
| Reusability | No caching | Full word bank with persistent cache |
| Visual Feedback | Character count | Token preview strip with cache status |

### Design Goals

- **Mirror TTS Portal** structure and layout for consistency
- **Make word tokenization visible** through real-time preview
- **Provide cache status feedback** (green = cached, orange = will generate, red = error)
- **Support two input modes**: Free text (automatic parsing) and Sentence Builder (manual composition)
- **Enable PA filter customization** for authentic Half-Life sound design

---

## Design Principles

### Consistency with Existing Portal Design

The VOX UI adheres to the established portal design language:

- **Two-column layout** (sidebar left, main panel right) matching TTS Portal
- **Portal Header component** with guild icon, navigation tabs, and status badge
- **Dark theme optimized** using existing design tokens
- **Toast notifications** for feedback
- **Status polling** for real-time updates

### VOX-Specific Enhancements

1. **Token-based visual feedback** - Replace character counter with token preview strip
2. **Cache status indicators** - Color-coded pills showing word availability
3. **Word bank management** - Admin tools for bulk generation, import/export, purging
4. **PA filter controls** - Specialized UI for audio processing parameters
5. **Sentence builder mode** - Drag-and-drop word composition interface

### Responsive Design

- **Desktop (1024px+)**: Full two-column layout with visible controls
- **Tablet (768px-1023px)**: Stacked layout, sentence builder hidden
- **Mobile (< 768px)**: Single column, free text mode only, collapsible settings

---

## Color & Typography

### Design Tokens

VOX uses the existing design system tokens from `design-system.md`:

#### Background Colors

```css
--color-bg-primary: #1d2022;      /* Main background */
--color-bg-secondary: #262a2d;    /* Cards, panels */
--color-bg-tertiary: #2f3336;     /* Elevated elements */
--color-bg-hover: #363a3e;        /* Hover states */
```

#### Text Colors

```css
--color-text-primary: #d7d3d0;    /* Primary text */
--color-text-secondary: #a8a5a3;  /* Secondary text, labels */
--color-text-tertiary: #7a7876;   /* Muted text */
```

#### Accent Colors

```css
--color-accent-orange: #cb4e1b;        /* Primary actions */
--color-accent-orange-hover: #e5591f;  /* Hover state */
--color-accent-blue: #098ecf;          /* Secondary actions */
--color-accent-blue-hover: #0ba3ea;    /* Hover state */
```

#### Semantic Colors

```css
--color-success: #10b981;     /* Green - cached words */
--color-warning: #f59e0b;     /* Amber - will generate */
--color-error: #ef4444;       /* Red - errors */
--color-info: #06b6d4;        /* Cyan - informational */
```

### VOX-Specific Token Extensions

Add these new tokens for VOX-specific UI elements:

```css
/* Token Preview Pills */
--color-token-cached: var(--color-success);           /* #10b981 green */
--color-token-cached-bg: rgba(16, 185, 129, 0.15);
--color-token-cached-border: rgba(16, 185, 129, 0.4);

--color-token-generate: var(--color-warning);         /* #f59e0b amber */
--color-token-generate-bg: rgba(245, 158, 11, 0.15);
--color-token-generate-border: rgba(245, 158, 11, 0.4);

--color-token-error: var(--color-error);              /* #ef4444 red */
--color-token-error-bg: rgba(239, 68, 68, 0.15);
--color-token-error-border: rgba(239, 68, 68, 0.4);

/* PA Filter Indicator */
--color-filter-active: #3b82f6;                       /* Blue - active filter */
--color-filter-active-bg: rgba(59, 130, 246, 0.15);

/* Word Gap Indicator */
--color-gap-indicator: var(--color-text-tertiary);    /* Gray pause markers */
```

### Typography

Use existing typography scale:

- **Headings**: `text-h4` (1.25rem, 600 weight) for section titles
- **Labels**: `text-sm` (0.875rem, 500 weight) for form labels
- **Body Text**: `text-base` (1rem) for input content
- **Metadata**: `text-xs` (0.75rem) for counts, timestamps
- **Monospace**: `font-mono` for word tokens, SSML, technical content

---

## Page Specifications

### 1. VOX Portal Page (`/Portal/VOX/{guildId}`)

Member-facing page for composing and sending VOX announcements.

#### Layout Structure

```
┌─────────────────────────────────────────────────────────────────────┐
│ Portal Header (Guild Icon, Name, Tabs, Status)                     │
├──────────────────┬──────────────────────────────────────────────────┤
│  Sidebar (Left)  │  VOX Composer (Right)                            │
│  300px fixed     │  Flexible width                                  │
│                  │                                                  │
│  ┌────────────┐  │  ┌─────────────────────────────────────────┐    │
│  │ Voice      │  │  │ Mode Tabs: Free Text | Sentence Builder │    │
│  │ Channel    │  │  └─────────────────────────────────────────┘    │
│  │ Selector   │  │                                                  │
│  │            │  │  ┌─────────────────────────────────────────┐    │
│  │ [Dropdown] │  │  │ Textarea (Free Text Mode)               │    │
│  │            │  │  │ or                                      │    │
│  │ [Join] [L] │  │  │ Word Bank Browser (Sentence Builder)    │    │
│  └────────────┘  │  └─────────────────────────────────────────┘    │
│                  │                                                  │
│  ┌────────────┐  │  ┌─────────────────────────────────────────┐    │
│  │ Now        │  │  │ Token Preview Strip                     │    │
│  │ Playing    │  │  │ [word] · [word] · [word]                │    │
│  │            │  │  └─────────────────────────────────────────┘    │
│  │ [Message]  │  │                                                  │
│  │ [Stop]     │  │  ┌─────────────────────────────────────────┐    │
│  └────────────┘  │  │ Settings (collapsible)                  │    │
│                  │  │ - Voice Preset                          │    │
│                  │  │ - Word Gap Slider                       │    │
│                  │  │ - PA Filter Controls                    │    │
│                  │  │ - Speed Slider                          │    │
│                  │  └─────────────────────────────────────────┘    │
│                  │                                                  │
│                  │  [Send VOX Message] Button                       │
└──────────────────┴──────────────────────────────────────────────────┘
```

#### ASCII Wireframe (Free Text Mode)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ [🎮 Guild Icon] Guild Name Portal                          [● Online]        │
│ ──────────────────────────────────────────────────────────────────────────  │
│ [Soundboard] [TTS] [VOX ✓]                                                  │
├────────────────────┬────────────────────────────────────────────────────────┤
│ VOICE CHANNEL      │ VOX MESSAGE COMPOSER                                   │
│ ──────────────     │                                                        │
│ ● Connected        │ [Free Text ✓] [Sentence Builder]                      │
│                    │                                                        │
│ Select Channel:    │ Message:                                               │
│ [General Voice ▼]  │ ┌──────────────────────────────────────────────────┐  │
│                    │ │ attention all personnel                           │  │
│ [Join] [Leave]     │ │ security breach in sector c                       │  │
│                    │ │                                                   │  │
│ ──────────────     │ └──────────────────────────────────────────────────┘  │
│ NOW PLAYING        │                                                        │
│ ──────────────     │ Token Preview (7 words, ~4.2s):                       │
│                    │ [attention]·[all]·[personnel]·[security]·[breach]·    │
│ [▶] intruder alert │ [in]·[sector]·[c]                                     │
│     Playing... [■] │  green      green  green     orange    green          │
│                    │  green green green                                     │
│                    │                                                        │
│                    │ ▼ Settings                                             │
│                    │                                                        │
│                    │ Voice Preset:                                          │
│                    │ [en-US-GuyNeural ▼]                                    │
│                    │                                                        │
│                    │ Word Gap: 50ms                                         │
│                    │ [────●─────────────] 20-200ms                          │
│                    │                                                        │
│                    │ PA Filter: [Heavy ▼]                                   │
│                    │ Speed: 1.0x [───●──────] 0.75x-1.5x                    │
│                    │                                                        │
│                    │ [Send VOX Message (7 words, ~4.2s)]                    │
└────────────────────┴────────────────────────────────────────────────────────┘
```

#### ASCII Wireframe (Sentence Builder Mode)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ [🎮 Guild Icon] Guild Name Portal                          [● Online]        │
│ ──────────────────────────────────────────────────────────────────────────  │
│ [Soundboard] [TTS] [VOX ✓]                                                  │
├────────────────────┬────────────────────────────────────────────────────────┤
│ (Sidebar same as   │ VOX MESSAGE COMPOSER                                   │
│  Free Text Mode)   │                                                        │
│                    │ [Free Text] [Sentence Builder ✓]                      │
│                    │                                                        │
│                    │ Word Bank:                                             │
│                    │ [Search words...          ] [Common ▼]                 │
│                    │                                                        │
│                    │ ┌────────────────────────────────────────────────┐     │
│                    │ │ Common Words:                                  │     │
│                    │ │ [attention] [alert] [warning] [caution]        │     │
│                    │ │ [all] [personnel] [security] [medical]         │     │
│                    │ │ [breach] [code] [evacuation] [lockdown]        │     │
│                    │ │                                                │     │
│                    │ │ Numbers:                                       │     │
│                    │ │ [zero] [one] [two] [three] [four] [five]      │     │
│                    │ │                                                │     │
│                    │ │ Locations:                                     │     │
│                    │ │ [sector] [a] [b] [c] [level] [area]           │     │
│                    │ └────────────────────────────────────────────────┘     │
│                    │                                                        │
│                    │ Composition Strip:                                     │
│                    │ ┌────────────────────────────────────────────────┐     │
│                    │ │ [attention✖] · [all✖] · [⏸️150ms✖] · [person…✖]│     │
│                    │ │  ↕ drag to reorder, X to remove, click gap +⏸ │     │
│                    │ └────────────────────────────────────────────────┘     │
│                    │                                                        │
│                    │ Quick Add: [type word + Enter_______________]          │
│                    │                                                        │
│                    │ (Settings same as Free Text Mode)                      │
│                    │                                                        │
│                    │ [Send VOX Message (4 words + 1 pause, ~3.1s)]          │
└────────────────────┴────────────────────────────────────────────────────────┘
```

#### Component Breakdown

##### Left Sidebar (300px Fixed Width)

**Voice Channel Panel** (reuse existing `_VoiceChannelPanel.cshtml` component):

Connection status indicator using `StatusIndicatorViewModel`:

```csharp
@using DiscordBot.Bot.ViewModels.Components

new StatusIndicatorViewModel {
    Status = Model.IsConnected ? StatusType.Online : StatusType.Offline,
    Text = Model.IsConnected ? "Connected" : "Disconnected",
    DisplayStyle = StatusDisplayStyle.DotWithText,
    Size = StatusSize.Medium
}
```

Render via:
```razor
<partial name="Shared/Components/_StatusIndicator" model="Model.ConnectionStatus" />
```

- Channel dropdown selector
- Join/Leave buttons
- Styled with `bg-bg-secondary`, `border-border-primary`, `rounded-lg`, `p-5`

**Now Playing Section**:
- Container: `bg-bg-primary`, `rounded-lg`, `p-4`
- Message display: `text-sm`, `font-mono`, `text-text-primary`, truncate at 100 chars
- Stop button: Use `ButtonViewModel` with `Variant = ButtonVariant.Danger`, `IsIconOnly = true`, `AriaLabel = "Stop playback"`

```csharp
@using DiscordBot.Bot.ViewModels.Components

new ButtonViewModel {
    Variant = ButtonVariant.Danger,
    IsIconOnly = true,
    AriaLabel = "Stop playback",
    IconRight = "..." // Stop/X icon
}
```

Render via:
```razor
<partial name="Shared/Components/_Button" model="Model.StopButton" />
```

##### Right Panel (Flexible Width)

**Mode Tabs** (use `NavTabs` component with `NavTabStyle.Pills`):
```csharp
@using DiscordBot.Bot.ViewModels.Components

new NavTabsViewModel {
    ContainerId = "voxModeTabs",
    StyleVariant = NavTabStyle.Pills,
    NavigationMode = NavMode.InPage, // Client-side tab switching without page reload
    Tabs = new List<NavTabItem> {
        new() { Id = "freetext", Label = "Free Text", IconPathOutline = "..." },
        new() { Id = "builder", Label = "Sentence Builder", IconPathOutline = "..." }
    },
    ActiveTabId = "freetext"
}
```

Render via:
```razor
<partial name="Shared/Components/_NavTabs" model="Model.ModeTabs" />
```

**Free Text Input** (default mode):
- Textarea: `w-full`, `h-32`, `bg-bg-primary`, `border-border-primary`, `rounded-lg`, `p-3`
- Placeholder: "Type your message and words will be tokenized automatically..."
- Font: `text-base`, `font-normal`, `text-text-primary`
- Resize: `resize-y`

**Token Preview Strip**:
- Container: `flex`, `flex-wrap`, `gap-2`, `mt-3`, `p-3`, `bg-bg-tertiary`, `rounded-lg`
- Header: `text-xs`, `text-text-secondary`, `mb-2`, "Token Preview (X words, ~Xs):"
- Word pills (see [Component: Token Preview Pill](#component-token-preview-pill))
- Gap indicators: `text-text-tertiary`, `text-lg`, "·" (middle dot)

**Settings Panel** (collapsible on mobile):
- Header: `flex`, `items-center`, `justify-between`, `cursor-pointer`, `py-3`
- Title: `text-sm`, `font-semibold`, `text-text-primary`
- Chevron icon: `w-5 h-5`, `text-text-secondary`, `transition-transform`
- Content: `space-y-4`, `mt-4`

**Voice Preset Dropdown**:

Use `FormSelectViewModel`:

```csharp
@using DiscordBot.Bot.ViewModels.Components

new FormSelectViewModel {
    Id = "voicePreset",
    Name = "VoicePreset",
    Label = "Voice Preset",
    SelectedValue = Model.SelectedVoice ?? "en-US-GuyNeural",
    Size = InputSize.Medium,
    Options = new List<SelectOption> {
        new() { Value = "en-US-GuyNeural", Text = "Guy - Male (Neural)" },
        new() { Value = "en-US-AriaNeural", Text = "Aria - Female (Neural)" },
        new() { Value = "en-US-GuyNeural", Text = "Guy - Male (Neural)" },
        // More voice options...
    }
}
```

Render via:
```razor
<partial name="Shared/Components/_FormSelect" model="Model.VoicePreset" />
```

**Word Gap Slider**:
- Label: `text-sm`, `font-medium`, `text-text-primary`, `mb-2`
- Value display: `text-xs`, `text-accent-orange`, `font-semibold`, "50ms"
- Slider: `form-range` class, `min="20"`, `max="200"`, `step="10"`
- Track: `bg-border-primary`, thumb: `bg-accent-orange`

**PA Filter Controls**:
- Dropdown: `form-select` with options: Off, Light, Heavy, Custom
- When "Custom" selected, reveal:
  - Highpass Frequency: slider 100-1000 Hz
  - Lowpass Frequency: slider 1000-8000 Hz
  - Compression Ratio: slider 1.0-10.0
- Each slider row: `flex`, `items-center`, `gap-3`
- Labels: `text-xs`, `min-w-[120px]`
- Values: `text-xs`, `text-accent-orange`, `font-mono`

**Speed Slider**:
- Same styling as TTS portal: `form-range`, 0.75x-1.5x, step 0.1
- Display value: `text-xs`, `text-accent-orange`, "1.0x"

**Send Button**:

Use `ButtonViewModel` with `Variant = ButtonVariant.Primary`:

```csharp
@using DiscordBot.Bot.ViewModels.Components

new ButtonViewModel {
    Text = "Send VOX Message (7 words, ~4.2s)",
    Variant = ButtonVariant.Primary,
    Size = ButtonSize.Large,
    Type = "submit",
    IconLeft = "broadcast", // Megaphone/broadcast icon
    IsDisabled = !Model.IsConnected || Model.Tokens.Count == 0,
    IsLoading = Model.IsSending,
    AdditionalAttributes = new Dictionary<string, object> {
        { "class", "w-full" }
    }
}
```

Render via:
```razor
<partial name="Shared/Components/_Button" model="Model.SendButton" />
```

Styling (in CSS):
- Container: `w-full`, `mt-6`
- Button: `bg-accent-orange`, `hover:bg-accent-orange-hover`, `text-white`
- Padding: `py-3`, `px-6`, `rounded-lg`
- Font: `text-base`, `font-semibold`
- States:
  - Disabled: `opacity-50`, `cursor-not-allowed` (when not connected or no words)
  - Loading: Show spinner, text "Generating..."
  - Progress: Show progress bar during multi-word generation

### Alerts and Feedback

Use `AlertViewModel` for success/error messages:

```csharp
@using DiscordBot.Bot.ViewModels.Components

// Settings save success
new AlertViewModel {
    Variant = AlertVariant.Success,
    Title = "Settings Saved",
    Message = "VOX configuration updated successfully",
    IsDismissible = true,
    ShowIcon = true
};

// Rate limit warning
new AlertViewModel {
    Variant = AlertVariant.Warning,
    Title = "Rate Limit Warning",
    Message = "User has reached 5 messages per minute",
    IsDismissible = true,
    ShowIcon = true
};

// Generation error
new AlertViewModel {
    Variant = AlertVariant.Error,
    Title = "Generation Failed",
    Message = "Failed to synthesize word 'xyz123': invalid characters",
    IsDismissible = true,
    ShowIcon = true
};
```

Render via:
```razor
@if (Model.Alert != null)
{
  <partial name="Shared/Components/_Alert" model="Model.Alert" />
}
```

#### Interaction States

**Textarea Focus**:
- Border: `border-accent-orange`
- Ring: `ring-2`, `ring-accent-orange/20`
- Remove default browser outline

**Token Pill Hover**:
- Scale: `transform scale-105`
- Shadow: `shadow-md`
- Play preview icon appears (optional feature)

**Slider Thumb Interaction**:
- Hover: `scale-110`
- Active: `scale-120`
- Transition: `transition-transform duration-150`

**Send Button States**:
- Normal: `bg-accent-orange`
- Hover: `bg-accent-orange-hover`, `shadow-lg`
- Active: `bg-accent-orange-active`, `scale-98`
- Disabled: `bg-bg-tertiary`, `text-text-tertiary`, `cursor-not-allowed`

**Settings Collapse Animation**:
- Transition: `max-height 0.3s ease-out`
- Chevron rotation: `transform rotate-180`, `transition-transform 0.2s`

#### Responsive Breakpoints

**Desktop (1024px+)**:
- Two-column layout: sidebar 300px, main flex-1
- All features visible
- Settings always expanded
- Sentence builder available

**Tablet (768px-1023px)**:
- Sidebar stacks above main panel
- Settings collapsible
- Sentence builder hidden (free text only)

**Mobile (< 768px)**:
- Single column layout
- Sidebar sections become accordion panels
- Token preview wraps naturally
- Settings always collapsed by default
- Sentence builder unavailable
- Send button sticky at bottom
- All touch targets minimum 44x44px

---

### 2. VOX Admin Page (`/Guilds/VOX/{guildId}`)

Administrator page for VOX configuration and word bank management.

#### Layout Structure

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ Guild Navigation (Breadcrumb)                                               │
│ Guild Name > VOX Configuration                                              │
├─────────────────────────────────────────────────────────────────────────────┤
│ [Settings ✓] [Word Bank]                                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│ (Tab Content Below)                                                         │
└─────────────────────────────────────────────────────────────────────────────┘
```

Use `NavTabs` component for the Settings/Word Bank tabs:

```csharp
@using DiscordBot.Bot.ViewModels.Components

new NavTabsViewModel {
    ContainerId = "voxAdminTabs",
    StyleVariant = NavTabStyle.Underline,
    NavigationMode = NavMode.InPage,
    Tabs = new List<NavTabItem> {
        new() { Id = "settings", Label = "Settings", Href = "#settingsPanel" },
        new() { Id = "wordbank", Label = "Word Bank", Href = "#wordbankPanel" }
    },
    ActiveTabId = "settings"
}
```

Render via:
```razor
<partial name="Shared/Components/_NavTabs" model="Model.AdminTabs" />
```

#### ASCII Wireframe (Settings Tab)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ [Settings ✓] [Word Bank]                                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│ ┌────────────────────────────────────────────────────────────────────────┐  │
│ │ VOX SYSTEM CONFIGURATION                                               │  │
│ │                                                                        │  │
│ │ Enable VOX:                                                            │  │
│ │ [Toggle ON/OFF]                                                        │  │
│ │ When enabled, members can send VOX messages via /vox command          │  │
│ │                                                                        │  │
│ │ ──────────────────────────────────────────────────────────────────────│  │
│ │                                                                        │  │
│ │ Default Voice Preset:                                                  │  │
│ │ [en-US-GuyNeural ▼]                                                    │  │
│ │ Used when members don't specify a voice                               │  │
│ │                                                                        │  │
│ │ Default PA Filter:                                                     │  │
│ │ [Heavy ▼]                                                              │  │
│ │ Applied to all announcements by default                               │  │
│ │                                                                        │  │
│ │ Default Word Gap:                                                      │  │
│ │ [────●─────────────] 50ms (20-200ms)                                   │  │
│ │                                                                        │  │
│ │ ──────────────────────────────────────────────────────────────────────│  │
│ │                                                                        │  │
│ │ Max Message Word Count:                                                │  │
│ │ [50          ] words per message                                       │  │
│ │ Prevent excessive message length                                      │  │
│ │                                                                        │  │
│ │ Rate Limit:                                                            │  │
│ │ [5           ] messages per minute per user                            │  │
│ │                                                                        │  │
│ │ Auto-Generate Missing Words:                                           │  │
│ │ [Toggle ON/OFF]                                                        │  │
│ │ Automatically generate uncached words when needed                     │  │
│ │                                                                        │  │
│ │ [Save Settings]                                                        │  │
│ └────────────────────────────────────────────────────────────────────────┘  │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

#### ASCII Wireframe (Word Bank Tab)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ [Settings] [Word Bank ✓]                                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│ ┌────────────────────────────────────────────────────────────────────────┐  │
│ │ WORD BANK STATISTICS                                                   │  │
│ │ Total Words: 1,247  |  Total Size: 24.5 MB  |  Voices: 3              │  │
│ └────────────────────────────────────────────────────────────────────────┘  │
│                                                                              │
│ ┌────────────────────────────────────────────────────────────────────────┐  │
│ │ BULK OPERATIONS                                                        │  │
│ │                                                                        │  │
│ │ [Generate Word Pack ▼] [Import ZIP] [Export All] [Purge Cache]       │  │
│ │                                                                        │  │
│ │ Word Packs:                                                            │  │
│ │ • Common English 500 - Most frequently used English words             │  │
│ │ • NATO Phonetic - Alpha, Bravo, Charlie... (26 words)                 │  │
│ │ • Numbers 0-100 - Zero through one hundred                            │  │
│ │ • Half-Life Classic - Iconic HL1 VOX phrases (87 words)               │  │
│ │                                                                        │  │
│ │ Custom Word List (paste or type, one word per line):                  │  │
│ │ ┌──────────────────────────────────────────────────────────────────┐  │  │
│ │ │ attention                                                        │  │  │
│ │ │ all                                                              │  │  │
│ │ │ personnel                                                        │  │  │
│ │ │ ...                                                              │  │  │
│ │ └──────────────────────────────────────────────────────────────────┘  │  │
│ │                                                                        │  │
│ │ Voice: [en-US-GuyNeural ▼]  [Generate Words]                          │  │
│ └────────────────────────────────────────────────────────────────────────┘  │
│                                                                              │
│ ┌────────────────────────────────────────────────────────────────────────┐  │
│ │ WORD BANK TABLE                                                        │  │
│ │                                                                        │  │
│ │ [Search words...          ] Sort: [Date Added ▼]                      │  │
│ │                                                                        │  │
│ │ ┌──────────┬──────────────────┬─────────┬──────────┬────────┬───────┐│  │
│ │ │ Word     │ Voice            │ Size    │ Duration │ Added  │ Play  ││  │
│ │ ├──────────┼──────────────────┼─────────┼──────────┼────────┼───────┤│  │
│ │ │ attention│ en-US-GuyNeural  │ 18.2 KB │ 0.8s     │ 1/15   │ [▶] [🗑]││  │
│ │ │ all      │ en-US-GuyNeural  │ 12.1 KB │ 0.4s     │ 1/15   │ [▶] [🗑]││  │
│ │ │ personnel│ en-US-GuyNeural  │ 24.7 KB │ 1.1s     │ 1/15   │ [▶] [🗑]││  │
│ │ │ ...      │ ...              │ ...     │ ...      │ ...    │ ...   ││  │
│ │ └──────────┴──────────────────┴─────────┴──────────┴────────┴───────┘│  │
│ │                                                                        │  │
│ │ Showing 1-50 of 1,247                            [< 1 2 3 ... 25 >]   │  │
│ └────────────────────────────────────────────────────────────────────────┘  │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

#### Component Breakdown

##### Settings Tab

**Form Container**:
- Background: `bg-bg-secondary`
- Border: `border-border-primary`, `rounded-lg`
- Padding: `p-6`
- Spacing: `space-y-6`

**Form Controls**:

Use `FormInputViewModel` for numeric settings:

```csharp
@using DiscordBot.Bot.ViewModels.Components

// Max Message Word Count
new FormInputViewModel {
    Id = "maxWordCount",
    Name = "MaxMessageWords",
    Label = "Max Message Word Count",
    Type = "number",
    Value = Model.MaxMessageWords.ToString(),
    Size = InputSize.Medium,
    HelpText = "Prevent excessive message length"
};

// Rate Limit
new FormInputViewModel {
    Id = "rateLimit",
    Name = "RateLimitPerMinute",
    Label = "Rate Limit",
    Type = "number",
    Value = Model.RateLimitPerMinute.ToString(),
    Size = InputSize.Medium,
    HelpText = "Messages per minute per user"
};
```

Render via:
```razor
<partial name="Shared/Components/_FormInput" model="Model.MaxWordCountInput" />
<partial name="Shared/Components/_FormInput" model="Model.RateLimitInput" />
```

- Toggle switches: Custom component with `bg-accent-orange` when ON
- Sliders: `form-range` class matching VOX portal
- Dropdowns: Use `FormSelectViewModel`

**Save Button**:

Use `ButtonViewModel`:

```csharp
@using DiscordBot.Bot.ViewModels.Components

new ButtonViewModel {
    Text = "Save Settings",
    Variant = ButtonVariant.Primary,
    Size = ButtonSize.Medium,
    Type = "submit",
    IsLoading = Model.IsSaving
}
```

Render via:
```razor
<partial name="Shared/Components/_Button" model="Model.SaveButton" />
```

##### Word Bank Tab

**Statistics Cards** (similar to Soundboard stats):

Use `CardViewModel` for each statistic:

```csharp
@using DiscordBot.Bot.ViewModels.Components

new CardViewModel {
    Title = "Total Words",
    BodyContent = "1,247",
    Variant = CardVariant.Default
}
```

Grid layout with 3 columns:
```razor
<div class="grid grid-cols-1 md:grid-cols-3 gap-4">
  @foreach (var stat in Model.Statistics)
  {
    <partial name="Shared/Components/_Card" model="stat" />
  }
</div>
```

Styling:
- Grid: `grid-cols-1 md:grid-cols-3`, `gap-4`
- Card: `bg-bg-secondary`, `border-border-primary`, `rounded-lg`, `p-4`
- Stat value: `text-2xl`, `font-bold`, `text-text-primary`
- Label: `text-xs`, `text-text-secondary`

**Bulk Operations Toolbar**:

Use `ButtonViewModel` for each toolbar action:

```csharp
@using DiscordBot.Bot.ViewModels.Components

var generateButton = new ButtonViewModel {
    Text = "Generate Word Pack",
    Variant = ButtonVariant.Secondary,
    IconRight = "chevron-down",
    Size = ButtonSize.Medium
};

var importButton = new ButtonViewModel {
    Text = "Import ZIP",
    Variant = ButtonVariant.Secondary,
    IconLeft = "upload",
    Size = ButtonSize.Medium
};

var exportButton = new ButtonViewModel {
    Text = "Export All",
    Variant = ButtonVariant.Secondary,
    IconLeft = "download",
    Size = ButtonSize.Medium
};

var purgeButton = new ButtonViewModel {
    Text = "Purge Cache",
    Variant = ButtonVariant.Danger,
    IconLeft = "trash",
    Size = ButtonSize.Medium
};
```

Render in toolbar:
```razor
<div class="flex flex-wrap gap-3 mb-6">
  <partial name="Shared/Components/_Button" model="Model.GenerateButton" />
  <partial name="Shared/Components/_Button" model="Model.ImportButton" />
  <partial name="Shared/Components/_Button" model="Model.ExportButton" />
  <partial name="Shared/Components/_Button" model="Model.PurgeButton" />
</div>
```

**Word Pack Selector**:
- Dropdown: `form-select`, `mb-4`
- Description text: `text-xs`, `text-text-tertiary`, `italic`
- Generate button: `btn-primary` (orange)

**Custom Word List**:
- Textarea: `w-full`, `h-48`, `font-mono`, `text-sm`
- Background: `bg-bg-primary`
- Placeholder: "Enter words, one per line..."

**Word Bank Search**:

Use `FormInputViewModel`:

```csharp
@using DiscordBot.Bot.ViewModels.Components

new FormInputViewModel {
    Id = "wordSearch",
    Name = "search",
    Placeholder = "Search words...",
    Type = "text",
    Size = InputSize.Medium,
    IconLeft = "search"
}
```

Render via:
```razor
<partial name="Shared/Components/_FormInput" model="Model.SearchInput" />
```

**Word Bank Sort Dropdown**:

Use `SortDropdownViewModel` for the sort selector:

```csharp
@using DiscordBot.Bot.ViewModels.Components

new SortDropdownViewModel {
    Id = "wordBankSort",
    CurrentSort = "dateAdded",
    ParameterName = "sort",
    SortOptions = new List<SortOption> {
        new() { Value = "dateAdded", Label = "Date Added" },
        new() { Value = "word", Label = "Word (A-Z)" },
        new() { Value = "voice", Label = "Voice" },
        new() { Value = "size", Label = "File Size" },
        new() { Value = "duration", Label = "Duration" }
    }
}
```

Render via:
```razor
<partial name="Shared/_SortDropdown" model="Model.SortDropdown" />
```

**Word Bank Table** (reuse existing table styles):
- Container: `overflow-x-auto`
- Table: `min-w-full`, `divide-y`, `divide-border-primary`
- Header: `bg-bg-tertiary`, `text-text-secondary`, `text-xs`, `font-semibold`, `uppercase`
- Rows: `hover:bg-bg-hover`, `transition-colors`
- Play button: `ButtonViewModel` with `Variant = ButtonVariant.Ghost` and play icon
- Delete button: `text-error`, `hover:text-red-600`

**Pagination** (use existing `PaginationViewModel` component):
```csharp
@using DiscordBot.Bot.ViewModels.Components

new PaginationViewModel {
    CurrentPage = 1,
    TotalPages = 25,
    TotalItems = 1247,
    PageSize = 50,
    ShowItemCount = true,
    ShowFirstLast = true,
    Style = PaginationStyle.Full
}
```

Render via:
```razor
<partial name="Shared/Components/_Pagination" model="Model.Pagination" />
```

#### Interaction States

**Toggle Switch States**:
- OFF: `bg-bg-tertiary`, thumb position left
- ON: `bg-accent-orange`, thumb position right
- Transition: `transition-all duration-300`
- Thumb: `w-5 h-5`, `bg-white`, `rounded-full`, `shadow`

**Bulk Operation Buttons**:
- Normal: `border-border-primary`, `text-text-primary`
- Hover: `bg-bg-hover`, `border-accent-blue`
- Active: `bg-bg-tertiary`, `scale-95`
- Disabled: `opacity-50`, `cursor-not-allowed`

**Table Row Hover**:
- Background: `bg-bg-hover`
- Action buttons fade in: `opacity-0 → opacity-100`
- Transition: `duration-150`

**Generate Words Progress**:
- Show modal with progress bar
- Progress: `bg-accent-orange`, animated stripes
- Text: "Generating X of Y words..."
- Cancel button available

---

## Component Specifications

### Component: Token Preview Pill

Visual representation of a single word token with cache status indicator.

#### Props

```csharp
public record TokenPillViewModel
{
    public string Word { get; init; } = "";
    public TokenStatus Status { get; init; } = TokenStatus.Cached;
    public bool IsClickable { get; init; } = true; // Enable preview playback
    public string? Tooltip { get; init; } // Optional tooltip text
}

public enum TokenStatus
{
    Cached,      // Green - word exists in cache
    WillGenerate, // Orange - will be generated on send
    Error        // Red - cannot synthesize this word
}
```

#### HTML Structure

```html
<span class="token-pill token-pill-{status}"
      data-word="{word}"
      role="button"
      tabindex="0"
      aria-label="{word} - {status}">
  <span class="token-pill-text">{word}</span>
  {if isClickable}
    <svg class="token-pill-icon"><!-- play icon --></svg>
  {/if}
</span>
```

#### CSS Styling

```css
.token-pill {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem; /* 6px */
  padding: 0.375rem 0.75rem; /* 6px 12px */
  border-radius: 9999px;
  font-size: 0.875rem; /* 14px */
  font-weight: 500;
  border-width: 1px;
  border-style: solid;
  transition: all 0.15s ease-in-out;
  cursor: default;
}

.token-pill.clickable {
  cursor: pointer;
}

/* Status variants */
.token-pill-cached {
  background-color: var(--color-token-cached-bg);
  border-color: var(--color-token-cached-border);
  color: var(--color-token-cached);
}

.token-pill-willgenerate {
  background-color: var(--color-token-generate-bg);
  border-color: var(--color-token-generate-border);
  color: var(--color-token-generate);
}

.token-pill-error {
  background-color: var(--color-token-error-bg);
  border-color: var(--color-token-error-border);
  color: var(--color-token-error);
}

/* Hover state (when clickable) */
.token-pill.clickable:hover {
  transform: scale(1.05);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
}

.token-pill.clickable:hover .token-pill-icon {
  opacity: 1;
}

/* Icon */
.token-pill-icon {
  width: 0.875rem; /* 14px */
  height: 0.875rem;
  opacity: 0.5;
  transition: opacity 0.15s;
}

/* Focus state (keyboard navigation) */
.token-pill:focus-visible {
  outline: 2px solid var(--color-border-focus);
  outline-offset: 2px;
}
```

#### Accessibility

- **Role**: `button` (when clickable)
- **Tabindex**: `0` (keyboard accessible)
- **Aria-label**: "{word} - {status description}"
- **Keyboard**: Enter/Space to preview word audio

#### Usage Example

```cshtml
<div class="token-preview-strip">
  <span class="token-preview-label">Token Preview (7 words, ~4.2s):</span>
  <div class="token-preview-pills">
    @foreach (var token in Model.Tokens)
    {
      <partial name="Components/_TokenPill" model="token" />
      @if (!token.IsLast)
      {
        <span class="token-gap-indicator">·</span>
      }
    }
  </div>
</div>
```

---

### Component: Word Bank Grid

Browsable grid of available words grouped by category (Sentence Builder mode).

#### Props

```csharp
public record WordBankGridViewModel
{
    public string SearchQuery { get; init; } = "";
    public string SelectedCategory { get; init; } = "common";
    public List<WordBankCategory> Categories { get; init; } = new();
}

public record WordBankCategory
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string IconPath { get; init; } = "";
    public List<WordBankWord> Words { get; init; } = new();
}

public record WordBankWord
{
    public string Word { get; init; } = "";
    public bool IsCached { get; init; } = true;
    public float DurationSeconds { get; init; } = 0.5f;
}
```

#### HTML Structure

```html
<div class="word-bank-grid">
  <!-- Search & Filter -->
  <div class="word-bank-header">
    <input type="text"
           class="word-bank-search"
           placeholder="Search words..."
           aria-label="Search words">
    <select class="word-bank-category-select" aria-label="Filter by category">
      <option value="all">All Categories</option>
      <option value="common">Common</option>
      <option value="numbers">Numbers</option>
      <option value="nato">NATO Phonetic</option>
      <!-- ... -->
    </select>
  </div>

  <!-- Word Grid by Category -->
  <div class="word-bank-content">
    @foreach (var category in Categories)
    {
      <div class="word-bank-category">
        <h4 class="word-bank-category-title">
          <svg>@category.IconPath</svg>
          @category.Name
        </h4>
        <div class="word-bank-words">
          @foreach (var word in category.Words)
          {
            <button class="word-bank-word-tile @(word.IsCached ? "cached" : "uncached")"
                    data-word="@word.Word"
                    aria-label="Add @word.Word">
              <span class="word-text">@word.Word</span>
              <span class="word-duration">@word.DurationSeconds.ToString("0.0")s</span>
            </button>
          }
        </div>
      </div>
    }
  </div>
</div>
```

#### CSS Styling

```css
.word-bank-grid {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  height: 400px; /* Fixed height with internal scroll */
  overflow: hidden;
}

.word-bank-header {
  display: flex;
  gap: 0.75rem;
  flex-shrink: 0;
}

.word-bank-search {
  flex: 1;
  padding: 0.5rem 0.75rem;
  background-color: var(--color-bg-primary);
  border: 1px solid var(--color-border-primary);
  border-radius: 0.5rem;
  font-size: 0.875rem;
  color: var(--color-text-primary);
}

.word-bank-category-select {
  min-width: 150px;
  padding: 0.5rem 2rem 0.5rem 0.75rem;
  background-color: var(--color-bg-primary);
  border: 1px solid var(--color-border-primary);
  border-radius: 0.5rem;
  font-size: 0.875rem;
}

.word-bank-content {
  flex: 1;
  overflow-y: auto;
  overflow-x: hidden;
  padding-right: 0.5rem;

  /* Custom scrollbar */
  scrollbar-width: thin;
  scrollbar-color: var(--color-border-primary) transparent;
}

.word-bank-category {
  margin-bottom: 1.5rem;
}

.word-bank-category-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--color-text-secondary);
  margin-bottom: 0.75rem;
}

.word-bank-words {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
  gap: 0.5rem;
}

.word-bank-word-tile {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.25rem;
  padding: 0.75rem 0.5rem;
  background-color: var(--color-bg-tertiary);
  border: 1px solid var(--color-border-primary);
  border-radius: 0.5rem;
  cursor: pointer;
  transition: all 0.15s;
}

.word-bank-word-tile:hover {
  background-color: var(--color-bg-hover);
  border-color: var(--color-accent-blue);
  transform: translateY(-2px);
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.3);
}

.word-bank-word-tile.cached::before {
  content: '';
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background-color: var(--color-success);
  position: absolute;
  top: 0.5rem;
  right: 0.5rem;
}

.word-text {
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--color-text-primary);
  text-align: center;
  word-break: break-word;
}

.word-duration {
  font-size: 0.625rem;
  color: var(--color-text-tertiary);
}
```

#### Interaction States

**Word Tile Click**:
1. Visual feedback: scale animation
2. Add word to composition strip
3. Show toast: "Added '{word}' to composition"

**Keyboard Navigation**:
- Arrow keys to navigate tiles
- Enter/Space to add word
- Tab to move between search, category select, and tiles

---

### Component: Composition Strip

Draggable horizontal strip showing the current word sequence (Sentence Builder mode).

#### Props

```csharp
public record CompositionStripViewModel
{
    public List<CompositionItem> Items { get; init; } = new();
    public int MaxWords { get; init; } = 50;
}

public record CompositionItem
{
    public string Id { get; init; } = ""; // Unique ID for drag-drop
    public CompositionItemType Type { get; init; } = CompositionItemType.Word;
    public string Word { get; init; } = ""; // For Word type
    public int PauseDurationMs { get; init; } = 100; // For Pause type
}

public enum CompositionItemType
{
    Word,
    Pause
}
```

#### HTML Structure

```html
<div class="composition-strip">
  <div class="composition-header">
    <span class="composition-label">Composition (@Model.Items.Count / @Model.MaxWords):</span>
    <button class="composition-clear" aria-label="Clear all">
      <svg><!-- X icon --></svg> Clear
    </button>
  </div>

  <div class="composition-items" id="compositionContainer">
    @foreach (var item in Model.Items)
    {
      if (item.Type == CompositionItemType.Word)
      {
        <div class="composition-word"
             data-id="@item.Id"
             draggable="true"
             role="button"
             tabindex="0">
          <span class="composition-word-text">@item.Word</span>
          <button class="composition-word-remove" aria-label="Remove @item.Word">
            <svg><!-- X icon --></svg>
          </button>
        </div>

        <!-- Gap indicator (clickable to insert pause) -->
        <button class="composition-gap"
                data-after-id="@item.Id"
                aria-label="Insert pause after @item.Word">
          <svg><!-- Plus icon on hover --></svg>
        </button>
      }
      else
      {
        <div class="composition-pause"
             data-id="@item.Id"
             draggable="true">
          <svg><!-- Pause icon --></svg>
          <span class="composition-pause-duration">@item.PauseDurationMs ms</span>
          <button class="composition-pause-remove" aria-label="Remove pause">
            <svg><!-- X icon --></svg>
          </button>
        </div>

        <button class="composition-gap"
                data-after-id="@item.Id">
          <svg><!-- Plus icon --></svg>
        </button>
      }
    }

    <!-- Empty state -->
    @if (!Model.Items.Any())
    {
      <partial name="Shared/Components/_EmptyState" model="Model.EmptyState" />
    }
  </div>
</div>
```

#### CSS Styling

```css
.composition-strip {
  background-color: var(--color-bg-tertiary);
  border: 1px solid var(--color-border-primary);
  border-radius: 0.5rem;
  padding: 1rem;
}

.composition-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 0.75rem;
}

.composition-label {
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--color-text-primary);
}

.composition-clear {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.375rem 0.75rem;
  background-color: transparent;
  border: 1px solid var(--color-border-primary);
  border-radius: 0.375rem;
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  cursor: pointer;
  transition: all 0.15s;
}

.composition-clear:hover {
  background-color: var(--color-error);
  border-color: var(--color-error);
  color: white;
}

.composition-items {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
  min-height: 80px;
  padding: 0.75rem;
  background-color: var(--color-bg-primary);
  border-radius: 0.375rem;
}

.composition-word {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.75rem;
  background-color: var(--color-bg-secondary);
  border: 1px solid var(--color-border-primary);
  border-radius: 0.5rem;
  cursor: grab;
  transition: all 0.15s;
}

.composition-word:hover {
  border-color: var(--color-accent-blue);
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.2);
}

.composition-word.dragging {
  opacity: 0.5;
  cursor: grabbing;
}

.composition-word-text {
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--color-text-primary);
}

.composition-word-remove {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 1rem;
  height: 1rem;
  padding: 0;
  background: none;
  border: none;
  color: var(--color-text-tertiary);
  cursor: pointer;
  transition: color 0.15s;
}

.composition-word-remove:hover {
  color: var(--color-error);
}

/* Gap indicator (between words/pauses) */
.composition-gap {
  width: 1.5rem;
  height: 1.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  background: none;
  border: none;
  color: var(--color-text-tertiary);
  cursor: pointer;
  position: relative;
}

.composition-gap::before {
  content: '·';
  font-size: 1.25rem;
}

.composition-gap:hover::before {
  content: '+';
  color: var(--color-accent-blue);
  font-weight: bold;
}

/* Pause element */
.composition-pause {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.5rem 0.75rem;
  background-color: var(--color-bg-secondary);
  border: 1px dashed var(--color-border-primary);
  border-radius: 0.5rem;
  cursor: grab;
}

.composition-pause-duration {
  font-size: 0.75rem;
  font-family: var(--font-family-mono);
  color: var(--color-text-secondary);
}

/* Empty state */
.composition-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  width: 100%;
  padding: 1.5rem;
  color: var(--color-text-tertiary);
  text-align: center;
}

.composition-empty svg {
  width: 2rem;
  height: 2rem;
  opacity: 0.5;
}

.composition-empty span {
  font-size: 0.875rem;
}
```

#### Drag-and-Drop Behavior

**Drag Start**:
1. Add `dragging` class to element
2. Set `dataTransfer.effectAllowed = 'move'`
3. Store item ID in `dataTransfer`

**Drag Over**:
1. Prevent default to allow drop
2. Show drop indicator (blue line) at insertion point
3. Calculate drop position based on cursor X coordinate

**Drop**:
1. Get dragged item ID from `dataTransfer`
2. Calculate new index based on drop position
3. Reorder items in state
4. Re-render composition strip
5. Show toast: "Reordered successfully"

**Remove Button**:
1. Click → confirm dialog (optional)
2. Remove item from composition
3. Re-render strip
4. Show toast: "Removed '{word}'"

**Keyboard Accessibility**:
- Tab to focus word/pause items
- Arrow keys to move focus
- Enter/Space to select/activate
- Delete key to remove focused item

---

### Component: Empty State

Use `EmptyStateViewModel` for empty composition strip and no results scenarios.

#### Props

```csharp
public record EmptyStateViewModel
{
    public EmptyStateType Type { get; init; } = EmptyStateType.NoData;
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public EmptyStateSize Size { get; init; } = EmptyStateSize.Default;
    public string? PrimaryActionText { get; init; }
    public string? PrimaryActionUrl { get; init; }
}

public enum EmptyStateType
{
    NoData,        // No items added yet
    NoResults,     // Search returned no results
    FirstTime,     // Welcome message for new users
    Error,         // Error occurred
    NoPermission,  // Permission denied
    Offline        // Connection lost
}

public enum EmptyStateSize
{
    Compact,
    Default,
    Large
}
```

#### Usage Examples

**Composition Strip Empty**:
```csharp
@using DiscordBot.Bot.ViewModels.Components

new EmptyStateViewModel {
    Type = EmptyStateType.NoData,
    Title = "No words yet",
    Description = "Click words from the Word Bank to build your announcement",
    Size = EmptyStateSize.Default,
    PrimaryActionText = "Browse Word Bank",
    PrimaryActionUrl = "#wordBankPanel"
}
```

**Word Bank Search No Results**:
```csharp
new EmptyStateViewModel {
    Type = EmptyStateType.NoResults,
    Title = "No words found",
    Description = $"No words match '{searchQuery}'. Try a different search.",
    Size = EmptyStateSize.Compact
}
```

**Word Bank Empty**:
```csharp
new EmptyStateViewModel {
    Type = EmptyStateType.FirstTime,
    Title = "Word Bank is empty",
    Description = "Generate some words to get started. Choose a word pack or upload a custom list.",
    Size = EmptyStateSize.Large,
    PrimaryActionText = "Generate Words",
    PrimaryActionUrl = "#bulkOperations"
}
```

Render via:
```razor
<partial name="Shared/Components/_EmptyState" model="Model.EmptyState" />
```

---

### Component: Badge

Use `BadgeViewModel` for status displays and word statistics.

#### Props

```csharp
public record BadgeViewModel
{
    public string Text { get; init; } = "";
    public BadgeVariant Variant { get; init; } = BadgeVariant.Default;
    public BadgeSize Size { get; init; } = BadgeSize.Medium;
    public BadgeStyle Style { get; init; } = BadgeStyle.Filled;
    public string? IconLeft { get; init; }
    public bool IsRemovable { get; init; } = false;
}

public enum BadgeVariant
{
    Default, Orange, Blue, Success, Warning, Error, Info
}

public enum BadgeSize
{
    Small, Medium, Large
}

public enum BadgeStyle
{
    Filled,
    Outline
}
```

#### Usage Examples

**Cached Word Status**:
```csharp
@using DiscordBot.Bot.ViewModels.Components

new BadgeViewModel {
    Text = "Cached",
    Variant = BadgeVariant.Success,
    Size = BadgeSize.Small,
    Style = BadgeStyle.Filled
}
```

**Word Statistics in Word Bank**:
```csharp
// Will generate status
new BadgeViewModel {
    Text = "Will Generate",
    Variant = BadgeVariant.Warning,
    Size = BadgeSize.Small,
    Style = BadgeStyle.Filled
};

// Error status
new BadgeViewModel {
    Text = "Error",
    Variant = BadgeVariant.Error,
    Size = BadgeSize.Small,
    Style = BadgeStyle.Filled
}
```

Render via:
```razor
<partial name="Shared/Components/_Badge" model="Model.StatusBadge" />
```

---

### Component: PA Filter Controls

Specialized controls for audio processing parameters.

#### Props

```csharp
public record PaFilterControlsViewModel
{
    public PaFilterPreset SelectedPreset { get; init; } = PaFilterPreset.Heavy;
    public int HighpassFrequency { get; init; } = 300; // Hz
    public int LowpassFrequency { get; init; } = 3000; // Hz
    public float CompressionRatio { get; init; } = 4.0f;
    public float Distortion { get; init; } = 0.2f; // 0.0-1.0
}

public enum PaFilterPreset
{
    Off,
    Light,
    Heavy,
    Custom
}
```

#### HTML Structure

```html
<div class="pa-filter-controls">
  <label class="form-label">PA System Filter</label>

  <select class="pa-filter-preset" id="paFilterPreset">
    <option value="off">Off (No Processing)</option>
    <option value="light">Light (Subtle PA Effect)</option>
    <option value="heavy" selected>Heavy (Classic Half-Life)</option>
    <option value="custom">Custom (Manual Settings)</option>
  </select>

  <!-- Custom controls (visible when preset = custom) -->
  <div class="pa-filter-custom" id="paFilterCustomControls" style="display: none;">

    <!-- Highpass Frequency -->
    <div class="pa-filter-param">
      <label class="pa-filter-param-label">
        Highpass Filter
        <span class="pa-filter-param-value" id="highpassValue">300 Hz</span>
      </label>
      <input type="range"
             class="form-range"
             id="highpassSlider"
             min="100"
             max="1000"
             step="50"
             value="300">
      <span class="pa-filter-param-hint">Removes low rumble</span>
    </div>

    <!-- Lowpass Frequency -->
    <div class="pa-filter-param">
      <label class="pa-filter-param-label">
        Lowpass Filter
        <span class="pa-filter-param-value" id="lowpassValue">3000 Hz</span>
      </label>
      <input type="range"
             class="form-range"
             id="lowpassSlider"
             min="1000"
             max="8000"
             step="100"
             value="3000">
      <span class="pa-filter-param-hint">Cuts high frequencies</span>
    </div>

    <!-- Compression Ratio -->
    <div class="pa-filter-param">
      <label class="pa-filter-param-label">
        Compression
        <span class="pa-filter-param-value" id="compressionValue">4.0:1</span>
      </label>
      <input type="range"
             class="form-range"
             id="compressionSlider"
             min="1.0"
             max="10.0"
             step="0.5"
             value="4.0">
      <span class="pa-filter-param-hint">Reduces dynamic range</span>
    </div>

    <!-- Distortion -->
    <div class="pa-filter-param">
      <label class="pa-filter-param-label">
        Distortion
        <span class="pa-filter-param-value" id="distortionValue">20%</span>
      </label>
      <input type="range"
             class="form-range"
             id="distortionSlider"
             min="0.0"
             max="1.0"
             step="0.05"
             value="0.2">
      <span class="pa-filter-param-hint">Adds grit and character</span>
    </div>

    <!-- Reset to Preset Button -->
    <button class="pa-filter-reset" id="paFilterReset">
      <svg><!-- Reset icon --></svg>
      Reset to Heavy Preset
    </button>

  </div>

  <!-- Filter Status Indicator -->
  <div class="pa-filter-status" id="paFilterStatus">
    <svg class="pa-filter-status-icon"><!-- Filter icon --></svg>
    <span>PA Filter: <strong>Heavy</strong> (300Hz-3kHz, 4:1 compression)</span>
  </div>

</div>
```

#### CSS Styling

```css
.pa-filter-controls {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.pa-filter-preset {
  /* Standard form-select styling */
}

.pa-filter-custom {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding: 1rem;
  background-color: var(--color-bg-primary);
  border: 1px solid var(--color-border-primary);
  border-radius: 0.5rem;
}

.pa-filter-param {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.pa-filter-param-label {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--color-text-primary);
}

.pa-filter-param-value {
  font-family: var(--font-family-mono);
  font-weight: 600;
  color: var(--color-accent-orange);
}

.pa-filter-param-hint {
  font-size: 0.625rem;
  color: var(--color-text-tertiary);
  font-style: italic;
}

.pa-filter-reset {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  padding: 0.5rem 1rem;
  background-color: transparent;
  border: 1px solid var(--color-border-primary);
  border-radius: 0.375rem;
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  cursor: pointer;
  transition: all 0.15s;
}

.pa-filter-reset:hover {
  background-color: var(--color-bg-hover);
  border-color: var(--color-accent-blue);
  color: var(--color-accent-blue);
}

.pa-filter-status {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1rem;
  background-color: var(--color-filter-active-bg);
  border: 1px solid rgba(59, 130, 246, 0.3);
  border-radius: 0.375rem;
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.pa-filter-status-icon {
  width: 1rem;
  height: 1rem;
  color: var(--color-filter-active);
}

.pa-filter-status strong {
  color: var(--color-filter-active);
  font-weight: 600;
}
```

#### JavaScript Behavior

**Preset Change**:
```javascript
// When preset dropdown changes
document.getElementById('paFilterPreset').addEventListener('change', (e) => {
  const preset = e.target.value;
  const customControls = document.getElementById('paFilterCustomControls');
  const statusElement = document.getElementById('paFilterStatus');

  if (preset === 'custom') {
    customControls.style.display = 'flex';
    updateFilterStatus('Custom');
  } else {
    customControls.style.display = 'none';

    // Apply preset values
    const presets = {
      off: { highpass: 0, lowpass: 20000, compression: 1.0, distortion: 0.0 },
      light: { highpass: 200, lowpass: 5000, compression: 2.0, distortion: 0.1 },
      heavy: { highpass: 300, lowpass: 3000, compression: 4.0, distortion: 0.2 }
    };

    applyPreset(presets[preset]);
    updateFilterStatus(preset);
  }
});
```

**Slider Updates**:
```javascript
// Real-time value display updates
document.getElementById('highpassSlider').addEventListener('input', (e) => {
  document.getElementById('highpassValue').textContent = e.target.value + ' Hz';
  updateFilterStatus('Custom');
});

// Similar for other sliders...
```

**Filter Status Display**:
```javascript
function updateFilterStatus(presetName) {
  const statusElement = document.getElementById('paFilterStatus');

  if (presetName === 'off') {
    statusElement.style.display = 'none';
  } else {
    statusElement.style.display = 'flex';

    const highpass = document.getElementById('highpassSlider').value;
    const lowpass = document.getElementById('lowpassSlider').value;
    const compression = document.getElementById('compressionSlider').value;

    statusElement.querySelector('span').innerHTML =
      `PA Filter: <strong>${presetName}</strong> (${highpass}Hz-${lowpass/1000}kHz, ${compression}:1 compression)`;
  }
}
```

---

### Component: Word Gap Slider

Specialized slider with visual gap indicators.

#### Props

```csharp
public record WordGapSliderViewModel
{
    public int CurrentGapMs { get; init; } = 50;
    public int MinGapMs { get; init; } = 20;
    public int MaxGapMs { get; init; } = 200;
    public int StepMs { get; init; } = 10;
}
```

#### HTML Structure

```html
<div class="word-gap-slider">
  <label class="word-gap-label">
    Word Gap
    <span class="word-gap-value" id="wordGapValue">50 ms</span>
  </label>

  <div class="word-gap-control">
    <input type="range"
           class="form-range word-gap-range"
           id="wordGapSlider"
           min="20"
           max="200"
           step="10"
           value="50">
  </div>

  <!-- Visual indicator -->
  <div class="word-gap-indicator">
    <span class="word-gap-indicator-word">word</span>
    <span class="word-gap-indicator-gap" id="wordGapIndicatorGap" style="width: 50px;">
      <span class="word-gap-indicator-line"></span>
    </span>
    <span class="word-gap-indicator-word">word</span>
  </div>

  <div class="word-gap-presets">
    <button class="word-gap-preset-btn" data-gap="20">Fast (20ms)</button>
    <button class="word-gap-preset-btn" data-gap="50">Normal (50ms)</button>
    <button class="word-gap-preset-btn" data-gap="100">Slow (100ms)</button>
    <button class="word-gap-preset-btn" data-gap="200">Very Slow (200ms)</button>
  </div>
</div>
```

#### CSS Styling

```css
.word-gap-slider {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.word-gap-label {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--color-text-primary);
}

.word-gap-value {
  font-family: var(--font-family-mono);
  font-weight: 600;
  color: var(--color-accent-orange);
}

.word-gap-indicator {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0;
  padding: 1rem;
  background-color: var(--color-bg-tertiary);
  border-radius: 0.375rem;
}

.word-gap-indicator-word {
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--color-text-primary);
  font-family: var(--font-family-mono);
}

.word-gap-indicator-gap {
  display: flex;
  align-items: center;
  justify-content: center;
  transition: width 0.2s ease-out;
  min-width: 20px;
  max-width: 200px;
}

.word-gap-indicator-line {
  width: 100%;
  height: 2px;
  background: repeating-linear-gradient(
    to right,
    var(--color-gap-indicator) 0px,
    var(--color-gap-indicator) 4px,
    transparent 4px,
    transparent 8px
  );
}

.word-gap-presets {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(100px, 1fr));
  gap: 0.5rem;
}

.word-gap-preset-btn {
  padding: 0.375rem 0.75rem;
  background-color: transparent;
  border: 1px solid var(--color-border-primary);
  border-radius: 0.375rem;
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  cursor: pointer;
  transition: all 0.15s;
}

.word-gap-preset-btn:hover {
  background-color: var(--color-bg-hover);
  border-color: var(--color-accent-blue);
  color: var(--color-accent-blue);
}

.word-gap-preset-btn.active {
  background-color: var(--color-accent-orange);
  border-color: var(--color-accent-orange);
  color: white;
}
```

#### JavaScript Behavior

```javascript
// Slider input updates visual indicator width
document.getElementById('wordGapSlider').addEventListener('input', (e) => {
  const gapMs = parseInt(e.target.value);

  // Update value display
  document.getElementById('wordGapValue').textContent = gapMs + ' ms';

  // Update visual indicator (map 20-200ms to 20-200px)
  document.getElementById('wordGapIndicatorGap').style.width = gapMs + 'px';

  // Highlight active preset button
  updateActivePreset(gapMs);
});

// Preset button clicks
document.querySelectorAll('.word-gap-preset-btn').forEach(btn => {
  btn.addEventListener('click', (e) => {
    const gapMs = parseInt(e.target.dataset.gap);
    document.getElementById('wordGapSlider').value = gapMs;
    document.getElementById('wordGapSlider').dispatchEvent(new Event('input'));
  });
});

function updateActivePreset(gapMs) {
  document.querySelectorAll('.word-gap-preset-btn').forEach(btn => {
    if (parseInt(btn.dataset.gap) === gapMs) {
      btn.classList.add('active');
    } else {
      btn.classList.remove('active');
    }
  });
}
```

---

## Animation & Transitions

### Page Transitions

**Portal Page Load**:
- Fade in main content: `opacity 0 → 1`, `duration 300ms`
- Slide in sidebar from left: `translateX(-20px) → 0`, `duration 300ms`, `delay 100ms`
- Slide in composer from right: `translateX(20px) → 0`, `duration 300ms`, `delay 100ms`

### Component Animations

**Token Pill Appear** (when typing in Free Text mode):
```css
@keyframes token-pill-appear {
  from {
    opacity: 0;
    transform: scale(0.8);
  }
  to {
    opacity: 1;
    transform: scale(1);
  }
}

.token-pill {
  animation: token-pill-appear 0.2s ease-out;
}
```

**Word Bank Tile Click Feedback**:
```css
@keyframes tile-click {
  0% { transform: scale(1); }
  50% { transform: scale(0.95); }
  100% { transform: scale(1); }
}

.word-bank-word-tile:active {
  animation: tile-click 0.15s ease-out;
}
```

**Composition Strip Reorder** (drag-drop):
- Dragged item: `opacity: 0.5`, `cursor: grabbing`
- Drop target indicator: Blue line, `height: 2px`, `bg-accent-blue`, fade in/out
- Dropped item: Smooth slide to new position, `transition: transform 0.3s ease-out`

**Send Button Loading State**:
```css
@keyframes button-pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.7; }
}

.send-btn.loading {
  animation: button-pulse 1.5s ease-in-out infinite;
  pointer-events: none;
}
```

**PA Filter Status Badge Pulse** (when active):
```css
@keyframes filter-glow {
  0%, 100% { box-shadow: 0 0 0 0 rgba(59, 130, 246, 0); }
  50% { box-shadow: 0 0 12px 2px rgba(59, 130, 246, 0.3); }
}

.pa-filter-status {
  animation: filter-glow 2s ease-in-out infinite;
}
```

### Toast Notifications

**Slide In from Right**:
```css
@keyframes toast-slide-in {
  from {
    transform: translateX(100%);
    opacity: 0;
  }
  to {
    transform: translateX(0);
    opacity: 1;
  }
}

.portal-toast {
  animation: toast-slide-in 0.3s ease-out;
}
```

**Slide Out (dismiss)**:
```css
@keyframes toast-slide-out {
  from {
    transform: translateX(0);
    opacity: 1;
  }
  to {
    transform: translateX(100%);
    opacity: 0;
  }
}

.portal-toast.dismissing {
  animation: toast-slide-out 0.2s ease-in forwards;
}
```

### Loading States

**Loading Spinner**:

Use `LoadingSpinnerViewModel` for overlay loading and page transitions:

```csharp
@using DiscordBot.Bot.ViewModels.Components

// Page load overlay
new LoadingSpinnerViewModel {
    Variant = SpinnerVariant.Pulse,
    Size = SpinnerSize.Large,
    Message = "Loading VOX portal...",
    SubMessage = "Initializing voice connection",
    Color = SpinnerColor.Blue,
    IsOverlay = true
};

// Inline loading (during word generation)
new LoadingSpinnerViewModel {
    Variant = SpinnerVariant.Dots,
    Size = SpinnerSize.Medium,
    Message = "Generating words...",
    IsOverlay = false
};
```

Render via:
```razor
<partial name="Shared/Components/_LoadingSpinner" model="Model.LoadingSpinner" />
```

**Word Generation Progress Bar**:
```html
<div class="generation-progress">
  <div class="generation-progress-bar" style="width: 45%;"></div>
  <span class="generation-progress-text">Generating 9 of 20 words...</span>
</div>
```

```css
.generation-progress {
  position: relative;
  height: 4px;
  background-color: var(--color-bg-tertiary);
  border-radius: 2px;
  overflow: hidden;
}

.generation-progress-bar {
  height: 100%;
  background: linear-gradient(90deg,
    var(--color-accent-orange),
    var(--color-accent-orange-hover)
  );
  transition: width 0.3s ease-out;
  position: relative;
  overflow: hidden;
}

.generation-progress-bar::after {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: linear-gradient(90deg,
    transparent,
    rgba(255, 255, 255, 0.3),
    transparent
  );
  animation: progress-shimmer 1.5s ease-in-out infinite;
}

@keyframes progress-shimmer {
  0% { transform: translateX(-100%); }
  100% { transform: translateX(100%); }
}

.generation-progress-text {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--color-text-primary);
  white-space: nowrap;
}
```

### Microinteractions

**Hover Effects**:
- All interactive elements: `transition: all 0.15s ease-in-out`
- Buttons: `hover:shadow-lg`, `hover:translateY(-1px)`
- Links: `hover:text-accent-orange-hover`

**Focus Indicators** (keyboard navigation):
- All focusable elements: `focus-visible:outline`, `outline-2`, `outline-accent-blue`, `outline-offset-2`
- No outline on mouse click (use `:focus-visible` not `:focus`)

**Active States**:
- Buttons: `active:scale-98`, `active:shadow-sm`
- Pills/badges: `active:scale-95`

---

## Accessibility

### WCAG 2.1 AA Compliance

All VOX UI components meet WCAG 2.1 Level AA standards:

#### Color Contrast

| Element | Foreground | Background | Ratio | Standard |
|---------|-----------|-----------|-------|----------|
| Primary text | `#d7d3d0` | `#1d2022` | 10.8:1 | AAA |
| Secondary text | `#a8a5a3` | `#1d2022` | 5.9:1 | AA |
| Token pill (green) | `#10b981` | `#1d2022` | 4.7:1 | AA |
| Token pill (orange) | `#f59e0b` | `#1d2022` | 7.2:1 | AAA |
| Token pill (red) | `#ef4444` | `#1d2022` | 4.8:1 | AA |
| Button text | `#ffffff` | `#cb4e1b` | 5.1:1 | AA |

#### Keyboard Navigation

**Tab Order**:
1. Voice channel selector
2. Join/Leave buttons
3. Mode tabs (Free Text / Sentence Builder)
4. Message textarea or Word Bank search
5. Word Bank tiles (in Sentence Builder mode)
6. Token preview pills (clickable for audio preview)
7. Settings section controls (Voice, Word Gap, PA Filter, Speed)
8. Send button

**Keyboard Shortcuts**:
- **Tab**: Move forward through interactive elements
- **Shift + Tab**: Move backward
- **Enter / Space**: Activate buttons, toggle switches, select words
- **Arrow keys**: Navigate word bank tiles (Sentence Builder mode)
- **Escape**: Close modals, cancel drag operations
- **Delete**: Remove focused token from composition strip

**Focus Management**:
- Visible focus indicators on all interactive elements
- Focus trapping in modals
- Focus returns to trigger element after modal close
- Skip to main content link for screen readers

#### Screen Reader Support

**ARIA Labels**:
```html
<!-- Mode tabs -->
<div role="tablist" aria-label="VOX input modes">
  <button role="tab"
          aria-selected="true"
          aria-controls="freetextPanel"
          id="freetextTab">Free Text</button>
  <button role="tab"
          aria-selected="false"
          aria-controls="builderPanel"
          id="builderTab">Sentence Builder</button>
</div>

<!-- Token pills -->
<span class="token-pill"
      role="button"
      tabindex="0"
      aria-label="attention - cached, click to preview">
  attention
</span>

<!-- Word gap slider -->
<input type="range"
       id="wordGapSlider"
       aria-label="Word gap in milliseconds"
       aria-valuemin="20"
       aria-valuemax="200"
       aria-valuenow="50"
       aria-valuetext="50 milliseconds">

<!-- Send button -->
<button class="send-btn"
        aria-label="Send VOX message with 7 words, estimated 4.2 seconds">
  Send VOX Message (7 words, ~4.2s)
</button>
```

**Live Regions** (dynamic content updates):
```html
<!-- Token preview updates -->
<div aria-live="polite" aria-atomic="true" class="sr-only">
  Token preview updated: 7 words, 5 cached, 2 will be generated, estimated duration 4.2 seconds
</div>

<!-- Now Playing updates -->
<div aria-live="assertive" aria-atomic="true" class="sr-only">
  Now playing: intruder alert
</div>

<!-- Generation progress -->
<div aria-live="polite" aria-atomic="false">
  Generating word 9 of 20
</div>
```

**Status Messages**:
```html
<!-- Connection status -->
<div role="status" aria-live="polite">
  <span class="sr-only">Voice channel connection status:</span>
  Connected to General Voice
</div>

<!-- PA Filter status -->
<div role="status" aria-live="polite">
  PA Filter: Heavy preset active, 300 Hz to 3 kHz, 4:1 compression
</div>
```

#### Semantic HTML

Use proper HTML5 elements:
- `<header>`, `<nav>`, `<main>`, `<aside>`, `<footer>` for page structure
- `<button>` for interactive actions (not `<div onclick>`)
- `<label>` properly associated with form controls (via `for` attribute)
- `<select>` for dropdowns (not custom div-based dropdowns)
- `<input type="range">` for sliders (enhanced with styling)

#### Motion & Animation

**Respect `prefers-reduced-motion`**:
```css
@media (prefers-reduced-motion: reduce) {
  * {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
  }

  .token-pill {
    animation: none;
  }

  .pa-filter-status {
    animation: none;
  }
}
```

#### Form Validation

**Error Messages**:
```html
<!-- Invalid word (cannot synthesize) -->
<span class="token-pill token-pill-error"
      aria-invalid="true"
      aria-describedby="token-error-xyz">
  xyz123
</span>
<span id="token-error-xyz" class="sr-only">
  Cannot synthesize word: xyz123. Use only alphanumeric words.
</span>

<!-- Max word count exceeded -->
<div role="alert" aria-live="assertive" class="form-error">
  Message exceeds maximum word count of 50. Current: 67 words.
</div>
```

---

## API Requirements

### Portal VOX API Endpoints

#### `GET /api/portal/vox/{guildId}/status`

**Description**: Get current VOX system status for a guild.

**Response**:
```json
{
  "isConnected": true,
  "currentChannelId": "123456789012345678",
  "isPlaying": true,
  "currentMessage": "attention all personnel",
  "maxMessageWords": 50,
  "rateLimitPerMinute": 5,
  "defaultVoice": "en-US-GuyNeural",
  "defaultPaFilter": "heavy",
  "defaultWordGapMs": 50
}
```

#### `POST /api/portal/vox/{guildId}/send`

**Description**: Send a VOX announcement to the connected voice channel.

**Request Body**:
```json
{
  "mode": "freetext",
  "message": "attention all personnel security breach in sector c",
  "voice": "en-US-GuyNeural",
  "wordGapMs": 50,
  "paFilter": "heavy",
  "speed": 1.0,
  "customFilterSettings": null
}
```

Or for Sentence Builder mode:
```json
{
  "mode": "builder",
  "words": ["attention", "all", "personnel"],
  "pauses": [
    { "afterWordIndex": 1, "durationMs": 150 }
  ],
  "voice": "en-US-GuyNeural",
  "wordGapMs": 50,
  "paFilter": "custom",
  "customFilterSettings": {
    "highpassHz": 300,
    "lowpassHz": 3000,
    "compressionRatio": 4.0,
    "distortion": 0.2
  }
}
```

**Response**:
```json
{
  "success": true,
  "message": "VOX announcement queued",
  "generatedWords": ["security", "breach"],
  "cachedWords": ["attention", "all", "personnel", "in", "sector", "c"],
  "estimatedDurationSeconds": 4.2
}
```

**Error Responses**:
- `400 Bad Request`: Invalid word, exceeds max words, empty message
- `429 Too Many Requests`: Rate limit exceeded
- `403 Forbidden`: VOX disabled for guild or user lacks permissions
- `500 Internal Server Error`: TTS generation failed

#### `GET /api/portal/vox/{guildId}/token-preview`

**Description**: Parse a message and return token preview data.

**Query Parameters**:
- `message` (string, required): The message to parse
- `voice` (string, optional): Voice to check cache against

**Response**:
```json
{
  "tokens": [
    {
      "word": "attention",
      "status": "cached",
      "durationSeconds": 0.8
    },
    {
      "word": "all",
      "status": "cached",
      "durationSeconds": 0.4
    },
    {
      "word": "security",
      "status": "will_generate",
      "durationSeconds": 0.0
    },
    {
      "word": "xyz123",
      "status": "error",
      "durationSeconds": 0.0,
      "errorMessage": "Cannot synthesize: invalid characters"
    }
  ],
  "totalWords": 7,
  "cachedWords": 5,
  "willGenerate": 1,
  "errorWords": 1,
  "estimatedDurationSeconds": 4.2
}
```

#### `POST /api/portal/vox/{guildId}/stop`

**Description**: Stop current VOX playback.

**Response**:
```json
{
  "success": true,
  "message": "Playback stopped"
}
```

### Admin VOX API Endpoints

#### `GET /api/guilds/{guildId}/vox/config`

**Description**: Get VOX configuration for a guild.

**Response**:
```json
{
  "isEnabled": true,
  "defaultVoice": "en-US-GuyNeural",
  "defaultPaFilter": "heavy",
  "defaultWordGapMs": 50,
  "maxMessageWords": 50,
  "rateLimitPerMinute": 5,
  "autoGenerateMissingWords": true
}
```

#### `PUT /api/guilds/{guildId}/vox/config`

**Description**: Update VOX configuration.

**Request Body**: Same as GET response.

**Response**:
```json
{
  "success": true,
  "message": "VOX configuration updated"
}
```

#### `GET /api/guilds/{guildId}/vox/wordbank`

**Description**: Get word bank statistics and words.

**Query Parameters**:
- `page` (int, default 1): Page number
- `pageSize` (int, default 50): Items per page
- `search` (string, optional): Filter by word
- `voice` (string, optional): Filter by voice
- `sort` (string, default "dateAdded"): Sort field (word, voice, size, duration, dateAdded)
- `order` (string, default "desc"): Sort order (asc, desc)

**Response**:
```json
{
  "stats": {
    "totalWords": 1247,
    "totalSizeBytes": 25698304,
    "voicesUsed": 3
  },
  "words": [
    {
      "id": "uuid-here",
      "word": "attention",
      "voice": "en-US-GuyNeural",
      "fileSizeBytes": 18648,
      "durationSeconds": 0.8,
      "dateAdded": "2026-01-15T10:30:00Z",
      "playUrl": "/api/guilds/123/vox/wordbank/uuid-here/play"
    }
  ],
  "pagination": {
    "currentPage": 1,
    "totalPages": 25,
    "totalItems": 1247,
    "pageSize": 50
  }
}
```

#### `POST /api/guilds/{guildId}/vox/wordbank/generate`

**Description**: Bulk generate words.

**Request Body**:
```json
{
  "words": ["attention", "all", "personnel", "..."],
  "voice": "en-US-GuyNeural",
  "packId": null
}
```

Or use predefined pack:
```json
{
  "words": null,
  "voice": "en-US-GuyNeural",
  "packId": "common-500"
}
```

**Response**:
```json
{
  "success": true,
  "message": "Generated 453 words",
  "generated": 453,
  "alreadyCached": 47,
  "failed": 0,
  "estimatedSizeBytes": 9437184
}
```

#### `DELETE /api/guilds/{guildId}/vox/wordbank/{wordId}`

**Description**: Delete a single word from the bank.

**Response**:
```json
{
  "success": true,
  "message": "Word 'attention' deleted"
}
```

#### `POST /api/guilds/{guildId}/vox/wordbank/purge`

**Description**: Purge word bank cache.

**Request Body**:
```json
{
  "voice": "en-US-GuyNeural",
  "all": false
}
```

**Response**:
```json
{
  "success": true,
  "message": "Purged 387 words for voice en-US-GuyNeural",
  "deletedWords": 387,
  "freedSpaceBytes": 7864320
}
```

#### `POST /api/guilds/{guildId}/vox/wordbank/import`

**Description**: Import word bank from ZIP file.

**Request**: `multipart/form-data` with file upload.

**Response**:
```json
{
  "success": true,
  "message": "Imported 124 words",
  "imported": 124,
  "skipped": 3,
  "errors": []
}
```

#### `GET /api/guilds/{guildId}/vox/wordbank/export`

**Description**: Export word bank as ZIP file.

**Query Parameters**:
- `voice` (string, optional): Export only specific voice

**Response**: Binary ZIP file download.

---

## Implementation Checklist

### Phase 1: Portal VOX Page - Free Text Mode

- [ ] Create `/Pages/Portal/VOX/Index.cshtml` and `IndexModel.cs`
- [ ] Implement portal header with VOX tab
- [ ] Build two-column layout (sidebar + main panel)
- [ ] Reuse `_VoiceChannelPanel` component for sidebar
- [ ] Add "Now Playing" section to sidebar
- [ ] Create Free Text textarea with character counter
- [ ] Implement Token Preview Strip component
- [ ] Add API endpoint: `GET /api/portal/vox/{guildId}/token-preview`
- [ ] Build real-time token parsing and status checking
- [ ] Create Voice Preset dropdown
- [ ] Implement Word Gap Slider component
- [ ] Build PA Filter Controls component
- [ ] Add Speed slider (reuse TTS logic)
- [ ] Create Send button with progress states
- [ ] Add API endpoint: `POST /api/portal/vox/{guildId}/send`
- [ ] Implement status polling (reuse TTS pattern)
- [ ] Add toast notifications for success/error feedback
- [ ] Style for responsive breakpoints (mobile/tablet/desktop)
- [ ] Test keyboard navigation and screen reader support

### Phase 2: Portal VOX Page - Sentence Builder Mode

- [ ] Create mode tab switcher (Free Text / Sentence Builder)
- [ ] Build Word Bank Grid component
- [ ] Implement word categorization system (Common, Numbers, NATO, etc.)
- [ ] Add word search and category filtering
- [ ] Create Composition Strip component
- [ ] Implement drag-and-drop reordering
- [ ] Add pause insertion functionality
- [ ] Build "Quick Add" word input
- [ ] Sync Sentence Builder state with Send button
- [ ] Handle conversion from composition to send payload
- [ ] Test drag-drop accessibility (keyboard fallbacks)
- [ ] Hide Sentence Builder on mobile/tablet

### Phase 3: Admin VOX Configuration Page

- [ ] Create `/Pages/Guilds/VOX/Index.cshtml` and `IndexModel.cs`
- [ ] Add VOX tab to Audio section navigation
- [ ] Build Settings/Word Bank tab switcher
- [ ] Create Settings tab form
  - [ ] Enable/disable toggle
  - [ ] Default voice dropdown
  - [ ] Default PA filter dropdown
  - [ ] Default word gap slider
  - [ ] Max message word count input
  - [ ] Rate limit input
  - [ ] Auto-generate toggle
- [ ] Add API endpoint: `GET /api/guilds/{guildId}/vox/config`
- [ ] Add API endpoint: `PUT /api/guilds/{guildId}/vox/config`
- [ ] Create Word Bank tab
  - [ ] Statistics cards (total words, size, voices)
  - [ ] Bulk operations toolbar
  - [ ] Word pack generator dropdown
  - [ ] Custom word list textarea
  - [ ] Word bank table with search/sort
  - [ ] Play button for each word
  - [ ] Delete button for each word
  - [ ] Pagination controls
- [ ] Add API endpoint: `GET /api/guilds/{guildId}/vox/wordbank`
- [ ] Add API endpoint: `POST /api/guilds/{guildId}/vox/wordbank/generate`
- [ ] Add API endpoint: `DELETE /api/guilds/{guildId}/vox/wordbank/{wordId}`
- [ ] Add API endpoint: `POST /api/guilds/{guildId}/vox/wordbank/purge`
- [ ] Add API endpoint: `POST /api/guilds/{guildId}/vox/wordbank/import`
- [ ] Add API endpoint: `GET /api/guilds/{guildId}/vox/wordbank/export`
- [ ] Implement word generation progress modal
- [ ] Add confirmation dialogs for destructive actions
- [ ] Test bulk operations with large word lists

### Phase 4: Backend VOX Service

- [ ] Create `VoxService.cs` in Infrastructure layer
- [ ] Implement word tokenization logic (parse sentence → words)
- [ ] Build word bank cache system (database + file storage)
- [ ] Integrate Azure TTS for individual word generation
- [ ] Implement audio concatenation logic
- [ ] Add configurable word gap insertion
- [ ] Create PA filter audio processing pipeline
  - [ ] Highpass filter (FFmpeg)
  - [ ] Lowpass filter (FFmpeg)
  - [ ] Compression (FFmpeg)
  - [ ] Distortion effect (FFmpeg)
- [ ] Implement preset filter profiles (Off, Light, Heavy)
- [ ] Build word bank import/export functionality
- [ ] Add predefined word pack definitions
  - [ ] Common English 500
  - [ ] NATO Phonetic Alphabet
  - [ ] Numbers 0-100
  - [ ] Half-Life Classic VOX phrases
- [ ] Implement rate limiting (per-user, per-guild)
- [ ] Add word validation (alphanumeric check)
- [ ] Create audit logging for VOX usage
- [ ] Write unit tests for tokenization logic
- [ ] Write integration tests for audio processing

### Phase 5: Polish & Accessibility

- [ ] Conduct WCAG 2.1 AA audit using axe DevTools
- [ ] Fix all accessibility violations
- [ ] Test with NVDA screen reader (Windows)
- [ ] Test with JAWS screen reader (Windows)
- [ ] Test with VoiceOver (macOS)
- [ ] Verify keyboard-only navigation
- [ ] Test with reduced motion preference
- [ ] Add missing ARIA labels and live regions
- [ ] Optimize animations for performance
- [ ] Test on mobile devices (iOS Safari, Android Chrome)
- [ ] Verify responsive layout at all breakpoints
- [ ] Optimize bundle size (CSS/JS minification)
- [ ] Add loading skeletons for async operations
- [ ] Implement error boundaries for graceful failures
- [ ] Write end-to-end tests (Playwright/Cypress)

### Phase 6: Documentation & Deployment

- [ ] Write API documentation (Swagger/OpenAPI)
- [ ] Create user guide for VOX portal
- [ ] Write admin guide for word bank management
- [ ] Document PA filter presets and parameters
- [ ] Create migration guide from TTS to VOX
- [ ] Add inline help text and tooltips
- [ ] Record demo video showing all features
- [ ] Update CLAUDE.md with VOX page routes
- [ ] Add VOX to navigation menu
- [ ] Deploy to staging environment
- [ ] Conduct user acceptance testing
- [ ] Fix bugs and gather feedback
- [ ] Deploy to production
- [ ] Monitor error logs and performance metrics

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.1 | 2026-02-02 | Aligned component references with component API; replaced raw HTML/CSS with concrete ViewModel examples for Button, Badge, StatusIndicator, Card, FormInput, FormSelect, Alert, LoadingSpinner, EmptyState, Pagination, NavTabs, and SortDropdown components |
| 1.0 | 2026-02-02 | Initial specification created |

---

**Questions or Feedback?**

This specification is a living document. If you encounter any ambiguities or have suggestions for improvements, please update this document or create a GitHub issue.
