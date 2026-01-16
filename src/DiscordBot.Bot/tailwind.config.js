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
        },
        // Semantic colors - mapped to CSS variables for theme support
        success: {
          DEFAULT: 'var(--color-success)',
          bg: '#10b98120',
          border: '#10b98150',
        },
        warning: {
          DEFAULT: 'var(--color-warning)',
          bg: '#f59e0b20',
          border: '#f59e0b50',
        },
        error: {
          DEFAULT: 'var(--color-error)',
          hover: '#dc2626',
          bg: '#ef444420',
          border: '#ef444450',
        },
        info: {
          DEFAULT: 'var(--color-info)',
          bg: '#06b6d420',
          border: '#06b6d450',
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
        'sm': '0 1px 2px 0 rgba(0, 0, 0, 0.3)',
        'DEFAULT': '0 4px 6px -1px rgba(0, 0, 0, 0.3), 0 2px 4px -1px rgba(0, 0, 0, 0.2)',
        'md': '0 4px 6px -1px rgba(0, 0, 0, 0.3), 0 2px 4px -1px rgba(0, 0, 0, 0.2)',
        'lg': '0 10px 15px -3px rgba(0, 0, 0, 0.3), 0 4px 6px -2px rgba(0, 0, 0, 0.2)',
        'xl': '0 20px 25px -5px rgba(0, 0, 0, 0.3), 0 10px 10px -5px rgba(0, 0, 0, 0.2)',
        'glow-orange': '0 0 20px rgba(203, 78, 27, 0.4)',
        'glow-blue': '0 0 20px rgba(9, 142, 207, 0.4)',
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
