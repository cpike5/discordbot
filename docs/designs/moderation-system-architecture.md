# Moderation System Architecture Specification

**Version:** 1.0
**Created:** 2025-12-30
**Status:** Draft
**Epic:** #291 - Moderation System

---

## Overview

Technical architecture for the Moderation System. Defines data models, services, APIs, and integration points.

## Data Layer

### New Entities

```csharp
// Core auto-moderation entities
public class FlaggedEvent
{
    public Guid Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public ulong? ChannelId { get; set; }
    public RuleType RuleType { get; set; }        // Spam, Content, Raid
    public Severity Severity { get; set; }         // Low, Medium, High, Critical
    public string Description { get; set; }
    public string Evidence { get; set; }           // JSON: message IDs, content
    public FlaggedEventStatus Status { get; set; } // Pending, Dismissed, Acknowledged, Actioned
    public string? ActionTaken { get; set; }
    public ulong? ReviewedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // Navigation
    public Guild Guild { get; set; }
}

public class GuildModerationConfig
{
    public ulong GuildId { get; set; }             // PK
    public ConfigMode Mode { get; set; }           // Simple, Advanced
    public string? SimplePreset { get; set; }      // Relaxed, Moderate, Strict
    public string SpamConfig { get; set; }         // JSON
    public string ContentFilterConfig { get; set; } // JSON
    public string RaidProtectionConfig { get; set; } // JSON
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Guild Guild { get; set; }
}

public class ModerationCase
{
    public Guid Id { get; set; }
    public long CaseNumber { get; set; }           // Auto-increment per guild
    public ulong GuildId { get; set; }
    public ulong TargetUserId { get; set; }
    public ulong ModeratorUserId { get; set; }
    public CaseType Type { get; set; }             // Warn, Kick, Ban, Mute, Note
    public string? Reason { get; set; }
    public TimeSpan? Duration { get; set; }        // For temp bans/mutes
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid? RelatedFlaggedEventId { get; set; }

    // Navigation
    public Guild Guild { get; set; }
    public FlaggedEvent? RelatedFlaggedEvent { get; set; }
}

public class ModNote
{
    public Guid Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong TargetUserId { get; set; }
    public ulong AuthorUserId { get; set; }
    public string Content { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Guild Guild { get; set; }
}

public class ModTag
{
    public Guid Id { get; set; }
    public ulong GuildId { get; set; }
    public string Name { get; set; }
    public string Color { get; set; }              // Hex color
    public string? Description { get; set; }
    public TagCategory Category { get; set; }      // Positive, Negative, Neutral
    public bool IsFromTemplate { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Guild Guild { get; set; }
    public ICollection<UserModTag> UserTags { get; set; }
}

public class UserModTag
{
    public Guid Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public Guid TagId { get; set; }
    public ulong AppliedByUserId { get; set; }
    public DateTime AppliedAt { get; set; }

    // Navigation
    public ModTag Tag { get; set; }
}

public class Watchlist
{
    public Guid Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public ulong AddedByUserId { get; set; }
    public string? Reason { get; set; }
    public DateTime AddedAt { get; set; }

    // Navigation
    public Guild Guild { get; set; }
}
```

### Enums

```csharp
public enum RuleType { Spam, Content, Raid }
public enum Severity { Low, Medium, High, Critical }
public enum FlaggedEventStatus { Pending, Dismissed, Acknowledged, Actioned }
public enum ConfigMode { Simple, Advanced }
public enum CaseType { Warn, Kick, Ban, Mute, Note }
public enum TagCategory { Positive, Negative, Neutral }
```

### Configuration DTOs (JSON-serialized)

```csharp
public class SpamDetectionConfig
{
    public bool Enabled { get; set; } = true;
    public int MessageFloodThreshold { get; set; } = 10;
    public int MessageFloodWindowSeconds { get; set; } = 30;
    public int DuplicateMessageThreshold { get; set; } = 3;
    public int DuplicateMessageWindowSeconds { get; set; } = 60;
    public int MentionAbuseLimit { get; set; } = 2;
    public int NewAccountDaysThreshold { get; set; } = 7;
    public AutoAction AutoAction { get; set; } = AutoAction.None;
}

public class ContentFilterConfig
{
    public bool Enabled { get; set; } = true;
    public List<string> CustomBlocklist { get; set; } = new();
    public List<string> RegexPatterns { get; set; } = new();
    public List<string> EnabledTemplates { get; set; } = new();
    public AutoAction AutoAction { get; set; } = AutoAction.None;
}

public class RaidProtectionConfig
{
    public bool Enabled { get; set; } = true;
    public int MassJoinThreshold { get; set; } = 10;
    public int MassJoinWindowMinutes { get; set; } = 5;
    public int NewAccountDaysFlag { get; set; } = 7;
    public RaidAutoAction AutoAction { get; set; } = RaidAutoAction.None;
}
```

---

## Service Layer

### Core Services

```
IModerationService
├── CreateCaseAsync(guildId, targetUserId, type, reason, moderatorId)
├── GetCaseAsync(caseId)
├── GetUserCasesAsync(guildId, userId, pagination)
├── UpdateCaseReasonAsync(caseId, reason)
└── GetGuildCasesAsync(guildId, filters, pagination)

IFlaggedEventService
├── CreateFlaggedEventAsync(guildId, userId, ruleType, severity, evidence)
├── GetFlaggedEventAsync(eventId)
├── GetPendingEventsAsync(guildId, filters, pagination)
├── DismissEventAsync(eventId, reviewerId)
├── AcknowledgeEventAsync(eventId, reviewerId)
├── TakeActionAsync(eventId, action, reviewerId)
└── GetUserFlagsAsync(guildId, userId, pagination)

IGuildModerationConfigService
├── GetConfigAsync(guildId)
├── UpdateConfigAsync(guildId, config)
├── ApplyPresetAsync(guildId, preset)
└── GetDefaultConfig()

IModNoteService
├── AddNoteAsync(guildId, targetUserId, content, authorId)
├── GetNotesAsync(guildId, targetUserId)
├── DeleteNoteAsync(noteId)

IModTagService
├── CreateTagAsync(guildId, name, color, category)
├── DeleteTagAsync(tagId)
├── ApplyTagToUserAsync(guildId, userId, tagId, appliedById)
├── RemoveTagFromUserAsync(guildId, userId, tagId)
├── GetUserTagsAsync(guildId, userId)
├── GetGuildTagsAsync(guildId)
└── ImportTemplateTagsAsync(guildId, templateNames)

IWatchlistService
├── AddToWatchlistAsync(guildId, userId, reason, addedById)
├── RemoveFromWatchlistAsync(guildId, userId)
├── GetWatchlistAsync(guildId, pagination)
└── IsOnWatchlistAsync(guildId, userId)
```

### Detection Services

```
ISpamDetectionService
├── AnalyzeMessageAsync(message) -> DetectionResult?
├── GetUserMessageRateAsync(guildId, userId, windowSeconds)
└── GetDuplicateCountAsync(guildId, userId, contentHash, windowSeconds)

IContentFilterService
├── AnalyzeMessageAsync(message, config) -> DetectionResult?
├── LoadGuildFiltersAsync(guildId)
└── MatchesPatternAsync(content, patterns)

IRaidDetectionService
├── AnalyzeJoinAsync(guildId, user) -> DetectionResult?
├── GetRecentJoinCountAsync(guildId, windowMinutes)
└── TriggerLockdownAsync(guildId)
```

---

## API Layer

### Endpoints

```
# Flagged Events
GET    /api/guilds/{guildId}/flagged-events
GET    /api/guilds/{guildId}/flagged-events/{id}
POST   /api/guilds/{guildId}/flagged-events/{id}/dismiss
POST   /api/guilds/{guildId}/flagged-events/{id}/acknowledge
POST   /api/guilds/{guildId}/flagged-events/{id}/action

# Moderation Cases
GET    /api/guilds/{guildId}/cases
GET    /api/guilds/{guildId}/cases/{id}
POST   /api/guilds/{guildId}/cases
PATCH  /api/guilds/{guildId}/cases/{id}/reason

# User Moderation Profile
GET    /api/guilds/{guildId}/users/{userId}/moderation
GET    /api/guilds/{guildId}/users/{userId}/cases
GET    /api/guilds/{guildId}/users/{userId}/notes
GET    /api/guilds/{guildId}/users/{userId}/flags
GET    /api/guilds/{guildId}/users/{userId}/tags

# Mod Notes
POST   /api/guilds/{guildId}/users/{userId}/notes
DELETE /api/guilds/{guildId}/notes/{noteId}

# Mod Tags
GET    /api/guilds/{guildId}/tags
POST   /api/guilds/{guildId}/tags
DELETE /api/guilds/{guildId}/tags/{tagId}
POST   /api/guilds/{guildId}/users/{userId}/tags/{tagId}
DELETE /api/guilds/{guildId}/users/{userId}/tags/{tagId}

# Watchlist
GET    /api/guilds/{guildId}/watchlist
POST   /api/guilds/{guildId}/watchlist
DELETE /api/guilds/{guildId}/watchlist/{userId}

# Configuration
GET    /api/guilds/{guildId}/moderation-config
PUT    /api/guilds/{guildId}/moderation-config
POST   /api/guilds/{guildId}/moderation-config/preset
```

---

## Discord Bot Integration

### Command Modules

```
ModerationCommandModule
├── WarnAsync(user, reason?)
├── KickAsync(user, reason?)
├── BanAsync(user, duration?, reason?)
├── MuteAsync(user, duration, reason?)
└── PurgeAsync(count, user?)

ModNoteCommandModule
├── AddNoteAsync(user, note)
├── ListNotesAsync(user)
└── RemoveNoteAsync(noteId)

ModTagCommandModule
├── AddTagAsync(user, tag)
├── RemoveTagAsync(user, tag)
├── ListTagsAsync(user?)
├── CreateTagAsync(name, color, description?)
└── DeleteTagAsync(name)

ModHistoryCommandModule
├── ModLogAsync(user)
├── CaseAsync(caseId)
├── ReasonAsync(caseId, reason)
└── ExportHistoryAsync(user)

WatchlistCommandModule
├── AddAsync(user, reason?)
├── RemoveAsync(user)
└── ListAsync()

InvestigateCommandModule
└── InvestigateAsync(user)

ModStatsCommandModule
└── ModStatsAsync(mod?, timeframe?)
```

### Event Handlers

```
MessageReceivedHandler
├── Run spam detection
├── Run content filter
└── Create flagged events if detected

UserJoinedHandler
├── Run raid detection
├── Check new account age
└── Create flagged events if detected
```

---

## Implementation Phases

### Phase 1: Data Foundation
- Add entities and migrations
- Create repositories
- Seed tag templates

### Phase 2: Manual Commands
- Implement moderation commands
- Create mod note/tag commands
- Add history/case commands

### Phase 3: Detection Services
- Implement spam detection
- Implement content filtering
- Implement raid detection

### Phase 4: Admin UI
- Flagged events page
- Guild moderation settings
- User moderation profile

### Phase 5: Integration
- Auto-action execution
- Discord channel alerts
- Watchlist notifications
