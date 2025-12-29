# Requirements: Moderation System

> Expands Epic #291 — Moderation System (v0.5.0)

## Executive Summary

Comprehensive moderation system combining manual mod commands with an intelligent auto-detection layer. The philosophy is **detection-first, action-optional** — build visibility and analytics before enforcement. Mods get a unified view of flagged activity with full drill-down capability, while guilds can opt into automated actions once confident in thresholds.

## Problem Statement

Server moderators need tools to:
1. Take manual moderation actions (warn, kick, ban, mute) with proper tracking
2. Detect problematic behavior automatically without constant manual monitoring
3. Review flagged activity with full context before deciding on action
4. Configure moderation rules per-guild based on community needs
5. Track user history and patterns over time

## Target Users

- **Server Moderators** — Primary users; review flags, take actions, manage users
- **Server Admins** — Configure auto-mod rules, set thresholds, manage mod team
- **Bot SuperAdmins** — Access cross-guild analytics, manage templates

---

## Part 1: Manual Moderation Commands

*Already defined in Epic #291 — included here for completeness.*

### Action Commands

| Command | Description |
|---------|-------------|
| `/warn <user> [reason]` | Issue formal warning with tracking |
| `/kick <user> [reason]` | Kick user with audit logging |
| `/ban <user> [duration] [reason]` | Ban with optional temp-ban support |
| `/mute <user> <duration> [reason]` | Timeout user |
| `/purge <count> [user]` | Bulk delete messages |

### Mod Notes

| Command | Description |
|---------|-------------|
| `/modnote add <user> <note>` | Add private mod note about a user |
| `/modnote list <user>` | View all mod notes for a user |
| `/modnote remove <id>` | Delete a specific note |

### Mod Tags

| Command | Description |
|---------|-------------|
| `/modtag add <user> <tag>` | Add a tag to a user |
| `/modtag remove <user> <tag>` | Remove a tag from a user |
| `/modtag list [user]` | List tags for user, or list all available tags |
| `/modtag create <name> <color> [description]` | Create a new guild tag definition |
| `/modtag delete <name>` | Delete a tag definition from the guild |

### History & Cases

| Command | Description |
|---------|-------------|
| `/modlog <user>` | View user's full history (warnings, kicks, bans, notes, tags) |
| `/case <id>` | View details of a specific moderation action |
| `/reason <case_id> <reason>` | Update or add reason to a past action |
| `/history export <user>` | Export user's moderation history to file |

### Monitoring

| Command | Description |
|---------|-------------|
| `/watchlist add/remove/list <user>` | Flag users for monitoring with alerts |
| `/investigate <user>` | Compile full user report (messages, history, account info) |
| `/modstats [mod] [timeframe]` | View moderation statistics and workload |

---

## Part 2: Auto-Moderation Detection System

### Philosophy

- **Detection ON by default** — System monitors and flags suspicious activity
- **Auto-actions OFF by default** — Guilds explicitly opt into automated enforcement
- **Graduated severity** — Low / Medium / High / Critical classifications
- **Highly configurable** — Every threshold tunable per-guild

### Rule Types

#### 1. Spam Detection

Detects message flooding and abuse patterns.

| Parameter | Description | Example Default |
|-----------|-------------|-----------------|
| Message flood threshold | Max messages per time window | 10 msgs / 30 sec |
| Duplicate message threshold | Same content repeated | 3x in 60 sec |
| @everyone/@here limit | Mention abuse detection | 2 per hour |
| New account sensitivity | Stricter rules for new accounts | Account < 7 days |

#### 2. Content Filtering

Detects prohibited content in messages.

| Feature | Description |
|---------|-------------|
| Custom word/phrase blocklist | Per-guild lists of banned terms |
| Regex pattern support | Advanced pattern matching for power users |
| Pre-built templates | Opt-in templates (slurs, common spam phrases, etc.) |

#### 3. Raid Protection

Detects coordinated attacks on the server.

| Parameter | Description | Example Default |
|-----------|-------------|-----------------|
| Mass join threshold | New members per time window | 10 joins / 5 min |
| New account flag | Accounts created recently | Created < 7 days ago |

### Severity Levels

| Level | Description | Example Triggers |
|-------|-------------|------------------|
| **Low** | Minor flag, informational | Single duplicate message |
| **Medium** | Notable pattern, worth reviewing | 3 spam flags in an hour |
| **High** | Significant concern | Raid pattern detected |
| **Critical** | Immediate attention needed | Known bad actor, severe content |

### Auto-Actions (Optional, Per-Rule)

When enabled by guild admins:

| Action | Description |
|--------|-------------|
| Mute | Timeout user for configurable duration |
| Kick | Remove user from server |
| Ban | Permanently ban user |
| Delete | Remove offending message(s) |

---

## Part 3: Per-Guild Configuration

### Configuration Modes

#### Simple Mode (Presets)

For guilds that want sensible defaults without complexity.

| Preset | Description |
|--------|-------------|
| **Relaxed** | Higher thresholds, fewer false positives. Best for smaller/trusted communities. |
| **Moderate** | Balanced defaults. Good starting point for most servers. |
| **Strict** | Lower thresholds, more aggressive detection. For larger/public servers. |

#### Advanced Mode

Full control over every parameter:

- Per-rule enable/disable toggles
- Custom thresholds for all parameters
- Severity mapping overrides
- Auto-action configuration per rule type

### Configuration Data Model

```
GuildModerationConfig
├── ConfigMode: Simple | Advanced
├── SimplePreset: Relaxed | Moderate | Strict (if Simple)
├── SpamDetectionConfig
│   ├── Enabled: bool
│   ├── MessageFloodThreshold: int
│   ├── MessageFloodWindowSeconds: int
│   ├── DuplicateMessageThreshold: int
│   ├── DuplicateMessageWindowSeconds: int
│   ├── MentionAbuseLimit: int
│   ├── NewAccountDaysThreshold: int
│   └── AutoAction: None | Mute | Kick | Ban
├── ContentFilterConfig
│   ├── Enabled: bool
│   ├── CustomBlocklist: string[]
│   ├── RegexPatterns: string[]
│   ├── EnabledTemplates: string[]
│   └── AutoAction: None | Delete | Mute | Kick | Ban
└── RaidProtectionConfig
    ├── Enabled: bool
    ├── MassJoinThreshold: int
    ├── MassJoinWindowMinutes: int
    ├── NewAccountDaysFlag: int
    └── AutoAction: None | Lockdown | Alert
```

---

## Part 4: Mod Tags Enhancement

### Pre-Built Tag Templates

Guilds can adopt common tags or create custom ones.

**Negative Tags:**
- Spammer
- Troll
- Repeat Offender
- Under Review
- Warned

**Positive Tags:**
- Trusted
- VIP
- Verified
- Helper

### Auto-Suggested Tags

System suggests tags based on detection patterns:

| Trigger | Suggested Tag |
|---------|---------------|
| 3+ spam detection flags | "Potential Spammer" |
| 2+ content filter hits | "Under Review" |
| On watchlist + new flags | "Repeat Offender" |

Suggestions appear in the UI; mods confirm or dismiss.

---

## Part 5: Admin UI — Flagged Events

### Flagged Events Log

Central view of all detected activity requiring review.

#### Filters

| Filter | Options |
|--------|---------|
| Guild | Select specific server |
| User | Search by username/ID |
| Channel | Filter by channel |
| Rule Type | Spam / Content / Raid |
| Severity | Low / Medium / High / Critical |
| Time Range | Last hour / 24h / 7 days / Custom |
| Status | Pending / Dismissed / Actioned |

#### List View

Columns:
- Timestamp
- User (avatar + name)
- Rule Type (icon + label)
- Severity (color-coded badge)
- Channel
- Brief description
- Status

#### Detail View (Drill-Down)

When clicking a flagged event:

- **Event Summary** — Rule triggered, severity, timestamp
- **User Info** — Avatar, username, account age, existing tags
- **Evidence** — Individual message(s) that triggered the flag
  - For spam: show message burst with timestamps
  - For content: highlight matched word/pattern
  - For raids: show list of accounts that joined
- **User History** — Previous flags, mod actions, notes
- **Actions Available** (see below)

### Available Actions

From the flagged event detail view:

| Action | Description |
|--------|-------------|
| **Dismiss** | Mark as false positive, clear the flag |
| **Acknowledge** | "I've seen this, no action needed" |
| **Take Action** | Kick / Ban / Mute directly from UI |
| **Add to Watchlist** | Flag user for ongoing monitoring |
| **Add Mod Note** | Attach a note to user's profile |
| **Apply Tag** | Add a tag (with auto-suggestions shown) |

---

## Part 6: Data Entities

### New Entities

```
FlaggedEvent
├── Id: Guid
├── GuildId: ulong
├── UserId: ulong
├── ChannelId: ulong?
├── RuleType: Spam | Content | Raid
├── Severity: Low | Medium | High | Critical
├── Description: string
├── Evidence: json (message IDs, content, timestamps)
├── Status: Pending | Dismissed | Acknowledged | Actioned
├── ActionTaken: string?
├── ReviewedByUserId: ulong?
├── CreatedAt: DateTime
└── ReviewedAt: DateTime?

GuildModerationConfig
├── GuildId: ulong (PK)
├── ConfigMode: Simple | Advanced
├── SimplePreset: string?
├── SpamConfig: json
├── ContentFilterConfig: json
├── RaidProtectionConfig: json
└── UpdatedAt: DateTime

ContentFilterTemplate
├── Id: Guid
├── Name: string
├── Description: string
├── Patterns: string[] (words/regex)
├── IsBuiltIn: bool
└── CreatedAt: DateTime

ModTagTemplate
├── Id: Guid
├── Name: string
├── Color: string
├── Description: string
├── IsBuiltIn: bool
├── Category: Positive | Negative | Neutral
└── CreatedAt: DateTime
```

### Existing Entities (from Epic #291)

- ModerationCase
- ModNote
- ModTag (per-guild definitions)
- UserModTag (assignments)
- Watchlist

---

## Future Considerations

### Discord AutoMod Integration

Discord's built-in AutoMod can block messages *before* they're sent (bots can only react after). Potential integration points:

1. **Read AutoMod audit logs** — Capture Discord AutoMod events and display in our flagged events UI for a unified view
2. **Complement, don't compete** — Focus our detection on behavioral patterns and analytics that Discord doesn't provide
3. **Future: Manage rules via UI** — Allow configuring Discord AutoMod rules through our admin interface

### Additional Enhancements

- Dashboard summary view (charts, trends, mod workload)
- Discord channel alerts (post to mod-log channel)
- Cross-guild pattern detection (for multi-server deployments)
- ML-based detection improvements
- Integration with external bad-actor databases

---

## Open Questions

1. **Retention policy** — Currently same as message logs. Should flagged events have separate retention?
2. **Permission model** — Which roles can configure auto-mod vs. just review flags?
3. **Rate limiting** — How to handle high-volume servers without overwhelming the database?

---

## Recommended Next Steps

1. **Update Epic #291** — Add auto-moderation section to existing epic
2. **Create child issues** — Break down into implementable tasks:
   - Data entities and migrations
   - Detection services (spam, content, raid)
   - Configuration UI
   - Flagged events UI
   - Mod tag enhancements
3. **Prototype UI** — Create HTML prototypes for:
   - Flagged events list/detail views
   - Guild moderation settings page
4. **Design API endpoints** — REST API for flagged events and configuration

---

*Document created: 2025-12-29*
*Related: Epic #291 — Moderation System (v0.5.0)*
