# SSML Support

This document describes Speech Synthesis Markup Language (SSML) features, which allow advanced voice control and styling for Text-to-Speech (TTS) messages in Discord voice channels and the web portal.

## Overview

SSML (Speech Synthesis Markup Language) is an XML-based markup language that provides fine-grained control over speech synthesis. Instead of plain text, SSML allows you to specify voice characteristics, emotional styles, pauses, emphasis, and pronunciation rules, resulting in more expressive and natural-sounding speech.

The bot integrates SSML with Azure Cognitive Services Speech to provide:

- **Emotional voice styles** - Cheerful, excited, sad, angry, and more
- **Style intensity control** - Adjust how strongly a style is applied (0.5 to 2.0)
- **Text emphasis** - Mark words or phrases for strong or moderate emphasis
- **Pauses and breaks** - Insert controlled silences between words or phrases
- **Pronunciation control** - Use `say-as` to ensure proper pronunciation of dates, numbers, currencies
- **Professional presets** - Pre-configured combinations of voice and style for common use cases
- **SSML validation** - Server-side validation with optional strict mode

Prerequisites: Your guild administrator must enable SSML in Audio Settings. See [Getting Started](#getting-started).

## Getting Started

### Enable SSML for Your Guild

SSML must be enabled per-guild by a user with administrator privileges.

**Steps:**

1. Go to the admin panel: `https://your-bot-domain/Guilds/AudioSettings/{guildId}`
2. Navigate to the **SSML Settings** section
3. Enable the **SSML Features** toggle
4. Configure optional settings (see [Guild Configuration](#guild-configuration) below)
5. Save settings

Guild administrators can also adjust the following:

| Setting | Description | Default |
|---------|-------------|---------|
| `SsmlEnabled` | Master toggle for SSML features | `false` |
| `StrictSsmlValidation` | Reject invalid SSML vs. fallback to plain text | `false` |
| `MaxSsmlComplexity` | Maximum nesting complexity score | `50` |
| `DefaultStyle` | Default speaking style for capable voices | `null` |
| `DefaultStyleDegree` | Default style intensity (0.5 to 2.0) | `1.0` |

### UI Modes

The TTS portal supports three modes for sending styled messages. Users can toggle between modes, and their preference persists in browser localStorage as `tts_mode_preference`.

#### Simple Mode (Default)

- No SSML features visible
- Standard plain text message input
- Best for basic TTS messages
- Voice, speed, and pitch controls only

#### Standard Mode

- Access to voice style presets
- Style selector dropdown (filtered by voice)
- Style intensity slider (0.5 to 2.0)
- Useful for applying consistent emotional tones

#### Pro Mode

- All Standard features plus:
- Text emphasis toolbar (select text, apply emphasis)
- Add pause controls with duration slider
- Say-as helpers for numbers, dates, currencies
- SSML preview panel (collapsible, shows generated SSML)
- Character count and complexity metrics

Switch modes via the button group in the TTS portal. Pro mode provides visual feedback for emphasis, pauses, and generates valid SSML automatically.

## Style Presets

Style presets are pre-configured combinations of voice name, speaking style, and intensity level. They provide quick access to common voice configurations without manual tuning.

### Emotional Presets

| Preset ID | Display Name | Voice | Style | Intensity | Description |
|-----------|-------------|-------|-------|-----------|-------------|
| `jenny-cheerful` | Cheerful Jenny | en-US-JennyNeural | cheerful | 1.5 | Upbeat and enthusiastic female voice |
| `guy-excited` | Excited Guy | en-US-GuyNeural | excited | 1.8 | Energetic and thrilled male voice |
| `aria-sad` | Sad Aria | en-US-AriaNeural | sad | 1.0 | Melancholic and somber female voice |
| `jenny-angry` | Angry Jenny | en-US-JennyNeural | angry | 1.5 | Frustrated and upset female voice |
| `davis-angry` | Angry Davis | en-US-DavisNeural | angry | 1.5 | Frustrated and stern male voice |

Featured presets (shown first in autocomplete): Cheerful Jenny, Excited Guy.

### Professional Presets

| Preset ID | Display Name | Voice | Style | Intensity | Description |
|-----------|-------------|-------|-------|-----------|-------------|
| `jenny-newscast` | Jenny Newscast | en-US-JennyNeural | newscast | 1.0 | Professional news broadcaster tone |
| `guy-newscast` | Guy Newscast | en-US-GuyNeural | newscast | 1.0 | Professional male news broadcaster |

Featured presets: Jenny Newscast, Guy Newscast.

### Character Presets

| Preset ID | Display Name | Voice | Style | Intensity | Description |
|-----------|-------------|-------|-------|-----------|-------------|
| `jenny-whispering` | Whispering Jenny | en-US-JennyNeural | whispering | 1.0 | Quiet, secretive whisper |
| `aria-terrified` | Terrified Aria | en-US-AriaNeural | terrified | 1.5 | Frightened and scared voice |
| `guy-shouting` | Shouting Guy | en-US-GuyNeural | shouting | 1.5 | Loud, yelling male voice |

### Assistant Presets

| Preset ID | Display Name | Voice | Style | Intensity | Description |
|-----------|-------------|-------|-------|-----------|-------------|
| `jenny-assistant` | Jenny Assistant | en-US-JennyNeural | assistant | 1.0 | Helpful AI assistant tone |
| `jenny-customerservice` | Jenny Customer Service | en-US-JennyNeural | customerservice | 1.0 | Polite customer support voice |

### Applying Presets

1. Switch to Standard or Pro mode in the TTS portal
2. Click a preset button in the presets panel
3. Voice, style, and intensity settings auto-populate
4. A toast notification confirms the preset was applied
5. Send your message - the preset will be applied to the SSML output

## Speaking Styles

Azure Neural voices support a variety of speaking styles. Not all voices support all styles. The dropdown in Standard/Pro modes automatically filters styles based on the selected voice.

### Style Compatibility Matrix

| Style | Jenny | Aria | Guy | Davis | Description |
|-------|-------|------|-----|-------|-------------|
| cheerful | Yes | Yes | Yes | Yes | Happy, upbeat tone |
| excited | Yes | Yes | Yes | Yes | Very enthusiastic |
| friendly | Yes | Yes | Yes | Yes | Warm, approachable |
| sad | Yes | Yes | Yes | Yes | Sorrowful |
| angry | Yes | Yes | Yes | Yes | Frustrated, upset |
| hopeful | Yes | Yes | Yes | Yes | Optimistic |
| whispering | Yes | Yes | Yes | Yes | Quiet, intimate |
| shouting | Yes | Yes | Yes | Yes | Loud, urgent |
| terrified | Yes | Yes | Yes | Yes | Scared, anxious |
| unfriendly | Yes | Yes | Yes | Yes | Cold, distant |
| newscast | Yes | No | Yes | No | Professional reporter |
| chat | Yes | Yes | No | Yes | Casual conversation |
| assistant | Yes | No | No | No | AI assistant tone |
| customerservice | Yes | Yes | No | No | Helpful support |
| empathetic | Yes | Yes | No | No | Understanding, caring |

**Voice Details:**

- **en-US-JennyNeural** - Most compatible voice (15 styles), friendly natural tone, good for general use
- **en-US-AriaNeural** - Expressive, conversational tone, supports most styles
- **en-US-GuyNeural** - Clear, professional male voice, wide style support
- **en-US-DavisNeural** - Warm, authoritative male voice, 11 styles including chat

### Style Intensity

Each style can be adjusted in intensity from 0.5 (subtle) to 2.0 (intense). The default is 1.0 (normal).

- **0.5** - Barely noticeable application of the style
- **1.0** - Normal style intensity (default)
- **1.5** - Strong application of the style
- **2.0** - Maximum style intensity

Intensity maps to the SSML `styledegree` attribute.

## Pro Mode Features

Pro mode provides advanced controls for crafting custom SSML-based messages.

### Text Emphasis

Select text in the message textarea to apply emphasis.

**How it works:**

1. Highlight text in the textarea
2. A floating toolbar appears above the selection
3. Click "Strong" (orange) or "Moderate" (blue) button
4. Text is highlighted with the corresponding color
5. The formatting persists in the textarea as visual markers

**Emphasis levels:**

- **Strong emphasis** - Orange highlight, maps to `<emphasis level="strong">`
- **Moderate emphasis** - Blue highlight, maps to `<emphasis level="moderate">`

**Keyboard shortcuts:**

- Ctrl+B - Apply strong emphasis to selected text
- Ctrl+E - Apply moderate emphasis to selected text

**SSML output examples:**

```xml
<speak version="1.0">
  <voice name="en-US-JennyNeural">
    This is <emphasis level="strong">very important</emphasis>.
    But this is <emphasis level="moderate">somewhat important</emphasis>.
  </voice>
</speak>
```

### Adding Pauses

Insert breaks and pauses into your message for dramatic effect or clarity.

**How it works:**

1. Click "Add Pause" button in the toolbar (or dedicated pause button)
2. A pause marker appears in the textarea as an inline gray element
3. Click the marker to edit duration
4. Slider: 100ms to 3,000ms
5. Quick presets: Short (250ms), Medium (500ms), Long (1,000ms)
6. Delete button to remove the pause

**SSML output example:**

```xml
<speak version="1.0">
  <voice name="en-US-JennyNeural">
    Here is a message.
    <break time="500ms"/>
    And here is the continuation after a half-second pause.
  </voice>
</speak>
```

### Say-As Helpers

Use `say-as` tags to control pronunciation of numbers, dates, currencies, and other special content.

**Available helpers:**

- **Say as Number** - Pronounces as cardinal number (123 = "one hundred twenty three")
- **Say as Date** - Reads date in spoken form (2025-01-28 = "January 28th, 2025")
- **Say as Telephone** - Reads as phone number (1-555-123-4567 = "1, 555, 1, 2, 3, 4, 5, 6, 7")

**SSML output examples:**

```xml
<speak version="1.0">
  <voice name="en-US-JennyNeural">
    I have <say-as interpret-as="cardinal">5</say-as> apples.
  </voice>
</speak>
```

### SSML Preview

Pro mode includes a collapsible SSML preview panel below the send button.

**Features:**

- **Read-only** - Shows the SSML that will be sent to Azure
- **Syntax highlighting** - XML tags highlighted for clarity
- **Auto-updates** - Reflects all settings and emphasis/pause changes in real-time
- **Copy button** - Copy entire SSML to clipboard with one click
- **Character count** - Shows SSML length vs. plain text length for reference
- **Collapsed by default** - Click to expand/collapse

The preview lets you verify the SSML structure before sending to ensure it's valid and matches your intentions.

## Discord Commands

### /tts-styled Command

Send a styled TTS message directly from Discord using a slash command.

**Command:** `/tts-styled`

**Parameters:**

| Parameter | Type | Required | Description | Max Length |
|-----------|------|----------|-------------|------------|
| `message` | string | Yes | Text to synthesize and play | 500 characters |
| `preset` | string | Yes | Preset name (with autocomplete) | N/A |

**Preconditions:**

- SSML must be enabled for the guild (`SsmlEnabled = true`)
- User must be in a voice channel
- Bot must have `Connect` and `Speak` permissions

**Autocomplete:**

The `preset` parameter provides autocomplete showing featured presets grouped by category (Emotional, Professional, Character, Assistant).

**Example usage:**

```
/tts-styled message:"Hello everyone! This is important news!" preset:guy-newscast
/tts-styled message:"Great job on that victory!" preset:jenny-cheerful
```

**Behavior:**

1. User invokes the command with a message and preset
2. System verifies preconditions (SSML enabled, in voice channel, etc.)
3. Bot auto-joins the user's voice channel if not already connected
4. SSML is generated from the preset and message
5. Azure Speech synthesizes the SSML
6. Audio is played in the voice channel
7. Message is logged to TTS history

## API Reference

### Portal API Endpoints

All endpoints require authentication. Guild-specific endpoints require `ModeratorAccess` policy.

#### Get Voice Capabilities

```
GET /api/portal-tts/voices/{voiceName}/capabilities
```

Retrieve the list of supported styles and roles for a specific voice.

**Parameters:**

| Name | Type | In | Description |
|------|------|----|----|
| `voiceName` | string | path | Voice identifier (e.g., `en-US-JennyNeural`) |

**Response (200 OK):**

```json
{
  "voiceName": "en-US-JennyNeural",
  "locale": "en-US",
  "gender": "Female",
  "supportedStyles": [
    "cheerful",
    "excited",
    "friendly",
    "sad",
    "angry",
    "hopeful",
    "whispering",
    "shouting",
    "terrified",
    "unfriendly",
    "newscast",
    "chat",
    "assistant",
    "customerservice",
    "empathetic"
  ]
}
```

#### Get Presets

```
GET /api/portal-tts/presets
```

Retrieve all available style presets.

**Response (200 OK):**

```json
{
  "presets": [
    {
      "id": "cheerful-jenny",
      "displayName": "Cheerful Jenny",
      "voiceName": "en-US-JennyNeural",
      "style": "cheerful",
      "styleDegree": 1.5,
      "category": "Emotional",
      "description": "Upbeat and enthusiastic female voice"
    },
    {
      "id": "newscast-guy",
      "displayName": "Newscast Guy",
      "voiceName": "en-US-GuyNeural",
      "style": "newscast",
      "styleDegree": 1.0,
      "category": "Professional",
      "description": "Professional news broadcaster (male)"
    }
  ]
}
```

#### Validate SSML

```
POST /api/portal-tts/validate-ssml
Content-Type: application/json
```

Validate SSML without synthesizing to audio. Useful for checking syntax before sending.

**Request body:**

```json
{
  "ssml": "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\">\n  <voice name=\"en-US-JennyNeural\" style=\"cheerful\">\n    Hello everyone!\n  </voice>\n</speak>"
}
```

**Response (200 OK):**

```json
{
  "isValid": true,
  "errors": [],
  "warnings": [],
  "detectedVoices": ["en-US-JennyNeural"],
  "estimatedDurationSeconds": 2.5,
  "plainTextLength": 16
}
```

**Response (400 Bad Request):**

```json
{
  "isValid": false,
  "errors": [
    "Voice 'en-US-InvalidNeural' is not supported"
  ],
  "warnings": [
    "Style 'invalid' is not compatible with voice 'en-US-JennyNeural'"
  ],
  "detectedVoices": [],
  "estimatedDurationSeconds": 0,
  "plainTextLength": 0
}
```

#### Build SSML

```
POST /api/portal-tts/build-ssml
Content-Type: application/json
```

Programmatically build SSML from structured segments.

**Request body:**

```json
{
  "language": "en-US",
  "segments": [
    {
      "voiceName": "en-US-JennyNeural",
      "style": "cheerful",
      "styleDegree": 1.5,
      "text": "Welcome to the server!"
    }
  ]
}
```

**Response (200 OK):**

```json
{
  "ssml": "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\">\n  <voice name=\"en-US-JennyNeural\" style=\"cheerful\" styledegree=\"1.5\">\n    Welcome to the server!\n  </voice>\n</speak>",
  "plainText": "Welcome to the server!"
}
```

#### Synthesize SSML

```
POST /api/portal/tts/{guildId}/synthesize-ssml
Content-Type: application/json
Authorization: Bearer {token}
```

Synthesize SSML to audio and play in the guild's voice channel.

**Parameters:**

| Name | Type | In | Description |
|------|------|----|----|
| `guildId` | ulong | path | Discord guild ID |

**Request body:**

```json
{
  "ssml": "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\">\n  <voice name=\"en-US-JennyNeural\" style=\"cheerful\" styledegree=\"1.5\">\n    Hello everyone!\n  </voice>\n</speak>"
}
```

**Response (200 OK):**

```json
{
  "success": true,
  "durationSeconds": 2.1,
  "voicesDetected": ["en-US-JennyNeural"]
}
```

**Response (400 Bad Request):**

```json
{
  "success": false,
  "error": "Invalid SSML: Style 'invalid' is not supported for voice 'en-US-JennyNeural'"
}
```

**Response (404 Not Found):**

```json
{
  "success": false,
  "error": "Guild not found or SSML not enabled"
}
```

**Response (429 Too Many Requests):**

```json
{
  "success": false,
  "error": "Rate limited. Please wait before sending another message."
}
```

## Troubleshooting

### SSML Not Available

**Symptom:** The SSML UI modes and presets are not visible in the portal.

**Causes:**

- SSML is not enabled for the guild
- User is not a guild member or authenticated

**Solutions:**

1. Verify SSML is enabled in guild Audio Settings:
   - Navigate to `/Guilds/AudioSettings/{guildId}`
   - Check that `SsmlEnabled` is enabled
   - Save if you made changes
2. If you're a guild member and authenticated, refresh the page
3. Clear browser cache if changes don't appear immediately

### Style Not Working

**Symptom:** Applied style doesn't affect voice output, or style is reset unexpectedly.

**Causes:**

- Selected voice doesn't support the chosen style
- Style incompatibility with voice
- SSML validation error

**Solutions:**

1. Check the style compatibility matrix above - not all voices support all styles
2. The dropdown automatically filters styles compatible with your selected voice
3. If switching voices, the style may reset to "(None)" if incompatible
4. Check SSML preview for syntax errors

### Validation Errors

**Symptom:** "Invalid SSML" error when sending a message.

**Handling modes:**

- **Strict mode enabled** (`StrictSsmlValidation = true`): Invalid SSML is rejected with detailed error messages
- **Strict mode disabled** (default): Falls back to plain text with a warning logged to the server

**Common validation errors:**

- **Malformed XML** - Unclosed tags, invalid characters, syntax errors
- **Unsupported voice** - Voice name doesn't exist in Azure Speech
- **Style incompatibility** - Selected style not compatible with voice
- **Document too long** - SSML exceeds complexity or length limits

**How to debug:**

1. Use the SSML preview panel to inspect the generated SSML
2. Check for unclosed tags and proper nesting
3. Verify voice and style names in the preview
4. If using manual SSML, validate against the [SSML specification](https://www.w3.org/TR/speech-synthesis11/)

### Complexity Limit Exceeded

**Symptom:** Error about SSML complexity being too high.

**Cause:** Message has too many nested elements or tags.

**Solutions:**

1. Simplify the message - use fewer emphasis or pause markers
2. Avoid deeply nested SSML structures
3. Ask your guild administrator to increase `MaxSsmlComplexity` in Audio Settings (default: 50)
4. Split complex messages into multiple shorter messages

### Voice Not Available

**Symptom:** Voice not appearing in dropdown, or getting "voice not found" errors.

**Possible causes:**

- Voice name is misspelled
- Voice is not available in your Azure region
- Azure Speech service is not configured

**Solutions:**

1. Check available voices by calling GET `/api/portal-tts/voices` API endpoint
2. Verify your Azure Speech service region includes the voice you want
3. Confirm Azure Speech is configured in the bot
4. Contact your server administrator if voices seem to be missing

### Audio Not Playing

**Symptom:** Message sends successfully but no audio is heard in voice channel.

**Possible causes:**

- Bot is not connected to a voice channel
- Bot lacks voice permissions
- Audio pipeline issue (FFmpeg, libsodium, libopus)

**Solutions:**

1. Verify bot is in the voice channel before sending SSML
2. Check bot has `Connect` and `Speak` permissions
3. Check [Audio Dependencies](audio-dependencies.md) for audio setup issues
4. Try a simple non-SSML TTS message to isolate the problem

## Best Practices

### When to Use Styles

**Announcements and alerts:**
- Use `newscast` style for formal, professional announcements
- Use `shouting` style sparingly for urgent alerts
- Example: Server maintenance notices, important updates

**Celebrations and positive messages:**
- Use `cheerful` or `excited` style for celebratory messages
- Use `hopeful` style for motivational content
- Example: Member milestones, event start announcements

**Storytelling and creative content:**
- Use varying styles across different parts (sad, then hopeful, then cheerful)
- Use `whispering` for intimate or mysterious moments
- Use `terrified` for dramatic effect in creative writing

**Customer service or help:**
- Use `customerservice` or `empathetic` style for support messages
- Maintains warm, helpful tone
- Example: Bot help messages, support announcements

### Performance Considerations

- SSML synthesis may take slightly longer than plain text TTS
- Complex SSML with many emphasis/pause markers takes longer to synthesize
- Use presets for consistent, quick-to-generate styled messages
- For frequent use of specific styles, create custom presets

### Accessibility Notes

- All SSML controls have proper ARIA labels for screen readers
- Text emphasis and pause markers are keyboard-accessible
- Mode toggle uses proper `role="tablist"` semantics for screen readers
- Toast notifications use `aria-live` regions for announcements
- Keyboard shortcuts (Ctrl+B, Ctrl+E) work in all compatible browsers

### Consistency Tips

- Create guild-specific style guidelines for consistency across messages
- Use presets to ensure everyone applies the same style configurations
- Document which styles work best for your guild's voice tone
- Test messages before using in important announcements

## Guild Configuration Reference

Guild administrators can configure SSML behavior in the Audio Settings page (`/Guilds/AudioSettings/{guildId}`).

| Setting | Type | Default | Range | Description |
|---------|------|---------|-------|-------------|
| `SsmlEnabled` | bool | false | N/A | Master toggle for SSML features. If disabled, portal only shows Simple mode |
| `StrictSsmlValidation` | bool | false | N/A | When true, invalid SSML is rejected. When false, falls back to plain text |
| `MaxSsmlComplexity` | int | 50 | 10-500 | Maximum SSML nesting complexity score. Higher allows more complex messages |
| `DefaultStyle` | string | null | N/A | Default speaking style for voices that support styles |
| `DefaultStyleDegree` | double | 1.0 | 0.5-2.0 | Default style intensity. Lower = subtle, higher = intense |

**Configuration examples:**

```json
{
  "SsmlEnabled": true,
  "StrictSsmlValidation": false,
  "MaxSsmlComplexity": 100,
  "DefaultStyle": "friendly",
  "DefaultStyleDegree": 1.2
}
```

## See Also

- [Text-to-Speech (TTS) Support](tts-support.md) - Core TTS feature documentation and Azure configuration
- [Audio Dependencies](audio-dependencies.md) - FFmpeg, libsodium, libopus setup required for all audio features
- [Design System](design-system.md) - UI component library and styling guidelines
- [Component API](component-api.md) - Razor component documentation
- [Azure Speech SSML Documentation](https://learn.microsoft.com/azure/ai-services/speech-service/speech-synthesis-markup) - Official SSML specification and examples
- [Voice Capability System](voice-capability-system.md) - Voice metadata and capability detection (if available)
