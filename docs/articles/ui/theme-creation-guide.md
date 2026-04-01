# Theme Creation Guide

**Version:** 1.0
**Last Updated:** 2026-02-19

---

## Overview

The admin UI theming system lets you add new color schemes without touching any component markup. Themes are defined as CSS custom property blocks in `site.css`, identified by a `data-theme` attribute on the `<html>` element, and registered in the database so the `ThemeService` can resolve and serve them.

### How the System Works

1. **CSS custom properties** — All colors in the UI are expressed as `var(--color-*)` references. The `:root` block in `site.css` defines the default values (Discord Dark). A theme replaces those values by overriding the same property names under a `[data-theme="your-key"]` selector.

2. **`data-theme` attribute** — The `ThemeService.GetCurrentThemeKeyAsync()` method determines the active theme key during server-side rendering and writes it to `<html data-theme="...">`. The `ThemeManager` JavaScript module keeps the attribute in sync on the client and persists it in a cookie.

3. **Tailwind integration** — `tailwind.config.js` maps every CSS custom property to a Tailwind color utility (e.g. `bg-bg-primary`, `text-text-primary`, `bg-accent-orange`). Because the utilities resolve at runtime via `var()`, they automatically reflect the active theme without any build step or class changes.

4. **Database-backed registration** — Each theme has a row in the `Themes` table (`ThemeKey`, `DisplayName`, `Description`, `ColorDefinition`, `IsActive`). The `ThemeRepository` reads this table; the `ThemeService` uses it to validate theme keys, build the user preference hierarchy (user preference → admin default → system default), and expose themes to the profile and settings pages.

### Accent Color Mapping Convention

This is the most important concept to understand before designing a new palette. The UI has exactly two logical accent roles:

| Role | Tailwind class prefix | CSS variable root | Discord Dark color |
|------|-----------------------|-------------------|--------------------|
| Primary accent | `accent-orange` | `--color-accent-orange` | Burnt orange `#cb4e1b` |
| Secondary accent | `accent-blue` | `--color-accent-blue` | Bright blue `#098ecf` |

Component markup always refers to `accent-orange` for primary actions (primary buttons, active navigation, toggles) and `accent-blue` for secondary actions (secondary buttons, links, focus rings). **You must map your theme's primary color onto `--color-accent-orange` and your secondary color onto `--color-accent-blue`.** You do not rename the variables or add new Tailwind utilities.

Purple Dusk demonstrates this: its purple primary accent overrides `--color-accent-orange`, and its pink secondary accent overrides `--color-accent-blue`. No component file was changed.

---

## Prerequisites

- Familiarity with CSS custom properties
- The project running locally (`dotnet run --project src/DiscordBot.Bot`)
- EF Core CLI tools: `dotnet tool install --global dotnet-ef` (or already installed)
- A color contrast checker — see [Testing > WCAG Tools](#wcag-tools)

---

## Step-by-Step Walkthrough

### Step 1 — Choose a Theme Key

The theme key is the `data-theme` attribute value and the primary identifier throughout the system.

**Naming rules:**

- Lowercase kebab-case only: `my-theme`, `forest-green`, `high-contrast-dark`
- Maximum 50 characters (enforced by database column)
- Must be unique across all rows in the `Themes` table
- Avoid generic names like `light` or `dark` — use descriptive names that survive future additions

**Examples of good keys:** `slate-night`, `forest-dawn`, `high-contrast-light`

### Step 2 — Define the CSS Variable Block in `site.css`

Open `src/DiscordBot.Bot/wwwroot/css/site.css`.

Add a new `[data-theme]` block after the Purple Dusk block (around line 176). Follow the exact structure below. Every variable in this list **must** be present — the browser will silently inherit the `:root` value for any variable you omit, which will produce incorrect colors when mixing dark-theme defaults with light-theme overrides.

```css
/* ============================================
   YOUR THEME NAME
   ============================================

   Brief description of the palette aesthetic.

   Color Palette Strategy:
   - Backgrounds: ...
   - Text: ...
   - Primary Accent: ... (maps to accent-orange classes)
   - Secondary Accent: ... (maps to accent-blue classes)

   WCAG Compliance:
   - text-primary on bg-primary: X.X:1 (AA/AAA)
   - text-secondary on bg-primary: X.X:1 (AA)
   - accent primary on bg-primary: X.X:1 (AA)
   ============================================ */
[data-theme="your-theme-key"] {

  /* --- Backgrounds -------------------------------------------------- */
  /* Layer the backgrounds from lightest (primary) to darkest (hover)
     for a dark theme, or darkest (primary) to lightest (hover) for light. */
  --color-bg-primary: #...;    /* Main page background */
  --color-bg-secondary: #...;  /* Cards, panels, sidebars */
  --color-bg-tertiary: #...;   /* Modals, dropdowns, elevated elements */
  --color-bg-hover: #...;      /* Interactive surface hover state */

  /* --- Text --------------------------------------------------------- */
  /* Maintain a clear hierarchy: primary is the most readable,
     tertiary is only for placeholders and disabled states. */
  --color-text-primary: #...;      /* Body text, headings */
  --color-text-secondary: #...;    /* Labels, secondary information */
  --color-text-tertiary: #...;     /* Placeholders, disabled text */
  --color-text-placeholder: #...;  /* Input placeholder text (often ≈ tertiary) */

  /* --- Primary Accent (maps to accent-orange Tailwind classes) ------ */
  /* Used by: .btn-primary, active sidebar items, toggles, badges */
  --color-accent-orange: #...;
  --color-accent-orange-hover: #...;
  --color-accent-orange-active: #...;   /* Pressed/active state, slightly darker */
  --color-accent-orange-muted: rgba(r, g, b, 0.2);  /* Subtle highlight backgrounds */

  /* --- Secondary Accent (maps to accent-blue Tailwind classes) ------ */
  /* Used by: .btn-accent, links, focus rings, informational elements */
  --color-accent-blue: #...;
  --color-accent-blue-hover: #...;
  --color-accent-blue-active: #...;
  --color-accent-blue-muted: rgba(r, g, b, 0.2);

  /* --- Semantic Colors ----------------------------------------------- */
  /* Dark theme can use the standard Tailwind palette values.
     Light themes MUST use the -600 Tailwind equivalents or darker
     to pass AA contrast against a light background. */
  --color-success: #...;  /* Recommended for light themes: #059669 (Emerald 600) */
  --color-warning: #...;  /* Recommended for light themes: #D97706 (Amber 600)   */
  --color-error: #...;    /* Recommended for light themes: #DC2626 (Red 600)      */
  --color-info: #...;     /* Recommended for light themes: #0891B2 (Cyan 600)     */

  /* --- Borders ------------------------------------------------------- */
  --color-border-primary: #...;    /* Default component borders */
  --color-border-secondary: #...;  /* Subtle dividers (table rows, etc.) */
  --color-border-focus: #...;      /* Focus ring color (usually matches primary accent) */

  /* --- Discord Brand ------------------------------------------------- */
  /* These remain consistent across all themes. Copy these exactly. */
  --color-discord: #5865F2;
  --color-discord-hover: #4752C4;

  /* --- Text Inverse -------------------------------------------------- */
  /* Text color used on top of accent-colored backgrounds (e.g. button labels).
     White works for most colored backgrounds. Use a dark color only if your
     accent color is very light. */
  --color-text-inverse: #FFFFFF;

  /* --- Glass Effect -------------------------------------------------- */
  /* Used by glassmorphism nav/sidebar components. Derive from your
     secondary background with appropriate opacity. */
  --color-glass-bg: rgba(r, g, b, 0.6);
  --color-glass-bg-hover: rgba(r, g, b, 0.7);
  --color-glass-bg-active: rgba(r, g, b, 0.8);
  --color-glass-border: rgba(r, g, b, 0.8);
  --color-glass-border-hover: rgba(r, g, b, 0.5);  /* Accent color at 50% opacity */

  /* --- Status Indicators --------------------------------------------- */
  /* Used in guild status pills. Derive from semantic colors. */
  --color-status-active: #...;         /* Match or derive from --color-success */
  --color-status-active-bg: rgba(r, g, b, 0.15);
  --color-status-active-border: rgba(r, g, b, 0.35);
  --color-status-inactive: #6B7280;
  --color-status-inactive-bg: rgba(107, 114, 128, 0.15);
  --color-status-inactive-border: rgba(107, 114, 128, 0.35);
}
```

#### Component-Specific Overrides

Inspect the existing Purple Dusk block for examples of component-scoped overrides (lines 181–218 in `site.css`). These are needed when a component's Tailwind utility chain produces an incorrect result for the specific luminance of your new palette. Common candidates:

- `.settings-tab` and `.settings-tab.active` — inactive tab contrast on light backgrounds
- `.tab-panel-tab` — underline indicator color on the reusable tab panel

Add these beneath your main `[data-theme]` block:

```css
[data-theme="your-theme-key"] .settings-tab {
  color: var(--color-text-secondary);
}

[data-theme="your-theme-key"] .settings-tab:hover {
  background-color: var(--color-bg-hover);
  color: var(--color-text-primary);
}

[data-theme="your-theme-key"] .settings-tab.active {
  background-color: var(--color-accent-orange);
  color: var(--color-text-inverse);
}
```

Only add overrides that are actually needed. Run the testing checklist (Step 5) to identify them.

### Step 3 — Tailwind Configuration

No changes to `tailwind.config.js` are required. The configuration already maps every CSS custom property to a utility class via `var()`. Your new theme automatically inherits all of these mappings.

The only scenario requiring a `tailwind.config.js` change is if you introduce a completely new semantic color role that does not exist in the current palette (not covered by this guide).

### Step 4 — Create the Database Migration

Themes are registered in the `Themes` table. Add your theme via a new EF Core migration. Both migration sets (SQLite and PostgreSQL) must be updated if you support both providers.

#### Generate the Migration

```bash
# SQLite
dotnet ef migrations add AddYourThemeName \
  --project src/DiscordBot.Infrastructure \
  --startup-project src/DiscordBot.Bot \
  --context SqliteBotDbContext \
  -o Migrations/Sqlite

# PostgreSQL
dotnet ef migrations add AddYourThemeName \
  --project src/DiscordBot.Infrastructure \
  --startup-project src/DiscordBot.Bot \
  --context PostgresBotDbContext \
  -o Migrations/Postgresql
```

#### Add the Seed Data

Open the generated `Up` method and add an `InsertData` call:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.InsertData(
        table: "Themes",
        columns: new[] { "ThemeKey", "DisplayName", "Description", "ColorDefinition", "IsActive", "CreatedAt" },
        values: new object[]
        {
            "your-theme-key",
            "Your Theme Name",
            "One-sentence description of the theme's aesthetic.",
            """{"bgPrimary":"#...","bgSecondary":"#...","bgTertiary":"#...","bgHover":"#...","textPrimary":"#...","textSecondary":"#...","textTertiary":"#...","textInverse":"#...","accentOrange":"#...","accentOrangeHover":"#...","accentOrangeActive":"#...","accentOrangeMuted":"#...33","accentBlue":"#...","accentBlueHover":"#...","accentBlueActive":"#...","accentBlueMuted":"#...33","borderPrimary":"#...","borderSecondary":"#...","borderFocus":"#..."}""",
            true,
            DateTime.UtcNow
        });
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DeleteData(
        table: "Themes",
        keyColumn: "ThemeKey",
        keyValue: "your-theme-key");
}
```

The `ColorDefinition` JSON is a flat key-value map of camelCase color names to hex values. This is stored for reference and future use; the CSS variable block in `site.css` is the authoritative source of truth for rendering. Keep the two in sync.

#### Apply the Migration

```bash
# SQLite
dotnet ef database update \
  --project src/DiscordBot.Infrastructure \
  --startup-project src/DiscordBot.Bot \
  --context SqliteBotDbContext

# PostgreSQL
dotnet ef database update \
  --project src/DiscordBot.Infrastructure \
  --startup-project src/DiscordBot.Bot \
  --context PostgresBotDbContext
```

### Step 5 — Verify Theme Resolution

Start the application and navigate to `/Account/Profile`. Your new theme should appear in the theme selector dropdown. Select it, save, and confirm the page reloads with your theme applied (`data-theme="your-theme-key"` visible on `<html>` in DevTools).

If the theme does not appear, check that:

- `IsActive` is `true` in the inserted row
- The migration was applied (`dotnet ef database update` ran without errors)
- The `ThemeKey` in the database row exactly matches the CSS selector attribute value (case-sensitive)

---

## WCAG Contrast Requirements

All color combinations used together in the UI must meet **WCAG 2.1 AA** as a minimum. AAA is preferred for primary text.

### Minimum Ratios

| Text size | Minimum ratio (AA) | Target ratio (AAA) |
|-----------|--------------------|--------------------|
| Normal text (< 18pt / < 14pt bold) | 4.5:1 | 7.0:1 |
| Large text (≥ 18pt or ≥ 14pt bold) | 3.0:1 | 4.5:1 |
| UI components and focus indicators | 3.0:1 | — |

### Combinations to Check

Check every combination in this table before shipping:

| Foreground | Background | Minimum |
|------------|------------|---------|
| `--color-text-primary` | `--color-bg-primary` | AA (target AAA) |
| `--color-text-secondary` | `--color-bg-primary` | AA |
| `--color-text-tertiary` | `--color-bg-primary` | AA (large text only is acceptable) |
| `--color-text-inverse` | `--color-accent-orange` (primary button background) | AA |
| `--color-text-inverse` | `--color-accent-blue` (secondary button background) | AA |
| `--color-success` | `--color-bg-primary` | AA |
| `--color-warning` | `--color-bg-primary` | AA |
| `--color-error` | `--color-bg-primary` | AA |
| `--color-info` | `--color-bg-primary` | AA |
| `--color-accent-orange` | `--color-bg-primary` | AA (for text/icon use) |
| `--color-border-focus` | `--color-bg-primary` | 3.0:1 (UI component) |

### WCAG Tools

- [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/) — quick pairwise checks
- [Colour Contrast Analyser](https://www.tpgi.com/color-contrast-analyser/) — desktop app, eyedropper support
- [Polypane](https://polypane.app/) — browser with built-in contrast overlay (paid)
- Browser DevTools — Chrome and Firefox both show contrast ratios in the color picker

For light themes, the standard Tailwind 500-level semantic colors (`#10b981`, `#f59e0b`, `#ef4444`, `#06b6d4`) typically fail AA on light backgrounds. Use the 600-level equivalents as a starting point (`#059669`, `#D97706`, `#DC2626`, `#0891B2`) and verify each.

---

## Testing Checklist

Apply your theme and work through each item below. Use both mouse and keyboard for interactive elements.

### Layout and Navigation

- [ ] Page background renders at the expected hue — no bleed from `:root` defaults
- [ ] Sidebar background and text are readable
- [ ] Active sidebar item uses `accent-orange` primary and `text-inverse` text
- [ ] Inactive sidebar items use `text-secondary` and are clearly distinguishable from active
- [ ] Navbar background renders correctly
- [ ] Navbar text and icon colors are readable
- [ ] Mobile/collapsed sidebar variant renders correctly

### Cards and Panels

- [ ] Card background (`bg-secondary`) is visually distinct from page background (`bg-primary`)
- [ ] Card header border is visible
- [ ] Card footer renders with the `bg-primary` background as specified
- [ ] Elevated cards (`card-elevated`) are distinguishable from standard cards

### Buttons

- [ ] `.btn-primary` — correct background, `text-inverse` label, adequate contrast
- [ ] `.btn-primary:hover` — visible state change
- [ ] `.btn-primary:disabled` — clearly disabled, not just faded
- [ ] `.btn-accent` — correct `accent-blue` background, adequate contrast
- [ ] `.btn-secondary` — transparent background, border visible, text readable
- [ ] `.btn-danger` — error red, readable label

### Forms

- [ ] Input borders are visible against `bg-secondary` card backgrounds
- [ ] Input placeholder text is readable (not invisible)
- [ ] Input focus state shows `border-focus` ring at 3.0:1+ against background
- [ ] Select dropdowns render background and text correctly
- [ ] Checkboxes and radio buttons are distinguishable
- [ ] Validation error state (error color) is readable

### Tables

- [ ] Table header background is distinct from body rows
- [ ] Row separators (`border-secondary`) are visible but subtle
- [ ] Row hover state (`bg-hover`) is visible
- [ ] Text in all columns is readable

### Badges and Status Pills

- [ ] Semantic color badges (success, warning, error, info) are readable
- [ ] Status active/inactive pills render with correct background and border

### Settings and Tab Panels

- [ ] Inactive tabs: text readable against tab bar background
- [ ] Active tab: clear visual distinction (background + text color change)
- [ ] Tab focus ring visible on keyboard navigation
- [ ] Disabled tabs clearly distinguished from inactive tabs

### Focus States

- [ ] Tab through all interactive elements — focus ring must be visible at every stop
- [ ] Focus ring color matches `--color-border-focus`
- [ ] No elements lose their focus indicator

### Modals and Overlays

- [ ] Modal background (`bg-tertiary`) is distinct from the page behind it
- [ ] Backdrop/overlay is visible without obscuring modal content

### Typography

- [ ] `text-primary` is readable and the darkest/lightest text color
- [ ] `text-secondary` provides clear hierarchy below primary
- [ ] `text-tertiary` is clearly subordinate but not invisible
- [ ] No text is lost against any background it appears on

---

## Complete CSS Variable Reference

The table below documents every variable required in a theme block, with values from both existing themes for comparison.

### Background Colors

| Variable | Purpose | Discord Dark | Purple Dusk |
|----------|---------|-------------|-------------|
| `--color-bg-primary` | Main page background | `#1d2022` | `#E8E3DF` |
| `--color-bg-secondary` | Cards, panels, sidebars | `#262a2d` | `#DAD4D0` |
| `--color-bg-tertiary` | Modals, dropdowns, elevated elements | `#2f3336` | `#CCC5C0` |
| `--color-bg-hover` | Interactive surface hover state | `#363a3e` | `#C0B8B2` |

### Text Colors

| Variable | Purpose | Discord Dark | Purple Dusk |
|----------|---------|-------------|-------------|
| `--color-text-primary` | Primary body and heading text | `#d7d3d0` | `#4F214A` |
| `--color-text-secondary` | Labels, secondary information | `#a8a5a3` | `#614978` |
| `--color-text-tertiary` | Placeholders, disabled text | `#7a7876` | `#887A99` |
| `--color-text-placeholder` | Input placeholder text | `#8a8886` | `#9A8DA8` |
| `--color-text-inverse` | Text on accent-colored backgrounds | `#FFFFFF` | `#FFFFFF` |

### Primary Accent (`accent-orange` classes)

| Variable | Purpose | Discord Dark | Purple Dusk |
|----------|---------|-------------|-------------|
| `--color-accent-orange` | Primary CTA, active nav, toggles | `#cb4e1b` | `#614978` |
| `--color-accent-orange-hover` | Hover state for primary accent | `#e5591f` | `#7A5C8F` |
| `--color-accent-orange-active` | Pressed/active state | `#b3440f` | `#4F214A` |
| `--color-accent-orange-muted` | Subtle highlight background | `rgba(203,78,27,0.2)` | `rgba(97,73,120,0.2)` |

### Secondary Accent (`accent-blue` classes)

| Variable | Purpose | Discord Dark | Purple Dusk |
|----------|---------|-------------|-------------|
| `--color-accent-blue` | Secondary actions, links, focus rings | `#098ecf` | `#D5345B` |
| `--color-accent-blue-hover` | Hover state for secondary accent | `#0ba3ea` | `#E5476D` |
| `--color-accent-blue-active` | Pressed/active state | `#0778ab` | `#B82A4D` |
| `--color-accent-blue-muted` | Subtle highlight background | `rgba(9,142,207,0.2)` | `rgba(213,52,91,0.2)` |

### Semantic Colors

| Variable | Purpose | Discord Dark | Purple Dusk |
|----------|---------|-------------|-------------|
| `--color-success` | Success states, confirmation | `#10b981` | `#059669` |
| `--color-warning` | Warning states, caution | `#f59e0b` | `#D97706` |
| `--color-error` | Error states, destructive actions | `#ef4444` | `#DC2626` |
| `--color-info` | Informational states | `#06b6d4` | `#0891B2` |

Note: `success-bg`, `success-border`, and equivalent `warning`, `error`, `info` variants are hardcoded in `tailwind.config.js` using hex alpha values and are not overridden per theme. They are sufficient for dark themes but may require component-level adjustments on very light backgrounds.

### Border Colors

| Variable | Purpose | Discord Dark | Purple Dusk |
|----------|---------|-------------|-------------|
| `--color-border-primary` | Default component borders | `#3f4447` | `#C0B8B2` |
| `--color-border-secondary` | Subtle dividers (table rows) | `#2f3336` | `#DAD4D0` |
| `--color-border-focus` | Focus ring color | `#098ecf` | `#614978` |

### Discord Brand Colors

| Variable | Purpose | Value (all themes) |
|----------|---------|-------------------|
| `--color-discord` | Discord OAuth button | `#5865F2` |
| `--color-discord-hover` | Discord OAuth button hover | `#4752C4` |

### Glass Effect Colors

| Variable | Purpose | Discord Dark | Purple Dusk |
|----------|---------|-------------|-------------|
| `--color-glass-bg` | Glassmorphism background | _(not overridden; uses `:root` default)_ | `rgba(218,212,208,0.6)` |
| `--color-glass-bg-hover` | Glass background on hover | — | `rgba(204,197,192,0.7)` |
| `--color-glass-bg-active` | Glass background active | — | `rgba(192,184,178,0.8)` |
| `--color-glass-border` | Glass border | — | `rgba(192,184,178,0.8)` |
| `--color-glass-border-hover` | Glass border on hover | — | `rgba(97,73,120,0.5)` |

Glass effect variables only need overriding if the default dark values look wrong on your theme's backgrounds.

### Status Indicator Colors

| Variable | Purpose | Discord Dark | Purple Dusk |
|----------|---------|-------------|-------------|
| `--color-status-active` | Active guild indicator | _(not overridden)_ | `#059669` |
| `--color-status-active-bg` | Active indicator background | — | `rgba(5,150,105,0.15)` |
| `--color-status-active-border` | Active indicator border | — | `rgba(5,150,105,0.35)` |
| `--color-status-inactive` | Inactive guild indicator | _(not overridden)_ | `#6B7280` |
| `--color-status-inactive-bg` | Inactive indicator background | — | `rgba(107,114,128,0.15)` |
| `--color-status-inactive-border` | Inactive indicator border | — | `rgba(107,114,128,0.35)` |

Status variables only need overriding when the default dark-theme values produce insufficient contrast or incorrect visual weight on your backgrounds.

---

## Common Pitfalls

**Partial variable definitions.** Omitting any variable causes the browser to fall back to the `:root` Discord Dark value for that property. On a light theme, a single omitted background variable can produce black text on a near-black background. Always define the full set.

**Forgetting muted variants.** The `*-muted` variables are rgba values used for subtle highlight backgrounds on cards, badges, and hover states. If you only define the solid color and forget the muted variant, those backgrounds will be fully opaque and visually jarring.

**Not adjusting semantic colors for light themes.** The standard semantic colors (`#10b981`, `#ef4444`, etc.) are chosen for dark backgrounds. On `#E8E3DF`-level backgrounds they drop well below 4.5:1. Always recalculate semantic contrast ratios against your actual `bg-primary` value.

**Using `text-inverse` as a dark color on light buttons.** The `text-inverse` variable is the label color for buttons with a colored background. If your accent colors are light (e.g. a pastel palette), white text on a pastel button will fail AA. In that case set `--color-text-inverse` to a dark value for your theme.

**ThemeKey mismatch between CSS and database.** The `[data-theme="your-key"]` CSS selector and the `ThemeKey` column value must be identical, including case. The `ThemeService` validates the key read from the user's cookie against the database, so an inconsistency means the theme silently falls back to the system default.

**Not testing the inactive state of UI tabs.** Settings and tab-panel tabs render with `text-tertiary` by default, which is tuned for dark backgrounds. On light themes the contrast against `bg-primary` often fails AA. Add a component override as shown in Step 2.

**Assuming glass effect variables are optional.** If your theme uses a light or mid-tone background, the default dark-theme glass variables will render as nearly opaque dark blocks across your navigation and sidebar. Override all five `--color-glass-*` variables.

**Not testing both keyboard and mouse interactions.** Focus rings are sized and colored by `--color-border-focus` and the global `*:focus-visible` rule. Verify focus rings are visible on every interactive element type.

---

## Related Documentation

- [Design System](design-system.md) — Color tokens, contrast tables, and component specifications
- [Component API](component-api.md) — Available UI components and their Tailwind class usage
- [Form Implementation Standards](form-implementation-standards.md) — Form and input patterns
- [Authorization Policies](authorization-policies.md) — Required role to set the system default theme (SuperAdmin)
