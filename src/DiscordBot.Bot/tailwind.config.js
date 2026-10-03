/** @type {import('tailwindcss').Config} */

// Every colour is an RGB triplet on :root (see wwwroot/css/site.css) so that
// Tailwind's opacity modifiers keep working across themes: `bg-success/20`
// becomes `rgba(var(--color-success-rgb), 0.2)` and follows the active theme.
const rgb = (name) => `rgba(var(--color-${name}-rgb), <alpha-value>)`;

// Fills sit behind white text (warning: behind --color-on-warning), so in the
// dark theme they are darker than the matching text colour. Background
// utilities use the fill: `bg-success` is the fill, `text-success` the ink,
// and `bg-success/10` is a tint of the fill. See docs/articles/design-system.md.
const fill = (name) => ({
  DEFAULT: rgb(`${name}-fill`),
  hover: rgb(`${name}-fill-hover`),
  active: rgb(`${name}-fill-active`),
});

module.exports = {
  content: [
    "./Pages/**/*.{razor,cshtml}",
    "./Components/**/*.{razor,cshtml}",
    "./wwwroot/**/*.html",
    "./wwwroot/**/*.js",
    // C# builds class strings too (badge and status switch expressions in view
    // models, helpers and page models), so scan it like markup.
    "./**/*.cs",
  ],
  // Component classes that are composed at runtime (C# switch expressions,
  // JS templates) must survive purging.
  safelist: [
    { pattern: /^(badge|btn|alert|status|severity|card|page|form|toggle|topbar|sidebar|bot-status|hero-metric|table|kbd|section)-/ },
    // Added at run time by ASP.NET tag helpers and jQuery validation, or composed by the
    // Phase 3 partials and scripts (modal motion, radio cards, pagination, skeletons)
    { pattern: /^(input-validation|field-validation|validation-summary|radio-card|pagination|qa|skeleton|empty-state)-/ },
    { pattern: /^(pagination|skeleton|empty-state|radio-card)$/ },
    { pattern: /^(badge|btn|alert|card|status-indicator|status-glass|kbd|surface|section-label|page-eyebrow)$/ },
    // Colour pairs composed from a variant name (`bg-@color/10 text-@color` in
    // _QuickActionsCard and the confirmation modals, `bg-${color}/20` in quick-actions.js)
    { pattern: /^(bg|text)-(accent-orange|accent-blue|accent-purple|success|warning|error|info)(\/(10|20))?$/ },
  ],
  theme: {
    extend: {
      colors: {
        // Surfaces
        bg: {
          primary: rgb('bg-primary'),
          secondary: rgb('bg-secondary'),
          tertiary: rgb('bg-tertiary'),
          hover: rgb('bg-hover'),
          inset: rgb('bg-inset'),
        },
        // Text
        text: {
          primary: rgb('text-primary'),
          secondary: rgb('text-secondary'),
          tertiary: rgb('text-tertiary'),
          placeholder: rgb('text-placeholder'),
          inverse: 'var(--color-text-inverse, #FFFFFF)',
        },
        // Text on a warning fill: `bg-warning text-on-warning`
        'on-warning': 'var(--color-on-warning)',
        // Accents — ember (primary/selected) and signal blue (links/info)
        accent: {
          orange: {
            DEFAULT: rgb('accent-orange'),
            hover: rgb('accent-orange-hover'),
            active: rgb('accent-orange-active'),
            muted: 'var(--color-accent-orange-muted)',
          },
          blue: {
            DEFAULT: rgb('accent-blue'),
            hover: rgb('accent-blue-hover'),
            active: rgb('accent-blue-active'),
            muted: 'var(--color-accent-blue-muted)',
          },
          purple: {
            DEFAULT: rgb('accent-purple'),
          },
        },
        // Semantic
        success: {
          DEFAULT: rgb('success'),
          hover: rgb('success-hover'),
          active: rgb('success-active'),
          bg: 'var(--color-success-bg)',
          border: 'var(--color-success-border)',
        },
        warning: {
          DEFAULT: rgb('warning'),
          hover: rgb('warning-hover'),
          active: rgb('warning-active'),
          bg: 'var(--color-warning-bg)',
          border: 'var(--color-warning-border)',
        },
        error: {
          DEFAULT: rgb('error'),
          hover: rgb('error-hover'),
          active: rgb('error-active'),
          bg: 'var(--color-error-bg)',
          border: 'var(--color-error-border)',
        },
        info: {
          DEFAULT: rgb('info'),
          hover: rgb('info-hover'),
          active: rgb('info-active'),
          bg: 'var(--color-info-bg)',
          border: 'var(--color-info-border)',
        },
        // Rules — hairlines carry their own alpha, so no opacity modifier here.
        // Focus is solid, so it takes one (`ring-border-focus/50`).
        border: {
          primary: 'var(--color-border-primary)',
          secondary: 'var(--color-border-secondary)',
          strong: 'var(--color-border-strong)',
          hover: 'var(--color-border-hover)',
          focus: rgb('border-focus'),
        },
        // Discord brand colour
        discord: {
          DEFAULT: 'var(--color-discord)',
          hover: 'var(--color-discord-hover)',
        },
      },
      // Background utilities resolve to the fills; deep-merged over `colors`,
      // so bg-success-bg, bg-accent-orange-muted and the rest are unchanged.
      backgroundColor: {
        accent: {
          orange: fill('accent-orange'),
          blue: fill('accent-blue'),
          purple: fill('accent-purple'),
        },
        success: fill('success'),
        warning: fill('warning'),
        error: fill('error'),
        info: fill('info'),
      },
      fontFamily: {
        display: ['var(--font-display)'],
        heading: ['var(--font-display)'],
        sans: ['var(--font-body)'],
        mono: ['var(--font-mono)'],
      },
      fontSize: {
        'display': ['clamp(2.25rem, 1.6rem + 2.2vw, 3.25rem)', { lineHeight: '1.02', letterSpacing: '-0.03em', fontWeight: '700' }],
        'h1': ['clamp(1.625rem, 1.3rem + 1.1vw, 2.125rem)', { lineHeight: '1.1', letterSpacing: '-0.02em', fontWeight: '700' }],
        'h2': ['clamp(1.375rem, 1.15rem + 0.8vw, 1.75rem)', { lineHeight: '1.15', letterSpacing: '-0.02em', fontWeight: '700' }],
        'h3': ['1.375rem', { lineHeight: '1.25', letterSpacing: '-0.015em', fontWeight: '600' }],
        'h4': ['1.125rem', { lineHeight: '1.3', letterSpacing: '-0.01em', fontWeight: '600' }],
        'h5': ['1rem', { lineHeight: '1.35', letterSpacing: '-0.005em', fontWeight: '600' }],
        'h6': ['0.875rem', { lineHeight: '1.4', fontWeight: '600' }],
      },
      spacing: {
        '128': '32rem',
        '144': '36rem',
      },
      boxShadow: {
        'sm': 'var(--shadow-sm)',
        'DEFAULT': 'var(--shadow-md)',
        'md': 'var(--shadow-md)',
        'lg': 'var(--shadow-lg)',
        'xl': 'var(--shadow-xl)',
        'highlight': 'var(--surface-highlight)',
        'glow-orange': '0 0 20px var(--glow-primary)',
        'glow-blue': '0 0 20px var(--glow-secondary)',
      },
      borderRadius: {
        'xs': 'var(--radius-xs)',
        'sm': 'var(--radius-sm)',
        'DEFAULT': 'var(--radius-md)',
        'md': 'var(--radius-md)',
        'lg': 'var(--radius-lg)',
        'xl': 'var(--radius-xl)',
        '2xl': '20px',
        '3xl': '28px',
      },
      transitionTimingFunction: {
        'out-quart': 'var(--ease-out-quart)',
        'out-expo': 'var(--ease-out-expo)',
      },
    },
  },
  plugins: [],
}
