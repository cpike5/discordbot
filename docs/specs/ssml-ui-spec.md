# SSML Enhancement Interface - UI/UX Specification

**Version:** 1.0
**Created:** 2026-01-27
**Target:** TTS Portal (`/Portal/TTS/{guildId}`)
**Status:** Draft

---

## Executive Summary

This specification defines the user experience for exposing Azure Speech Service SSML (Speech Synthesis Markup Language) features in the Discord bot's TTS Portal. The design prioritizes **non-technical user accessibility** while providing powerful voice customization capabilities.

### Key Design Goals

1. **Zero XML Knowledge Required** - Users never see or write markup
2. **Progressive Disclosure** - Basic features upfront, advanced features accessible
3. **Intuitive Visual Language** - "Speech Director" metaphor, not "Markup Editor"
4. **Mobile-First Responsive** - Works on phones and tablets
5. **Consistent with Design System** - Uses existing Tailwind tokens and components

---

## Table of Contents

1. [User Research & Requirements](#1-user-research--requirements)
2. [Information Architecture](#2-information-architecture)
3. [Component Specifications](#3-component-specifications)
4. [Layout & Wireframes](#4-layout--wireframes)
5. [Interaction Patterns](#5-interaction-patterns)
6. [Mobile Considerations](#6-mobile-considerations)
7. [Accessibility](#7-accessibility)
8. [Error States & Feedback](#8-error-states--feedback)
9. [Implementation Roadmap](#9-implementation-roadmap)

---

## 1. User Research & Requirements

### Target Users

| Persona | Technical Level | Primary Use Case | Key Needs |
|---------|----------------|------------------|-----------|
| **DJ Dan** | Low | Stream announcements, hype building | Quick presets, emotional voice |
| **Admin Alice** | Medium | Server announcements, moderation | Professional tone, emphasis control |
| **Streamer Sam** | Low-Medium | Interactive TTS, comedy bits | Character voices, dramatic pauses |
| **Power User Paul** | High | Complex audio productions | Fine-grained control, style mixing |

### User Stories

```
As a DJ Dan (novice):
- I want to make the bot sound excited when announcing a song
- So that my stream feels more energetic
- WITHOUT learning XML or technical syntax

As an Admin Alice (intermediate):
- I want to emphasize important words in announcements
- So that members pay attention to key information
- USING simple point-and-click selection

As a Streamer Sam (intermediate):
- I want to switch between different character voices
- So that I can create entertaining content
- WITH presets I can quickly apply

As a Power User Paul (advanced):
- I want to fine-tune speaking style intensity
- So that I can create professional audio content
- WHILE seeing real-time preview of settings
```

### SSML Feature Priority Matrix

| Feature | User Value | Implementation Complexity | Priority |
|---------|-----------|---------------------------|----------|
| **Speaking Styles** | HIGH | Medium | P0 (MVP) |
| **Presets** | HIGH | Low | P0 (MVP) |
| **Emphasis** | MEDIUM | Medium | P1 |
| **Breaks/Pauses** | MEDIUM | Low | P1 |
| **Say-as (Dates/Times)** | LOW | High | P2 |
| **Prosody (Fine-tune)** | LOW | Low | P2 |

---

## 2. Information Architecture

### UI Mode System

The interface operates in **three progressive disclosure levels**:

```
┌─────────────────────────────────────────────────┐
│  SIMPLE MODE (Default)                          │
│  - Voice dropdown                               │
│  - Message textarea                             │
│  - Speed/Pitch sliders                          │
│  - [Expand to Advanced] button                  │
└─────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────┐
│  STANDARD MODE (Most users)                     │
│  - All Simple features                          │
│  - Speaking Style selector                      │
│  - Style Intensity slider                       │
│  - Quick Presets buttons                        │
│  - [Expand to Pro] button                       │
└─────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────┐
│  PRO MODE (Power users)                         │
│  - All Standard features                        │
│  - Text selection emphasis tools                │
│  - Break/pause insertion                        │
│  - Say-as helpers (dates, numbers)              │
│  - SSML preview (read-only)                     │
└─────────────────────────────────────────────────┘
```

### Navigation Structure

```
TTS Portal
├── Voice Channel (Sidebar)
│   ├── Connection Status
│   ├── Channel Selector
│   └── Join/Leave Buttons
│
├── Now Playing (Sidebar)
│   └── Current Message Display
│
└── TTS Form (Main Panel)
    ├── [NEW] Mode Toggle (Simple/Standard/Pro)
    ├── Message Input
    ├── [NEW] Quick Presets Bar
    ├── Voice Settings
    │   ├── Voice Dropdown
    │   ├── [NEW] Speaking Style (Standard+)
    │   └── [NEW] Style Intensity (Standard+)
    ├── Voice Tuning
    │   ├── Speed Slider
    │   └── Pitch Slider
    ├── [NEW] Advanced Tools (Pro)
    │   ├── Text Emphasis Toolbar
    │   ├── Pause Insertion
    │   └── Say-as Helpers
    └── Send Button
```

---

## 3. Component Specifications

### 3.1 Mode Toggle Component

**Component Name:** `ModeSwitcher`

**Location:** Top of TTS form panel

**Visual Design:**
```
┌──────────────────────────────────────────────┐
│ [Simple]  [Standard*]  [Pro]                │
└──────────────────────────────────────────────┘
```

**Implementation:**
- Segmented control (pill-style buttons)
- Active state: Orange accent (`--color-accent-orange`)
- Inactive state: Secondary background (`--color-bg-tertiary`)
- Persists selection in localStorage: `tts_mode_preference`

**States:**
- **Simple**: Icon only (microphone icon)
- **Standard**: Icon + "Standard" label (default)
- **Pro**: Icon + "Pro" label (bolt icon)

**Behavior:**
- Clicking switches mode instantly
- Expanding components slide in with 200ms transition
- Collapsing components slide out with fade

---

### 3.2 Quick Presets Component

**Component Name:** `PresetBar`

**Location:** Below mode toggle, above message input

**Visual Design:**
```
┌──────────────────────────────────────────────────────────┐
│ Quick Presets:                                           │
│ [Excited] [Announcer] [Robot] [Friendly]                 │
│ [Angry]   [Narrator]  [Whisper] [+ More...]              │
└──────────────────────────────────────────────────────────┘
```
*Each preset button displays a Heroicon representing the style.*

**Preset Structure:**
```typescript
interface VoicePreset {
  id: string;
  name: string;
  icon: string;             // Heroicon name (e.g., "sparkles", "megaphone")
  description: string;
  voiceName: string;        // e.g., "en-US-JennyNeural"
  style?: string;           // e.g., "cheerful", "angry"
  styleIntensity?: number;  // 1-2 (0.5-2.0 scale)
  speed: number;            // 0.5-2.0
  pitch: number;            // 0.5-2.0
}
```

**Default Presets:**

| Icon | Name | Voice | Style | Speed | Pitch | Use Case |
|------|------|-------|-------|-------|-------|----------|
| sparkles | Excited | Jenny | cheerful | 1.2x | 1.1x | Announcements, celebrations |
| megaphone | Announcer | Guy | newscast | 1.0x | 0.9x | Professional announcements |
| computer-desktop | Robot | Aria | — | 1.0x | 0.7x | Robotic effects |
| face-smile | Friendly | Jenny | friendly | 1.0x | 1.0x | General conversation |
| fire | Angry | Guy | angry | 1.1x | 1.2x | Comedic anger |
| microphone | Narrator | Davis | narration-professional | 0.9x | 1.0x | Story narration |
| speaker-x-mark | Whisper | Jenny | whispering | 0.8x | 0.95x | Secretive tone |
| speaker-wave | Shouting | Guy | shouting | 1.15x | 1.3x | Emphasis, urgency |

**Interaction:**
- Clicking preset applies all settings instantly
- Visual feedback: 200ms highlight pulse
- Applied preset shows subtle border indicator
- Toast notification: "Applied [Preset Name]"

**Component Library Addition:**
```csharp
// ViewModels/Components/PresetButtonViewModel.cs
public record PresetButtonViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;  // Heroicon name
    public string Description { get; init; } = string.Empty;
    public bool IsActive { get; init; } = false;
    public string OnClick { get; init; } = string.Empty;
}
```

**Styling:**
```css
.preset-button {
    display: inline-flex;
    align-items: center;
    gap: 0.5rem;
    padding: 0.5rem 0.75rem;
    background-color: var(--color-bg-tertiary);
    border: 1px solid var(--color-border-primary);
    border-radius: 0.5rem;
    font-size: 0.875rem;
    color: var(--color-text-primary);
    cursor: pointer;
    transition: all 0.2s;
}

.preset-button:hover {
    background-color: var(--color-bg-hover);
    border-color: var(--color-accent-orange);
}

.preset-button.active {
    background-color: var(--color-accent-orange-muted);
    border-color: var(--color-accent-orange);
    color: var(--color-accent-orange);
}

.preset-button .preset-icon {
    width: 1.125rem;
    height: 1.125rem;
    color: var(--color-text-secondary);
}

.preset-button:hover .preset-icon,
.preset-button.active .preset-icon {
    color: var(--color-accent-orange);
}
```

---

### 3.3 Speaking Style Selector

**Component Name:** `StyleSelector`

**Location:** TTS form, between voice dropdown and speed/pitch sliders

**Visibility:** Standard and Pro modes only

**Visual Design:**
```
┌──────────────────────────────────────────────┐
│ Speaking Style                                │
│ [Dropdown with style options ▼]              │
│                                               │
│ Style Intensity                      Moderate │
│ ━━━━━━━●━━━━━━━━━━━━━━━━━              │
│ Subtle                            Intense     │
└──────────────────────────────────────────────┘
```

**Available Styles** (Azure Neural Voices):

| Style | Description | Voice Compatibility | Icon (Heroicon) |
|-------|-------------|---------------------|-----------------|
| (None) | Natural speech | All voices | — |
| cheerful | Happy, energetic | Jenny, Aria, Guy | face-smile |
| excited | Very enthusiastic | Guy, Aria | sparkles |
| friendly | Warm, approachable | Jenny, Aria | hand-raised |
| sad | Sorrowful, downcast | Jenny, Aria | face-frown |
| angry | Upset, frustrated | Jenny, Guy | fire |
| fearful | Scared, anxious | Jenny | exclamation-triangle |
| whispering | Quiet, intimate | Jenny | speaker-x-mark |
| shouting | Loud, urgent | Guy | speaker-wave |
| newscast | Professional reporter | Guy | newspaper |
| narration-professional | Storytelling | Davis | microphone |
| customerservice | Helpful, patient | Aria | briefcase |

**Style Intensity Slider:**
- Range: Subtle (0.5) to Intense (2.0)
- Default: Moderate (1.0)
- Step: 0.1
- Visual indicator: Current value displayed on right
- Maps to SSML `styledegree` attribute

**Behavior:**
- Style dropdown auto-filters based on selected voice
- If selected style not available for voice, auto-reset to "(None)"
- Intensity slider disabled when style is "(None)"
- Tooltip on hover shows example: "Try saying: 'I'm so excited!'"

**Component Library Addition:**
```csharp
// ViewModels/Components/StyleSelectorViewModel.cs
public record StyleSelectorViewModel
{
    public string SelectedVoice { get; init; } = string.Empty;
    public string SelectedStyle { get; init; } = string.Empty;
    public decimal StyleIntensity { get; init; } = 1.0m;
    public List<StyleOption> AvailableStyles { get; init; } = new();
}

public record StyleOption
{
    public string Value { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;  // Heroicon name
    public string Description { get; init; } = string.Empty;
    public string Example { get; init; } = string.Empty;
}
```

---

### 3.4 Text Selection Toolbar (Pro Mode)

**Component Name:** `EmphasisToolbar`

**Location:** Floating toolbar above message textarea

**Visibility:** Pro mode only, appears when text is selected

**Visual Design:**
```
┌──────────────────────────────────────────────┐
│ [B] [I] [Strong] [Moderate] [Pause]          │
└──────────────────────────────────────────────┘
       ↑ Appears floating above selected text
```

**Tools:**

| Icon | Label | SSML Effect | Visual Feedback |
|------|-------|-------------|-----------------|
| **B** | Bold | `<emphasis level="strong">` | Text highlighted orange |
| bolt | Emphasize | `<emphasis level="moderate">` | Text highlighted blue |
| pause | Add Pause | `<break time="500ms"/>` after selection | Gray pause indicator |
| hashtag | Say as Number | `<say-as interpret-as="cardinal">` | Underline green |
| calendar | Say as Date | `<say-as interpret-as="date">` | Underline cyan |
| x-mark | Clear | Remove all formatting | Remove highlights |

**Interaction Flow:**
1. User selects text in message textarea
2. Toolbar appears floating 10px above selection (centered)
3. User clicks formatting button
4. Text visually updates with color indicator
5. Toolbar remains visible for additional formatting
6. Click outside or deselect text to hide toolbar

**Visual Indicators in Textarea:**
- **Strong emphasis**: Orange underline + light orange background
- **Moderate emphasis**: Blue underline + light blue background
- **Pauses**: Gray `[pause icon 500ms]` inline marker (non-editable)
- **Say-as markers**: Colored dashed underline

**Styling:**
```css
.emphasis-toolbar {
    position: absolute;
    display: flex;
    gap: 0.5rem;
    padding: 0.5rem;
    background-color: var(--color-bg-secondary);
    border: 1px solid var(--color-border-primary);
    border-radius: 0.5rem;
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.5);
    z-index: 100;
}

.emphasis-toolbar button {
    padding: 0.375rem 0.625rem;
    background-color: var(--color-bg-tertiary);
    border: 1px solid var(--color-border-primary);
    border-radius: 0.375rem;
    font-size: 0.875rem;
    color: var(--color-text-primary);
    cursor: pointer;
    transition: all 0.2s;
}

.emphasis-toolbar button:hover {
    background-color: var(--color-bg-hover);
    border-color: var(--color-accent-orange);
}

/* Text formatting indicators */
.tts-message-formatted {
    position: relative;
}

.emphasis-strong {
    background-color: rgba(203, 78, 27, 0.15);
    border-bottom: 2px solid var(--color-accent-orange);
}

.emphasis-moderate {
    background-color: rgba(9, 142, 207, 0.15);
    border-bottom: 2px solid var(--color-accent-blue);
}

.break-marker {
    display: inline-block;
    padding: 0.125rem 0.375rem;
    margin: 0 0.25rem;
    background-color: var(--color-bg-tertiary);
    border: 1px solid var(--color-border-primary);
    border-radius: 0.25rem;
    font-size: 0.75rem;
    color: var(--color-text-secondary);
    user-select: none;
}
```

---

### 3.5 SSML Preview Panel (Pro Mode)

**Component Name:** `SsmlPreview`

**Location:** Collapsible panel below send button

**Visibility:** Pro mode only

**Visual Design:**
```
┌──────────────────────────────────────────────┐
│ [▼] SSML Preview (Read-only)                 │
├──────────────────────────────────────────────┤
│ <speak version="1.0" ...>                    │
│   <voice name="en-US-JennyNeural">           │
│     <prosody rate="1.2" pitch="1.1">         │
│       <mstts:express-as style="cheerful"     │
│                         styledegree="1.5">   │
│         Hello there!                         │
│       </mstts:express-as>                    │
│     </prosody>                               │
│   </voice>                                    │
│ </speak>                                      │
│                                               │
│ [Copy SSML]                                  │
└──────────────────────────────────────────────┘
```

**Features:**
- Syntax-highlighted XML (read-only code block)
- Auto-updates as user makes changes
- Copy button with toast confirmation
- Collapsible (starts collapsed by default)
- Character count showing SSML length vs plain text length

**Behavior:**
- Expands/collapses with smooth animation
- Syntax highlighting colors:
  - Tags: `--color-accent-blue`
  - Attributes: `--color-accent-orange`
  - Values: `--color-success`
  - Text content: `--color-text-primary`

---

### 3.6 Pause Insertion Modal

**Component Name:** `PauseInsertionModal`

**Trigger:** Click "Add Pause" in emphasis toolbar or dedicated button

**Visual Design:**
```
┌──────────────────────────────────────────────┐
│ Insert Pause                            [×]   │
├──────────────────────────────────────────────┤
│ Duration                                      │
│ ━━━━━━━●━━━━━━━━━━━━━━━━━              │
│ 100ms               1000ms          3000ms    │
│                                               │
│ Quick Durations:                              │
│ [Short (250ms)] [Medium (500ms)] [Long (1s)] │
│                                               │
│ Preview: "Hello [500ms] World"               │
│                                               │
│           [Cancel]        [Insert Pause]      │
└──────────────────────────────────────────────┘
```

**Features:**
- Slider for precise duration (100ms - 3000ms)
- Quick preset buttons for common durations
- Live preview of message with pause indicator
- Insert at cursor position or after selected text

---

## 4. Layout & Wireframes

### 4.1 Desktop Layout (1024px+)

```
┌─────────────────────────────────────────────────────────────────┐
│ Portal Header                                                    │
│ [Bot Icon] TTS Portal         [@GuildName]         [Connected]  │
└─────────────────────────────────────────────────────────────────┘
┌─────────────┬───────────────────────────────────────────────────┐
│  SIDEBAR    │  TTS FORM PANEL                                   │
│  (300px)    │  (Flex)                                           │
│             │                                                    │
│ Voice       │ ┌───────────────────────────────────────────────┐ │
│ Channel     │ │ [Simple] [Standard*] [Pro]                    │ │
│ ────────    │ └───────────────────────────────────────────────┘ │
│ Connected   │                                                    │
│             │ ┌───────────────────────────────────────────────┐ │
│ [Channel ▼] │ │ Quick Presets:                                │ │
│             │ │ [Excited] [Announcer] [Robot] ...   │ │
│ [Join]      │ └───────────────────────────────────────────────┘ │
│ [Leave]     │                                                    │
│             │ Message ─────────────────────────────────────────  │
│ ────────    │ ┌───────────────────────────────────────────────┐ │
│ Now Playing │ │                                               │ │
│             │ │ Enter message here...                         │ │
│ [Playing]   │ │                                               │ │
│ Message...  │ │                                               │ │
│ [Stop]      │ └───────────────────────────────────────────────┘ │
│             │ 0 / 500                                            │
│             │                                                    │
│             │ Voice ────────────────────────────────────────────  │
│             │ [en-US-JennyNeural ▼]                             │
│             │                                                    │
│             │ Speaking Style ───────────────────────────────────  │
│             │ [Cheerful ▼]                                       │
│             │ Intensity          Moderate                        │
│             │ ━━━━━━━●━━━━━━━━━━━━━━━━━                   │
│             │                                                    │
│             │ Speed      1.0x    Pitch      1.0x                 │
│             │ ━━━●━━━━━━━━━     ━━━●━━━━━━━━━            │
│             │                                                    │
│             │ [Send Message]                                     │
└─────────────┴───────────────────────────────────────────────────┘
```

### 4.2 Mobile Layout (< 768px)

```
┌─────────────────────────────────┐
│ Portal Header                    │
│ [☰] TTS Portal    [Connected]    │
└─────────────────────────────────┘
│ Voice Channel ────────────────── │
│ Connected  [Channel ▼]           │
│ [Join] [Leave]                   │
│                                  │
│ Now Playing ──────────────────── │
│ [Playing] Message... [Stop]      │
│                                  │
│ Mode ─────────────────────────── │
│ [Simple*] [Standard] [Pro]       │
│                                  │
│ Quick Presets ────────────────── │
│ [Excited] [Announcer] [Robot]   │
│ [Friendly] [Angry] [Whisper]    │
│                                  │
│ Message ──────────────────────── │
│ ┌──────────────────────────────┐ │
│ │                              │ │
│ │ Enter message...             │ │
│ │                              │ │
│ └──────────────────────────────┘ │
│ 0 / 500                          │
│                                  │
│ Voice ────────────────────────── │
│ [en-US-JennyNeural ▼]            │
│                                  │
│ [▼ Advanced Settings]            │
│                                  │
│ [Send Message]                   │
└─────────────────────────────────┘
```

---

## 5. Interaction Patterns

### 5.1 Text Emphasis Workflow

**User Goal:** Emphasize "urgent" in "This is an urgent announcement"

**Steps:**
1. User switches to Pro mode
2. Types or pastes message
3. Selects word "urgent"
4. Emphasis toolbar appears floating above selection
5. User clicks "Strong" button
6. Word "urgent" gains orange underline + background
7. User continues editing or sends message

**Technical Flow:**
```javascript
// Client-side state management
const formattedText = {
  plain: "This is an urgent announcement",
  markers: [
    { start: 11, end: 17, type: "emphasis", level: "strong" }
  ]
};

// SSML generation on send
function generateSSML(formattedText, voice, style, speed, pitch) {
  let ssml = `<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis"
    xmlns:mstts="http://www.w3.org/2001/mstts" xml:lang="en-US">
    <voice name="${voice}">
      <prosody rate="${speed}" pitch="${pitch}">`;

  if (style) {
    ssml += `<mstts:express-as style="${style}" styledegree="${intensity}">`;
  }

  // Insert emphasis markers
  let text = formattedText.plain;
  formattedText.markers.forEach(marker => {
    const before = text.substring(0, marker.start);
    const content = text.substring(marker.start, marker.end);
    const after = text.substring(marker.end);
    text = `${before}<emphasis level="${marker.level}">${content}</emphasis>${after}`;
  });

  ssml += text;

  if (style) {
    ssml += `</mstts:express-as>`;
  }

  ssml += `</prosody></voice></speak>`;

  return ssml;
}
```

---

### 5.2 Preset Application Flow

**User Goal:** Quickly apply "Excited" preset

**Steps:**
1. User clicks "Excited" preset button
2. Button highlights with pulse animation
3. All settings update instantly:
   - Voice: en-US-JennyNeural
   - Style: cheerful
   - Intensity: 1.5
   - Speed: 1.2x
   - Pitch: 1.1x
4. Toast appears: "Applied Excited preset"
5. User types message and sends

**State Management:**
```javascript
const presets = {
  excited: {
    name: "Excited",
    icon: "sparkles",
    voice: "en-US-JennyNeural",
    style: "cheerful",
    intensity: 1.5,
    speed: 1.2,
    pitch: 1.1
  }
  // ... other presets
};

function applyPreset(presetId) {
  const preset = presets[presetId];

  // Update UI controls
  document.getElementById('voiceSelect').value = preset.voice;
  document.getElementById('styleSelect').value = preset.style;
  document.getElementById('intensitySlider').value = preset.intensity;
  document.getElementById('speedSlider').value = preset.speed;
  document.getElementById('pitchSlider').value = preset.pitch;

  // Trigger change events
  updateAllControls();

  // Visual feedback
  highlightPresetButton(presetId);
  showToast('success', `Applied ${preset.name} preset`);

  // Save to localStorage
  localStorage.setItem('tts_last_preset', presetId);
}
```

---

### 5.3 Mode Switching Behavior

**Standard → Pro Transition:**
```
┌─────────────────────────────────┐
│ [Simple] [Standard*] [Pro]      │  ← User clicks Pro
├─────────────────────────────────┤
│ Quick Presets: [visible]        │
│ Message: [visible]               │
│ Voice: [visible]                 │
│ Style: [visible]                 │
│ Speed/Pitch: [visible]           │
│                                  │  ← Slide-in animation (200ms)
│ [NEW] Text Emphasis Toolbar      │  ← Appears
│ [NEW] SSML Preview Panel         │  ← Appears (collapsed)
└─────────────────────────────────┘
```

**Pro → Simple Transition:**
- Fade out Pro-only features (150ms)
- Collapse Standard features (200ms)
- Warning modal if text has formatting: "Switching to Simple mode will remove text emphasis. Continue?"

---

## 6. Mobile Considerations

### 6.1 Touch-Optimized Controls

| Control | Desktop | Mobile | Notes |
|---------|---------|--------|-------|
| **Mode Toggle** | 3 buttons inline | 3 buttons stacked | Min 44px touch target |
| **Preset Buttons** | 2 rows, 4 per row | Horizontal scroll | Snap-scroll enabled |
| **Style Slider** | Standard slider | Larger thumb (24px) | Increased touch area |
| **Text Selection** | Precision cursor | Native selection handles | Use system UI |
| **Emphasis Toolbar** | Floating above text | Bottom sheet modal | Easier thumb reach |

### 6.2 Mobile-Specific UI Patterns

**Preset Carousel:**
```css
.preset-carousel-mobile {
    display: flex;
    overflow-x: auto;
    scroll-snap-type: x mandatory;
    -webkit-overflow-scrolling: touch;
    padding: 0.5rem;
    gap: 0.5rem;
}

.preset-button-mobile {
    scroll-snap-align: start;
    min-width: 120px;
    flex-shrink: 0;
}
```

**Collapsible Sections:**
- Voice Settings: Collapsed by default on mobile
- Advanced Settings: Accordion-style expansion
- SSML Preview: Hidden by default (Show button if needed)

---

## 7. Accessibility

### 7.1 Keyboard Navigation

| Key | Action | Context |
|-----|--------|---------|
| **Tab** | Navigate between controls | All modes |
| **Space/Enter** | Activate button/toggle | Buttons, presets |
| **Arrow Keys** | Adjust slider values | Sliders (style, speed, pitch) |
| **Escape** | Close modal/toolbar | Emphasis toolbar, modals |
| **Ctrl+B** | Apply strong emphasis | Pro mode, text selected |
| **Ctrl+E** | Apply moderate emphasis | Pro mode, text selected |

### 7.2 Screen Reader Support

**ARIA Attributes:**
```html
<!-- Mode Toggle -->
<div role="tablist" aria-label="TTS Mode Selection">
  <button role="tab" aria-selected="true" aria-controls="standard-panel">
    Standard
  </button>
</div>

<!-- Preset Buttons -->
<button aria-label="Apply Excited preset: cheerful voice, faster speed">
  <svg class="preset-icon"><!-- sparkles icon --></svg>
  Excited
</button>

<!-- Style Intensity Slider -->
<input type="range"
       role="slider"
       aria-label="Speaking style intensity"
       aria-valuemin="0.5"
       aria-valuemax="2"
       aria-valuenow="1.0"
       aria-valuetext="Moderate intensity" />

<!-- Emphasis Toolbar -->
<div role="toolbar" aria-label="Text emphasis tools">
  <button aria-label="Apply strong emphasis to selected text">
    Strong
  </button>
</div>
```

**Live Regions:**
```html
<!-- Status announcements -->
<div aria-live="polite" aria-atomic="true" class="sr-only">
  <span id="status-message">Applied Excited preset</span>
</div>

<!-- Error messages -->
<div aria-live="assertive" aria-atomic="true" class="sr-only">
  <span id="error-message">Message exceeds maximum length</span>
</div>
```

### 7.3 Focus Management

- **Preset Click:** Focus remains on button
- **Modal Open:** Focus moves to first interactive element
- **Modal Close:** Focus returns to trigger element
- **Text Selection:** Focus indicator on toolbar buttons
- **Error State:** Focus moves to first invalid field

---

## 8. Error States & Feedback

### 8.1 Validation Rules

| Field | Validation | Error Message | Prevention |
|-------|-----------|---------------|------------|
| **Message** | Max 500 chars | "Message exceeds 500 characters" | Disabled send button |
| **Voice** | Must be selected | "Please select a voice" | Disable send if empty |
| **Style** | Must match voice | Auto-reset to "(None)" | Dropdown filtering |
| **Speed** | 0.5 - 2.0 | N/A | Slider constraints |
| **Pitch** | 0.5 - 2.0 | N/A | Slider constraints |

### 8.2 Error Display Patterns

**Inline Field Errors:**
```html
<div class="form-group error">
  <label class="form-label">Message</label>
  <textarea class="form-textarea error-state"></textarea>
  <div class="error-message">
    <svg class="error-icon"><!-- exclamation-triangle icon --></svg>
    Message exceeds 500 characters
  </div>
</div>
```

**Toast Notifications:**
```javascript
showToast('error', 'Failed to send message: Bot not connected');
showToast('warning', 'Style "cheerful" not available for this voice. Reset to default.');
showToast('success', 'Message sent successfully!');
showToast('info', 'SSML copied to clipboard');
```

### 8.3 Loading States

**Send Button Loading:**
```html
<button class="send-btn loading" disabled>
  <svg class="spinner"></svg>
  Sending...
</button>
```

**Preset Application:**
```html
<button class="preset-button applying">
  <svg class="preset-icon"><!-- sparkles icon --></svg>
  Excited
  <svg class="spinner-small"></svg>
</button>
```

### 8.4 Empty States

**No Voices Available:**
```
┌──────────────────────────────────┐
│ [microphone icon]                 │
│ No Voices Available               │
│                                   │
│ Voice synthesis is currently      │
│ unavailable. Please try again     │
│ later or contact support.         │
│                                   │
│ [Retry]                           │
└──────────────────────────────────┘
```

**No Presets Configured:**
```
┌──────────────────────────────────┐
│ Quick Presets:                    │
│ [+ Create Custom Preset]          │
└──────────────────────────────────┘
```

---

## 9. Implementation Roadmap

### Phase 1: Foundation (Sprint 1-2)

**Goal:** Basic SSML support with presets

**Deliverables:**
- [ ] Mode toggle component (Simple/Standard)
- [ ] Quick presets bar with 8 default presets
- [ ] Speaking style dropdown
- [ ] Style intensity slider
- [ ] Backend SSML generation logic
- [ ] Preset persistence in localStorage

**Component Library Additions:**
- `ModeSwitcher.cshtml` / `ModeSwitcherViewModel.cs`
- `PresetBar.cshtml` / `PresetBarViewModel.cs`
- `StyleSelector.cshtml` / `StyleSelectorViewModel.cs`

**API Changes:**
```csharp
// Updated TTS request DTO
public class TtsRequest
{
    public string Message { get; set; }
    public string Voice { get; set; }
    public decimal Speed { get; set; } = 1.0m;
    public decimal Pitch { get; set; } = 1.0m;

    // NEW SSML properties
    public string? Style { get; set; }
    public decimal? StyleIntensity { get; set; }
    public string? PresetId { get; set; }
}
```

**Testing Focus:**
- Unit tests for SSML generation
- Voice/style compatibility validation
- Preset application flow
- Mobile responsive layout

---

### Phase 2: Text Emphasis (Sprint 3-4)

**Goal:** Pro mode with text formatting

**Deliverables:**
- [ ] Pro mode toggle
- [ ] Text selection toolbar
- [ ] Emphasis markers (strong, moderate)
- [ ] Visual formatting indicators
- [ ] SSML preview panel
- [ ] Copy SSML functionality

**Component Library Additions:**
- `EmphasisToolbar.cshtml` / `EmphasisToolbarViewModel.cs`
- `SsmlPreview.cshtml` / `SsmlPreviewViewModel.cs`

**Technical Challenges:**
- Maintaining cursor position during text formatting
- Cross-browser text selection handling
- Mobile text selection UI
- State synchronization between plain text and SSML

**API Changes:**
```csharp
public class TtsRequest
{
    // ... existing properties ...

    // NEW formatting
    public List<TextMarker>? Markers { get; set; }
}

public class TextMarker
{
    public int Start { get; set; }
    public int End { get; set; }
    public string Type { get; set; } // "emphasis", "break", "say-as"
    public Dictionary<string, string> Attributes { get; set; }
}
```

---

### Phase 3: Advanced Features (Sprint 5-6)

**Goal:** Pauses and say-as helpers

**Deliverables:**
- [ ] Pause insertion modal
- [ ] Break markers in text
- [ ] Say-as helpers (dates, numbers)
- [ ] Custom preset creation UI
- [ ] Preset management (save, delete, share)

**Component Library Additions:**
- `PauseModal.cshtml` / `PauseModalViewModel.cs`
- `SayAsHelper.cshtml` / `SayAsHelperViewModel.cs`
- `PresetManager.cshtml` / `PresetManagerViewModel.cs`

**Database Schema:**
```sql
CREATE TABLE TtsPresets (
    Id BIGINT PRIMARY KEY,
    UserId BIGINT NOT NULL,
    GuildId BIGINT,
    Name NVARCHAR(100) NOT NULL,
    Emoji NVARCHAR(10),
    VoiceName NVARCHAR(100) NOT NULL,
    Style NVARCHAR(50),
    StyleIntensity DECIMAL(3,1),
    Speed DECIMAL(3,1) NOT NULL,
    Pitch DECIMAL(3,1) NOT NULL,
    IsPublic BIT DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2 NOT NULL,
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);
```

---

### Phase 4: Polish & Optimization (Sprint 7)

**Goal:** Production-ready polish

**Deliverables:**
- [ ] Performance optimization (lazy loading)
- [ ] Accessibility audit & fixes
- [ ] Mobile UX refinement
- [ ] Documentation & tooltips
- [ ] Analytics tracking
- [ ] A/B testing setup (Simple vs Standard default)

**Metrics to Track:**
- Mode usage distribution (Simple/Standard/Pro)
- Preset usage frequency
- Feature adoption rates
- Error rates by feature
- Average message complexity (SSML depth)

---

## Appendix A: Design Tokens Reference

### Color Classes (Tailwind)

```css
/* Backgrounds */
.bg-bg-primary         /* #1d2022 */
.bg-bg-secondary       /* #262a2d */
.bg-bg-tertiary        /* #2f3336 */
.bg-bg-hover           /* #363a3e */

/* Text */
.text-text-primary     /* #d7d3d0 */
.text-text-secondary   /* #a8a5a3 */
.text-text-tertiary    /* #7a7876 */

/* Accents */
.text-accent-orange    /* #cb4e1b */
.bg-accent-orange      /* #cb4e1b */
.border-accent-orange  /* #cb4e1b */

.text-accent-blue      /* #098ecf */
.bg-accent-blue        /* #098ecf */
.border-accent-blue    /* #098ecf */

/* Semantic */
.text-success          /* #10b981 */
.text-warning          /* #f59e0b */
.text-error            /* #ef4444 */
.text-info             /* #06b6d4 */

/* Borders */
.border-border-primary   /* #3f4447 */
.border-border-secondary /* #2f3336 */
```

### Spacing Scale

| Class | Value | Use Case |
|-------|-------|----------|
| `gap-1` | 0.25rem (4px) | Tight inline elements |
| `gap-2` | 0.5rem (8px) | Button groups, badges |
| `gap-3` | 0.75rem (12px) | Form field spacing |
| `gap-4` | 1rem (16px) | Section spacing |
| `gap-6` | 1.5rem (24px) | Component separation |

### Typography

| Class | Size | Weight | Line Height | Use Case |
|-------|------|--------|-------------|----------|
| `text-xs` | 0.75rem | 400 | 1.5 | Captions, labels |
| `text-sm` | 0.875rem | 400 | 1.5 | Body text, form inputs |
| `text-base` | 1rem | 400 | 1.5 | Default body |
| `text-lg` | 1.125rem | 600 | 1.5 | Subheadings |
| `text-xl` | 1.25rem | 600 | 1.5 | Headings |

---

## Appendix B: Azure Voice Style Compatibility

### Neural Voice Style Matrix

| Voice Name | Locale | Styles | Notes |
|------------|--------|--------|-------|
| **en-US-JennyNeural** | en-US | assistant, chat, customerservice, newscast, angry, cheerful, excited, friendly, hopeful, sad, shouting, terrified, unfriendly, whispering | Most versatile |
| **en-US-GuyNeural** | en-US | newscast, angry, cheerful, excited, friendly, hopeful, sad, shouting, terrified, unfriendly, whispering | Male equivalent to Jenny |
| **en-US-AriaNeural** | en-US | chat, customerservice, cheerful, empathetic, angry, sad, excited, friendly | Customer service optimized |
| **en-US-DavisNeural** | en-US | chat, angry, cheerful, excited, friendly, hopeful, sad, shouting, terrified, unfriendly, whispering | Professional male |
| **en-US-JaneNeural** | en-US | angry, cheerful, excited, friendly, hopeful, sad, shouting, terrified, unfriendly, whispering | Alternative female |
| **en-US-JasonNeural** | en-US | angry, cheerful, excited, friendly, hopeful, sad, shouting, terrified, unfriendly, whispering | Alternative male |

### Style Descriptions

| Style | Effect | Best For | Avoid With |
|-------|--------|----------|------------|
| **cheerful** | Happy, upbeat tone | Celebrations, positive news | Serious announcements |
| **excited** | Very enthusiastic | Hype moments, victories | Calm contexts |
| **friendly** | Warm, approachable | Greetings, general chat | Formal content |
| **sad** | Sorrowful, melancholic | Sympathetic messages | Comedy |
| **angry** | Frustrated, upset | Comedic anger, urgency | Genuine conflicts |
| **whispering** | Quiet, intimate | Secrets, ASMR | Public announcements |
| **shouting** | Loud, urgent | Emphasis, alerts | Subtle content |
| **newscast** | Professional reporter | Announcements, updates | Casual chat |
| **customerservice** | Helpful, patient | Instructions, support | Entertainment |

---

## Appendix C: User Testing Scripts

### Test Scenario 1: First-Time User (Simple Mode)

**Objective:** Send a basic TTS message

**Steps:**
1. Navigate to TTS Portal
2. Join voice channel
3. Select a voice from dropdown
4. Type "Hello everyone!"
5. Adjust speed to 1.2x
6. Click Send Message

**Success Criteria:**
- [ ] Completes task without assistance
- [ ] Understands all UI elements
- [ ] Message sends successfully
- [ ] Time to complete: < 2 minutes

---

### Test Scenario 2: Intermediate User (Standard Mode)

**Objective:** Apply a preset and customize

**Steps:**
1. Switch to Standard mode
2. Click "Excited" preset
3. Verify settings auto-populate
4. Type "We're going live in 5 minutes!"
5. Adjust style intensity to "Intense"
6. Send message

**Success Criteria:**
- [ ] Understands preset concept
- [ ] Recognizes settings changed
- [ ] Can fine-tune after preset
- [ ] Time to complete: < 3 minutes

---

### Test Scenario 3: Power User (Pro Mode)

**Objective:** Emphasize text and add pauses

**Steps:**
1. Switch to Pro mode
2. Type "This is urgent! Please read immediately."
3. Select "urgent"
4. Apply strong emphasis
5. Insert 500ms pause after "urgent!"
6. Review SSML preview
7. Send message

**Success Criteria:**
- [ ] Discovers text selection toolbar
- [ ] Successfully applies formatting
- [ ] Understands visual markers
- [ ] Time to complete: < 5 minutes

---

## Appendix D: Analytics Events

### Tracking Events

```javascript
// Mode switching
trackEvent('tts_mode_switch', {
  from: 'simple',
  to: 'standard',
  timestamp: Date.now()
});

// Preset usage
trackEvent('tts_preset_applied', {
  preset_id: 'excited',
  preset_name: 'Excited',
  user_id: userId,
  guild_id: guildId
});

// Feature adoption
trackEvent('tts_feature_used', {
  feature: 'text_emphasis',
  emphasis_type: 'strong',
  message_length: 42
});

// Error tracking
trackEvent('tts_error', {
  error_type: 'voice_incompatible_style',
  voice: 'en-US-JennyNeural',
  style: 'newscast' // invalid combo
});

// SSML complexity
trackEvent('tts_message_sent', {
  has_style: true,
  has_emphasis: false,
  has_breaks: false,
  ssml_tag_count: 5,
  char_count: 87
});
```

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-01-27 | AI Design System | Initial specification |

---

## Approval Signatures

| Role | Name | Signature | Date |
|------|------|-----------|------|
| Product Owner | — | — | — |
| Lead Developer | — | — | — |
| UX Designer | — | — | — |
| Accessibility Lead | — | — | — |

---

**Document Status:** Draft - Pending Review
**Next Review Date:** TBD
**Contact:** [Project Team]
