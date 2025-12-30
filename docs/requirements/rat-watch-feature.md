# Requirements: Rat Watch Feature

## Executive Summary

A Discord accountability system where users can put each other "on watch" when commitments are made. At the scheduled time, if the accused hasn't checked in, a public vote determines their guilt. Rat records are tracked with stats and leaderboard commands.

---

## Problem Statement

Friends in a gaming group need a fun, social-pressure-based accountability system for when someone makes a commitment (e.g., "I'll be on at 10pm") and fails to show up — colloquially known as "ratting."

## Primary Purpose

Allow users to schedule accountability checks on Discord messages, with community voting and stat tracking for repeat offenders.

## Target Users

- **Server members**: Anyone in a server with Rat Watch enabled can initiate watches, vote, and check stats
- **Accused ("rats")**: Can clear themselves by checking in early
- **Server admins**: Enable/disable feature, configure settings via web UI

---

## Core Features (MVP)

### 1. Rat Watch Command (Context Menu)

- Right-click a message → Apps → **Rat Watch**
- Parameters:
  - `time` (required): When to trigger the check — supports relative (`10m`, `2h`) and absolute (`10pm`, `22:00`)
  - `message` (optional): Custom message (default: "🐀 Rat check! @Username")
- Validation: No duplicate watches for the same user at the same time
- Maximum advance time: Configurable per server (default: 24 hours)

### 2. Watch Notification

- Bot mentions the accused in the channel: "⏰ @Username is now on Rat Watch! Check in before [time] or face judgment."
- Includes a **"I'm Here!"** button for early check-in

### 3. Early Check-In

- Accused can clear themselves via:
  - The button in the notification message
  - `/rat-clear` slash command
- Clears the watch, posts confirmation: "✅ @Username checked in early. Honor preserved!"

### 4. Rat Check (Scheduled Message)

- Posts at scheduled time if not cleared
- Message: Custom text or default "🐀 Rat check! @Username"
- Two buttons: **Rat 🐀** / **Not Rat ✓**
- Links back to original message for context

### 5. Voting System

- Anyone in the channel can vote (including the accused)
- One vote per person, locked in (no changes)
- 5-minute voting window
- After voting closes:
  - Message updates with final verdict and tally
  - Example: "🐀 **GUILTY** — 3 Rat, 1 Not Rat" or "✅ **CLEARED** — 2 Not Rat, 1 Rat"

### 6. Rat Record Tracking

- Store guilty verdicts per user per server
- Track: user ID, server ID, timestamp, vote tally, original message reference

### 7. Stats Commands

- `/rat-stats @user`: View a user's rat record (guilty count, recent incidents)
- `/rat-leaderboard`: Top rats in the server (sorted by guilty count)

### 8. Admin UI (Web)

- **Settings page**: Enable/disable Rat Watch per server, configure max advance time, default timezone
- **Management page**: View all pending watches, cancel watches, view history

### 9. Server Configuration

- Feature toggle: Enable/disable per server
- Timezone: Server default (configurable), used for absolute time parsing
- Max advance time: How far ahead a watch can be scheduled (default: 24 hours)

---

## Future Features

- **Analytics dashboard**: Trends over time, rat frequency, peak ratting hours
- **Streak tracking**: Consecutive shows or no-shows
- **Redemption arc**: Track improvement over time

## Out of Scope

- DM notifications to the accused
- Cross-server rat records
- Role-based permissions for initiating watches
- Automatic time parsing from message content

---

## Tech Stack

Building on existing DiscordBot project:

- **Bot**: Discord.NET slash commands and context menus
- **Scheduling**: Existing scheduled message infrastructure or new lightweight scheduler
- **Storage**: EF Core entities for watches, votes, and rat records
- **Admin UI**: Razor Pages (consistent with existing admin pages)
- **Interactive components**: Existing `ComponentIdBuilder` and `IInteractionStateService` patterns

---

## Design Preferences

- **Tone**: Fun, playful, trash-talk friendly
- **Emoji usage**: Rat emoji 🐀 as primary branding
- **UI consistency**: Match existing admin UI design system

---

## Constraints

- **Missed watches**: If bot is offline when timer fires, skip (don't fire late)
- **Multiple watches**: A user can have multiple active watches (different times)
- **No duplicate watches**: Can't set two watches for same user at same time

---

## Open Questions

None currently — requirements are complete for MVP.

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Context menu over slash command | More intuitive UX, directly tied to a message |
| 5-minute voting window | Long enough for participation, short enough to stay relevant |
| One vote, locked in | Prevents gaming the system |
| Server-configurable timezone | Open source flexibility while defaulting to simple setup |
| Skip missed watches | Cleaner than late-firing, avoids confusion |

---

## Recommended Next Steps

1. **Create GitHub issues** for the feature (Epic → Features → Tasks)
2. **Design database schema** for RatWatch, RatVote, RatRecord entities
3. **Implementation planning** with systems-architect agent
4. **Build in phases**: Core command → Voting → Stats → Admin UI
