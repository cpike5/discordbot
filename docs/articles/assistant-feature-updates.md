---
uid: assistant-feature-updates
title: Adding Features to Assistant Knowledge
description: How to update the AI assistant's knowledge when adding new bot features
---

# Adding Features to Assistant Knowledge

When adding a new feature to the bot, the AI assistant needs to be updated so it can answer questions about the feature. This guide covers all locations that need updating.

## Overview

The assistant's knowledge comes from three sources:

1. **Agent Prompt** (`docs/agents/assistant-agent.md`) - Static knowledge embedded in the system prompt
2. **Documentation Tools** - Dynamic access to feature documentation via tool calls
3. **Feature Mappings** - Code that maps feature names to documentation files

All three must be updated for the assistant to fully understand a new feature.

---

## Step 1: Update the Agent Prompt

**File:** `docs/agents/assistant-agent.md`

This is the system prompt that defines the assistant's personality, security rules, and baseline knowledge.

### Available Features List

Find the `get_feature_documentation` tool section and add the feature name:

```markdown
**Available features:** soundboard, rat-watch, tts, vox, reminder, member-directory, ...
```

### Supported Commands Section

Add commands under the appropriate category (General, Administration, Audio & Voice, etc.):

```markdown
### Audio & Voice
- `/play <sound>` - Play a sound in voice channel
- `/vox <message> [gap]` - Play a VOX announcement (Half-Life PA system style)
- `/fvox <message> [gap]` - Play an FVOX announcement (Half-Life HEV suit style)
```

### Feature Documentation Section

Add a link to the feature's documentation:

```markdown
### Feature Documentation
- [Soundboard](https://github.com/cpike5/discordbot/blob/main/docs/articles/soundboard.md) - Audio playback in voice channels
- [VOX System](https://github.com/cpike5/discordbot/blob/main/docs/articles/vox-system-spec.md) - Half-Life style concatenated clip announcements
```

### Portal URLs Section (if applicable)

If the feature has a portal page, add a URL section:

```markdown
## VOX URL

The VOX Portal URL is https://discordbot.cpike.ca/Portal/VOX/{{GUILD_ID}}

VOX is a Half-Life style concatenated clip announcement system. It plays pre-recorded word clips in sequence to create robotic, word-by-word announcements. Three clip groups are available:
- **VOX** (`/vox`) - Half-Life PA system announcements
- **FVOX** (`/fvox`) - Half-Life HEV suit (female voice)
- **HGrunt** (`/hgrunt`) - Half-Life military grunt radio
```

---

## Step 2: Update Documentation Tool Provider

**File:** `src/DiscordBot.Infrastructure/Services/LLM/Providers/DocumentationToolProvider.cs`

This file maps feature names to documentation files and provides metadata for the `list_features` tool.

### FeatureDocumentationMap

Add entries that map feature names (and common aliases) to the documentation file:

```csharp
private static readonly Dictionary<string, string> FeatureDocumentationMap = new(StringComparer.OrdinalIgnoreCase)
{
    // ... existing entries ...
    { "vox", "vox-system-spec.md" },
    { "vox-system", "vox-system-spec.md" },
    { "fvox", "vox-system-spec.md" },      // aliases point to same doc
    { "hgrunt", "vox-system-spec.md" },
};
```

### AllFeatures List

Add the feature to the `AllFeatures` list for the `list_features` tool:

```csharp
private static readonly List<FeatureInfo> AllFeatures = new()
{
    // ... existing entries ...
    new("VOX System", "Audio", "Half-Life style concatenated clip announcements using /vox, /fvox, and /hgrunt commands.", "vox-system-spec"),
};
```

**FeatureInfo parameters:**
- `Name` - Display name of the feature
- `Category` - Category grouping (Audio, Accountability, Productivity, etc.)
- `Description` - Brief description shown in feature list
- `DocumentationFile` - Filename (without `.md` extension) in `docs/articles/`

---

## Step 3: Update Documentation Tools

**File:** `src/DiscordBot.Infrastructure/Services/LLM/Implementations/DocumentationTools.cs`

Update the tool description to include the new feature name.

### Tool Description

Find the `CreateGetFeatureDocumentationTool` method and add the feature to the description:

```csharp
Description = "Retrieves comprehensive documentation for a bot feature including ALL related commands, configuration options, usage instructions, and examples. This is the BEST tool for 'how do I use X' questions - use it FIRST before search_commands. Feature names: soundboard, rat-watch, tts, vox, reminder, member-directory, moderation, welcome, scheduled-messages, consent, privacy, commands, settings, audio, performance, audit.",
```

---

## Step 4: Ensure Documentation Exists

The feature must have a documentation file in `docs/articles/` that the assistant can access.

**Requirements:**
- File must exist at the path specified in `FeatureDocumentationMap`
- File should include commands, configuration, and usage examples
- Keep documentation concise but comprehensive

**Example structure:**
```markdown
# Feature Name

Brief overview of the feature.

## Commands

- `/command` - Description

## Configuration

Settings in `appsettings.json`...

## Usage Examples

Examples of how to use the feature...
```

---

## Verification Checklist

After updating, verify the assistant can:

- [ ] Answer "how do I use [feature]" questions
- [ ] List the feature when asked "what features do you have"
- [ ] Provide correct Portal URLs (if applicable)
- [ ] Show the feature's commands when asked "what commands do you have"

### Testing

Enable the assistant in a test guild and ask:

```
@Bot How do I use VOX?
@Bot What features are available?
@Bot What's the VOX portal URL?
```

---

## Quick Reference

| File | What to Update |
|------|----------------|
| `docs/agents/assistant-agent.md` | Available features list, commands, documentation links, portal URLs |
| `src/.../Providers/DocumentationToolProvider.cs` | `FeatureDocumentationMap`, `AllFeatures` list |
| `src/.../Implementations/DocumentationTools.cs` | Tool description feature list |
| `docs/articles/{feature}.md` | Ensure documentation exists |

---

## Related Documentation

- [AI Assistant](ai-assistant.md) - Full assistant documentation
- [Agent Prompt](../agents/assistant-agent.md) - The system prompt file
