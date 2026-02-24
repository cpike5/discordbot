/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./Pages/**/*.{razor,cshtml}",
    "./Components/**/*.{razor,cshtml}",
    "./wwwroot/**/*.html",
    "./wwwroot/**/*.js",
  ],
  theme: {
    extend: {
      colors: {
        // Background layers - mapped to CSS variables for theme support
        bg: {
          primary: 'var(--color-bg-primary)',
          secondary: 'var(--color-bg-secondary)',
          tertiary: 'var(--color-bg-tertiary)',
          hover: 'var(--color-bg-hover)',
        },
        // Text colors - mapped to CSS variables for theme support
        text: {
          primary: 'var(--color-text-primary)',
          secondary: 'var(--color-text-secondary)',
          tertiary: 'var(--color-text-tertiary)',
          placeholder: 'var(--color-text-placeholder)',
          inverse: 'var(--color-text-inverse, #FFFFFF)', // Theme-aware: white on colored backgrounds
        },
        // Brand accent colors - mapped to CSS variables for theme support
        accent: {
          orange: {
            DEFAULT: 'var(--color-accent-orange)',
            hover: 'var(--color-accent-orange-hover)',
            active: 'var(--color-accent-orange-active)',
            muted: 'var(--color-accent-orange-muted)',
          },
          blue: {
            DEFAULT: 'var(--color-accent-blue)',
            hover: 'var(--color-accent-blue-hover)',
            active: 'var(--color-accent-blue-active)',
            muted: 'var(--color-accent-blue-muted)',
          },
          purple: {
            DEFAULT: 'var(--color-accent-purple)',
          },
        },
        // Semantic colors - mapped to CSS variables for theme support
        success: {
          DEFAULT: 'var(--color-success)',
          bg: 'var(--color-success-bg)',
          border: 'var(--color-success-border)',
        },
        warning: {
          DEFAULT: 'var(--color-warning)',
          bg: 'var(--color-warning-bg)',
          border: 'var(--color-warning-border)',
        },
        error: {
          DEFAULT: 'var(--color-error)',
          hover: '#dc2626',
          bg: 'var(--color-error-bg)',
          border: 'var(--color-error-border)',
        },
        info: {
          DEFAULT: 'var(--color-info)',
          bg: 'var(--color-info-bg)',
          border: 'var(--color-info-border)',
        },
        // Border colors - mapped to CSS variables for theme support
        border: {
          primary: 'var(--color-border-primary)',
          secondary: 'var(--color-border-secondary)',
          focus: 'var(--color-border-focus)',
        },
        // Discord brand color
        discord: {
          DEFAULT: 'var(--color-discord)',
          hover: 'var(--color-discord-hover)',
        },
      },
      fontFamily: {
        heading: [
          '"Plus Jakarta Sans"',
          '-apple-system',
          'BlinkMacSystemFont',
          '"Segoe UI"',
          'Roboto',
          '"Helvetica Neue"',
          'Arial',
          'sans-serif',
        ],
        sans: [
          '-apple-system',
          'BlinkMacSystemFont',
          '"Segoe UI"',
          'Roboto',
          '"Helvetica Neue"',
          'Arial',
          'sans-serif',
          '"Apple Color Emoji"',
          '"Segoe UI Emoji"',
          '"Segoe UI Symbol"',
        ],
        mono: [
          'ui-monospace',
          'SFMono-Regular',
          '"SF Mono"',
          'Menlo',
          'Monaco',
          'Consolas',
          '"Liberation Mono"',
          '"Courier New"',
          'monospace',
        ],
      },
      fontSize: {
        'display': ['3rem', { lineHeight: '1.1', letterSpacing: '-0.02em', fontWeight: '700' }],
        'h1': ['2.25rem', { lineHeight: '1.2', letterSpacing: '-0.01em', fontWeight: '700' }],
        'h2': ['1.875rem', { lineHeight: '1.3', letterSpacing: '-0.01em', fontWeight: '600' }],
        'h3': ['1.5rem', { lineHeight: '1.35', fontWeight: '600' }],
        'h4': ['1.25rem', { lineHeight: '1.4', fontWeight: '600' }],
        'h5': ['1.125rem', { lineHeight: '1.4', fontWeight: '600' }],
        'h6': ['1rem', { lineHeight: '1.5', fontWeight: '600' }],
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
        'glow-orange': '0 0 20px var(--glow-primary)',
        'glow-blue': '0 0 20px var(--glow-secondary)',
      },
      borderRadius: {
        'sm': '0.25rem',
        'DEFAULT': '0.375rem',
        'md': '0.375rem',
        'lg': '0.5rem',
        'xl': '0.75rem',
      },
    },
  },
  plugins: [],
}
