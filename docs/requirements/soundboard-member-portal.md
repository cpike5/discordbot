# Requirements: Soundboard Guild Member Portal

## Problem Statement

Guild members currently have no self-service way to access the soundboard. They must either use Discord slash commands or rely on admins to manage sounds via the admin UI. This creates friction and limits engagement with the soundboard feature.

## Primary Purpose

Provide guild members with a simple, standalone web portal to play sounds, upload new sounds, and manage the bot's voice channel presence - without requiring admin access.

## Target Users

- **Guild Members**: Any Discord user who is a member of the guild (verified via Discord OAuth)

## Core Features (MVP)

### 1. Sound Button Grid
- Flat grid of all guild sounds
- Sort order: Favorites first, then alphabetical
- Each sound card displays:
  - Play button icon
  - Sound name
  - Play count
  - Star icon to toggle favorite
- Clicking a button plays the sound in the current voice channel
- Visual indicator confirms the click (button highlights/animates)

### 2. Search
- Search bar above the sounds grid
- Filter sounds by name as you type
- Maintains favorite-first ordering within filtered results

### 3. Favorites
- Star icon on each sound card to toggle favorite status
- Favorites stored in browser localStorage (device-specific)
- Favorites appear first in the grid before non-favorites

### 4. Voice Channel Selector
- Dropdown to view/select which voice channel the bot should join
- Shows current connection status (Connected/Disconnected badge)
- If bot isn't in a channel when play is clicked, prompt user to select one

### 5. Now Playing Indicator
- Shows currently playing sound (if any)
- Single sound playback only (no queue functionality)
- Replaces queue section shown in mockup

### 6. Sound Upload
- Any guild member can upload new sound files
- Drag-and-drop upload area
- Click to browse alternative
- Shows supported formats and limits from SoundboardOptions:
  - File types: MP3, WAV, OGG
  - Max file size: 5 MB
  - Max duration: 30 seconds
  - Max sounds per guild: 50

### 7. Play Count Tracking
- Track and display play count per sound
- Increment on each successful play
- Displayed on sound cards

### 8. Discord OAuth Authentication
- Unauthenticated users see a landing page before OAuth redirect
- Access granted only to verified guild members
- Dependency on issues #861 / #905 for guild membership verification

### 9. Unauthenticated Landing Page
- Displayed when user visits portal without being logged in
- Shows guild icon and guild name (from cached guild data)
- Brief explanation: "To access this soundboard, we need to verify you're a member of this server"
- Login button that triggers Discord OAuth flow
- Permission notice: "We'll request access to see your server memberships"
- Returns 404 if guild not found or portal not enabled (don't reveal which)
- After successful OAuth, redirects back to the portal page
- Dark theme matching the rest of the portal
- Soundboard-specific for now (not a shared pattern)

### 10. Admin Toggle
- Guild admins can enable/disable the member portal in guild settings
- When disabled, portal returns 404 or access denied for that guild

## Page Layout

Based on mockup, three-column layout on desktop:

```
+--------------------------------------------------+
| [Icon] Soundboard                      [Online]  |
|        Guild Name                                |
+--------------------------------------------------+
|                           |                      |
| Sounds            count   | Voice Channel  [status]
| [Search bar............]  | [Channel dropdown]   |
|                           |                      |
| [★] [★] [☆] [☆]          | Now Playing          |
| snd  snd  snd  snd        | [sound name or empty]|
| 4x   2x   1x   0x         |                      |
|                           +----------------------+
| [☆] [☆] [☆] [☆]          |                      |
| snd  snd  snd  snd        | Upload Sounds        |
|                           | [drag-drop area]     |
|                           |                      |
|                           | Supported Formats    |
|                           | - MP3, WAV, OGG      |
|                           | - Max 5 MB           |
|                           | - Max 30 seconds     |
|                           | - Max 50 per guild   |
+---------------------------+----------------------+
```

**Mobile Layout:**
- Single column stack
- Sound grid full-width at top
- Voice channel selector below
- Upload section at bottom

## Future Features

- User-defined sound categories/folders
- Sound attribution (who uploaded)
- Database-backed favorites (cross-device sync)
- Sound preview (play locally before broadcasting)
- Queue functionality (play multiple sounds in sequence)
- Role-based upload restrictions

## Out of Scope

- Delete sounds (admin UI only)
- Rename sounds (admin UI only)
- Sound file management beyond upload
- Role-based upload restrictions (all members can upload)

## Tech Stack

- **Frontend**: Razor Pages (standalone layout, not admin UI)
- **Backend**: Existing .NET 8 Web API + Discord.NET
- **Auth**: Discord OAuth + guild membership verification
- **Storage**: Existing sound file storage; localStorage for favorites

## Design Preferences

- **Style**: Clean, minimal standalone design - no admin navigation
- **Layout**: Mobile-friendly grid of sound buttons
- **Components**: Reuse existing UI components where appropriate
- **Theme**: Dark theme matching mockup (dark blue/slate background)

## URL Structure

- Portal page: `/portal/soundboard/{guildId}`
- Sets precedent for future member-facing pages under `/portal/`

## Dependencies

- Issue #861 / #905: Discord OAuth guild membership verification

## Database Changes

- Add `PlayCount` column to sound files table (or create separate play tracking table)
- Add `EnableMemberPortal` boolean to guild settings

## API Endpoints Needed

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/portal/soundboard/{guildId}/sounds` | GET | List sounds with play counts |
| `/api/portal/soundboard/{guildId}/sounds` | POST | Upload new sound |
| `/api/portal/soundboard/{guildId}/play/{soundId}` | POST | Play sound in voice channel |
| `/api/portal/soundboard/{guildId}/channels` | GET | List available voice channels |
| `/api/portal/soundboard/{guildId}/channel` | POST | Join voice channel |
| `/api/portal/soundboard/{guildId}/channel` | DELETE | Leave voice channel |
| `/api/portal/soundboard/{guildId}/status` | GET | Bot connection status, now playing |

## Constraints

- Upload limits governed by existing `SoundboardOptions` configuration
- Sound playback requires bot to have voice channel access in the guild
- Portal access requires guild to have soundboard feature enabled AND member portal enabled

## Security Considerations

- All endpoints require Discord OAuth authentication
- Guild membership must be verified before granting access
- Rate limiting on play and upload endpoints to prevent abuse
- Validate file uploads server-side (type, size, duration)

## Decisions Made

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Grid organization | Favorites first, then alphabetical | Surfaces most-used sounds while maintaining predictability |
| Upload permissions | Any guild member | Maximizes engagement, matches requested simplicity |
| Play feedback | Visual indicator only | Keeps UI clean, avoids notification fatigue |
| No channel handling | Prompt to select | Clear user guidance without assumptions |
| URL pattern | `/portal/soundboard/{guildId}` | Establishes pattern for future member portals |
| Attribution | None shown | Keeps UI minimal, reduces complexity |
| Page design | Separate minimal layout | Clean member experience, distinct from admin |
| Search | Search bar above grid | Familiar pattern, easy to find |
| Favorites | Star icons + localStorage | No database changes for favorites, acceptable trade-off |
| Admin control | Toggle in guild settings | Gives guilds control over feature availability |
| Queue | No queue, just "Now Playing" | Keeps MVP simple, queue can be added later |
| Play counts | Display on cards | Shows engagement, already in mockup |
| Unauthenticated flow | Landing page before OAuth | Better UX than immediate redirect, explains permissions needed |
| Landing page scope | Soundboard-specific | Keep it simple for MVP, can extract shared pattern later |
| Guild not found | Return 404 | Don't reveal whether guild exists or portal is disabled |

## Mockup Reference

See mockup image showing:
- Dark themed standalone page
- Sound grid with play buttons and counts
- Voice channel selector with status badge
- Upload area with drag-drop
- Queue section (simplified to "Now Playing" for MVP)
