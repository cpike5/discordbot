# Voice Capability-Aware UI System

Plan for expanding the TTS voice capability registry and making the Portal UI dynamically adapt to show only features the selected voice supports.

## Problem

The `VoiceCapabilityProvider` only has capability data for 4 out of 34 curated voices. Voices not in the registry cause the capabilities API to return 404, and the UI has no way to know whether a voice supports styles or emphasis. The EmphasisToolbar is always shown in Pro mode regardless of voice support.

### Current Gaps

| Gap | Impact |
|-----|--------|
| Only 4 voices in capability registry | 30 voices return 404 from capabilities API |
| No `SupportsEmphasis` property on model | UI can't adapt emphasis toolbar per voice |
| Aria missing 4 styles | `customerservice`, `narration-professional`, `newscast-casual`, `newscast-formal` not available |
| No "no styles" UI message | Users see a style dropdown with all options disabled and no explanation |
| Emphasis toolbar ignores voice capability | Shows in Pro mode even for voices that don't support `<emphasis>` |

## Azure Voice Capability Reference

Source: [Azure Language Support - Voice Styles and Roles](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support?tabs=tts#voice-styles-and-roles)

### Emphasis Support

Only 3 voices support the SSML `<emphasis>` element:

| Voice | Levels |
|-------|--------|
| `en-US-GuyNeural` | reduced, none, moderate, strong |
| `en-US-DavisNeural` | reduced, none, moderate, strong |
| `en-US-JaneNeural` | reduced, none, moderate, strong |

### Style Support by Voice

#### en-US Voices with Styles

| Voice | Gender | Styles | Emphasis |
|-------|--------|--------|----------|
| `en-US-JennyNeural` | F | angry, assistant, chat, cheerful, customerservice, excited, friendly, hopeful, newscast, sad, shouting, terrified, unfriendly, whispering | No |
| `en-US-AriaNeural` | F | angry, chat, cheerful, customerservice, empathetic, excited, friendly, hopeful, narration-professional, newscast-casual, newscast-formal, sad, shouting, terrified, unfriendly, whispering | No |
| `en-US-GuyNeural` | M | angry, cheerful, excited, friendly, hopeful, newscast, sad, shouting, terrified, unfriendly, whispering | Yes |
| `en-US-DavisNeural` | M | angry, chat, cheerful, excited, friendly, hopeful, sad, shouting, terrified, unfriendly, whispering | Yes |
| `en-US-JaneNeural` | F | angry, cheerful, excited, friendly, hopeful, sad, shouting, terrified, unfriendly, whispering | Yes |
| `en-US-JasonNeural` | M | angry, cheerful, excited, friendly, hopeful, sad, shouting, terrified, unfriendly, whispering | No |

#### Non-en-US Voices with Styles

| Voice | Gender | Styles |
|-------|--------|--------|
| `en-GB-RyanNeural` | M | chat, cheerful, sad, whispering |
| `en-GB-SoniaNeural` | F | cheerful, sad |
| `ja-JP-NanamiNeural` | F | chat, cheerful, customerservice |
| `fr-FR-DeniseNeural` | F | cheerful, excited, sad, whispering |
| `fr-FR-HenriNeural` | M | cheerful, excited, sad, whispering |
| `de-DE-ConradNeural` | M | cheerful, sad |
| `it-IT-DiegoNeural` | M | cheerful, excited, sad |
| `es-ES-AlvaroNeural` | M | cheerful, sad |
| `es-MX-DaliaNeural` | F | cheerful, sad, whispering |
| `hi-IN-SwaraNeural` | F | cheerful, empathetic, newscast |
| `zh-CN-XiaoxiaoNeural` | F | affectionate, angry, assistant, calm, chat, chat-casual, cheerful, customerservice, disgruntled, excited, fearful, friendly, gentle, lyrical, newscast, poetry-reading, sad, serious, sorry, whispering |
| `zh-CN-YunxiNeural` | M | angry, assistant, chat, cheerful, depressed, disgruntled, embarrassed, fearful, narration-relaxed, newscast, sad, serious |
| `zh-CN-YunyangNeural` | M | customerservice, narration-professional, newscast-casual |

**Roles** (zh-CN only): `zh-CN-YunxiNeural` supports roles: Boy, Narrator, YoungAdultMale.

#### Voices with NO Style Support

All other curated voices have no style or emphasis support:

`en-GB-LibbyNeural`, `ja-JP-KeitaNeural`, `ja-JP-MayuNeural`, `ja-JP-NaokiNeural`, `fr-FR-BrigitteNeural`, `de-DE-KatjaNeural`, `it-IT-ElsaNeural`, `es-ES-ElviraNeural`, `hi-IN-MadhurNeural`, `sv-SE-SofieNeural`, `sv-SE-MattiasNeural`, `ru-RU-SvetlanaNeural`, `ru-RU-DmitryNeural`, `ar-SA-ZariyahNeural`, `ar-SA-HamedNeural`

## Implementation Plan

### Step 1: Add `SupportsEmphasis` to VoiceCapabilities Model

**File:** `src/DiscordBot.Core/Models/VoiceCapabilities.cs`

Add a `bool SupportsEmphasis` property. Defaults to `false` (non-breaking additive change). The existing capabilities API endpoint already serializes the full `VoiceCapabilities` object, so the frontend will immediately receive this field.

### Step 2: Expand VoiceCapabilityProvider Registry

**File:** `src/DiscordBot.Bot/Services/Tts/VoiceCapabilityProvider.cs`

Expand the static `KnownVoices` dictionary from 4 entries to all 34 curated voices using the data tables above. Each entry gets a full `VoiceCapabilities` object. Voices with no style support get empty `SupportedStyles` arrays.

Key corrections to existing entries:
- `en-US-AriaNeural`: Add missing styles (`customerservice`, `narration-professional`, `newscast-casual`, `newscast-formal`)
- `en-US-GuyNeural`: Set `SupportsEmphasis = true`
- `en-US-DavisNeural`: Set `SupportsEmphasis = true`

New entries include `en-US-JaneNeural` (with `SupportsEmphasis = true`), all non-en-US voices with styles, and all no-style voices.

### Step 3: API Fallback for Unknown Voices

**File:** `src/DiscordBot.Bot/Controllers/PortalTtsController.cs` (~line 1157)

Change `GetVoiceCapabilities` to return a basic `VoiceCapabilities` with empty styles/emphasis instead of 404 when a voice isn't in the registry. Log a warning so mismatches between the curated list and registry are caught. After Step 2 this should never trigger for curated voices, but it prevents breakage if a voice is added to the curated list without updating the registry.

### Step 4: StyleSelector -- Handle Empty Styles

**File:** `src/DiscordBot.Bot/Pages/Shared/Components/_StyleSelector.cshtml` (~line 430)

Modify `styleSelector_loadStyles()`:
1. After fetching capabilities, check if `supportedStyles` is empty
2. If empty: disable the style `<select>`, disable the intensity slider, show an italic message: *"This voice does not support speaking styles."*
3. If styles exist: re-enable the dropdown and hide the message
4. Store `data.supportsEmphasis` from the response on the container element as a data attribute
5. Call `window.emphasisToolbar_updateCapability(data.supportsEmphasis)` to notify the emphasis toolbar

### Step 5: EmphasisToolbar -- Capability-Aware Emphasis Buttons

**File:** `src/DiscordBot.Bot/Pages/Shared/Components/_EmphasisToolbar.cshtml`

The toolbar has 6 buttons: **Strong** (emphasis), **Moderate** (emphasis), Pause, Say-as Number, Say-as Date, Clear. Pause, say-as, and clear are universal SSML features. Only the two emphasis buttons depend on voice support.

Changes:
1. Add module-level `emphasisSupported` flag (default `true`)
2. Expose `window.emphasisToolbar_updateCapability(bool)`:
   - When `false`: disable the two emphasis buttons (`data-action="emphasis"`), add a visual disabled class, show a small notice: *"Emphasis is not supported by this voice."*
   - When `true`: re-enable buttons, hide notice
3. Guard `applyFormatting()` -- return early for `action === 'emphasis'` when `!emphasisSupported`
4. Guard keyboard shortcuts (Ctrl+B, Ctrl+E) the same way
5. CSS: `.emphasis-toolbar__button--disabled` with reduced opacity on the emphasis buttons only (the rest of the toolbar remains fully functional)

## Verification

1. `dotnet build` -- no compilation errors
2. `dotnet test` -- existing tests pass
3. Manual testing in Portal:
   - **Jenny** (styles, no emphasis): Style dropdown populated, emphasis buttons disabled with notice
   - **Guy** (styles + emphasis): Style dropdown populated, all emphasis buttons enabled
   - **Libby** (no styles, no emphasis): Style dropdown disabled with "no styles" message, emphasis buttons disabled
   - **Xiaoxiao** (20+ styles): Style dropdown shows all applicable styles
4. Mode switching: Simple hides all, Standard shows styles, Pro shows all (emphasis conditionally disabled per voice)
5. Voice switching updates both StyleSelector and EmphasisToolbar reactively

## Architecture Notes

- The capability registry is static data (no runtime Azure API calls) -- must be manually updated when Azure changes voice capabilities
- A comment in `VoiceCapabilityProvider` references the Azure docs URL for periodic verification
- The API fallback (Step 3) + warning log ensures new curated voices degrade gracefully before being registered
