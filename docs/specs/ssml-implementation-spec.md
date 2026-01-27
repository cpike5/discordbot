# SSML (Speech Synthesis Markup Language) Support Implementation

## Overview

This specification defines the implementation of SSML support for the Discord bot's TTS system. The architecture extends the existing Azure TTS service to support advanced speech synthesis features including speaking styles, emotional tones, emphasis, breaks, and multi-voice conversations.

**Key Benefits:**

- **Enhanced expressiveness** - Support emotional styles (cheerful, sad, angry, whisper, etc.)
- **Precise control** - Fine-grained control over pauses, emphasis, and pronunciation
- **Multi-voice conversations** - Multiple speakers within a single message
- **Backward compatibility** - Existing plain text TTS remains unchanged
- **Voice capability awareness** - System knows which voices support which styles
- **Preset system** - Quick access to common voice+style combinations

## Current Architecture

```
TtsModule (Discord Commands)
     │
     ├─> ITtsService.SynthesizeSpeechAsync(text, TtsOptions)
     │        │
     │        └─> AzureTtsService.BuildSsml(text, options)
     │                 └─> Basic SSML with prosody only
     │
PortalTtsController (Web API)
     │
     └─> ITtsService.SynthesizeSpeechAsync(text, TtsOptions)

TtsOptions:
  - Voice: string
  - Speed: double (0.5-2.0)
  - Pitch: double (0.5-1.5)
  - Volume: double (0.0-1.0)

GuildTtsSettings:
  - DefaultVoice, DefaultSpeed, DefaultPitch, DefaultVolume
  - MaxMessageLength, RateLimitPerMinute
  - AutoPlayOnSend, AnnounceJoinsLeaves
```

## Enhanced Architecture

```
┌─────────────────────────────────────────────────────┐
│         TtsModule / PortalTtsController             │
│  - Accepts plain text OR SSML input                 │
│  - Specifies SynthesisMode                          │
└──────────────────┬──────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────┐
│              ITtsService                            │
│  - SynthesizeSpeechAsync(text, options, mode)       │
│  - GetVoiceCapabilities(voice)                      │
│  - GetStylePresets()                                │
└──────────────────┬──────────────────────────────────┘
                   │
     ┌─────────────┼──────────────┐
     │             │              │
     ▼             ▼              ▼
┌─────────┐  ┌──────────────┐  ┌──────────────┐
│ISsmlBuilder│  │ISsmlValidator│  │VoiceCapability│
│             │  │              │  │Metadata       │
│ - Build     │  │ - Validate   │  │              │
│   structured│  │   SSML       │  │ - Styles     │
│   SSML      │  │ - Sanitize   │  │ - Roles      │
│ - Prosody   │  │ - Fallback   │  │ - Features   │
│ - Styles    │  │              │  │              │
│ - Multi-    │  │              │  │              │
│   voice     │  │              │  │              │
└─────────────┘  └──────────────┘  └──────────────┘
     │                  │                  │
     └──────────────────┼──────────────────┘
                        │
              ┌─────────▼──────────┐
              │  AzureTtsService   │
              │  (builds final     │
              │   SSML)            │
              └────────────────────┘
```

## Core Interfaces

### ITtsService Extensions

**Location:** `src/DiscordBot.Core/Interfaces/ITtsService.cs`

```csharp
namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for text-to-speech synthesis.
/// </summary>
public interface ITtsService
{
    // EXISTING METHODS (unchanged)
    Task<Stream> SynthesizeSpeechAsync(string text, TtsOptions? options = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<VoiceInfo>> GetAvailableVoicesAsync(string? locale = "en-US", CancellationToken cancellationToken = default);
    bool IsConfigured { get; }
    IEnumerable<VoiceInfo> GetCuratedVoices();

    // NEW METHODS

    /// <summary>
    /// Synthesizes speech from text or SSML markup.
    /// </summary>
    /// <param name="input">Text or SSML content</param>
    /// <param name="options">TTS options (voice, speed, pitch, volume)</param>
    /// <param name="mode">Synthesis mode (PlainText or Ssml)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream containing synthesized audio in PCM format</returns>
    /// <exception cref="SsmlValidationException">Thrown when SSML markup is invalid</exception>
    Task<Stream> SynthesizeSpeechAsync(
        string input,
        TtsOptions? options,
        SynthesisMode mode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the capabilities of a specific voice (supported styles, roles, features).
    /// </summary>
    /// <param name="voiceName">Voice short name (e.g., "en-US-JennyNeural")</param>
    /// <returns>Voice capability information or null if voice not found</returns>
    Task<VoiceCapabilities?> GetVoiceCapabilitiesAsync(string voiceName);

    /// <summary>
    /// Gets predefined style presets for common voice+style combinations.
    /// </summary>
    /// <returns>Collection of style presets</returns>
    IEnumerable<StylePreset> GetStylePresets();

    /// <summary>
    /// Validates SSML markup without performing synthesis.
    /// </summary>
    /// <param name="ssml">SSML markup to validate</param>
    /// <returns>Validation result with errors if invalid</returns>
    SsmlValidationResult ValidateSsml(string ssml);
}
```

### ISsmlBuilder

**Location:** `src/DiscordBot.Core/Interfaces/ISsmlBuilder.cs`

```csharp
namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Builder interface for constructing SSML markup programmatically.
/// Provides a fluent API for creating complex SSML documents.
/// </summary>
public interface ISsmlBuilder
{
    /// <summary>
    /// Starts a new SSML document.
    /// </summary>
    ISsmlBuilder BeginDocument(string language = "en-US");

    /// <summary>
    /// Adds a voice section with the specified voice.
    /// </summary>
    ISsmlBuilder WithVoice(string voiceName);

    /// <summary>
    /// Adds prosody controls (rate, pitch, volume, contour).
    /// </summary>
    ISsmlBuilder WithProsody(
        double? rate = null,
        double? pitch = null,
        double? volume = null,
        string? contour = null);

    /// <summary>
    /// Adds speaking style (requires style-capable voice like Jenny, Aria, Guy, Davis).
    /// </summary>
    /// <param name="style">Style name (cheerful, sad, angry, excited, friendly, terrified, shouting, unfriendly, whispering, hopeful)</param>
    /// <param name="degree">Style intensity (0.01-2.0, default 1.0)</param>
    ISsmlBuilder WithStyle(string style, double degree = 1.0);

    /// <summary>
    /// Adds role/personality (e.g., "YoungAdultFemale", "OlderAdultMale").
    /// </summary>
    ISsmlBuilder WithRole(string role);

    /// <summary>
    /// Adds plain text content (automatically escaped).
    /// </summary>
    ISsmlBuilder AddText(string text);

    /// <summary>
    /// Adds a break/pause.
    /// </summary>
    /// <param name="duration">Duration (e.g., "500ms", "1s", "weak", "medium", "strong")</param>
    ISsmlBuilder AddBreak(string duration);

    /// <summary>
    /// Adds emphasis to text.
    /// </summary>
    /// <param name="text">Text to emphasize</param>
    /// <param name="level">Emphasis level (reduced, none, moderate, strong)</param>
    ISsmlBuilder AddEmphasis(string text, string level = "moderate");

    /// <summary>
    /// Adds say-as interpretation.
    /// </summary>
    /// <param name="text">Text to interpret</param>
    /// <param name="interpretAs">Interpretation type (date, time, telephone, cardinal, ordinal, currency, etc.)</param>
    /// <param name="format">Optional format specifier</param>
    ISsmlBuilder AddSayAs(string text, string interpretAs, string? format = null);

    /// <summary>
    /// Adds phoneme pronunciation.
    /// </summary>
    /// <param name="text">Text to pronounce</param>
    /// <param name="alphabet">Phonetic alphabet (ipa or x-sampa)</param>
    /// <param name="ph">Phonetic spelling</param>
    ISsmlBuilder AddPhoneme(string text, string alphabet, string ph);

    /// <summary>
    /// Adds word substitution.
    /// </summary>
    /// <param name="alias">Text to speak</param>
    /// <param name="text">Text to display</param>
    ISsmlBuilder AddSubstitution(string alias, string text);

    /// <summary>
    /// Closes the current voice section.
    /// </summary>
    ISsmlBuilder EndVoice();

    /// <summary>
    /// Closes the current prosody section.
    /// </summary>
    ISsmlBuilder EndProsody();

    /// <summary>
    /// Closes the current style section.
    /// </summary>
    ISsmlBuilder EndStyle();

    /// <summary>
    /// Builds the final SSML string.
    /// </summary>
    /// <returns>Valid SSML markup</returns>
    string Build();

    /// <summary>
    /// Resets the builder to start a new document.
    /// </summary>
    ISsmlBuilder Reset();
}
```

### ISsmlValidator

**Location:** `src/DiscordBot.Core/Interfaces/ISsmlValidator.cs`

```csharp
namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Validator for SSML markup. Checks syntax, structure, and Azure-specific constraints.
/// </summary>
public interface ISsmlValidator
{
    /// <summary>
    /// Validates SSML markup.
    /// </summary>
    /// <param name="ssml">SSML markup to validate</param>
    /// <returns>Validation result</returns>
    SsmlValidationResult Validate(string ssml);

    /// <summary>
    /// Attempts to sanitize/fix common SSML issues.
    /// </summary>
    /// <param name="ssml">Potentially invalid SSML</param>
    /// <returns>Sanitized SSML or original if no fixes possible</returns>
    string Sanitize(string ssml);

    /// <summary>
    /// Checks if a voice supports a specific style.
    /// </summary>
    /// <param name="voiceName">Voice short name</param>
    /// <param name="style">Style name</param>
    /// <returns>True if voice supports the style</returns>
    bool IsStyleSupported(string voiceName, string style);

    /// <summary>
    /// Extracts plain text from SSML (strips all markup).
    /// </summary>
    /// <param name="ssml">SSML markup</param>
    /// <returns>Plain text content</returns>
    string ExtractPlainText(string ssml);
}
```

## New DTOs and Models

### SynthesisMode Enum

**Location:** `src/DiscordBot.Core/Enums/SynthesisMode.cs`

```csharp
namespace DiscordBot.Core.Enums;

/// <summary>
/// Specifies the input format for TTS synthesis.
/// </summary>
public enum SynthesisMode
{
    /// <summary>
    /// Plain text input (default). System will wrap in basic SSML with prosody.
    /// </summary>
    PlainText = 0,

    /// <summary>
    /// SSML markup input. System will validate and pass directly to Azure.
    /// </summary>
    Ssml = 1,

    /// <summary>
    /// Auto-detect mode. System attempts to detect SSML by looking for &lt;speak&gt; tag.
    /// </summary>
    Auto = 2
}
```

### VoiceCapabilities

**Location:** `src/DiscordBot.Core/Models/VoiceCapabilities.cs`

```csharp
namespace DiscordBot.Core.Models;

/// <summary>
/// Describes the capabilities of a specific TTS voice.
/// </summary>
public class VoiceCapabilities
{
    /// <summary>
    /// Voice short name (e.g., "en-US-JennyNeural").
    /// </summary>
    public required string VoiceName { get; init; }

    /// <summary>
    /// Display name for the voice.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Locale code (e.g., "en-US").
    /// </summary>
    public required string Locale { get; init; }

    /// <summary>
    /// Gender (Female, Male, Neutral).
    /// </summary>
    public required string Gender { get; init; }

    /// <summary>
    /// Supported speaking styles (cheerful, sad, angry, etc.).
    /// Empty if voice doesn't support styles.
    /// </summary>
    public IReadOnlyList<string> SupportedStyles { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Supported roles (YoungAdultFemale, OlderAdultMale, etc.).
    /// Empty if voice doesn't support roles.
    /// </summary>
    public IReadOnlyList<string> SupportedRoles { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Whether voice supports multilingual content.
    /// </summary>
    public bool SupportsMultilingual { get; init; }

    /// <summary>
    /// Whether voice is premium/paid tier.
    /// </summary>
    public bool IsPremium { get; init; }

    /// <summary>
    /// Voice type classification (neural, standard, wavenet, etc.).
    /// </summary>
    public string VoiceType { get; init; } = "Neural";

    /// <summary>
    /// Sample rate in Hz (typically 48000 for neural voices).
    /// </summary>
    public int SampleRate { get; init; } = 48000;
}
```

### StylePreset

**Location:** `src/DiscordBot.Core/Models/StylePreset.cs`

```csharp
namespace DiscordBot.Core.Models;

/// <summary>
/// Predefined combination of voice and style for quick access.
/// </summary>
public class StylePreset
{
    /// <summary>
    /// Unique preset identifier (e.g., "jenny-cheerful", "guy-angry").
    /// </summary>
    public required string PresetId { get; init; }

    /// <summary>
    /// Display name for UI (e.g., "Cheerful Jenny", "Angry Guy").
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Voice short name.
    /// </summary>
    public required string VoiceName { get; init; }

    /// <summary>
    /// Style name.
    /// </summary>
    public required string Style { get; init; }

    /// <summary>
    /// Style degree (0.01-2.0).
    /// </summary>
    public double StyleDegree { get; init; } = 1.0;

    /// <summary>
    /// Optional prosody overrides.
    /// </summary>
    public TtsOptions? ProsodyOptions { get; init; }

    /// <summary>
    /// Description/use case for this preset.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Category for grouping (e.g., "Emotional", "Professional", "Character").
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Whether this is a featured/popular preset.
    /// </summary>
    public bool IsFeatured { get; init; }
}
```

### SsmlValidationResult

**Location:** `src/DiscordBot.Core/Models/SsmlValidationResult.cs`

```csharp
namespace DiscordBot.Core.Models;

/// <summary>
/// Result of SSML validation.
/// </summary>
public class SsmlValidationResult
{
    /// <summary>
    /// Whether the SSML is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Validation errors if invalid.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Validation warnings (non-critical issues).
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Detected voices used in the SSML.
    /// </summary>
    public IReadOnlyList<string> DetectedVoices { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Estimated audio duration in seconds (if calculable).
    /// </summary>
    public double? EstimatedDurationSeconds { get; init; }

    /// <summary>
    /// Character count of plain text content (excludes markup).
    /// </summary>
    public int PlainTextLength { get; init; }
}
```

### SsmlValidationException

**Location:** `src/DiscordBot.Core/Exceptions/SsmlValidationException.cs`

```csharp
namespace DiscordBot.Core.Exceptions;

/// <summary>
/// Exception thrown when SSML validation fails.
/// </summary>
public class SsmlValidationException : Exception
{
    /// <summary>
    /// Validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Invalid SSML content.
    /// </summary>
    public string InvalidSsml { get; }

    public SsmlValidationException(string message, IReadOnlyList<string> errors, string invalidSsml)
        : base(message)
    {
        Errors = errors;
        InvalidSsml = invalidSsml;
    }
}
```

## Database Changes

### GuildTtsSettings Extensions

**Location:** `src/DiscordBot.Core/Entities/GuildTtsSettings.cs`

Add new properties:

```csharp
/// <summary>
/// Whether SSML mode is enabled for this guild (requires admin permission).
/// </summary>
public bool SsmlEnabled { get; set; } = false;

/// <summary>
/// Whether to validate SSML strictly (reject on any error) or permissively (fallback to plain text).
/// </summary>
public bool StrictSsmlValidation { get; set; } = false;

/// <summary>
/// Maximum SSML complexity score to prevent abuse (based on nested elements).
/// </summary>
public int MaxSsmlComplexity { get; set; } = 50;

/// <summary>
/// Default style for style-capable voices (null = no style).
/// </summary>
public string? DefaultStyle { get; set; }

/// <summary>
/// Default style degree (0.01-2.0).
/// </summary>
public double DefaultStyleDegree { get; set; } = 1.0;
```

### Migration

**Migration Name:** `AddSsmlSupportToGuildTtsSettings`

```csharp
migrationBuilder.AddColumn<bool>(
    name: "SsmlEnabled",
    table: "GuildTtsSettings",
    type: "INTEGER",
    nullable: false,
    defaultValue: false);

migrationBuilder.AddColumn<bool>(
    name: "StrictSsmlValidation",
    table: "GuildTtsSettings",
    type: "INTEGER",
    nullable: false,
    defaultValue: false);

migrationBuilder.AddColumn<int>(
    name: "MaxSsmlComplexity",
    table: "GuildTtsSettings",
    type: "INTEGER",
    nullable: false,
    defaultValue: 50);

migrationBuilder.AddColumn<string>(
    name: "DefaultStyle",
    table: "GuildTtsSettings",
    type: "TEXT",
    nullable: true);

migrationBuilder.AddColumn<double>(
    name: "DefaultStyleDegree",
    table: "GuildTtsSettings",
    type: "REAL",
    nullable: false,
    defaultValue: 1.0);
```

## Configuration Extensions

### Azure Speech Configuration

**Location:** `appsettings.json` / User Secrets

Add new section under `AzureSpeech`:

```json
{
  "AzureSpeech": {
    "SubscriptionKey": "***",
    "Region": "eastus",
    "Ssml": {
      "EnableValidation": true,
      "StrictMode": false,
      "MaxComplexityScore": 50,
      "MaxDocumentLength": 5000,
      "EnableSanitization": true,
      "EnableStylePresets": true,
      "CacheVoiceCapabilities": true,
      "CacheDurationMinutes": 1440
    }
  }
}
```

### AzureSpeechSsmlOptions

**Location:** `src/DiscordBot.Core/Configuration/AzureSpeechSsmlOptions.cs`

```csharp
namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for SSML support in Azure Speech service.
/// </summary>
public class AzureSpeechSsmlOptions
{
    /// <summary>
    /// Whether to validate SSML before sending to Azure.
    /// </summary>
    public bool EnableValidation { get; set; } = true;

    /// <summary>
    /// Whether to reject invalid SSML (true) or fallback to plain text (false).
    /// </summary>
    public bool StrictMode { get; set; } = false;

    /// <summary>
    /// Maximum SSML complexity score (nested elements).
    /// </summary>
    public int MaxComplexityScore { get; set; } = 50;

    /// <summary>
    /// Maximum SSML document length in characters.
    /// </summary>
    public int MaxDocumentLength { get; set; } = 5000;

    /// <summary>
    /// Whether to attempt automatic sanitization of invalid SSML.
    /// </summary>
    public bool EnableSanitization { get; set; } = true;

    /// <summary>
    /// Whether to enable style presets feature.
    /// </summary>
    public bool EnableStylePresets { get; set; } = true;

    /// <summary>
    /// Whether to cache voice capabilities metadata.
    /// </summary>
    public bool CacheVoiceCapabilities { get; set; } = true;

    /// <summary>
    /// Duration to cache voice capabilities (minutes).
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 1440; // 24 hours
}
```

## Implementation Components

### SsmlBuilder

**Location:** `src/DiscordBot.Bot/Services/Tts/SsmlBuilder.cs`

Implements `ISsmlBuilder` with fluent API for constructing SSML.

**Key responsibilities:**
- Build valid SSML documents programmatically
- Automatic XML escaping
- Nested element tracking (voice, prosody, style, emphasis)
- Validation during build process
- Support for all Azure SSML elements

**Example usage:**
```csharp
var ssml = ssmlBuilder
    .BeginDocument("en-US")
    .WithVoice("en-US-JennyNeural")
    .WithStyle("cheerful", 1.5)
    .AddText("Hello! ")
    .AddEmphasis("This is exciting!", "strong")
    .AddBreak("500ms")
    .AddText("Let me tell you more.")
    .EndStyle()
    .EndVoice()
    .Build();
```

### SsmlValidator

**Location:** `src/DiscordBot.Bot/Services/Tts/SsmlValidator.cs`

Implements `ISsmlValidator` for validation and sanitization.

**Key responsibilities:**
- XML schema validation
- Azure-specific constraint checking
- Voice capability validation (does this voice support this style?)
- Complexity score calculation
- Plain text extraction
- Sanitization of common issues (unclosed tags, invalid characters, etc.)

**Validation rules:**
1. Must be well-formed XML
2. Root element must be `<speak>`
3. Voice names must exist and be valid
4. Styles must be supported by the specified voice
5. Prosody values must be within Azure limits
6. Break durations must be valid formats
7. No more than 3 levels of nested elements (configurable)
8. Total document length under limit
9. No script injection attempts

### VoiceCapabilityProvider

**Location:** `src/DiscordBot.Bot/Services/Tts/VoiceCapabilityProvider.cs`

Provides voice capability metadata.

**Key responsibilities:**
- Maintain registry of voice capabilities
- Support for dynamic capability detection (via Azure API)
- Caching of capability data
- Query interface for checking feature support

**Capability data:**
```csharp
private static readonly Dictionary<string, VoiceCapabilities> _knownCapabilities = new()
{
    ["en-US-JennyNeural"] = new()
    {
        VoiceName = "en-US-JennyNeural",
        DisplayName = "Jenny",
        Locale = "en-US",
        Gender = "Female",
        SupportedStyles = new[]
        {
            "angry", "assistant", "chat", "cheerful", "customerservice",
            "empathetic", "excited", "friendly", "hopeful", "newscast",
            "sad", "shouting", "terrified", "unfriendly", "whispering"
        },
        SupportedRoles = Array.Empty<string>(),
        SupportsMultilingual = true,
        IsPremium = false,
        VoiceType = "Neural"
    },
    ["en-US-AriaNeural"] = new()
    {
        VoiceName = "en-US-AriaNeural",
        DisplayName = "Aria",
        Locale = "en-US",
        Gender = "Female",
        SupportedStyles = new[]
        {
            "angry", "chat", "cheerful", "empathetic", "excited",
            "friendly", "hopeful", "sad", "shouting", "terrified",
            "unfriendly", "whispering"
        },
        SupportedRoles = Array.Empty<string>(),
        SupportsMultilingual = false,
        IsPremium = false,
        VoiceType = "Neural"
    },
    ["en-US-GuyNeural"] = new()
    {
        VoiceName = "en-US-GuyNeural",
        DisplayName = "Guy",
        Locale = "en-US",
        Gender = "Male",
        SupportedStyles = new[]
        {
            "angry", "cheerful", "excited", "friendly", "hopeful",
            "newscast", "sad", "shouting", "terrified", "unfriendly",
            "whispering"
        },
        SupportedRoles = Array.Empty<string>(),
        SupportsMultilingual = false,
        IsPremium = false,
        VoiceType = "Neural"
    },
    ["en-US-DavisNeural"] = new()
    {
        VoiceName = "en-US-DavisNeural",
        DisplayName = "Davis",
        Locale = "en-US",
        Gender = "Male",
        SupportedStyles = new[]
        {
            "angry", "chat", "cheerful", "excited", "friendly",
            "hopeful", "sad", "shouting", "terrified", "unfriendly",
            "whispering"
        },
        SupportedRoles = Array.Empty<string>(),
        SupportsMultilingual = false,
        IsPremium = false,
        VoiceType = "Neural"
    }
    // Add other voices without style support with empty SupportedStyles array
};
```

### StylePresetProvider

**Location:** `src/DiscordBot.Bot/Services/Tts/StylePresetProvider.cs`

Provides predefined style presets.

**Key responsibilities:**
- Maintain curated list of useful voice+style combinations
- Support for custom/user-defined presets (future)
- Category organization

**Default presets:**
```csharp
private static readonly List<StylePreset> _defaultPresets = new()
{
    // Emotional - Female
    new()
    {
        PresetId = "jenny-cheerful",
        DisplayName = "Cheerful Jenny",
        VoiceName = "en-US-JennyNeural",
        Style = "cheerful",
        StyleDegree = 1.5,
        Description = "Upbeat and enthusiastic female voice",
        Category = "Emotional",
        IsFeatured = true
    },
    new()
    {
        PresetId = "aria-sad",
        DisplayName = "Sad Aria",
        VoiceName = "en-US-AriaNeural",
        Style = "sad",
        StyleDegree = 1.0,
        Description = "Melancholic and somber female voice",
        Category = "Emotional",
        IsFeatured = false
    },
    new()
    {
        PresetId = "jenny-angry",
        DisplayName = "Angry Jenny",
        VoiceName = "en-US-JennyNeural",
        Style = "angry",
        StyleDegree = 1.5,
        Description = "Frustrated and upset female voice",
        Category = "Emotional",
        IsFeatured = false
    },

    // Emotional - Male
    new()
    {
        PresetId = "guy-excited",
        DisplayName = "Excited Guy",
        VoiceName = "en-US-GuyNeural",
        Style = "excited",
        StyleDegree = 1.8,
        Description = "Energetic and thrilled male voice",
        Category = "Emotional",
        IsFeatured = true
    },
    new()
    {
        PresetId = "davis-angry",
        DisplayName = "Angry Davis",
        VoiceName = "en-US-DavisNeural",
        Style = "angry",
        StyleDegree = 1.5,
        Description = "Frustrated and stern male voice",
        Category = "Emotional",
        IsFeatured = false
    },

    // Professional
    new()
    {
        PresetId = "jenny-newscast",
        DisplayName = "Jenny Newscast",
        VoiceName = "en-US-JennyNeural",
        Style = "newscast",
        StyleDegree = 1.0,
        Description = "Professional news broadcaster tone",
        Category = "Professional",
        IsFeatured = true
    },
    new()
    {
        PresetId = "guy-newscast",
        DisplayName = "Guy Newscast",
        VoiceName = "en-US-GuyNeural",
        Style = "newscast",
        StyleDegree = 1.0,
        Description = "Professional male news broadcaster",
        Category = "Professional",
        IsFeatured = true
    },

    // Character Voices
    new()
    {
        PresetId = "jenny-whispering",
        DisplayName = "Whispering Jenny",
        VoiceName = "en-US-JennyNeural",
        Style = "whispering",
        StyleDegree = 1.0,
        Description = "Quiet, secretive whisper",
        Category = "Character",
        IsFeatured = false
    },
    new()
    {
        PresetId = "aria-terrified",
        DisplayName = "Terrified Aria",
        VoiceName = "en-US-AriaNeural",
        Style = "terrified",
        StyleDegree = 1.5,
        Description = "Frightened and scared voice",
        Category = "Character",
        IsFeatured = false
    },
    new()
    {
        PresetId = "guy-shouting",
        DisplayName = "Shouting Guy",
        VoiceName = "en-US-GuyNeural",
        Style = "shouting",
        StyleDegree = 1.5,
        Description = "Loud, yelling male voice",
        Category = "Character",
        IsFeatured = false
    },

    // Assistant/Helper
    new()
    {
        PresetId = "jenny-assistant",
        DisplayName = "Jenny Assistant",
        VoiceName = "en-US-JennyNeural",
        Style = "assistant",
        StyleDegree = 1.0,
        Description = "Helpful AI assistant tone",
        Category = "Assistant",
        IsFeatured = true
    },
    new()
    {
        PresetId = "jenny-customerservice",
        DisplayName = "Jenny Customer Service",
        VoiceName = "en-US-JennyNeural",
        Style = "customerservice",
        StyleDegree = 1.0,
        Description = "Polite customer support voice",
        Category = "Assistant",
        IsFeatured = false
    }
};
```

## API Changes

### PortalTtsController Extensions

**Location:** `src/DiscordBot.Bot/Controllers/PortalTtsController.cs`

Add new endpoints:

```csharp
/// <summary>
/// Gets voice capabilities including supported styles and roles.
/// </summary>
/// <param name="voiceName">Voice short name</param>
[HttpGet("voices/{voiceName}/capabilities")]
[ProducesResponseType(typeof(VoiceCapabilities), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetVoiceCapabilities([FromRoute] string voiceName)
{
    var capabilities = await _ttsService.GetVoiceCapabilitiesAsync(voiceName);

    if (capabilities == null)
    {
        return NotFound(new { error = "Voice not found" });
    }

    return Ok(capabilities);
}

/// <summary>
/// Gets all available style presets.
/// </summary>
[HttpGet("presets")]
[ProducesResponseType(typeof(IEnumerable<StylePreset>), StatusCodes.Status200OK)]
public IActionResult GetStylePresets()
{
    var presets = _ttsService.GetStylePresets();
    return Ok(presets);
}

/// <summary>
/// Validates SSML without performing synthesis.
/// </summary>
/// <param name="request">SSML validation request</param>
[HttpPost("validate-ssml")]
[ProducesResponseType(typeof(SsmlValidationResult), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public IActionResult ValidateSsml([FromBody] SsmlValidationRequest request)
{
    if (string.IsNullOrWhiteSpace(request.Ssml))
    {
        return BadRequest(new { error = "SSML is required" });
    }

    var result = _ttsService.ValidateSsml(request.Ssml);
    return Ok(result);
}

/// <summary>
/// Synthesizes speech from SSML markup (requires SSML enabled for guild).
/// </summary>
/// <param name="guildId">Guild ID</param>
/// <param name="request">SSML synthesis request</param>
[HttpPost("guilds/{guildId}/synthesize-ssml")]
[Authorize(Policy = AuthorizationPolicies.ModeratorAccess)]
[ProducesResponseType(typeof(TtsSynthesisResponse), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public async Task<IActionResult> SynthesizeSsml(
    [FromRoute] ulong guildId,
    [FromBody] SsmlSynthesisRequest request)
{
    // Check if SSML is enabled for guild
    var settings = await _ttsSettingsService.GetOrCreateSettingsAsync(guildId);

    if (!settings.SsmlEnabled)
    {
        return StatusCode(StatusCodes.Status403Forbidden,
            new { error = "SSML mode is not enabled for this guild" });
    }

    // Validate SSML
    var validationResult = _ttsService.ValidateSsml(request.Ssml);

    if (!validationResult.IsValid)
    {
        if (settings.StrictSsmlValidation)
        {
            return BadRequest(new
            {
                error = "Invalid SSML markup",
                errors = validationResult.Errors
            });
        }
        else
        {
            // Log warning but attempt synthesis anyway
            _logger.LogWarning(
                "SSML validation failed but continuing in permissive mode. Errors: {Errors}",
                string.Join(", ", validationResult.Errors));
        }
    }

    // Check complexity limit
    var complexity = CalculateComplexity(request.Ssml);
    if (complexity > settings.MaxSsmlComplexity)
    {
        return BadRequest(new
        {
            error = $"SSML complexity ({complexity}) exceeds limit ({settings.MaxSsmlComplexity})"
        });
    }

    // Perform synthesis
    try
    {
        var audioStream = await _ttsService.SynthesizeSpeechAsync(
            request.Ssml,
            options: null, // SSML includes all options
            mode: SynthesisMode.Ssml);

        // Store audio and return response (similar to existing synthesize endpoint)
        var audioId = Guid.NewGuid().ToString();
        await _audioCache.StoreAsync(audioId, audioStream);

        return Ok(new TtsSynthesisResponse
        {
            AudioId = audioId,
            DurationSeconds = validationResult.EstimatedDurationSeconds,
            VoicesUsed = validationResult.DetectedVoices.ToList()
        });
    }
    catch (SsmlValidationException ex)
    {
        return BadRequest(new
        {
            error = "SSML synthesis failed",
            errors = ex.Errors
        });
    }
}

/// <summary>
/// Builds SSML from structured request (easier than writing raw SSML).
/// </summary>
/// <param name="request">Structured SSML build request</param>
[HttpPost("build-ssml")]
[ProducesResponseType(typeof(SsmlBuildResponse), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public IActionResult BuildSsml([FromBody] SsmlBuildRequest request)
{
    try
    {
        var ssml = BuildSsmlFromRequest(request);
        var validationResult = _ttsService.ValidateSsml(ssml);

        return Ok(new SsmlBuildResponse
        {
            Ssml = ssml,
            IsValid = validationResult.IsValid,
            Errors = validationResult.Errors.ToList(),
            Warnings = validationResult.Warnings.ToList()
        });
    }
    catch (Exception ex)
    {
        return BadRequest(new { error = ex.Message });
    }
}
```

### Request/Response DTOs

**Location:** `src/DiscordBot.Core/DTOs/`

```csharp
// SsmlValidationRequest.cs
public class SsmlValidationRequest
{
    public required string Ssml { get; init; }
}

// SsmlSynthesisRequest.cs
public class SsmlSynthesisRequest
{
    public required string Ssml { get; init; }
    public bool PlayInVoiceChannel { get; init; } = false;
}

// SsmlBuildRequest.cs
public class SsmlBuildRequest
{
    public required string Language { get; init; } = "en-US";
    public required List<SsmlSegment> Segments { get; init; }
}

public class SsmlSegment
{
    public required string VoiceName { get; init; }
    public string? Style { get; init; }
    public double? StyleDegree { get; init; }
    public double? Rate { get; init; }
    public double? Pitch { get; init; }
    public double? Volume { get; init; }
    public required string Text { get; init; }
    public List<SsmlElement>? Elements { get; init; }
}

public class SsmlElement
{
    public required string Type { get; init; } // "break", "emphasis", "say-as", etc.
    public Dictionary<string, string>? Attributes { get; init; }
    public string? Content { get; init; }
}

// SsmlBuildResponse.cs
public class SsmlBuildResponse
{
    public required string Ssml { get; init; }
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

// TtsSynthesisResponse.cs (extend existing)
public class TtsSynthesisResponse
{
    public required string AudioId { get; init; }
    public double? DurationSeconds { get; init; }
    public List<string>? VoicesUsed { get; init; } // NEW: for multi-voice SSML
}
```

## Discord Command Extensions

### TtsModule Extensions

**Location:** `src/DiscordBot.Bot/Commands/TtsModule.cs`

Add new slash command for style presets:

```csharp
/// <summary>
/// Converts text to speech using a style preset.
/// </summary>
/// <param name="message">Text to speak</param>
/// <param name="preset">Style preset ID</param>
[SlashCommand("tts-styled", "Convert text to speech with emotional style")]
[RequireVoiceChannel]
public async Task TtsStyledAsync(
    [Summary("message", "The text to speak (max 500 characters)")]
    [MaxLength(500)]
    string message,
    [Summary("preset", "Voice style preset")]
    [Autocomplete(typeof(StylePresetAutocompleteHandler))]
    string preset)
{
    var guildId = Context.Guild.Id;
    var userId = Context.User.Id;

    // Check if SSML is enabled
    var settings = await _ttsSettingsService.GetOrCreateSettingsAsync(guildId);

    if (!settings.SsmlEnabled)
    {
        var notEnabledEmbed = new EmbedBuilder()
            .WithTitle("Styled TTS Not Available")
            .WithDescription("Styled TTS requires SSML mode to be enabled by an administrator.")
            .WithColor(Color.Orange)
            .WithCurrentTimestamp()
            .Build();

        await RespondAsync(embed: notEnabledEmbed, ephemeral: true);
        return;
    }

    // Get preset
    var stylePresets = _ttsService.GetStylePresets();
    var selectedPreset = stylePresets.FirstOrDefault(p => p.PresetId == preset);

    if (selectedPreset == null)
    {
        await RespondAsync("Invalid preset selected.", ephemeral: true);
        return;
    }

    // Build SSML using preset
    var ssml = _ssmlBuilder
        .BeginDocument("en-US")
        .WithVoice(selectedPreset.VoiceName)
        .WithStyle(selectedPreset.Style, selectedPreset.StyleDegree);

    // Apply prosody if preset includes it
    if (selectedPreset.ProsodyOptions != null)
    {
        ssml.WithProsody(
            selectedPreset.ProsodyOptions.Speed,
            selectedPreset.ProsodyOptions.Pitch,
            selectedPreset.ProsodyOptions.Volume);
    }

    ssml.AddText(message)
        .EndStyle()
        .EndVoice();

    var ssmlText = ssml.Build();

    // Synthesize and play (similar to existing TtsAsync logic but with SSML mode)
    await DeferAsync(ephemeral: true);

    // ... (join voice channel, synthesize, play - same as TtsAsync)

    var audioStream = await _ttsService.SynthesizeSpeechAsync(
        ssmlText,
        options: null,
        mode: SynthesisMode.Ssml);

    // ... (play audio, log history, send response)
}
```

### Autocomplete Handler

**Location:** `src/DiscordBot.Bot/Autocomplete/StylePresetAutocompleteHandler.cs`

```csharp
public class StylePresetAutocompleteHandler : AutocompleteHandler
{
    private readonly ITtsService _ttsService;

    public StylePresetAutocompleteHandler(ITtsService ttsService)
    {
        _ttsService = ttsService;
    }

    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var presets = _ttsService.GetStylePresets()
            .Where(p => p.IsFeatured) // Show featured presets first
            .OrderBy(p => p.Category)
            .ThenBy(p => p.DisplayName)
            .Take(25); // Discord limit

        var results = presets.Select(p => new AutocompleteResult(
            name: $"{p.Category}: {p.DisplayName}",
            value: p.PresetId));

        return AutocompletionResult.FromSuccess(results);
    }
}
```

## UI Changes

### TTS Portal Extensions

**Location:** `src/DiscordBot.Bot/Pages/Guilds/TextToSpeech/Index.cshtml`

Add new UI sections:

1. **Mode Toggle**: Switch between Plain Text and SSML modes
2. **Style Selector**: Dropdown for style presets (only shown if SSML enabled)
3. **SSML Editor**: Syntax-highlighted textarea for raw SSML input
4. **Voice Capability Indicator**: Show which styles current voice supports
5. **SSML Builder**: Visual builder for constructing SSML without writing markup

**Wireframe structure:**

```
┌──────────────────────────────────────────────────┐
│ TTS Portal                                       │
├──────────────────────────────────────────────────┤
│                                                  │
│ Mode: [Plain Text ▼] | [SSML] (if enabled)      │
│                                                  │
│ ┌────────────────────────────────────────────┐  │
│ │ Voice: Jenny ▼                              │  │
│ │ [Styles: 13 supported] [View capabilities]  │  │
│ └────────────────────────────────────────────┘  │
│                                                  │
│ ┌─ Plain Text Mode ─────────────────────────┐   │
│ │ Speed: [====|====] 1.0x                    │   │
│ │ Pitch: [====|====] 0%                      │   │
│ │ Volume: [========|==] 80%                  │   │
│ │                                            │   │
│ │ ┌────────────────────────────────────────┐ │   │
│ │ │ Enter your text here...                │ │   │
│ │ │                                        │ │   │
│ │ └────────────────────────────────────────┘ │   │
│ └────────────────────────────────────────────┘   │
│                                                  │
│ ┌─ SSML Mode (Styled) ───────────────────────┐  │
│ │ Preset: [Cheerful Jenny ▼]                 │  │
│ │                                             │  │
│ │ OR                                          │  │
│ │                                             │  │
│ │ Custom Style: [cheerful ▼] Degree: [1.5]   │  │
│ │                                             │  │
│ │ ┌─────────────────────────────────────────┐ │  │
│ │ │ Hello! <emphasis>This is great!</emph> │ │  │
│ │ └─────────────────────────────────────────┘ │  │
│ │                                             │  │
│ │ [Visual Builder] [Validate]                 │  │
│ └─────────────────────────────────────────────┘  │
│                                                  │
│ [Preview] [Send to Voice] [Save]                │
│                                                  │
└──────────────────────────────────────────────────┘
```

### JavaScript Extensions

**Location:** `src/DiscordBot.Bot/wwwroot/js/tts-portal.js`

Add new functions:

```javascript
// Fetch voice capabilities
async function loadVoiceCapabilities(voiceName) {
    const response = await fetch(`/api/portal-tts/voices/${voiceName}/capabilities`);
    if (response.ok) {
        const capabilities = await response.json();
        updateStyleDropdown(capabilities.supportedStyles);
        showCapabilityIndicator(capabilities);
    }
}

// Validate SSML
async function validateSsml(ssml) {
    const response = await fetch('/api/portal-tts/validate-ssml', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ssml })
    });

    const result = await response.json();
    displayValidationResults(result);
    return result.isValid;
}

// Build SSML from visual builder
function buildSsmlFromVisualBuilder() {
    const segments = collectBuilderSegments();

    return fetch('/api/portal-tts/build-ssml', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ language: 'en-US', segments })
    })
    .then(r => r.json())
    .then(result => {
        if (result.isValid) {
            document.getElementById('ssml-editor').value = result.ssml;
        } else {
            showErrors(result.errors);
        }
    });
}

// Load style presets
async function loadStylePresets() {
    const response = await fetch('/api/portal-tts/presets');
    const presets = await response.json();

    const select = document.getElementById('preset-select');
    presets.forEach(preset => {
        const option = document.createElement('option');
        option.value = preset.presetId;
        option.text = `${preset.category}: ${preset.displayName}`;
        option.dataset.description = preset.description;
        select.appendChild(option);
    });
}

// Apply preset
function applyPreset(presetId) {
    const presets = _cachedPresets;
    const preset = presets.find(p => p.presetId === presetId);

    if (preset) {
        document.getElementById('voice-select').value = preset.voiceName;
        document.getElementById('style-select').value = preset.style;
        document.getElementById('style-degree').value = preset.styleDegree;

        if (preset.prosodyOptions) {
            document.getElementById('speed-slider').value = preset.prosodyOptions.speed;
            document.getElementById('pitch-slider').value = preset.prosodyOptions.pitch;
            document.getElementById('volume-slider').value = preset.prosodyOptions.volume;
        }
    }
}
```

### Settings Page Extensions

**Location:** `src/DiscordBot.Bot/Pages/Guilds/Settings/Index.cshtml`

Add TTS Settings section:

```html
<div class="settings-section">
    <h3>TTS Settings</h3>

    <div class="form-group">
        <label>
            <input type="checkbox" asp-for="TtsSettings.SsmlEnabled" />
            Enable SSML Mode (Advanced)
        </label>
        <p class="help-text">
            Allows users to use styled voices and advanced speech controls.
            Requires moderator+ permission to use.
        </p>
    </div>

    <div class="form-group" data-conditional="TtsSettings.SsmlEnabled">
        <label>
            <input type="checkbox" asp-for="TtsSettings.StrictSsmlValidation" />
            Strict SSML Validation
        </label>
        <p class="help-text">
            Reject invalid SSML instead of falling back to plain text.
        </p>
    </div>

    <div class="form-group" data-conditional="TtsSettings.SsmlEnabled">
        <label for="max-ssml-complexity">Max SSML Complexity</label>
        <input type="number"
               id="max-ssml-complexity"
               asp-for="TtsSettings.MaxSsmlComplexity"
               min="10"
               max="200"
               class="form-control" />
        <p class="help-text">
            Limits nested SSML elements to prevent abuse (default: 50).
        </p>
    </div>

    <div class="form-group" data-conditional="TtsSettings.SsmlEnabled">
        <label for="default-style">Default Style (Optional)</label>
        <select id="default-style"
                asp-for="TtsSettings.DefaultStyle"
                class="form-control">
            <option value="">None</option>
            <option value="cheerful">Cheerful</option>
            <option value="sad">Sad</option>
            <option value="angry">Angry</option>
            <option value="excited">Excited</option>
            <option value="friendly">Friendly</option>
            <!-- ... other styles -->
        </select>
    </div>
</div>
```

## Error Handling Strategy

### Error Types and Responses

| Error | HTTP Code | Handling Strategy |
|-------|-----------|-------------------|
| Invalid XML | 400 | Return validation errors; suggest plain text fallback |
| Unsupported style for voice | 400 | List supported styles for that voice |
| SSML too complex | 400 | Inform complexity score and limit |
| SSML too long | 400 | Inform character count and limit |
| Voice not found | 404 | Suggest similar voices or default |
| Azure synthesis failure | 500 | Log details, return generic error to user |
| SSML disabled for guild | 403 | Inform user, link to settings page |

### Fallback Behavior

**Strict mode OFF (default):**
1. Attempt to validate SSML
2. If validation fails, extract plain text content
3. Synthesize plain text with basic prosody
4. Log warning for debugging
5. Return audio with warning note

**Strict mode ON:**
1. Validate SSML
2. If validation fails, reject immediately with errors
3. Return 400 with detailed error messages
4. Do not attempt synthesis

### Logging Strategy

**Log levels:**
- **Debug**: SSML content, validation details, complexity scores
- **Information**: Successful SSML synthesis, preset usage
- **Warning**: Validation failures in permissive mode, unsupported features used
- **Error**: Validation failures in strict mode, Azure API errors, unexpected exceptions

**Sample log messages:**
```
[Information] SSML synthesis completed for guild {GuildId}, voice={Voice}, style={Style}, complexity={Complexity}
[Warning] SSML validation failed in permissive mode for guild {GuildId}: {Errors}. Falling back to plain text.
[Error] SSML synthesis failed for guild {GuildId}: {ErrorMessage}
```

## Testing Strategy

### Unit Tests

**Location:** `tests/DiscordBot.Tests/Services/Tts/`

1. **SsmlBuilderTests.cs**
   - Test fluent API
   - Test nesting (voice → style → prosody → text)
   - Test automatic escaping
   - Test edge cases (empty text, special characters)

2. **SsmlValidatorTests.cs**
   - Test valid SSML documents
   - Test invalid XML
   - Test unsupported voice/style combinations
   - Test complexity calculation
   - Test sanitization

3. **VoiceCapabilityProviderTests.cs**
   - Test capability lookups
   - Test cache behavior
   - Test unknown voices

4. **StylePresetProviderTests.cs**
   - Test preset retrieval
   - Test category filtering
   - Test featured presets

### Integration Tests

**Location:** `tests/DiscordBot.Tests/Integration/`

1. **SsmlSynthesisTests.cs**
   - Test end-to-end SSML synthesis
   - Test multi-voice SSML
   - Test style synthesis
   - Test fallback behavior

2. **PortalTtsSsmlTests.cs**
   - Test API endpoints
   - Test validation endpoint
   - Test build-ssml endpoint
   - Test synthesis with SSML mode

### Manual Testing Checklist

- [ ] Plain text TTS still works (backward compatibility)
- [ ] Style presets produce expected voice styles
- [ ] Multi-voice SSML switches voices correctly
- [ ] Invalid SSML is rejected in strict mode
- [ ] Invalid SSML falls back in permissive mode
- [ ] Voice capability endpoint returns correct data
- [ ] SSML disabled guilds cannot use styled TTS
- [ ] Complexity limits prevent abuse
- [ ] UI mode toggle switches between plain text and SSML
- [ ] Visual builder generates valid SSML
- [ ] Validation endpoint provides helpful error messages

## Migration Path

### Phase 1: Backend Infrastructure (Week 1)

**Goal:** Implement core SSML support without UI changes.

**Tasks:**
1. Create interfaces: `ISsmlBuilder`, `ISsmlValidator`
2. Create models: `VoiceCapabilities`, `StylePreset`, `SsmlValidationResult`, `SynthesisMode`
3. Implement `SsmlBuilder` service
4. Implement `SsmlValidator` service
5. Implement `VoiceCapabilityProvider`
6. Implement `StylePresetProvider`
7. Add database migration for `GuildTtsSettings` extensions
8. Update `AzureTtsService` to accept `SynthesisMode` parameter
9. Add configuration: `AzureSpeechSsmlOptions`
10. Write unit tests

**Deliverables:**
- Core SSML services functional
- Backward compatibility maintained (existing TTS works unchanged)
- All tests passing

### Phase 2: API Extensions (Week 2)

**Goal:** Expose SSML capabilities via API.

**Tasks:**
1. Add new `ITtsService` methods
2. Update `PortalTtsController` with new endpoints:
   - GET `/voices/{voiceName}/capabilities`
   - GET `/presets`
   - POST `/validate-ssml`
   - POST `/synthesize-ssml`
   - POST `/build-ssml`
3. Create request/response DTOs
4. Add authorization checks (SSML requires moderator+)
5. Write integration tests

**Deliverables:**
- API endpoints functional
- Swagger documentation updated
- Integration tests passing

### Phase 3: Discord Commands (Week 3)

**Goal:** Add Discord slash command support for styled TTS.

**Tasks:**
1. Add `/tts-styled` command to `TtsModule`
2. Create `StylePresetAutocompleteHandler`
3. Update `TtsModule` error handling for SSML
4. Add help text for new command
5. Test in Discord environment

**Deliverables:**
- Discord command working
- Autocomplete functional
- Error messages helpful

### Phase 4: Web UI (Week 4)

**Goal:** Build web portal UI for SSML features.

**Tasks:**
1. Update TTS Portal page with mode toggle
2. Add style preset selector
3. Add SSML editor with syntax highlighting
4. Build visual SSML builder interface
5. Add voice capability indicator
6. Update settings page with SSML configuration
7. Add client-side validation
8. Implement real-time SSML preview

**Deliverables:**
- Web UI fully functional
- Visual builder generates valid SSML
- Settings page allows guild admins to configure SSML

### Phase 5: Documentation and Polish (Week 5)

**Goal:** Finalize documentation and user experience.

**Tasks:**
1. Write user documentation: `docs/articles/ssml-support.md`
2. Add examples to documentation
3. Create tutorial video/guide
4. Update API documentation
5. Add inline help tooltips in UI
6. Performance testing
7. Security review (prevent SSML injection)
8. Beta testing with select guilds

**Deliverables:**
- Complete documentation
- Tested and ready for production
- Security validated

## Security Considerations

### SSML Injection Prevention

**Risks:**
- Malicious users crafting SSML that crashes Azure service
- Extremely long SSML documents causing performance issues
- Nested elements causing exponential processing time

**Mitigations:**
1. **Strict validation** - Reject invalid SSML before sending to Azure
2. **Complexity limits** - Limit nesting depth and total element count
3. **Length limits** - Cap SSML document size
4. **Sanitization** - Remove potentially dangerous elements
5. **Permission requirement** - SSML mode requires moderator+ role
6. **Rate limiting** - Existing TTS rate limits apply to SSML
7. **Content filtering** - Check for obvious abuse patterns

### Voice Impersonation

**Risk:** Users creating multi-voice SSML to impersonate announcements or other users.

**Mitigations:**
1. Guild setting to disable multi-voice SSML
2. Log all SSML usage with full content for audit
3. Display "Styled TTS" indicator in voice channel (future enhancement)

### Privacy

**Consideration:** SSML content may contain personal information or sensitive data.

**Mitigations:**
1. Do not log SSML content at INFO level (only DEBUG)
2. Respect existing TTS privacy settings
3. Add option to disable SSML history logging

## Performance Considerations

### Caching Strategy

**Voice capabilities:**
- Cache for 24 hours (rarely change)
- Invalidate on service restart or manual trigger

**Style presets:**
- Static data, no cache needed (in-memory list)

**SSML validation:**
- Consider caching validation results by SSML hash
- Short TTL (5 minutes) to prevent stale data

### Synthesis Performance

**Impact:** SSML synthesis may be slightly slower than plain text due to:
- Additional parsing by Azure
- Style application processing
- Multi-voice segments

**Optimizations:**
1. Reuse `SpeechSynthesizer` instance (already implemented)
2. Validate SSML client-side before sending to server
3. Use connection pooling for Azure API calls
4. Monitor synthesis time and alert on slowdowns

### Database Impact

**New columns:** Minimal impact (5 new columns in `GuildTtsSettings`)

**Queries:** No new complex queries; all settings loaded in single query (existing pattern)

## Future Enhancements

**Phase 6+ (Future):**

1. **Custom Presets**
   - Allow users to create and save personal presets
   - Share presets across guilds
   - Preset marketplace

2. **SSML Templates**
   - Predefined templates for common scenarios (greetings, announcements, jokes)
   - Template variables for personalization

3. **Voice Channel Indicators**
   - Show "Styled TTS" badge in Discord when styled voice is playing
   - Display current voice/style in channel

4. **Multi-Segment Conversations**
   - Build conversation scripts with multiple speakers
   - Save and replay multi-voice scripts

5. **SSML Learning Mode**
   - Interactive tutorial for learning SSML syntax
   - Real-time feedback on SSML construction

6. **Voice Cloning Integration**
   - Support for custom neural voices (Azure Custom Neural Voice)
   - Guild-specific voice training

7. **Accessibility Features**
   - SSML for better pronunciation of usernames
   - Regional accent support via roles
   - Dyslexia-friendly prosody presets

## Success Metrics

**Adoption:**
- Percentage of guilds enabling SSML mode
- Number of styled TTS commands used per day
- Most popular style presets

**Quality:**
- SSML validation failure rate
- Synthesis error rate (SSML vs. plain text)
- User feedback on voice quality

**Performance:**
- Average synthesis time (SSML vs. plain text)
- API endpoint response times
- Cache hit rates

**Support:**
- Number of support tickets related to SSML
- Common error patterns
- Documentation engagement

## Conclusion

This specification provides a comprehensive plan for implementing SSML support in the Discord bot TTS system. The phased approach ensures backward compatibility while introducing powerful new features for expressive speech synthesis.

Key principles:
- **Backward compatible** - Existing plain text TTS unchanged
- **Secure** - Validation, sanitization, and permission checks prevent abuse
- **User-friendly** - Visual builder and presets make SSML accessible
- **Extensible** - Architecture supports future enhancements
- **Well-tested** - Comprehensive testing strategy ensures quality

Implementation timeline: 5 weeks for core features, with future enhancements planned for subsequent releases.
