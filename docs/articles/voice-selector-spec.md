# Voice Selector Component Specification

## Overview

Replace the native `<select>` voice dropdown on the TTS Portal with a custom 2-tier dropdown component that organizes voices by **Language (Tier 1) → Voice Name (Gender) (Tier 2)**, with collapsible language groups and a search/filter input.

## User Experience

### Visual Layout

```
┌─────────────────────────────────────────────────┐
│ Voice                                           │
│ ┌─────────────────────────────────────────────┐ │
│ │ Jenny (Female) - English (US)            ⌄  │ │  ← Trigger button (selected voice)
│ └─────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────┐ │
│ │ 🔍 Search voices...                         │ │  ← Search input
│ ├─────────────────────────────────────────────┤ │
│ │ ▼ ENGLISH (US)                          (6) │ │  ← Collapsible group header
│ │   Jenny (Female)                          ✓ │ │  ← Selected voice
│ │   Guy (Male)                                │ │
│ │   Aria (Female)                             │ │
│ │   Davis (Male)                              │ │
│ │   Jane (Female)                             │ │
│ │   Jason (Male)                              │ │
│ │ ▼ ENGLISH (UK)                          (3) │ │
│ │   Sonia (Female)                            │ │
│ │   Ryan (Male)                               │ │
│ │   Libby (Female)                            │ │
│ │ ▶ JAPANESE                              (4) │ │  ← Collapsed group
│ │ ▼ FRENCH                                (3) │ │
│ │   ...                                       │ │
│ └─────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────┘
```

### Interactions

1. **Opening the Dropdown**
   - Click the trigger button
   - Press Enter, Space, or ArrowDown when trigger is focused
   - Dropdown appears below the trigger
   - Search input receives focus automatically

2. **Searching/Filtering**
   - Type to filter voices by name, gender, or language
   - Matching is case-insensitive substring search
   - Groups with zero matches are hidden
   - Groups with matches auto-expand during search
   - "No voices found" message when nothing matches
   - Clearing search restores previous group collapse states

3. **Collapsing/Expanding Groups**
   - Click a language group header to toggle collapse
   - Chevron rotates to indicate state (▼ expanded, ▶ collapsed)
   - All groups start expanded by default

4. **Selecting a Voice**
   - Click a voice option
   - Dropdown closes
   - Trigger button updates to show "DisplayName (Gender) - Language"
   - Callback fires to update styles, save preference, rebuild SSML

5. **Closing the Dropdown**
   - Click a voice option (auto-closes)
   - Press Escape
   - Click outside the component

6. **Keyboard Navigation**
   - ArrowDown/ArrowUp: Move through visible options
   - Enter: Select focused option
   - Escape: Close dropdown
   - Home/End: Jump to first/last visible option
   - Typing goes to search input

## Technical Implementation

### Architecture

The component follows the established shared component pattern:

| Layer | File | Purpose |
|-------|------|---------|
| Helper | `src/DiscordBot.Bot/Helpers/LocaleDisplayNames.cs` | Static locale code → display name mapping |
| ViewModel | `src/DiscordBot.Bot/ViewModels/Components/VoiceSelectorViewModel.cs` | Component data model |
| View | `src/DiscordBot.Bot/Pages/Shared/Components/_VoiceSelector.cshtml` | Self-contained HTML + CSS + JS |
| Integration | `src/DiscordBot.Bot/Pages/Portal/TTS/Index.cshtml.cs` | Build ViewModel from curated voices |
| Integration | `src/DiscordBot.Bot/Pages/Portal/TTS/Index.cshtml` | Replace `<select>` with `<partial>` |
| Integration | `src/DiscordBot.Bot/wwwroot/js/portal-tts.js` | Update 5 voice references + add callback |

### Locale Display Name Mapping

Static dictionary in `LocaleDisplayNames.cs`:

| Locale Code | Display Name |
|-------------|-------------|
| `en-US` | English (US) |
| `en-GB` | English (UK) |
| `ja-JP` | Japanese |
| `fr-FR` | French |
| `de-DE` | German |
| `it-IT` | Italian |
| `es-ES` | Spanish (Spain) |
| `es-MX` | Spanish (Mexico) |
| `hi-IN` | Hindi |
| `zh-CN` | Chinese (Mandarin) |
| `sv-SE` | Swedish |
| `ru-RU` | Russian |
| `ar-SA` | Arabic |

Falls back to the raw locale code if not found.

### ViewModel

```csharp
public record VoiceSelectorViewModel
{
    public IReadOnlyList<VoiceSelectorVoiceOption> Voices { get; init; }
    public string SelectedVoice { get; init; } = string.Empty;
    public string ContainerId { get; init; } = "voiceSelector";
    public string? OnVoiceChange { get; init; }
    public string Placeholder { get; init; } = "Select a voice...";
    public string SearchPlaceholder { get; init; } = "Search voices...";
}

public record VoiceSelectorVoiceOption
{
    public string Value { get; init; }           // e.g., "en-US-JennyNeural"
    public string DisplayName { get; init; }     // e.g., "Jenny"
    public string Gender { get; init; }          // e.g., "Female"
    public string Locale { get; init; }          // e.g., "en-US"
    public string LocaleDisplayName { get; init; } // e.g., "English (US)"
}
```

### JavaScript API

The component exposes window-level functions following the existing pattern:

| Function | Description |
|----------|-------------|
| `voiceSelector_getValue(containerId)` | Returns the selected voice short name |
| `voiceSelector_setValue(containerId, voiceName, suppressCallback?)` | Sets voice programmatically |
| `voiceSelector_toggleGroup(containerId, locale)` | Toggles a language group |

### Portal TTS Integration Points

Six changes in `portal-tts.js`:

| Location | Current | New |
|----------|---------|-----|
| `setupEventHandlers()` | `voiceSelect.addEventListener('change', ...)` | Remove (logic moves to callback) |
| `loadSavedVoice()` | `voiceSelect.value = saved` | `voiceSelector_setValue(id, saved, true)` |
| `sendTtsMessage()` | `getElementById('voiceSelect').value` | `voiceSelector_getValue(id)` |
| `portalHandlePresetApply()` | `voiceSelect.value = preset.voice` | `voiceSelector_setValue(id, voice, true)` |
| `buildSsmlFromCurrentState()` | `voiceSelect?.value` | `voiceSelector_getValue(id)` |
| **New callback** | N/A | `portalHandleVoiceChange(voiceName)` |

### CSS Design Tokens

Uses the project's design system variables:

| Element | Background | Border | Text |
|---------|-----------|--------|------|
| Trigger | `--color-bg-primary` | `--color-border-primary` | `--color-text-primary` |
| Dropdown | `--color-bg-tertiary` | `--color-border-primary` | — |
| Search | `--color-bg-primary` | `--color-border-primary` | `--color-text-primary` |
| Group header | `--color-bg-secondary` | — | `--color-text-secondary` |
| Option | transparent | — | `--color-text-primary` |
| Option hover | `--color-bg-hover` | — | — |
| Option selected | `--color-accent-orange-muted` | — | — |
| Focus ring | — | `--color-accent-orange` | — |

### Accessibility

- Trigger: `aria-haspopup="listbox"`, `aria-expanded`
- Dropdown: `role="listbox"`, `aria-label`
- Options: `role="option"`, `aria-selected`
- Group headers: `aria-expanded` for collapse state
- Focus management: search on open, trigger on close
- Full keyboard navigation (Arrow keys, Enter, Escape, Home, End)

## Data Source

The curated voice list from `AzureTtsService.GetCuratedVoices()` provides 32 voices across 13 locales. The same data source is used by the Discord `/tts` command autocomplete for consistency.

## Future Considerations

- **Voice Favorites**: The [Voice Favorites Spec](voice-favorites-spec.md) describes adding star/favorite support. The custom dropdown provides the foundation for this since favorite stars can be added to each option row.
- **Guild Admin TTS Page**: The Guilds TTS admin page (`Pages/Guilds/TextToSpeech/Index.cshtml`) has a similar voice `<select>` that could adopt this component in a follow-up.
- **Dynamic voice list**: If the curated list grows significantly, the search feature ensures usability at scale.
