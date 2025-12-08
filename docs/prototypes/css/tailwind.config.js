/**
 * Shared Tailwind Configuration
 * Discord Bot Admin UI
 *
 * This file is loaded before Tailwind CDN and provides the design system colors.
 * Usage in HTML:
 *   <script src="css/tailwind.config.js"></script>
 *   <script src="https://cdn.tailwindcss.com"></script>
 *   <script>tailwind.config = window.tailwindConfig;</script>
 */

window.tailwindConfig = {
  theme: {
    extend: {
      colors: {
        bg: {
          primary: '#1d2022',
          secondary: '#262a2d',
          tertiary: '#2f3336',
          hover: '#363a3e',
        },
        text: {
          primary: '#d7d3d0',
          secondary: '#a8a5a3',
          tertiary: '#7a7876',
          inverse: '#1d2022',
        },
        accent: {
          orange: {
            DEFAULT: '#cb4e1b',
            hover: '#e5591f',
            active: '#b04517',
            muted: 'rgba(203, 78, 27, 0.2)',
          },
          blue: {
            DEFAULT: '#098ecf',
            hover: '#0ba3ea',
            active: '#0879b3',
            muted: 'rgba(9, 142, 207, 0.2)',
          },
        },
        success: {
          DEFAULT: '#10b981',
          bg: 'rgba(16, 185, 129, 0.1)',
          border: 'rgba(16, 185, 129, 0.3)',
        },
        warning: {
          DEFAULT: '#f59e0b',
          bg: 'rgba(245, 158, 11, 0.1)',
          border: 'rgba(245, 158, 11, 0.3)',
        },
        error: {
          DEFAULT: '#ef4444',
          bg: 'rgba(239, 68, 68, 0.1)',
          border: 'rgba(239, 68, 68, 0.3)',
        },
        info: {
          DEFAULT: '#06b6d4',
          bg: 'rgba(6, 182, 212, 0.1)',
          border: 'rgba(6, 182, 212, 0.3)',
        },
        border: {
          primary: '#3f4447',
          secondary: '#2f3336',
          focus: '#098ecf',
        },
      },
      fontFamily: {
        sans: ['-apple-system', 'BlinkMacSystemFont', '"Segoe UI"', 'Roboto', '"Helvetica Neue"', 'Arial', 'sans-serif'],
        mono: ['ui-monospace', 'SFMono-Regular', '"SF Mono"', 'Menlo', 'Monaco', 'Consolas', 'monospace'],
      },
    },
  },
};
