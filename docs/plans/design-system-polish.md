# Design System Polish - Implementation Plan

## Context

A design system review identified 5 high-priority polish items that improve theming correctness, interaction feel, and visual distinctiveness. These are CSS/config-only changes with zero impact on C# code or component logic.

**Problem:** The design system has strong foundations but several gaps: semantic color backgrounds break on the light theme, transitions feel monotone, defined glow effects go unused, shadows are too heavy on light themes, and typography uses a generic system font stack.

---

## Phase 1: Fix Semantic Color Token Theming (Bug Fix)

**Files:**
- `src/DiscordBot.Bot/wwwroot/css/site.css` (lines 28-79, `:root` block)
- `src/DiscordBot.Bot/wwwroot/css/site.css` (lines 108-176, `[data-theme="purple-dusk"]` block)
- `src/DiscordBot.Bot/tailwind.config.js` (lines 42-63)

**What:** Semantic color `bg` and `border` variants (`success-bg`, `error-border`, etc.) are hardcoded hex in `tailwind.config.js`. They need to be CSS variables so the purple-dusk theme can override them.

**Steps:**
1. Add 8 new CSS variables to `:root` in site.css:
   ```css
   --color-success-bg: rgba(16, 185, 129, 0.15);
   --color-success-border: rgba(16, 185, 129, 0.3);
   --color-warning-bg: rgba(245, 158, 11, 0.15);
   --color-warning-border: rgba(245, 158, 11, 0.3);
   --color-error-bg: rgba(239, 68, 68, 0.15);
   --color-error-border: rgba(239, 68, 68, 0.3);
   --color-info-bg: rgba(6, 182, 212, 0.15);
   --color-info-border: rgba(6, 182, 212, 0.3);
   ```
2. Add purple-dusk overrides with theme-appropriate values (darker alpha on light backgrounds for visibility):
   ```css
   --color-success-bg: rgba(5, 150, 105, 0.12);
   --color-success-border: rgba(5, 150, 105, 0.3);
   --color-warning-bg: rgba(217, 119, 6, 0.12);
   --color-warning-border: rgba(217, 119, 6, 0.3);
   --color-error-bg: rgba(220, 38, 38, 0.12);
   --color-error-border: rgba(220, 38, 38, 0.3);
   --color-info-bg: rgba(8, 145, 178, 0.12);
   --color-info-border: rgba(8, 145, 178, 0.3);
   ```
3. Update `tailwind.config.js` to reference variables instead of hardcoded hex:
   ```js
   success: { DEFAULT: 'var(--color-success)', bg: 'var(--color-success-bg)', border: 'var(--color-success-border)' }
   ```
4. Note: `moderation.css` already uses `var(--color-success-bg, fallback)` pattern -- defining the variables means those fallbacks become unnecessary (but harmless to leave).

---

## Phase 2: Add Transition Timing Tokens

**Files:**
- `src/DiscordBot.Bot/wwwroot/css/site.css` (`:root` block + component classes)

**What:** Currently every interaction uses `150ms ease-in-out`. Add named transition tokens for different interaction types, then migrate existing component transitions.

**Steps:**
1. Add transition CSS variables to `:root`:
   ```css
   --transition-fast: 120ms ease-out;        /* hover, focus rings */
   --transition-normal: 180ms ease-in-out;    /* toggles, state changes */
   --transition-smooth: 250ms cubic-bezier(0.4, 0, 0.2, 1);  /* layout, accordion */
   --transition-enter: 300ms cubic-bezier(0, 0, 0.2, 1);     /* modals, toasts */
   ```
2. Update component transition classes in site.css to use the tokens:
   - `.btn` (line 225): `transition: all var(--transition-fast)` (buttons should feel snappy)
   - `.btn-secondary` (line 310): same
   - `.form-control-static` (line 472): `transition: color var(--transition-normal)`
   - Links (line 578): `transition: color var(--transition-fast)`
   - Card hover effects: `transition: all var(--transition-smooth)`
   - Floating labels (line 321): keep existing cubic-bezier (already appropriate)
3. Do NOT rewrite every instance -- migrate the `.btn`, `.form-*`, and link base classes. Individual Tailwind `duration-*` usage in templates can be addressed incrementally.

---

## Phase 3: Utilize Glow Shadows

**Files:**
- `src/DiscordBot.Bot/wwwroot/css/site.css` (component classes)

**What:** `glow-orange` and `glow-blue` are defined in tailwind.config.js but used nowhere. Add subtle glow effects to high-impact interactive elements.

**Steps:**
1. Add focused/active glow to `.btn-primary`:
   ```css
   .btn-primary:focus-visible {
     box-shadow: 0 0 0 2px var(--color-bg-primary), 0 0 12px rgba(203, 78, 27, 0.3);
   }
   ```
2. Add focused glow to `.btn-accent` (blue variant):
   ```css
   .btn-accent:focus-visible {
     box-shadow: 0 0 0 2px var(--color-bg-primary), 0 0 12px rgba(9, 142, 207, 0.3);
   }
   ```
3. Add theme-aware glow variables so purple-dusk maps to its accent colors:
   ```css
   :root {
     --glow-primary: rgba(203, 78, 27, 0.3);
     --glow-secondary: rgba(9, 142, 207, 0.3);
   }
   [data-theme="purple-dusk"] {
     --glow-primary: rgba(97, 73, 120, 0.3);
     --glow-secondary: rgba(213, 52, 91, 0.3);
   }
   ```
4. Use `var(--glow-primary)` and `var(--glow-secondary)` in the glow box-shadows above and update the Tailwind glow definitions to use variables too.

---

## Phase 4: Theme-Aware Shadows

**Files:**
- `src/DiscordBot.Bot/wwwroot/css/site.css` (`:root` and purple-dusk blocks)
- `src/DiscordBot.Bot/tailwind.config.js` (boxShadow)

**What:** Shadows use `rgba(0,0,0,0.3)` which is too heavy on the light theme. Make shadow intensity theme-aware.

**Steps:**
1. Add shadow variables to `:root`:
   ```css
   --shadow-sm: 0 1px 2px 0 rgba(0, 0, 0, 0.3);
   --shadow-md: 0 4px 6px -1px rgba(0, 0, 0, 0.3), 0 2px 4px -1px rgba(0, 0, 0, 0.2);
   --shadow-lg: 0 10px 15px -3px rgba(0, 0, 0, 0.3), 0 4px 6px -2px rgba(0, 0, 0, 0.2);
   --shadow-xl: 0 20px 25px -5px rgba(0, 0, 0, 0.3), 0 10px 10px -5px rgba(0, 0, 0, 0.2);
   ```
2. Add lighter shadows for purple-dusk:
   ```css
   [data-theme="purple-dusk"] {
     --shadow-sm: 0 1px 2px 0 rgba(80, 50, 40, 0.1);
     --shadow-md: 0 4px 6px -1px rgba(80, 50, 40, 0.1), 0 2px 4px -1px rgba(80, 50, 40, 0.06);
     --shadow-lg: 0 10px 15px -3px rgba(80, 50, 40, 0.1), 0 4px 6px -2px rgba(80, 50, 40, 0.06);
     --shadow-xl: 0 20px 25px -5px rgba(80, 50, 40, 0.1), 0 10px 10px -5px rgba(80, 50, 40, 0.06);
   }
   ```
3. Update `tailwind.config.js` boxShadow to reference variables:
   ```js
   boxShadow: {
     'sm': 'var(--shadow-sm)',
     'DEFAULT': 'var(--shadow-md)',
     'md': 'var(--shadow-md)',
     'lg': 'var(--shadow-lg)',
     'xl': 'var(--shadow-xl)',
     'glow-orange': '0 0 20px var(--glow-primary)',
     'glow-blue': '0 0 20px var(--glow-secondary)',
   }
   ```

---

## Phase 5: Heading Typeface

**Files:**
- `src/DiscordBot.Bot/Pages/Shared/_Layout.cshtml` (add font link)
- `src/DiscordBot.Bot/tailwind.config.js` (add `fontFamily.heading`)
- `src/DiscordBot.Bot/wwwroot/css/site.css` (heading base styles)

**What:** Add a distinctive heading font to differentiate from the system font stack. Body text stays as system fonts for performance and readability.

**Steps:**
1. Add Google Fonts `<link>` to `_Layout.cshtml` `<head>` for **Plus Jakarta Sans** (weight 600-700 only, ~15KB):
   ```html
   <link rel="preconnect" href="https://fonts.googleapis.com">
   <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
   <link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@600;700&display=swap" rel="stylesheet">
   ```
2. Add `fontFamily.heading` to tailwind.config.js:
   ```js
   heading: ['"Plus Jakarta Sans"', ...sans-fallback-stack]
   ```
3. Add base heading styles in site.css `@layer base`:
   ```css
   h1, h2, h3, .text-display, .text-h1, .text-h2, .text-h3 {
     font-family: 'Plus Jakarta Sans', var(--font-sans);
   }
   ```
4. This gives headings personality while body text (paragraphs, table cells, form labels) stays on the performant system stack.

---

## Verification

1. **Build CSS**: Run `npx tailwindcss -i ./wwwroot/css/site.css -o ./wwwroot/css/app.css` (or the project's existing build command) and verify no Tailwind errors
2. **Dark theme**: Load the portal in Discord Dark theme -- verify buttons, cards, alerts, badges render identically to before
3. **Light theme**: Switch to Purple Dusk -- verify:
   - Alert/badge backgrounds use lighter, theme-appropriate tints (not dark-theme-tuned opacity)
   - Shadows are softer and don't look heavy
   - Button glow effects use purple/pink instead of orange/blue
4. **Transitions**: Click buttons, hover cards, open modals -- buttons should feel snappier (120ms), cards smoother (250ms)
5. **Typography**: Headings (h1-h3) should render in Plus Jakarta Sans; body text unchanged
6. **Reduced motion**: Enable `prefers-reduced-motion` in dev tools -- verify all animations still disabled
7. **Build & test**: `dotnet build` and `dotnet test` to ensure no regressions

## File Summary

| File | Changes |
|------|---------|
| `src/DiscordBot.Bot/wwwroot/css/site.css` | Add semantic bg/border vars, transition tokens, glow vars, shadow vars, heading font rule |
| `src/DiscordBot.Bot/tailwind.config.js` | Point semantic colors/shadows/glows at CSS vars, add heading font family |
| `src/DiscordBot.Bot/Pages/Shared/_Layout.cshtml` | Add Google Fonts link for Plus Jakarta Sans |

3 files modified. Zero C# changes. Zero component template changes.
