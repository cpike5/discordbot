# Requirements: TTS Portal Page

## Executive Summary

Create a new TTS Portal page (`/Portal/TTS/{guildId}`) that mirrors the existing Soundboard Portal pattern - a lightweight, user-facing interface for guild members to send TTS messages to voice channels without requiring admin permissions.

## Problem Statement

Currently, only the admin TTS management page exists (`/Guilds/TextToSpeech/{guildId}`), which requires admin authorization. Guild members who want to use TTS must use Discord slash commands. We need a web-based portal that any authenticated guild member can access to send TTS messages with a simple, streamlined interface.

## Primary Purpose

Provide a lightweight, user-friendly portal where authenticated guild members can compose and send TTS messages to voice channels with real-time feedback and voice channel control - following the exact same pattern as the existing Soundboard Portal.

## Target Users

- **Guild Members**: Any Discord user authenticated via OAuth who is a member of the guild
- **No admin/moderator role required**
- **Access Pattern**: Identical to Soundboard Portal - Discord OAuth authentication validates guild membership

## Core Features (MVP)

### 1. Landing Page (Unauthenticated Users)
- Guild icon display (or first letter placeholder)
- Guild name prominently displayed
- "TTS Portal" badge
- Description: "To access text-to-speech, we need to verify you're a member of this server"
- Discord OAuth login button
- Permissions notice (same as Soundboard portal)
- Privacy policy link

### 2. Portal Header (Authenticated Users)
- Bot icon
- "Text-to-Speech" title
- Guild name subtitle
- Online/Offline status badge (bot status)

### 3. Sidebar (Left - Fixed 300px)

#### Voice Channel Panel
- Connection status badge (Connected/Disconnected)
- Channel selector dropdown
- Join/Leave buttons (with loading states)
- Now Playing section (shows current TTS message playing)
- Empty state when nothing playing

#### TTS Configuration Panel
- Voice selector dropdown (categorized - English US, English UK, etc.)
- Speed slider (0.5x - 2.0x, default 1.0x)
- Pitch slider (0.5x - 2.0x, default 1.0x)
- Session-based values (not persisted)

### 4. Main Content (Right - Flexible)

#### TTS Message Composer
- Large textarea for message input
- Character counter (e.g., "0/500")
- Real-time character count updates
- Warning color when approaching limit
- Error color when at limit
- "Send to Channel" button (primary action)
- Loading state during synthesis ("Sending...")
- Disabled state when not connected to voice channel

### 5. AJAX Interactions
- All form submissions via AJAX (no page reloads)
- Toast notification system:
  - Success: "TTS message sent successfully"
  - Error: Specific error messages (bot not connected, Azure unavailable, message too long, etc.)
  - Warning: "Please join a voice channel first!"
  - Info: "Disconnected from voice channel"
- Loading states on buttons during API calls
- Form resets on successful submission (clear textarea, keep voice/speed/pitch)

### 6. Real-Time Status Polling
- Poll `/api/portal/tts/{guildId}/status` every 3 seconds
- Update connection status if changed externally
- Update now playing status
- Clear playing state when playback finishes

### 7. Voice Channel Highlighting
- When user tries to send TTS without being connected
- Highlight channel selector with warning color
- Show toast: "Please join a voice channel first!"

## Out of Scope

- Stats cards (Messages Today, Total Playback, etc.)
- Message history display
- Toggle switches (Auto-play on send, Announce joins/leaves)
- Volume slider
- Preview functionality
- Persistent user preferences across sessions
- Admin settings management
- Replay previous messages
- Favorites/bookmarks

## Technical Requirements

### Routes
- **Landing/Portal page**: `/Portal/TTS/{guildId}`
- **Authorization**: Authenticated user with guild membership (same as Soundboard portal)

### API Endpoints (To be created)
- `POST /api/portal/tts/{guildId}/send` - Send TTS message
- `GET /api/portal/tts/{guildId}/status` - Get bot connection status
- `POST /api/portal/tts/{guildId}/channel` - Join voice channel
- `DELETE /api/portal/tts/{guildId}/channel` - Leave voice channel

### Backend
- Continue logging to `TtsMessages` table (GuildId, UserId, Username, Message, Voice, DurationSeconds, CreatedAt)
- Use existing `ITtsService` for Azure Speech synthesis
- Use existing `IAudioService` for voice channel management
- Respect existing rate limiting from `GuildTtsSettings.RateLimitPerMinute`
- Validate guild membership via Discord OAuth claims

### Frontend
- Custom CSS (similar to Portal/Soundboard styling)
- Dark theme color palette
- Toast notification system (similar to Soundboard portal)
- AJAX with fetch API
- Responsive design: sidebar stacks on top on mobile

### File Structure
```
Pages/
├── Portal/
│   ├── TTS/
│   │   ├── Index.cshtml          # New TTS portal page
│   │   └── Index.cshtml.cs       # New PageModel
│   ├── Soundboard/
│   │   └── Index.cshtml          # Existing reference
│   ├── _PortalLayout.cshtml      # Existing shared layout
│   ├── _ViewImports.cshtml       # Existing
│   └── _ViewStart.cshtml         # Existing

Controllers/Api/Portal/
├── TtsPortalController.cs        # New API controller
└── SoundboardPortalController.cs # Existing reference
```

## Design Specifications

### Layout
- Two-column layout (same as Soundboard):
  - Left sidebar: 300px fixed width
  - Right main content: Flexible width
- Responsive: Stack vertically on tablets/mobile
- Max container width: 1600px
- Padding: 1.5rem

### Color Palette (Match Soundboard Portal)
- Background primary: `#1d2022`
- Background secondary: `#262a2d`
- Background tertiary: `#1a1d23`
- Border primary: `#40444b`
- Text primary: `#dbdee1`
- Text secondary: `#d7d3d0`
- Text tertiary: `#949ba4`
- Accent orange: `#cb4e1b` (TTS uses orange instead of blue)
- Accent orange hover: `#e5591f`
- Success: `#57ab5a`
- Error: `#ef4444`
- Warning: `#fbbf24`
- Info: `#3b82f6`

### Typography
- Headers: Bold, 1.125rem
- Body: 0.875rem
- Labels: 0.75rem uppercase, letter-spacing 0.05em

### Components
- Sliders: Range input with visual thumb, 0.5x-2.0x range
- Dropdowns: Custom styled select with chevron icon
- Buttons: Rounded 0.5rem, hover states, disabled states
- Toast notifications: Bottom-right corner, slide-in animation, auto-dismiss
- Text area: Dark background, border, rounded corners, character counter overlay

## User Flow

1. **User navigates to `/Portal/TTS/{guildId}`**
   - If not authenticated → Landing page with Discord OAuth button
   - If authenticated but not guild member → 403 error
   - If authenticated and guild member → TTS Portal interface

2. **User arrives at TTS Portal**
   - Sees bot online/offline status in header
   - Sees voice channel panel in sidebar (disconnected state)
   - Sees TTS configuration sliders
   - Sees empty textarea in main content

3. **User selects voice channel and joins**
   - Selects channel from dropdown
   - Clicks "Join" button
   - Button shows loading state ("Joining...")
   - Toast notification: "Connected to voice channel"
   - Now Playing section becomes active

4. **User composes TTS message**
   - Types message in textarea
   - Character counter updates in real-time
   - Optionally adjusts voice, speed, pitch sliders
   - Clicks "Send to Channel" button

5. **TTS message sent**
   - Button shows loading state ("Sending...")
   - AJAX POST to `/api/portal/tts/{guildId}/send`
   - Azure synthesizes speech (1-3 seconds)
   - Audio plays in voice channel
   - Now Playing panel updates with message text
   - Toast notification: "TTS message sent successfully"
   - Textarea clears, sliders remain at current values

6. **Error handling**
   - Bot not connected: Toast warning "Please join a voice channel first!" + highlight channel selector
   - Azure unavailable: Toast error "TTS service unavailable. Please try again."
   - Message too long: Toast error "Message exceeds 500 character limit"
   - Rate limited: Toast warning "You're sending messages too quickly. Please wait."

## Open Questions

None - all requirements clarified based on existing Soundboard portal pattern.

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Separate portal page (not redesign existing admin page) | Maintains clean separation between admin management and user interaction |
| Follow Soundboard portal pattern exactly | Proven pattern, consistent UX, reusable code structure |
| Session-based settings (not persisted) | Simpler implementation, fresh state on page load |
| No stats/history in portal | Portal is for sending messages, not analytics or management |
| No tabbed navigation between portals | Each portal is standalone, users navigate via URLs/links |
| Orange accent color for TTS | Differentiate from Soundboard (blue), matches existing TTS branding |
| Continue backend logging | Critical for analytics, auditing, and usage tracking |
| Toast notifications bottom-right | Matches Soundboard portal, non-intrusive |

## Recommended Next Steps

1. **Review** this requirements document with the user for final approval

2. **Create prototype** in `docs/prototypes/features/tts-portal/` to visualize the UI

3. **Create GitHub issues** via `/create-issue`:
   - TTS Portal UI implementation (frontend + Razor Pages)
   - TTS Portal API controller (backend endpoints)
   - Update Portal landing/navigation (if needed)

4. **Generate implementation plan** via systems-architect or `/plan` to:
   - Define file structure and class hierarchy
   - Plan API endpoints and request/response models
   - Identify code reuse opportunities from Soundboard portal
   - Define testing strategy

## Reference Documents

- [Soundboard Portal Implementation](../../src/DiscordBot.Bot/Pages/Portal/Soundboard/Index.cshtml) - Existing reference implementation
- [TTS Support Documentation](../articles/tts-support.md) - TTS feature overview and configuration
- [Soundboard Documentation](../articles/soundboard.md) - Soundboard feature overview
- [Form Implementation Standards](../articles/form-implementation-standards.md) - AJAX patterns and validation
- [Design System](../articles/design-system.md) - UI tokens and component specifications
