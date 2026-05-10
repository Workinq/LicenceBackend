import type { Config } from 'tailwindcss';

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        surface: {
          DEFAULT: '#fdf8f3',
          elevated: '#ffffff',
          sunken: '#f7efe5',
        },
        ink: {
          DEFAULT: '#2a1f17',
          muted: '#5d4d3e',
          subtle: '#8a7a68',
        },
        border: {
          DEFAULT: '#efe5d8',
          strong: '#d8c8b0',
        },
        accent: {
          DEFAULT: '#b85c3a',
          soft: '#f4d8cc',
        },
        status: {
          active: { bg: '#e8f3e8', fg: '#2d5a2d' },
          suspended: { bg: '#fef0d4', fg: '#8a5a00' },
          revoked: { bg: '#fce0e0', fg: '#8a2828' },
        },
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
        display: ['Fraunces', 'Georgia', 'serif'],
        mono: ['JetBrains Mono', 'ui-monospace', 'monospace'],
      },
      borderRadius: {
        DEFAULT: '6px',
        lg: '8px',
        pill: '99px',
      },
      boxShadow: {
        card: '0 1px 2px rgba(42,31,23,0.04), 0 1px 3px rgba(42,31,23,0.05)',
      },
    },
  },
} satisfies Config;
