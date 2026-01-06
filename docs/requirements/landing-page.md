# Requirements: Public Landing Page

## Executive Summary

A public landing page for anonymous visitors that showcases the bot's features and links to the GitHub project. Single scrolling page with a clean, minimal design. Think marketing page, but casual.

**Target Release:** TBD

---

## Problem Statement

Right now, unauthenticated users just hit a login page with no context. The landing page gives visitors an idea of what the bot does before asking them to log in.

## Primary Purpose

Showcase the bot and link to the GitHub repo for anyone who lands on the URL without being logged in.

## Target Users

- Anyone who reaches the bot URL and isn't logged in
- Server owners checking out bot options
- Developers who might want to fork or learn from the project

---

## Page Structure

Single scrolling page with sticky navigation header.

### 1. Navigation Header (Sticky)

- Section anchor links: Features, Tech Stack, Open Source, About
- **Login button**: Bottom right corner, hidden until hover (desktop only)
  - Intentionally subtle — for people who know to look for it
  - Mobile users can go to `/login` directly

### 2. Hero Section

- Project name (e.g., "cpike's Discord Bot")
- Casual tagline: "A Discord bot that does a bunch of stuff."
- Emphasis: Free. Open source. Yours to use.
- Primary CTA: View on GitHub button

### 3. Features Section

Feature cards with placeholder areas for screenshots (to be added later). Use Heroicons for visual interest.

**Current Features:**

| Feature | Description |
|---------|-------------|
| Moderation | Warn, kick, ban, mute, auto-mod, watchlists |
| Analytics | Engagement and moderation metrics |
| Admin Dashboard | Web-based guild management and configuration |
| Channel Management | Scheduled messages, reminders, welcome messages |
| Observability | Elastic APM, tracing, metrics, centralized logs |

**Coming Soon (with badge):**

| Feature | Description |
|---------|-------------|
| Audio/Soundboard | Play sounds in voice channels |
| TTS Support | Text-to-speech in voice channels |
| Docker | Containerized deployment |

### 4. Tech Stack Section

Simple list or badge-style display:

- .NET 8
- Discord.NET
- Entity Framework Core
- Tailwind CSS
- Serilog
- OpenTelemetry
- Elastic APM
- SQLite / PostgreSQL

### 5. Open Source Section

- MIT License
- Messaging: "Fork it, customize it, make it yours."
- **Not** seeking contributors to the main repo — this is a personal project opened up for others to use
- Quick-start callout: Prerequisites (`.NET 8 SDK`, Discord Developer Portal setup) with link to README

### 6. About Section

Brief origin story:

> Started as a side project to learn .NET and have a bot for my friends' server. Still adding stuff. Feel free to use it or fork it.

### 7. Footer

- GitHub link
- Current version (pulled from app or hardcoded)
- Copyright (e.g., "© 2025 cpike")
- No "Made with heart" or similar

---

## Design Requirements

- **Style**: Clean, minimal design using existing admin UI design tokens and theme
- **Visuals**: Heroicons for feature icons, placeholder areas for screenshots
- **Responsive**: Mobile-friendly layout
- **Login button**: Desktop only, bottom-right, visible on hover

## Technical Implementation

- Razor Page (fits existing stack)
- Anonymous access — no authentication required
- Static content — no live data from the bot
- Reuse existing Tailwind setup and design tokens

---

## Excluded

- Rat Watch feature (inside joke, not for public showcase)
- "Add Bot to Server" invite link
- Documentation link
- Discord support server link
- Live bot stats

---

## Open Questions

- Final project name for hero (currently "cpike's Discord Bot")
- Confirm MIT license before launch

---

## Related Documents

- [audio-support.md](audio-support.md) — Audio/Soundboard feature requirements
- [docker-containerization.md](docker-containerization.md) — Docker deployment requirements
- [design-system.md](../articles/design-system.md) — UI design tokens and theme
