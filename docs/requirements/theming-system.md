# Requirements: Theming System

## Executive Summary

Implement a flexible theming system that allows users to choose between light and dark themes for the admin UI. The system will support user-level theme preferences with admin-configurable defaults, starting with two themes: the existing "Discord Dark" theme and a new "Purple Dusk" light theme.

## Problem Statement

The current admin UI only supports a single Discord-inspired dark theme. Users have different visual preferences and working environments. Some prefer lighter themes for daytime work or personal aesthetic reasons. The application needs a theming infrastructure that supports multiple color schemes while maintaining visual consistency and accessibility.

## Primary Purpose

Enable users to personalize their admin UI experience by choosing between light and dark themes, with a foundation that makes adding future themes straightforward.

## Target Users

- **All authenticated users**: Can set personal theme preference via user profile page
- **SuperAdmin users**: Can configure the default theme for new users via Admin Settings
- **System**: Falls back to hardcoded default when no preferences are set

## Core Features (MVP)

### 1. Two Initial Themes

#### Discord Dark (Existing)
- Current default theme
- Dark charcoal backgrounds (#1d2022, #262a2d, #2f3336)
- Light warm gray text (#d7d3d0, #a8a5a3, #7a7876)
- Orange primary accent (#cb4e1b)
- Blue secondary accent (#098ecf)
- Optimized for low-light environments

#### Purple Dusk (New Light Theme)
- Warm light beige backgrounds (#E8E3DF, #DAD4D0, #CCC5C0)
- Deep purple text (#4F214A, #614978, #887A99)
- Purple primary accent (#614978)
- Pink secondary accent (#D5345B)
- Sophisticated, warm, light theme

### 2. Theme Preference Hierarchy

**Priority order:**
1. **User preference** - Individual user's saved theme choice
2. **Admin default** - Default theme configured by SuperAdmin
3. **System default** - Hardcoded fallback (Discord Dark)

### 3. Admin Settings - Appearance Tab

**Location:** `/Admin/Settings` page, new "Appearance" tab

**Features:**
- Theme dropdown selector (Discord Dark, Purple Dusk)
- "This will be the default theme for all users who haven't set a personal preference"
- Save button (applies after save, no live preview)
- Follows existing Settings page patterns (tabbed interface, category save/reset)

**Authorization:** SuperAdmin only (consistent with Settings page)

### 4. User Profile Page - Theme Selector

**Location:** New `/Account/Profile` page

**Features:**
- Theme dropdown selector (Discord Dark, Purple Dusk)
- "Save Preferences" button (applies after save)
- Simple, focused page - just theme preference for MVP
- Show current theme selection on page load

**Authorization:** Any authenticated user

**Note:** Full user profile functionality (display name, email, notifications, etc.) is out of scope for this implementation and should be properly spec'd out later.

### 5. Theme Application Mechanism

**User Experience:**
- User selects theme from dropdown
- Clicks "Save" button
- Page reloads with new theme applied
- No live preview required for MVP

**Technical Implementation:**
- Theme stored in database (user preference or admin default)
- Theme applied via CSS variables at page load
- CSS variable values determined by selected theme
- Theme persists across sessions via cookie or session storage

### 6. Theme Infrastructure

**Design for extensibility:**
- Theme definitions stored as structured data (JSON, enum, or theme class)
- Each theme defines complete color palette mapping
- CSS variables used throughout application
- Easy to add new themes by creating new theme definition

**Theme Definition Structure (example):**
```json
{
  "id": "purple-dusk",
  "name": "Purple Dusk",
  "displayName": "Purple Dusk",
  "description": "Light theme with warm beige backgrounds and purple accents",
  "colors": {
    "bg-primary": "#E8E3DF",
    "bg-secondary": "#DAD4D0",
    "text-primary": "#4F214A",
    ...
  }
}
```

## Purple Dusk Theme - Complete Color Specification

| Category | CSS Variable | Hex Code | HSL | Usage |
|----------|-------------|----------|-----|-------|
| **Backgrounds** | `--color-bg-primary` | `#E8E3DF` | 25° 18% 89% | Main page background |
| | `--color-bg-secondary` | `#DAD4D0` | 26° 14% 83% | Cards, panels |
| | `--color-bg-tertiary` | `#CCC5C0` | 27° 12% 77% | Modals, elevated elements |
| | `--color-bg-hover` | `#C0B8B2` | 27° 9% 66% | Interactive hover surfaces |
| **Text** | `--color-text-primary` | `#4F214A` | 307° 41% 22% | Main content |
| | `--color-text-secondary` | `#614978` | 280° 24% 41% | Labels, less important text |
| | `--color-text-tertiary` | `#887A99` | 275° 12% 54% | Disabled, placeholders |
| | `--color-text-inverse` | `#E8E3DF` | 25° 18% 89% | Light text on dark buttons |
| **Purple Accent** | `--color-accent-purple` | `#614978` | 280° 24% 41% | Primary actions, CTAs, active nav |
| | `--color-accent-purple-hover` | `#7A5C8F` | 280° 24% 46% | Hover state |
| | `--color-accent-purple-active` | `#4F214A` | 307° 41% 22% | Active/pressed state |
| | `--color-accent-purple-muted` | `#61497833` | — | 20% opacity - subtle backgrounds |
| **Pink Accent** | `--color-accent-pink` | `#D5345B` | 346° 66% 52% | Secondary actions, highlights |
| | `--color-accent-pink-hover` | `#E5476D` | 346° 66% 58% | Hover state |
| | `--color-accent-pink-active` | `#B82A4D` | 346° 66% 45% | Active/pressed state |
| | `--color-accent-pink-muted` | `#D5345B33` | — | 20% opacity - subtle backgrounds |
| **Borders** | `--color-border-primary` | `#C0B8B2` | 27° 10% 72% | Default borders |
| | `--color-border-secondary` | `#DAD4D0` | 26° 14% 83% | Subtle dividers |
| | `--color-border-focus` | `#614978` | 280° 24% 41% | Focus rings (purple accent) |

**Semantic Colors (Shared Across Themes):**
- Success: `#10b981` (emerald green)
- Warning: `#f59e0b` (amber)
- Error: `#ef4444` (red)
- Info: `#06b6d4` (cyan)

## Out of Scope (MVP)

- System default option (follows browser/OS dark/light preference)
- Live preview before saving
- Per-guild theming
- More than 2 themes
- Theme customization/editing in UI
- Custom user-created themes
- Full user profile page (display name, avatar, email, notification preferences, etc.)
- Theme preview thumbnails in selector
- Accessibility mode (high contrast theme)

## Future Considerations

### System Default Option
Allow users to choose "System Default" which automatically switches between light/dark themes based on browser/OS preference using `prefers-color-scheme` media query.

### Full User Profile Page
Properly spec out complete user profile functionality:
- Display name and avatar
- Email address management
- Password change
- Notification preferences
- Connected Discord accounts
- Privacy settings
- Session management

### Additional Themes
- High contrast themes for accessibility
- Additional color schemes (blue, green, etc.)
- Community-contributed themes

## Technical Requirements

### Database Schema Changes

**New Table: `Themes`**
```sql
CREATE TABLE Themes (
    Id INT PRIMARY KEY IDENTITY(1,1),
    ThemeKey NVARCHAR(50) NOT NULL UNIQUE,  -- 'discord-dark', 'purple-dusk'
    DisplayName NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500),
    ColorDefinition NVARCHAR(MAX) NOT NULL, -- JSON color palette
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
)
```

**Modify Table: `AspNetUsers`**
```sql
ALTER TABLE AspNetUsers
ADD PreferredThemeId INT NULL,
CONSTRAINT FK_Users_Themes FOREIGN KEY (PreferredThemeId) REFERENCES Themes(Id)
```

**New Application Setting: `DefaultThemeId`**
Store in existing settings system or add to `ApplicationOptions`:
```json
{
  "Application": {
    "DefaultThemeId": 1  // or store as ThemeKey string
  }
}
```

### Backend Implementation

**New Files:**
```
src/DiscordBot.Core/
├── Entities/
│   └── Theme.cs                       # Theme entity
├── Enums/
│   └── ThemeType.cs                   # Enum: DiscordDark, PurpleDusk
└── Interfaces/
    └── IThemeService.cs               # Theme business logic

src/DiscordBot.Infrastructure/
├── Data/
│   └── Migrations/
│       └── {timestamp}_AddThemeSupport.cs
└── Services/
    └── ThemeService.cs                # Implementation

src/DiscordBot.Bot/
├── Pages/
│   ├── Admin/
│   │   └── Settings.cshtml            # Add Appearance tab
│   └── Account/
│       ├── Profile.cshtml             # New user profile page
│       └── Profile.cshtml.cs
└── Controllers/Api/
    └── ThemeController.cs             # API for theme operations
```

**Theme Service Interface:**
```csharp
public interface IThemeService
{
    Task<Theme> GetThemeByKeyAsync(string themeKey);
    Task<Theme> GetUserThemeAsync(string userId);
    Task<Theme> GetDefaultThemeAsync();
    Task<IEnumerable<Theme>> GetActiveThemesAsync();
    Task SetUserThemeAsync(string userId, int themeId);
    Task SetDefaultThemeAsync(int themeId);
}
```

### Frontend Implementation

**CSS Variable System:**
- Root CSS file with theme variables
- Theme-specific CSS files for each theme
- Dynamic class application based on selected theme
- Tailwind configuration updated to use theme variables

**Theme Application:**
```html
<!-- Applied to <html> or <body> tag -->
<html data-theme="purple-dusk">
```

**CSS Structure:**
```css
/* Base variables (Discord Dark - default) */
:root {
  --color-bg-primary: #1d2022;
  --color-text-primary: #d7d3d0;
  /* ... */
}

/* Purple Dusk theme override */
[data-theme="purple-dusk"] {
  --color-bg-primary: #E8E3DF;
  --color-text-primary: #4F214A;
  /* ... */
}
```

**Tailwind Config Update:**
```javascript
// tailwind.config.js
module.exports = {
  theme: {
    extend: {
      colors: {
        'bg-primary': 'var(--color-bg-primary)',
        'text-primary': 'var(--color-text-primary)',
        // ... map all theme variables
      }
    }
  }
}
```

### Routes

| Page | URL | Authorization | Description |
|------|-----|---------------|-------------|
| User Profile | `/Account/Profile` | Authenticated | User theme preference |
| Admin Settings | `/Admin/Settings` | SuperAdmin | Default theme configuration (existing page, add Appearance tab) |

### API Endpoints

| Method | Endpoint | Authorization | Description |
|--------|----------|---------------|-------------|
| `GET` | `/api/theme/available` | Authenticated | List all active themes |
| `GET` | `/api/theme/current` | Authenticated | Get current user's theme |
| `POST` | `/api/theme/user` | Authenticated | Set user theme preference |
| `POST` | `/api/theme/default` | SuperAdmin | Set system default theme |

## User Flows

### User Changes Theme Preference

1. User navigates to `/Account/Profile`
2. Sees current theme selection in dropdown
3. Selects "Purple Dusk" from dropdown
4. Clicks "Save Preferences" button
5. Page reloads with new theme applied
6. Theme persists across sessions

### Admin Sets Default Theme

1. SuperAdmin navigates to `/Admin/Settings`
2. Clicks "Appearance" tab
3. Sees current default theme in dropdown
4. Selects "Purple Dusk" from dropdown
5. Clicks "Save Changes" button
6. Success alert: "Default theme updated. New users will see Purple Dusk theme."
7. Existing users with no preference now see new default

### New User First Login

1. User logs in for first time (no theme preference set)
2. System checks user preference (none found)
3. System checks admin default theme setting
4. Applies admin default or system default (Discord Dark)
5. User can change preference anytime via Profile page

## Design Specifications

### User Profile Page

**Layout:**
- Page title: "Profile"
- Subtitle: "Manage your personal preferences"
- Simple card with theme selector
- Future-proof layout for additional profile settings

**Theme Selector:**
- Label: "Theme Preference"
- Dropdown with theme options (name + description)
- Help text: "Choose your preferred color scheme"
- "Save Preferences" button (primary purple action button)

### Admin Settings - Appearance Tab

**Tab Location:** Fourth tab after General, Features, Advanced

**Tab Icon:** Palette icon (consistent with design system)

**Card Layout:**
- Card title: "Appearance Settings"
- Card subtitle: "Configure the default theme for your application"
- Theme selector dropdown
- Help text: "This will be the default theme for all users who haven't set a personal preference"
- "Save Changes" button (orange action button, consistent with other tabs)
- "Reset" button (secondary)

## Accessibility Considerations

- All color combinations must meet WCAG 2.1 AA contrast ratios
- Purple Dusk theme text/background contrasts validated
- Focus indicators visible in both themes
- Theme switching doesn't break keyboard navigation
- Screen reader announcements for theme changes

## Testing Requirements

### Unit Tests
- ThemeService: GetUserTheme, GetDefaultTheme, SetUserTheme, SetDefaultTheme
- Theme hierarchy logic (user > admin > system)
- Theme entity validation

### Integration Tests
- User can change theme preference via Profile page
- Admin can change default theme via Settings
- Theme persists across sessions
- New users receive correct default theme

### Visual Tests
- All UI components render correctly in both themes
- Hover states work in both themes
- Focus states visible in both themes
- Cards, buttons, forms, tables, badges all tested

### Browser Testing
- Chrome, Firefox, Safari, Edge
- Mobile responsive views in both themes

## Open Questions

None - all requirements clarified.

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Start with 2 themes only | Validate infrastructure before adding more themes |
| User preference > Admin default > System default | Gives users control while allowing org-wide defaults |
| Apply theme after save (no preview) | Simpler implementation, still good UX for MVP |
| Application-wide (not per-guild) | User preference should be consistent, not context-dependent |
| CSS variables for theming | Standard, performant, widely supported approach |
| Store themes in database | Allows dynamic theme management without code changes |
| Defer system default (prefers-color-scheme) | Nice-to-have, not essential for MVP |
| Defer full user profile page | Theme switcher only for MVP, profile needs proper spec |
| Purple Dusk name | Descriptive, evocative, matches aesthetic |
| Appearance tab in Settings | Natural location, consistent with settings structure |

## Recommended Next Steps

1. **Review** this requirements document for final approval

2. **Create prototype** in `docs/prototypes/features/theming/` to visualize:
   - User profile page with theme switcher
   - Admin settings Appearance tab
   - Side-by-side comparison of Discord Dark vs Purple Dusk themes

3. **Update design system documentation** (`docs/articles/design-system.md`):
   - Document Purple Dusk theme color palette
   - Document theming architecture and CSS variable system
   - Document how to add new themes in the future

4. **Create GitHub issues** via `/create-issue`:
   - Backend: Theme infrastructure (database, entities, services, API)
   - Frontend: CSS variable system and theme definitions
   - UI: User profile page with theme selector
   - UI: Admin settings Appearance tab
   - Testing: Theme system test suite
   - Documentation: Update design system docs

5. **Generate implementation plan** via systems-architect to:
   - Define complete file structure
   - Plan database migration strategy
   - Identify code reuse opportunities
   - Define testing strategy
   - Plan rollout approach (feature flag if needed)

## Reference Documents

- [Design System](../articles/design-system.md) - Current design tokens and color palette
- [Settings Page](../../src/DiscordBot.Bot/Pages/Admin/Settings.cshtml) - Existing settings implementation pattern
- [Authorization Policies](../articles/authorization-policies.md) - Role-based access control
- [Form Implementation Standards](../articles/form-implementation-standards.md) - Razor Pages form patterns

## Prototype Files

- [Purple Dusk Preview](../../docs/prototypes/features/theming/purple-dusk-preview.html) - Initial color exploration
- Lovable refined preview (React component) - Refined color palette with interactive components
