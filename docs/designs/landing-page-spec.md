# Public Landing Page Implementation Specification

**Version:** 1.0
**Created:** 2026-01-05
**Status:** Draft
**Requirements:** [landing-page.md](../requirements/landing-page.md)

---

## Overview

A public marketing-style landing page for anonymous visitors. Single scrolling page with sticky nav, feature showcase, tech stack, open source messaging, and subtle login access. No live data, just static content that sells the project.

The design phase (HTML prototype) must be completed and approved before any .NET implementation begins.

---

## Epic and Feature Breakdown

### Epic: Public Landing Page

A public-facing page that gives visitors context about the bot before asking them to log in.

### Feature 1: Design System and Prototype

**Priority:** Must complete first

Create an HTML prototype using existing design tokens and Tailwind CSS. This validates the layout, visual design, and responsive behavior before touching any .NET code.

**Deliverables:**
- HTML prototype at `docs/prototypes/features/landing-page/`
- Uses existing design tokens from `docs/articles/design-system.md`
- Responsive layout (mobile, tablet, desktop)
- Sticky navigation with section anchor links
- Hidden login button behavior (desktop hover only)
- Hero section with CTA
- Feature cards with Heroicons and placeholder screenshot areas
- "Coming Soon" badges for planned features
- Tech stack badges
- Open source messaging section
- About section with origin story
- Footer with GitHub link and version

**Acceptance Criteria:**
- [ ] Prototype renders correctly at all breakpoints
- [ ] Sticky nav scrolls with page and anchor links work
- [ ] Login button appears on hover (desktop) and is hidden on mobile
- [ ] Feature cards display current and coming soon features
- [ ] Uses existing color palette and typography tokens
- [ ] No Rat Watch feature mentioned
- [ ] Reviewed and approved before Feature 2 begins

### Feature 2: Landing Page Razor Implementation

**Priority:** After Feature 1 approval

Implement the approved design as a Razor Page in the existing .NET application.

**Deliverables:**
- Razor Page at `src/DiscordBot.Bot/Pages/Landing.cshtml`
- Page model at `src/DiscordBot.Bot/Pages/Landing.cshtml.cs`
- Anonymous access (no `[Authorize]` attribute)
- Static content matching approved prototype
- Version pulled from application (or hardcoded initially)

**Acceptance Criteria:**
- [ ] Page matches approved prototype design
- [ ] Accessible without authentication
- [ ] Responsive behavior matches prototype
- [ ] Login button hover behavior works on desktop
- [ ] All section anchor links function correctly
- [ ] Version displays in footer
- [ ] GitHub link works

### Feature 3: Navigation and Routing Updates

**Priority:** After Feature 2

Update routing so anonymous users see the landing page by default, while authenticated users go to the dashboard.

**Deliverables:**
- Update root route (`/`) behavior based on auth state
- Anonymous users see Landing page
- Authenticated users redirect to Dashboard
- Login page link from landing page works

**Acceptance Criteria:**
- [ ] Anonymous user hitting `/` sees Landing page
- [ ] Authenticated user hitting `/` sees Dashboard
- [ ] Login flow from landing page works correctly
- [ ] No broken navigation for existing users

---

## Implementation Order

```
Feature 1 (Prototype) --> Review/Approval --> Feature 2 (Razor) --> Feature 3 (Routing)
```

The prototype must be reviewed and approved before any .NET implementation begins. This prevents rework and ensures design decisions are locked in.

---

## Technical Considerations

### Razor Page Implementation

- Standard Razor Page (not Blazor component)
- No `[Authorize]` attribute - anonymous access
- Minimal PageModel - mostly static content
- Version can be pulled from `ApplicationOptions` or `Directory.Build.props`

### Design and Styling

- Reuse existing Tailwind CSS setup
- Use design tokens from `design-system.md`:
  - Background: `--color-bg-primary` (#1d2022)
  - Cards: `--color-bg-secondary` (#262a2d)
  - Text: `--color-text-primary`, `--color-text-secondary`
  - Accent: `--color-accent-orange` for primary CTA
  - Accent: `--color-accent-blue` for secondary elements
- Heroicons for feature icons (already in project)

### Sticky Navigation

- Fixed position header
- Smooth scroll to anchor sections
- Active section highlighting optional (nice to have)

### Hidden Login Button

- Desktop: positioned bottom-right, opacity 0 by default, opacity 1 on hover
- Mobile: hidden entirely (users can navigate to `/Account/Login` directly)
- CSS-only solution preferred

### Responsive Design

- Mobile-first approach
- Breakpoints per design system:
  - sm: 640px
  - md: 768px
  - lg: 1024px
  - xl: 1280px

### Static Content

- No live data from bot or database
- Feature list is hardcoded
- Version can be injected or hardcoded
- "Coming Soon" features marked with badge

---

## File Locations

| Artifact | Location |
|----------|----------|
| HTML Prototype | `docs/prototypes/features/landing-page/index.html` |
| Prototype CSS (if needed) | `docs/prototypes/features/landing-page/styles.css` |
| Razor Page | `src/DiscordBot.Bot/Pages/Landing.cshtml` |
| Page Model | `src/DiscordBot.Bot/Pages/Landing.cshtml.cs` |

---

## Content Checklist

### Features Section (Current)

| Feature | Icon | Description |
|---------|------|-------------|
| Moderation | shield-check | Warn, kick, ban, mute, auto-mod, watchlists |
| Analytics | chart-bar | Engagement and moderation metrics |
| Admin Dashboard | computer-desktop | Web-based guild management and configuration |
| Channel Management | chat-bubble-left-right | Scheduled messages, reminders, welcome messages |
| Observability | eye | Elastic APM, tracing, metrics, centralized logs |

### Features Section (Coming Soon)

| Feature | Icon | Description |
|---------|------|-------------|
| Audio/Soundboard | speaker-wave | Play sounds in voice channels |
| TTS Support | microphone | Text-to-speech in voice channels |
| Docker | cube | Containerized deployment |

### Tech Stack

- .NET 8
- Discord.NET
- Entity Framework Core
- Tailwind CSS
- Serilog
- OpenTelemetry
- Elastic APM
- SQLite / PostgreSQL

### Excluded Content

- Rat Watch (internal feature, not for public)
- "Add Bot to Server" invite link
- Documentation link
- Discord support server link
- Live bot stats

---

## Open Questions

Carried forward from requirements:
- Final project name for hero section
- Confirm MIT license before launch

---

## Related Documents

- [Requirements: landing-page.md](../requirements/landing-page.md)
- [Design System](../articles/design-system.md)
- Existing prototypes: `docs/prototypes/`
