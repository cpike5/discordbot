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

The Tailwind configuration includes custom theme extensions from the design system:

### Colors

- **Background layers**: `bg-primary`, `bg-secondary`, `bg-tertiary`, `bg-hover`
- **Text colors**: `text-primary`, `text-secondary`, `text-tertiary`, `text-inverse`
- **Accent colors**: `accent-orange`, `accent-blue` (with hover/active/muted variants)
- **Semantic colors**: `success`, `warning`, `error`, `info` (with bg/border variants)
- **Border colors**: `border-primary`, `border-secondary`, `border-focus`

### Typography

- **Headings**: `text-display`, `text-h1`, `text-h2`, `text-h3`, `text-h4`, `text-h5`, `text-h6`
- **Font families**: `font-sans` (system UI stack), `font-mono`

### Custom Component Classes

The following component classes are available in `site.css`:

- **Buttons**: `.btn`, `.btn-primary`, `.btn-secondary`, `.btn-accent`, `.btn-danger`, `.btn-sm`, `.btn-lg`, `.btn-icon`
- **Cards**: `.card`, `.card-header`, `.card-title`, `.card-body`, `.card-footer`, `.card-elevated`, `.card-interactive`
- **Forms**: `.form-group`, `.form-label`, `.form-input`, `.form-select`, `.form-help`, `.form-error`, `.form-success`, `.toggle`, `.toggle-slider`, `.toggle-label`
- **Navigation**: `.navbar`, `.navbar-brand`, `.navbar-link`, `.sidebar`, `.sidebar-link`, `.breadcrumb`
- **Tables**: `.table-container`, `.table`, `.table-header`, `.table-cell-header`, `.table-body`, `.table-row`, `.table-cell`
- **Status**: `.badge`, `.status-indicator`
- **Alerts**: `.alert`, `.alert-info`, `.alert-success`, `.alert-warning`, `.alert-error`

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
- [Design System Documentation](../../docs/design-system.md)
- [Component Examples](../../docs/design-system.md#4-component-guidelines)
