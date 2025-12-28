# Dashboard Redesign - Design Specification

**Version:** 1.0
**Created:** 2025-12-27
**Status:** Proposed
**Target:** Discord Bot Admin UI v0.4.0

## Executive Summary

This specification outlines a comprehensive redesign of the Discord Bot Admin UI's main layout and dashboard. The redesign focuses on improving information hierarchy, enhancing visual consistency, and creating a more modern, scalable interface.

### Key Improvements

- **Enhanced Visual Hierarchy**: Clear distinction between primary metrics, secondary data, and contextual information
- **Improved Information Density**: Better use of space without overwhelming users
- **Modernized Component Design**: Updated card styles, refined spacing, and improved micro-interactions
- **Better Status Communication**: More prominent bot status and real-time activity indicators
- **Streamlined Navigation**: Simplified sidebar with better organization and visual feedback
- **Enhanced Responsiveness**: Optimized layouts for all screen sizes from mobile to ultra-wide displays

## Layout Structure

### Overall Page Layout

```
┌─────────────────────────────────────────────────────────────────┐
│                        Top Navigation Bar (64px)                 │
├──────────┬──────────────────────────────────────────────────────┤
│          │              Page Header (Variable)                   │
│          │  ┌─────────────────────────────────────────────┐     │
│          │  │ Title, Subtitle, Actions                     │     │
│          │  └─────────────────────────────────────────────┘     │
│  Sidebar │              Hero Metrics (4 cards)                  │
│  (256px) │  ┌────────┐  ┌────────┐  ┌────────┐  ┌────────┐    │
│          │  │        │  │        │  │        │  │        │    │
│          │  └────────┘  └────────┘  └────────┘  └────────┘    │
│          │           Primary Content Grid (2/3 + 1/3)           │
│          │  ┌──────────────────────┐  ┌───────────────┐         │
│          │  │   Server Table       │  │   Activity    │         │
│          │  │                      │  │     Feed      │         │
│          │  └──────────────────────┘  └───────────────┘         │
│          │              Quick Actions Panel                      │
│          │  ┌───────────────────────────────────────────┐       │
│          │  │  Add Server | Commands | Logs | Restart   │       │
│          │  └───────────────────────────────────────────┘       │
└──────────┴──────────────────────────────────────────────────────┘
```

### Layout Specifications

#### Top Navigation Bar
- **Height**: 64px
- **Background**: `#262a2d`
- **Border**: 1px solid `#3f4447` on bottom
- **Components**: Mobile menu toggle, Logo, Search, Notifications, User menu

#### Sidebar Navigation
- **Width**: 256px (collapsible to 72px on desktop, slide-out on mobile)
- **Position**: Fixed left
- **Background**: `#262a2d`
- **Sections**: Main Navigation, Admin, Developer Tools, Support, Bot Status Footer

#### Main Content Area
- **Margin Left**: 256px on lg+, 0 on mobile
- **Padding**: 32px on lg+, 16px on mobile
- **Background**: `#1d2022`

## Dashboard Components

### 1. Bot Status Banner
Prominent banner showing bot connection status, uptime, and key metrics.

### 2. Hero Metrics Cards (4 cards)
| Card | Accent Color | Icon |
|------|-------------|------|
| Total Servers | Blue `#098ecf` | Server |
| Active Users | Green `#10b981` | Users |
| Commands Today | Orange `#cb4e1b` | Terminal |
| Uptime | Cyan `#06b6d4` | Clock |

**Card Features:**
- Gradient background
- Icon badge (top right)
- Large metric value (40px)
- Trend indicator with arrow
- Hover: lift effect, accent border

### 3. Connected Servers Table
- Server avatar with gradient
- Member count
- Command usage
- Status badge (Online/Idle/Offline)
- Action menu

### 4. Activity Feed with Timeline
- Vertical timeline connector
- Colored status dots (success, info, warning, error)
- Command code highlighting
- Relative timestamps

### 5. Quick Actions Panel
6 action cards in responsive grid:
- Add Server (primary)
- Manage Commands
- View Logs
- Export Data
- Settings
- Restart Bot (warning)

## Visual Improvements

### Enhanced Card Design
```css
.card-enhanced {
  background-color: #262a2d;
  border: 1px solid #3f4447;
  border-radius: 12px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.2);
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
}

.card-enhanced:hover {
  border-color: rgba(9, 142, 207, 0.3);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
  transform: translateY(-1px);
}
```

### Sidebar Active State
```css
.sidebar-link.active {
  background: linear-gradient(90deg, rgba(203, 78, 27, 0.15) 0%, transparent 100%);
  color: #d7d3d0;
}

.sidebar-link.active::before {
  content: '';
  position: absolute;
  left: 0;
  width: 3px;
  height: 24px;
  background: #cb4e1b;
  border-radius: 0 2px 2px 0;
}
```

## Responsive Design

### Breakpoints
| Breakpoint | Width | Layout Changes |
|------------|-------|----------------|
| Mobile | < 768px | Sidebar hidden, 1 column metrics, stacked content |
| Tablet | 768-1023px | Sidebar toggle, 2 column metrics |
| Desktop | 1024-1279px | Sidebar visible, 4 column metrics, 2/3+1/3 split |
| Large | 1280px+ | Increased spacing, max-width 1600px |

## Prototype Files

| File | Description |
|------|-------------|
| `main-layout.html` | Main layout with navbar, collapsible sidebar, content structure |
| `dashboard.html` | Complete dashboard with all components |

## Implementation Phases

### Phase 1: Dashboard Only (v0.4.0-alpha)
- Dashboard page redesign
- Feature flag: `Features:EnhancedDashboard`

### Phase 2: Core Pages (v0.4.0-beta)
- Servers, Commands, Settings pages
- Shared layout components

### Phase 3: Full Migration (v0.4.0)
- All pages migrated
- Old components removed

## Color Reference

From existing design system (`docs/articles/design-system.md`):

- Primary Background: `#1d2022`
- Secondary Background: `#262a2d`
- Text Primary: `#d7d3d0`
- Text Secondary: `#a8a5a3`
- Accent Orange: `#cb4e1b`
- Accent Blue: `#098ecf`
- Border Primary: `#3f4447`
- Success: `#10b981`
- Warning: `#f59e0b`
- Error: `#ef4444`

## References

- [Design System](../../articles/design-system.md)
- [WCAG 2.1 AA Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)
