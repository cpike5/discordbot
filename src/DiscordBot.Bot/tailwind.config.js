/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./Pages/**/*.{razor,cshtml}",
    "./Components/**/*.{razor,cshtml}",
    "./wwwroot/**/*.html",
  ],
  theme: {
    extend: {
      colors: {
        // Background layers
        bg: {
          primary: '#1d2022',
          secondary: '#262a2d',
          tertiary: '#2f3336',
          hover: '#363a3e',
        },
        // Text colors
        text: {
          primary: '#d7d3d0',
          secondary: '#a8a5a3',
          tertiary: '#7a7876',
          inverse: '#1d2022',
        },
        // Brand accent colors
        accent: {
          orange: {
            DEFAULT: '#cb4e1b',
            hover: '#e5591f',
            active: '#b04517',
            muted: '#cb4e1b33',
          },
          blue: {
            DEFAULT: '#098ecf',
            hover: '#0ba3ea',
            active: '#0879b3',
            muted: '#098ecf33',
          },
        },
        // Semantic colors
        success: {
          DEFAULT: '#10b981',
          bg: '#10b98120',
          border: '#10b98150',
        },
        warning: {
          DEFAULT: '#f59e0b',
          bg: '#f59e0b20',
          border: '#f59e0b50',
        },
        error: {
          DEFAULT: '#ef4444',
          hover: '#dc2626',
          bg: '#ef444420',
          border: '#ef444450',
        },
        info: {
          DEFAULT: '#06b6d4',
          bg: '#06b6d420',
          border: '#06b6d450',
        },
        // Border colors
        border: {
          primary: '#3f4447',
          secondary: '#2f3336',
          focus: '#098ecf',
        },
        // Discord brand color
        discord: {
          DEFAULT: '#5865F2',
          hover: '#4752C4',
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
