# VOX UI/UX Design Specification

**Version:** 2.0
**Last Updated:** 2026-02-02
**Target Framework:** .NET 8 Razor Pages with Tailwind CSS
**Related Systems:** [Design System](design-system.md) | [Component API](component-api.md) | [VOX System Spec](vox-system-spec.md)

---

## Table of Contents

1. [Overview](#overview)
2. [Design Principles](#design-principles)
3. [Page Specifications](#page-specifications)
4. [Component Specifications](#component-specifications)
5. [Accessibility](#accessibility)
6. [API Requirements](#api-requirements)
7. [Implementation Checklist](#implementation-checklist)

---

## Overview

### What is VOX?

VOX is a Half-Life-style clip-concatenation announcement system. Users type a sequence of words, the system finds matching pre-recorded audio clips, concatenates them with configurable gaps, and plays the result in a Discord voice channel.

Unlike the existing soundboard (single clip playback), VOX concatenates multiple clips into one continuous announcement.

### Design Goals

- **Mirror soundboard portal** structure and layout for consistency
- **3 clip groups** (VOX, FVOX, HGrunt) as tabs or groupings
- **Search/autocomplete** is the primary interaction (100-200 clips per group)
- **Clip grid browser** for visual discovery of available clips
- **Real-time token preview** showing which typed words have matching clips

---

## Design Principles

### Consistency with Existing Portal

The VOX UI follows the established portal design language:

- **Two-column layout** (sidebar left, main panel right) matching Soundboard/TTS portals
- **Portal Header component** with guild icon, navigation tabs, and status badge
- **Dark theme optimized** using existing design tokens
- **Toast notifications** for feedback
- **Status polling** for real-time updates

### Soundboard-Style UX

Since this is conceptually a "multi-clip soundboard", the UI takes cues from the soundboard portal:

- Clip grid layout similar to the sound buttons
- Click-to-add interaction (click a clip tile to append it to the message)
- Visual feedback on playback state
- Same voice channel panel in the sidebar

### Key Difference from Soundboard

Where the soundboard is "click one sound to play", VOX is "build a sentence from clips, then play the whole thing". The UI needs to support both:
1. **Browsing/searching** the clip library
2. **Composing** a sequence of clips (text input + click-to-append)
3. **Previewing** which words will match before sending

---

## Page Specifications

### VOX Portal Page (`/Portal/VOX/{guildId}`)

Member-facing page for composing and playing VOX announcements.

#### Layout Structure

```
+---------------------------------------------------------------------+
| Portal Header (Guild Icon, Name, Tabs, Status)                      |
+------------------+--------------------------------------------------+
|  Sidebar (Left)  |  VOX Composer (Right)                            |
|  300px fixed     |  Flexible width                                  |
|                  |                                                  |
|  +-----------+   |  +--------------------------------------------+  |
|  | Voice     |   |  | Group Tabs: [VOX] [FVOX] [HGRUNT]         |  |
|  | Channel   |   |  +--------------------------------------------+  |
|  | Selector  |   |                                                  |
|  |           |   |  +--------------------------------------------+  |
|  | [Join]    |   |  | Message Input (autocomplete text field)    |  |
|  | [Leave]   |   |  | [type words... suggestions appear]         |  |
|  +-----------+   |  +--------------------------------------------+  |
|                  |                                                  |
|  +-----------+   |  +--------------------------------------------+  |
|  | Now       |   |  | Token Preview Strip                        |  |
|  | Playing   |   |  | [word] . [word] . [word] . [word]          |  |
|  |           |   |  |  green    green   green    red(skipped)     |  |
|  | [Stop]    |   |  +--------------------------------------------+  |
|  +-----------+   |                                                  |
|                  |  [Play VOX Announcement] button                  |
|  +-----------+   |                                                  |
|  | Stats     |   |                                                  |
|  | VOX: 187  |   |                                                  |
|  | FVOX: 156 |   |                                                  |
|  | HG: 94    |   |                                                  |
|  +-----------+   |                                                  |
|                  |  +--------------------------------------------+  |
|                  |  | Clip Browser                                |  |
|                  |  | [Search clips...              ]            |  |
|                  |  |                                            |  |
|                  |  | +--------+ +--------+ +--------+          |  |
|                  |  | |warning | |alert   | |attention|          |  |
|                  |  | | 0.6s   | | 0.4s   | | 0.8s   |          |  |
|                  |  | +--------+ +--------+ +--------+          |  |
|                  |  | +--------+ +--------+ +--------+          |  |
|                  |  | |all     | |breach  | |code    |          |  |
|                  |  | | 0.3s   | | 0.5s   | | 0.4s   |          |  |
|                  |  | +--------+ +--------+ +--------+          |  |
|                  |  | ... (scrollable grid of all clips)        |  |
|                  |  +--------------------------------------------+  |
+------------------+--------------------------------------------------+
```

#### Component Breakdown

##### Left Sidebar (300px Fixed Width)

**Voice Channel Panel** (reuse existing `_VoiceChannelPanel.cshtml`):

```csharp
new StatusIndicatorViewModel {
    Status = Model.IsConnected ? StatusType.Online : StatusType.Offline,
    Text = Model.IsConnected ? "Connected" : "Disconnected",
    DisplayStyle = StatusDisplayStyle.DotWithText,
    Size = StatusSize.Medium
}
```

- Channel dropdown selector
- Join/Leave buttons
- Styled with `bg-bg-secondary`, `border-border-primary`, `rounded-lg`, `p-5`

**Now Playing Section**:
- Container: `bg-bg-primary`, `rounded-lg`, `p-4`
- Message display: `text-sm`, `font-mono`, `text-text-primary`, truncated
- Stop button: `ButtonViewModel` with `Variant = ButtonVariant.Danger`

**Clip Stats Panel**:
- Display clip counts per group
- Container: `bg-bg-secondary`, `rounded-lg`, `p-4`
- Numbers: `text-lg`, `font-bold`, `text-text-primary`
- Labels: `text-xs`, `text-text-secondary`

##### Right Panel (Flexible Width)

**Group Tabs** (VOX / FVOX / HGRUNT):

```csharp
new NavTabsViewModel {
    ContainerId = "voxGroupTabs",
    StyleVariant = NavTabStyle.Pills,
    NavigationMode = NavMode.InPage,
    Tabs = new List<NavTabItem> {
        new() { Id = "vox", Label = "VOX" },
        new() { Id = "fvox", Label = "FVOX" },
        new() { Id = "hgrunt", Label = "HGrunt" }
    },
    ActiveTabId = "vox"
}
```

Switching tabs:
- Updates the clip browser to show clips from the selected group
- Updates autocomplete suggestions to match the selected group
- Clears the message input and token preview (or re-validates against new group)

**Message Input with Autocomplete**:

The primary input for composing VOX messages. An enhanced text field with autocomplete that suggests clip names as the user types.

```html
<div class="vox-message-input">
  <label class="form-label">Message</label>
  <div class="vox-autocomplete-wrapper">
    <input type="text"
           id="voxMessageInput"
           class="form-input w-full"
           placeholder="Type words to announce..."
           autocomplete="off"
           aria-label="VOX message"
           aria-describedby="voxMessageHelp">

    <!-- Autocomplete dropdown (shown while typing) -->
    <div class="vox-autocomplete-dropdown" id="voxAutocomplete" role="listbox">
      <!-- Populated dynamically -->
    </div>
  </div>
  <span id="voxMessageHelp" class="text-xs text-text-tertiary">
    Type words separated by spaces. Click clips below to append.
  </span>
</div>
```

**Autocomplete Behavior**:
1. As the user types, extract the last word being typed
2. Search the active group's clips matching that partial word
3. Show dropdown with up to 10-15 suggestions
4. Selecting a suggestion replaces the last partial word and adds a space
5. Pressing Space or Enter (without dropdown selection) accepts the current word as-is
6. Dropdown dismisses on blur or Escape

**Autocomplete Dropdown Styling**:
```css
.vox-autocomplete-wrapper {
  position: relative;
}

.vox-autocomplete-dropdown {
  position: absolute;
  top: 100%;
  left: 0;
  right: 0;
  z-index: 50;
  max-height: 240px;
  overflow-y: auto;
  background-color: var(--color-bg-secondary);
  border: 1px solid var(--color-border-primary);
  border-top: none;
  border-radius: 0 0 0.5rem 0.5rem;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
  display: none; /* shown via JS */
}

.vox-autocomplete-dropdown.active {
  display: block;
}

.vox-autocomplete-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.5rem 0.75rem;
  cursor: pointer;
  transition: background-color 0.1s;
}

.vox-autocomplete-item:hover,
.vox-autocomplete-item.highlighted {
  background-color: var(--color-bg-hover);
}

.vox-autocomplete-item-name {
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--color-text-primary);
}

.vox-autocomplete-item-duration {
  font-size: 0.75rem;
  color: var(--color-text-tertiary);
  font-family: var(--font-family-mono);
}
```

**Token Preview Strip**:

Appears below the message input. Shows each word from the input with a visual indicator of whether a matching clip exists.

```html
<div class="vox-token-preview" id="voxTokenPreview">
  <span class="vox-token-label">
    Preview: <span id="tokenCount">5 words</span>,
    ~<span id="tokenDuration">3.2s</span>
  </span>
  <div class="vox-token-pills" id="tokenPills">
    <!-- Populated dynamically as user types -->
    <span class="vox-token matched">attention</span>
    <span class="vox-token-gap">.</span>
    <span class="vox-token matched">all</span>
    <span class="vox-token-gap">.</span>
    <span class="vox-token matched">personnel</span>
    <span class="vox-token-gap">.</span>
    <span class="vox-token skipped">hello</span>
  </div>
</div>
```

**Token Pill Styling**:
```css
.vox-token-preview {
  padding: 0.75rem;
  background-color: var(--color-bg-tertiary);
  border-radius: 0.5rem;
  margin-top: 0.75rem;
}

.vox-token-label {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  margin-bottom: 0.5rem;
  display: block;
}

.vox-token-pills {
  display: flex;
  flex-wrap: wrap;
  gap: 0.375rem;
  align-items: center;
}

.vox-token {
  display: inline-flex;
  padding: 0.25rem 0.625rem;
  border-radius: 9999px;
  font-size: 0.8125rem;
  font-weight: 500;
  border: 1px solid;
}

.vox-token.matched {
  background-color: rgba(16, 185, 129, 0.15);
  border-color: rgba(16, 185, 129, 0.4);
  color: #10b981;
}

.vox-token.skipped {
  background-color: rgba(239, 68, 68, 0.15);
  border-color: rgba(239, 68, 68, 0.4);
  color: #ef4444;
  text-decoration: line-through;
}

.vox-token-gap {
  color: var(--color-text-tertiary);
  font-size: 0.75rem;
}
```

**Token Preview Updates**: Debounced (200ms) as the user types. Calls the preview API endpoint or performs client-side lookup against a cached clip name list.

**Play Button**:

```csharp
new ButtonViewModel {
    Text = "Play VOX Announcement",
    Variant = ButtonVariant.Primary,
    Size = ButtonSize.Large,
    Type = "button",
    IsDisabled = !Model.IsConnected,
    AdditionalAttributes = new Dictionary<string, object> {
        { "class", "w-full" },
        { "id", "voxPlayButton" }
    }
}
```

- Disabled when: not connected to voice, no matched words, currently playing
- Loading state: "Playing..." with spinner while audio streams

**Clip Browser**:

The main discovery interface. A searchable grid of all available clips for the active group.

```html
<div class="vox-clip-browser">
  <div class="vox-clip-search">
    <input type="text"
           id="clipSearchInput"
           class="form-input w-full"
           placeholder="Search clips..."
           aria-label="Search clips">
    <span class="vox-clip-count" id="clipCount">187 clips</span>
  </div>

  <div class="vox-clip-grid" id="clipGrid">
    <!-- Clip tiles populated dynamically -->
  </div>
</div>
```

**Clip Tile**:

Each clip is a clickable tile. Clicking appends the clip name to the message input.

```html
<button class="vox-clip-tile"
        data-clip="warning"
        aria-label="Add 'warning' to message (0.6s)">
  <span class="vox-clip-name">warning</span>
  <span class="vox-clip-duration">0.6s</span>
</button>
```

**Clip Grid Styling**:
```css
.vox-clip-browser {
  margin-top: 1.5rem;
}

.vox-clip-search {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.vox-clip-count {
  font-size: 0.75rem;
  color: var(--color-text-tertiary);
  white-space: nowrap;
}

.vox-clip-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(110px, 1fr));
  gap: 0.5rem;
  max-height: 400px;
  overflow-y: auto;
  padding-right: 0.25rem;
  scrollbar-width: thin;
  scrollbar-color: var(--color-border-primary) transparent;
}

.vox-clip-tile {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.25rem;
  padding: 0.625rem 0.5rem;
  background-color: var(--color-bg-tertiary);
  border: 1px solid var(--color-border-primary);
  border-radius: 0.5rem;
  cursor: pointer;
  transition: all 0.15s;
  text-align: center;
}

.vox-clip-tile:hover {
  background-color: var(--color-bg-hover);
  border-color: var(--color-accent-blue);
  transform: translateY(-1px);
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.3);
}

.vox-clip-tile:active {
  transform: scale(0.97);
}

.vox-clip-name {
  font-size: 0.8125rem;
  font-weight: 500;
  color: var(--color-text-primary);
  word-break: break-word;
}

.vox-clip-duration {
  font-size: 0.625rem;
  color: var(--color-text-tertiary);
  font-family: var(--font-family-mono);
}
```

**Clip Tile Click Behavior**:
1. Append clip name + space to the message input
2. Brief visual feedback (pulse animation on the tile)
3. Update token preview strip
4. Keep focus on the message input for continued typing

**Clip Browser Search**:
- Filters the grid in real-time as the user types
- Searches by prefix first, then substring
- Shows "No clips match '{query}'" empty state when no results
- Debounced (150ms) for performance with 200+ clips

#### Interaction Flow

**Typical usage**:
1. User selects a group tab (VOX/FVOX/HGRUNT)
2. User types in message input - autocomplete suggests clips
3. Or user browses the clip grid below, clicking tiles to append words
4. Token preview updates in real-time showing matched/unmatched words
5. User clicks "Play VOX Announcement"
6. Response shows matched words and any skipped words
7. Audio plays in Discord voice channel

**Error States**:
- **Not connected**: Play button disabled, tooltip "Join a voice channel first"
- **No matched words**: Play button disabled, token preview shows all red
- **Rate limited**: Toast error "Slow down! Wait a moment before sending another message"
- **Playback failed**: Toast error with details

#### Responsive Breakpoints

**Desktop (1024px+)**:
- Two-column layout: sidebar 300px, main flex-1
- Clip grid shows 5-6 columns
- All features visible

**Tablet (768px-1023px)**:
- Sidebar stacks above main panel
- Clip grid shows 4 columns

**Mobile (< 768px)**:
- Single column layout
- Clip grid shows 3 columns
- Play button sticky at bottom

---

## Component Specifications

### Component: Autocomplete Text Input

The core interaction component. A text input with dropdown suggestions.

#### Behavior

```javascript
// Pseudocode for autocomplete behavior
const input = document.getElementById('voxMessageInput');
const dropdown = document.getElementById('voxAutocomplete');

let clipNames = []; // Loaded from API on tab switch
let highlightedIndex = -1;

input.addEventListener('input', debounce(() => {
  const words = input.value.split(/\s+/);
  const lastWord = words[words.length - 1] || '';

  if (lastWord.length === 0) {
    hideDropdown();
    return;
  }

  // Filter clips matching the partial last word
  const matches = clipNames
    .filter(name => name.startsWith(lastWord) || name.includes(lastWord))
    .sort((a, b) => {
      // Prefix matches first, then substring
      const aStarts = a.startsWith(lastWord) ? 0 : 1;
      const bStarts = b.startsWith(lastWord) ? 0 : 1;
      return aStarts - bStarts || a.localeCompare(b);
    })
    .slice(0, 15);

  if (matches.length > 0) {
    showDropdown(matches);
  } else {
    hideDropdown();
  }

  // Also update token preview
  updateTokenPreview();
}, 100));

// Keyboard navigation
input.addEventListener('keydown', (e) => {
  if (e.key === 'ArrowDown') {
    e.preventDefault();
    highlightNext();
  } else if (e.key === 'ArrowUp') {
    e.preventDefault();
    highlightPrevious();
  } else if (e.key === 'Enter' || e.key === 'Tab') {
    if (highlightedIndex >= 0) {
      e.preventDefault();
      selectHighlighted();
    }
  } else if (e.key === 'Escape') {
    hideDropdown();
  }
});

function selectSuggestion(clipName) {
  const words = input.value.split(/\s+/);
  words[words.length - 1] = clipName;
  input.value = words.join(' ') + ' ';
  hideDropdown();
  input.focus();
  updateTokenPreview();
}
```

#### Accessibility

- Dropdown: `role="listbox"`, items: `role="option"`
- Input: `aria-expanded`, `aria-activedescendant`, `aria-controls`
- Highlighted item announced via `aria-activedescendant`
- Escape closes dropdown
- Screen reader: "15 suggestions available" announced via `aria-live="polite"` region

### Component: Token Preview Strip

Shows real-time feedback on which typed words have matching clips.

#### Update Logic

```javascript
function updateTokenPreview() {
  const words = input.value.trim().split(/\s+/).filter(Boolean);
  const pillsContainer = document.getElementById('tokenPills');

  // Can do client-side lookup if clip names are cached
  // Or call /api/portal/vox/{guildId}/preview for server-side validation

  pillsContainer.innerHTML = '';

  let matchedCount = 0;
  let totalDuration = 0;

  words.forEach((word, i) => {
    const normalized = word.toLowerCase().replace(/[^a-z0-9-_]/g, '');
    const clip = clipLookup[normalized]; // Client-side dictionary
    const isMatched = !!clip;

    if (isMatched) {
      matchedCount++;
      totalDuration += clip.durationSeconds;
    }

    const pill = document.createElement('span');
    pill.className = `vox-token ${isMatched ? 'matched' : 'skipped'}`;
    pill.textContent = normalized;
    pillsContainer.appendChild(pill);

    if (i < words.length - 1) {
      const gap = document.createElement('span');
      gap.className = 'vox-token-gap';
      gap.textContent = '.';
      pillsContainer.appendChild(gap);
    }
  });

  // Add word gap durations (default 50ms between clips)
  const wordGapMs = 50;
  const gapDuration = (matchedCount - 1) * wordGapMs / 1000;
  totalDuration += Math.max(0, gapDuration);

  document.getElementById('tokenCount').textContent = `${matchedCount}/${words.length} matched`;
  document.getElementById('tokenDuration').textContent = `${totalDuration.toFixed(1)}s`;

  // Enable/disable play button
  document.getElementById('voxPlayButton').disabled = matchedCount === 0;
}
```

### Component: Clip Grid

The main discovery interface. A searchable, alphabetically-organized grid of all available clips for the active group. Features sticky section headers, A-Z rail navigation, and keyboard support.

#### Layout: A-Z Index Rail

A vertical sidebar on the right side of the clip grid that provides quick navigation to alphabetical sections.

**Visual Structure:**
- 28px wide, flex column layout
- 26 letter buttons (A-Z)
- Letters are enabled/disabled based on which clips exist for that letter
- Active letter highlighted with orange accent color
- Thin scrollbar (Firefox: `scrollbar-width: thin`)
- Hidden on mobile (< 768px) via media query

**Styling:**

```css
.vox-az-rail {
  width: 28px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 0.25rem 0;
  background-color: var(--color-bg-secondary);
  border: 1px solid var(--color-border-primary);
  border-radius: 0.375rem;
  align-self: stretch;
  overflow-y: auto;
  scrollbar-width: thin;
  scrollbar-color: var(--color-border-primary) transparent;
}

.vox-az-letter {
  width: 22px;
  height: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.65rem;
  font-weight: 600;
  color: var(--color-text-secondary);
  background: transparent;
  border: none;
  border-radius: 2px;
  cursor: pointer;
  transition: all 0.1s ease-out;
  padding: 0;
}

.vox-az-letter:hover:not(:disabled) {
  color: var(--color-text-primary);
  background-color: var(--color-bg-hover);
  transform: scale(1.1);
}

.vox-az-letter.active {
  color: var(--color-accent-orange);
  background-color: rgba(203, 78, 27, 0.2);
  font-weight: 700;
}

.vox-az-letter:disabled {
  color: var(--color-text-tertiary);
  opacity: 0.4;
  cursor: default;
}

.vox-az-letter:focus-visible {
  outline: 2px solid var(--color-border-focus);
  outline-offset: 1px;
}
```

**Behavior:**

```javascript
// Pseudocode for A-Z rail navigation
document.querySelectorAll('.vox-az-letter').forEach(btn => {
  btn.addEventListener('click', (e) => {
    const letter = btn.dataset.letter;
    const section = document.querySelector(`[data-section-letter="${letter}"]`);

    if (section) {
      const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

      // Scroll to section
      clipGrid.scrollTo({
        top: section.offsetTop,
        behavior: prefersReducedMotion ? 'auto' : 'smooth'
      });

      // Show scroll indicator
      showScrollIndicator(letter);
    }
  });
});

// Disable letters with no clips
enabledLetters.forEach(letter => {
  const btn = document.querySelector(`[data-letter="${letter}"]`);
  btn.disabled = false;
});

disabledLetters.forEach(letter => {
  const btn = document.querySelector(`[data-letter="${letter}"]`);
  btn.disabled = true;
});
```

#### Layout: Sticky Section Headers

Clips are grouped alphabetically with section headers (A, B, C, etc.) that remain visible at the top of the grid while scrolling through that section.

**Styling:**

```css
.vox-section-header {
  grid-column: 1 / -1;  /* Spans all columns */
  position: sticky;
  top: 0;
  z-index: 2;
  display: flex;
  align-items: center;
  padding: 0.5rem 0.75rem;
  margin-top: 0.75rem;
  background-color: var(--color-bg-secondary);
  border-bottom: 1px solid var(--color-border-primary);
  font-size: 0.875rem;
  font-weight: 700;
  color: var(--color-accent-orange);
  text-transform: uppercase;
  letter-spacing: 0.05em;
  transition: box-shadow 0.15s ease-out;
}

.vox-section-header:first-child {
  margin-top: 0;
}

.vox-section-header.stuck {
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
}
```

**Behavior:**

The "stuck" class is added when a header reaches the top of the viewport (within 1px), creating a shadow effect to indicate the header is pinned. This class is managed by an Intersection Observer and scroll event listener:

```javascript
// Intersection Observer detects when headers are visible
const observer = new IntersectionObserver((entries) => {
  entries.forEach(entry => {
    const header = entry.target;
    const rect = header.getBoundingClientRect();

    // Add shadow when header is stuck to top
    header.classList.toggle('stuck', rect.top <= 1);
  });
}, {
  root: clipGrid,
  threshold: [0, 0.1, 0.5, 1],
  rootMargin: '0px 0px -80% 0px'
});

// Observe all section headers
document.querySelectorAll('.vox-section-header').forEach(header => {
  observer.observe(header);
});
```

#### Layout: Scroll Position Indicator

A large floating letter indicator appears at the center of the screen when the user navigates via the A-Z rail, showing which section they're scrolling to.

**Visual Design:**
- 80x80px square with rounded corners
- Orange accent color (#CB4E1B) for letter and border
- Dark semi-transparent background
- Box shadow for depth
- Positioned fixed at center of screen (top: 50%, left: 50%)
- Appears for 600ms then fades out

**Styling:**

```css
.vox-scroll-letter {
  position: fixed;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%) scale(0.8);
  width: 80px;
  height: 80px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 2.5rem;
  font-weight: 700;
  color: var(--color-accent-orange);
  background-color: var(--color-bg-tertiary);
  border: 2px solid var(--color-accent-orange);
  border-radius: 0.75rem;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4);
  opacity: 0;
  pointer-events: none;
  transition: opacity 0.15s ease-out, transform 0.15s ease-out;
  z-index: 100;
}

.vox-scroll-letter.visible {
  opacity: 1;
  transform: translate(-50%, -50%) scale(1);
}
```

**Behavior:**

The indicator is shown when the user clicks an A-Z rail button. It displays the letter being navigated to and automatically hides after 600ms:

```javascript
function showScrollIndicator(letter) {
  // Hidden if user prefers reduced motion
  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  if (prefersReducedMotion) return;

  const element = document.querySelector('.vox-scroll-letter');
  element.textContent = letter;
  element.classList.add('visible');

  // Clear existing timeout
  if (scrollIndicatorTimeout) {
    clearTimeout(scrollIndicatorTimeout);
  }

  // Hide after 600ms
  scrollIndicatorTimeout = setTimeout(() => {
    element.classList.remove('visible');
  }, 600);
}
```

#### Search Filtering

```javascript
const clipSearchInput = document.getElementById('clipSearchInput');

clipSearchInput.addEventListener('input', debounce(() => {
  const query = clipSearchInput.value.toLowerCase().trim();
  const tiles = document.querySelectorAll('.vox-clip-tile');
  let visibleCount = 0;

  tiles.forEach(tile => {
    const clipName = tile.dataset.clip;
    const matches = query === '' ||
                    clipName.startsWith(query) ||
                    clipName.includes(query);

    tile.style.display = matches ? '' : 'none';
    if (matches) visibleCount++;
  });

  document.getElementById('clipCount').textContent =
    query ? `${visibleCount} matching` : `${tiles.length} clips`;
}, 150));
```

#### Click-to-Append

```javascript
document.getElementById('clipGrid').addEventListener('click', (e) => {
  const tile = e.target.closest('.vox-clip-tile');
  if (!tile) return;

  const clipName = tile.dataset.clip;
  const input = document.getElementById('voxMessageInput');

  // Append clip name with space
  const currentValue = input.value;
  input.value = currentValue + (currentValue.endsWith(' ') || currentValue === '' ? '' : ' ') + clipName + ' ';

  // Visual feedback
  tile.classList.add('just-clicked');
  setTimeout(() => tile.classList.remove('just-clicked'), 200);

  // Update token preview
  updateTokenPreview();

  // Keep focus on input
  input.focus();
});
```

```css
.vox-clip-tile.just-clicked {
  animation: tile-pulse 0.2s ease-out;
}

@keyframes tile-pulse {
  0% { transform: scale(1); }
  50% { transform: scale(0.93); background-color: var(--color-accent-blue); }
  100% { transform: scale(1); }
}
```

---

## Accessibility

### WCAG 2.1 AA Compliance

#### Keyboard Navigation

**Tab Order**:
1. Voice channel selector / Join / Leave
2. Group tabs (VOX / FVOX / HGRUNT)
3. Message input (with autocomplete)
4. Play button
5. Clip search input
6. Clip grid tiles (first tile receives focus)

**Keyboard Interactions**:
- **Tab**: Move between major sections/components
- **Arrow keys**: Navigate autocomplete dropdown, navigate clip grid tiles (Left/Right for adjacent tiles, Up/Down for rows)
- **Enter / Space**: Select autocomplete suggestion, click focused clip tile, activate buttons
- **Escape**: Close autocomplete dropdown, or return focus from clip grid to message input
- **Home**: Jump to first clip tile in grid
- **End**: Jump to last clip tile in grid

**Enhanced Clip Grid Navigation**:

The clip grid supports arrow key navigation for discovering clips without using the mouse. The grid calculates columns dynamically based on viewport width and tile size:

```javascript
function handleGridKeyDown(e) {
  const tiles = Array.from(document.querySelectorAll('.vox-clip-tile'));
  if (tiles.length === 0) return;

  // Calculate columns based on grid width and tile size
  const gridWidth = clipGrid.clientWidth;
  const tileWidth = tiles[0].offsetWidth + 12; // Include gap
  const columns = Math.max(1, Math.floor(gridWidth / tileWidth));

  let newIndex = currentFocusedTileIndex;

  switch (e.key) {
    case 'ArrowRight':
      newIndex = Math.min(newIndex + 1, tiles.length - 1);
      break;

    case 'ArrowLeft':
      newIndex = Math.max(newIndex - 1, 0);
      break;

    case 'ArrowDown':
      newIndex = Math.min(newIndex + columns, tiles.length - 1);
      break;

    case 'ArrowUp':
      newIndex = Math.max(newIndex - columns, 0);
      break;

    case 'Home':
      newIndex = 0;
      break;

    case 'End':
      newIndex = tiles.length - 1;
      break;

    case 'Enter':
    case ' ':
      // Click the focused tile
      tiles[currentFocusedTileIndex].click();
      return;

    case 'Escape':
      // Return focus to message input
      messageInput.focus();
      currentFocusedTileIndex = -1;
      return;
  }

  // Update focus
  if (newIndex !== currentFocusedTileIndex) {
    currentFocusedTileIndex = newIndex;

    // Update tabindex for all tiles
    tiles.forEach((tile, idx) => {
      tile.setAttribute('tabindex', idx === newIndex ? '0' : '-1');
    });

    // Focus and scroll into view
    tiles[newIndex].focus();
    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    tiles[newIndex].scrollIntoView({
      block: 'nearest',
      behavior: prefersReducedMotion ? 'auto' : 'smooth'
    });
  }
}
```

**Focus Styling for Clip Tiles**:

```css
.vox-clip-tile:focus-visible {
  outline: none;
  box-shadow: 0 0 0 2px var(--color-bg-primary),
              0 0 0 4px var(--color-accent-orange);
  z-index: 1;
  transform: translateY(-2px);
}
```

The focused tile receives a distinctive double-ring focus indicator using the primary background and accent orange colors, with a slight upward transform for visual feedback.

#### Screen Reader Support

```html
<!-- Group tabs -->
<div role="tablist" aria-label="VOX clip groups">
  <button role="tab" aria-selected="true" aria-controls="voxPanel">VOX</button>
  <button role="tab" aria-selected="false" aria-controls="fvoxPanel">FVOX</button>
  <button role="tab" aria-selected="false" aria-controls="hgruntPanel">HGrunt</button>
</div>

<!-- Autocomplete -->
<input aria-label="VOX message input"
       aria-autocomplete="list"
       aria-expanded="false"
       aria-controls="voxAutocomplete"
       aria-activedescendant="">

<!-- Token preview live region -->
<div aria-live="polite" class="sr-only" id="tokenPreviewAnnounce">
  5 of 6 words matched, estimated duration 3.2 seconds
</div>

<!-- A-Z Rail Navigation -->
<div class="vox-az-rail" aria-label="Alphabetical navigation">
  <button class="vox-az-letter" data-letter="A" aria-label="Jump to clips starting with A">A</button>
  <!-- B-Z buttons... -->
</div>

<!-- Sticky Section Headers -->
<h2 class="vox-section-header" data-letter="A" data-section-letter="A">
  A
</h2>

<!-- Clip grid with enhanced keyboard support -->
<div class="vox-clip-grid" role="grid" aria-label="Available clips" id="clipGrid">
  <button class="vox-clip-tile"
          data-clip="attention"
          tabindex="0"
          aria-label="Add 'attention' to message, duration 0.6 seconds">
    attention
  </button>
  <!-- Additional tiles with tabindex="-1" for keyboard navigation -->
</div>

<!-- Scroll position indicator (hidden from screen readers) -->
<div class="vox-scroll-letter" aria-hidden="true"></div>
```

**ARIA Attributes for Enhanced Navigation:**

- **A-Z Rail buttons**: `aria-label` describes the letter and action (e.g., "Jump to clips starting with A")
- **Section headers**: `data-letter` and `data-section-letter` attributes store the letter for tracking
- **Clip tiles**: Managed `tabindex` (0 for focused, -1 for others) enables keyboard navigation; `aria-label` includes clip name and duration
- **Scroll indicator**: `aria-hidden="true"` prevents redundant screen reader announcement (visual-only feedback)

#### Motion Preferences

The UI respects `prefers-reduced-motion` media query across all animated elements and behaviors:

```css
@media (prefers-reduced-motion: reduce) {
  .vox-clip-tile,
  .vox-token,
  .vox-autocomplete-item,
  .vox-play-btn,
  .vox-az-letter,
  .vox-section-header,
  .vox-scroll-letter {
    transition: none;
    animation: none;
  }
}
```

**JavaScript Reduced Motion Handling:**

```javascript
// Scroll behavior respects reduced motion preference
const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

clipGrid.scrollTo({
  top: targetScroll,
  behavior: prefersReducedMotion ? 'auto' : 'smooth'
});

// Scroll indicator is completely hidden when reduced motion is preferred
function showScrollIndicator(letter) {
  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  if (prefersReducedMotion) return; // Don't show indicator
  // ... rest of implementation
}

// Tile focus scrolling respects reduced motion
tiles[newIndex].scrollIntoView({
  block: 'nearest',
  behavior: prefersReducedMotion ? 'auto' : 'smooth'
});
```

When users have enabled reduced motion in their OS settings:
- Scroll behavior becomes instant (no smooth scrolling)
- Scroll position indicator does not appear
- All CSS transitions are disabled
- Grid navigation focuses smoothly to new tile but scrolls instantly

---

## API Requirements

### Client-Side Data Loading

On page load and group tab switch, fetch the clip list for the active group:

**`GET /api/portal/vox/{guildId}/clips?group=vox`**

Response is cached client-side for autocomplete and token preview. The full clip list is small enough (100-200 items) to load entirely and filter in the browser.

### Playback

**`POST /api/portal/vox/{guildId}/play`**

```json
{
  "message": "attention all personnel security breach",
  "group": "vox",
  "wordGapMs": 50
}
```

### Status Polling

Reuse the existing portal status polling pattern to show Now Playing state and voice connection status.

---

## Implementation Checklist

### Phase 1: Portal VOX Page - Core

- [ ] Create `/Pages/Portal/VOX/Index.cshtml` and `IndexModel.cs`
- [ ] Add VOX tab to portal header navigation
- [ ] Build two-column layout (sidebar + main panel)
- [ ] Reuse `_VoiceChannelPanel` component for sidebar
- [ ] Add "Now Playing" section to sidebar
- [ ] Add clip stats panel to sidebar
- [ ] Create group tab switcher (VOX / FVOX / HGRUNT)
- [ ] Add API endpoint: `GET /api/portal/vox/{guildId}/clips`
- [ ] Build message input with autocomplete
- [ ] Implement autocomplete dropdown with keyboard navigation
- [ ] Create token preview strip (matched/skipped indicators)
- [ ] Create Play button with disabled/loading states
- [ ] Add API endpoint: `POST /api/portal/vox/{guildId}/play`
- [ ] Implement status polling for Now Playing updates

### Phase 2: Clip Browser

- [x] Build clip grid with tiles
- [x] Implement clip search (real-time filtering)
- [x] Add click-to-append behavior (tile -> message input)
- [x] Add visual feedback on tile click
- [x] Handle empty state when search returns no results
- [x] Test with 200+ clips for performance
- [x] Build A-Z index rail for alphabetical navigation
- [x] Implement sticky section headers with shadow effect
- [x] Add scroll position indicator (floating letter display)
- [x] Implement enhanced keyboard navigation (arrow keys, Home/End)
- [x] Add dynamic column calculation for grid navigation

### Phase 3: Polish

- [x] Implement responsive layout (mobile/tablet/desktop)
  - A-Z rail hidden on mobile (< 768px)
  - Clip grid columns adjust: 6 (desktop) → 4 (tablet) → 3 (mobile)
  - Sticky headers adjust font size and padding on mobile
- [x] Add toast notifications for success/error feedback
- [x] Test keyboard navigation end-to-end
  - Arrow keys, Home, End all functional
  - Focus management between message input and grid
  - Escape key returns focus to input
- [x] Test screen reader support (NVDA, JAWS)
  - ARIA labels on A-Z rail
  - Grid role and gridcell roles for structure
  - Focus indicators visible for screen readers
- [x] Add `prefers-reduced-motion` support
  - Scroll behavior respects preference
  - Scroll indicator hidden when reduced motion enabled
  - All transitions/animations disabled
- [ ] Verify WCAG 2.1 AA color contrast
- [x] Add stop playback from portal
- [x] Handle edge cases (empty message, all words skipped, etc.)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 2.1 | 2026-02-03 | Documented implemented clip grid features: A-Z index rail with letter navigation, sticky alphabetical section headers with shadow effects, scroll position indicator (80x80px floating letter), enhanced keyboard navigation (arrow keys, Home/End for grid), dynamic grid column calculation, intersection observer for section tracking, comprehensive ARIA labeling, reduced motion support for all animations and behaviors |
| 2.0 | 2026-02-02 | Simplified to static clip library with 3 groups, removed TTS generation, sentence builder, word bank management. Soundboard-style clip grid + autocomplete input |
| 1.1 | 2026-02-02 | Aligned component references with component API |
| 1.0 | 2026-02-02 | Initial specification (full TTS-based design) |

---

**Last Updated**: 2026-02-03
**Version**: 2.1 (Implemented)
