# Tailwind CSS Integration

This document describes the Tailwind CSS setup for the Discord Bot Admin UI.

## Overview

Tailwind CSS is configured to build automatically on `dotnet build` via MSBuild targets. The design system tokens from `docs/design-system.md` are implemented as custom theme extensions.

## Configuration Files

- **`package.json`** - npm dependencies (tailwindcss, postcss, autoprefixer)
- **`tailwind.config.js`** - Tailwind configuration with custom design tokens
- **`postcss.config.js`** - PostCSS configuration for Tailwind processing
- **`wwwroot/css/site.css`** - Source CSS file with Tailwind directives and custom components
- **`wwwroot/css/app.css`** - Generated output file (minified for production)

## Build Integration

### Automatic Build (Production)

Tailwind CSS builds automatically when you run:

```bash
dotnet build
```

The MSBuild targets in `DiscordBot.Bot.csproj` will:
1. Run `npm install` if `node_modules` doesn't exist
2. Run `npm run build:css` to generate minified CSS
3. Output to `wwwroot/css/app.css`

### Development with Hot Reload

For active development with file watching, run this command in a separate terminal:

```bash
cd src/DiscordBot.Bot
npm run watch:css
```

This will watch for changes in:
- `wwwroot/css/site.css`
- `Pages/**/*.razor`
- `Components/**/*.razor`
- `wwwroot/**/*.html`

And automatically rebuild `wwwroot/css/app.css` on changes.

## NPM Scripts

- **`npm run build:css`** - Build and minify CSS for production
- **`npm run watch:css`** - Watch for changes and rebuild automatically

## Design System

`tailwind.config.js` is deliberately thin: every value is a CSS custom property declared in `wwwroot/css/site.css` (see `docs/articles/design-system.md`, v2.0 "Graphite").

### Colors

Colours are published as RGB triplets (`--color-success-rgb`) and wired as `rgb(var(--…-rgb) / <alpha-value>)`, so opacity modifiers such as `bg-success/20` or `text-accent-orange/60` follow the active theme.

- **Surfaces**: `bg-bg-primary` (canvas), `bg-bg-secondary` (panels), `bg-bg-tertiary` (elevated), `bg-bg-hover`, `bg-bg-inset` (inputs)
- **Text**: `text-text-primary`, `text-text-secondary`, `text-text-tertiary`, `text-text-placeholder`, `text-text-inverse`
- **Accents**: `accent-orange` (ember — selected / primary) and `accent-blue` (signal blue — links / info), each with `-hover`, `-active`, `-muted`; `accent-purple`
- **Semantic**: `success`, `warning`, `error`, `info`, each with `-hover`, `-active`, `-bg`, `-border`
- **Rules**: `border-border-primary`, `border-border-secondary`, `border-border-strong`, `border-border-hover`, `border-border-focus` (alpha hairlines — no opacity modifier)

### Typography

- `font-display` / `font-heading` → Bricolage Grotesque, `font-sans` → DM Sans, `font-mono` → JetBrains Mono (all loaded in the layouts from Google Fonts)
- Headings: `text-display`, `text-h1` … `text-h6` (fluid `clamp()` sizes on the two largest)

### Radius & depth

- `rounded-sm` 4px · `rounded-md` 6px (controls) · `rounded-lg` 10px (panels) · `rounded-xl` 14px
- `shadow-sm|md|lg|xl` are layered token shadows; `shadow-highlight` is the 1px inner top light every panel carries

### Component Classes

Defined in `site.css` (`@layer components`):

- **Buttons**: `.btn`, `.btn-primary`, `.btn-secondary`, `.btn-accent`, `.btn-danger`, `.btn-warning`, `.btn-ghost`, `.btn-sm`, `.btn-lg`, `.btn-icon`, `.btn-block`
- **Surfaces**: `.surface`, `.card`, `.card-header`, `.card-title`, `.card-body`, `.card-footer`, `.card-elevated`, `.card-interactive`, `.card-enhanced` (+ `.accent-*`), `.hero-metric-card` (+ `.hero-metric-label|value|icon`)
- **Page header**: `.page-header`, `.page-header-text`, `.page-eyebrow`, `.page-title`, `.page-subtitle`, `.page-actions`
- **Forms**: `.form-group`, `.form-label`, `.form-input`, `.form-select`, `.form-textarea`, `.form-help`, `.form-error`, `.form-success`, `.toggle`, `.toggle-slider`, `.toggle-label`
- **Shell**: `.sidebar-redesign`, `.sidebar-brand`, `.sidebar-group`, `.sidebar-section-header`, `.sidebar-link-redesign`, `.sidebar-footer`, `.bot-status-led`, `.topbar`, `.topbar-icon-btn`, `.topbar-search-input`, `.topbar-user`, `.user-menu-*`, `.main-content-redesign`, `.page-container`
- **Tables**: `.table-container`, `.table`, `.table-header`, `.table-cell-header`, `.table-body`, `.table-row`, `.table-cell`
- **Status**: `.badge` (+ `.badge-orange|blue|purple|success|warning|error|info|gray`, `.badge-solid`, `.badge-outline`, `.badge-subtle`, `.badge-sm|lg`, `.badge-case`), `.status-indicator`, `.status-glass`
- **Alerts**: `.alert`, `.alert-info|success|warning|error`, `.alert-icon`, `.alert-title`, `.alert-message`
- **Type helpers**: `.section-label`, `.kbd`, `.num`

Classes rendered by JavaScript (notifications, toasts, search, popups) live outside `@layer` so they are never purged.

## Usage in Razor Pages

Reference the compiled CSS in your layout:

```html
<link rel="stylesheet" href="~/css/app.css" />
```

Example usage with custom component classes:

```html
<button class="btn btn-primary">Save Changes</button>

<div class="card">
  <div class="card-header">
    <h3 class="card-title">Server Statistics</h3>
  </div>
  <div class="card-body">
    <p class="text-base text-secondary">Content here...</p>
  </div>
</div>
```

Example usage with Tailwind utility classes:

```html
<div class="flex items-center gap-4 p-6 bg-bg-secondary rounded-lg">
  <span class="text-h4 text-text-primary">Dashboard</span>
  <span class="badge badge-success">Active</span>
</div>
```

## Troubleshooting

### CSS not updating

1. Delete `wwwroot/css/app.css`
2. Run `npm run build:css` manually
3. Run `dotnet build`

### npm install fails

Ensure Node.js and npm are installed:

```bash
node --version  # Should be v16 or higher
npm --version   # Should be v7 or higher
```

### Build warnings about unused CSS

This is expected if you haven't added Razor pages with Tailwind classes yet. The warning will disappear once you start using Tailwind utilities in your components.

## References

- [Tailwind CSS Documentation](https://tailwindcss.com/docs)
- [Design System Documentation](../../docs/articles/design-system.md)
- [Component Examples](../../docs/articles/design-system.md#4-component-guidelines)
