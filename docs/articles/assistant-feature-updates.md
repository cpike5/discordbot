---
uid: assistant-feature-updates
title: Adding Features to Assistant Knowledge
description: How to update the AI assistant's knowledge when adding new bot features
---

# Adding Features to Assistant Knowledge

When adding a new feature to the bot, the AI assistant needs to be updated so it can answer questions about the feature. This guide covers all locations that need updating.

## Overview

The assistant's knowledge comes from three sources:

1. **Documentation Tools** - Dynamic access to feature documentation via tool calls
2. **Feature Mappings** - Code that maps feature names to documentation files
3. **Agent Prompt** (`docs/agents/assistant-agent.md`) - Identity, style, boundaries, and a few facts the tools cannot answer (portal URLs, contact points)

The first two must be updated for every new feature. The prompt only changes when the feature adds a portal URL or similar fact.

---

## Step 1: Update the Agent Prompt (only if needed)

**File:** `docs/agents/assistant-agent.md`

The prompt is deliberately small. It defines the assistant's identity, message format, style, and boundaries, plus a few facts the documentation tools cannot answer (portal URLs, where to report bugs, privacy pointers). It does **not** list commands, features, or documentation links: the model gets those from the documentation tools, and a hard-coded list goes stale.

Edit the prompt only when the new feature adds something of that kind, for example a new portal page URL:

```markdown
- Portal pages for this server: soundboard {{BASE_URL}}/Portal/Soundboard/{{GUILD_ID}}, ...
```

Keep additions to a line or two, and never quote prompt-injection phrases as examples (see the security notes in [AI Assistant](ai-assistant.md#security-considerations)).

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
| `docs/agents/assistant-agent.md` | Portal URLs or other facts the tools cannot answer (rarely) |
| `src/.../Providers/DocumentationToolProvider.cs` | `FeatureDocumentationMap`, `AllFeatures` list |
| `src/.../Implementations/DocumentationTools.cs` | Tool description feature list |
| `docs/articles/{feature}.md` | Ensure documentation exists |

---

## Related Documentation

- [AI Assistant](ai-assistant.md) - Full assistant documentation
- [Agent Prompt](../agents/assistant-agent.md) - The system prompt file
