/**
 * Design System - Theme Constants
 * Centralized design tokens for consistent styling
 */

export const theme = {
  colors: {
    primary: {
      50: '#f0f9ff',
      100: '#e0f2fe',
      200: '#bae6fd',
      300: '#7dd3fc',
      400: '#38bdf8',
      500: '#0ea5e9',
      600: '#0284c7',
      700: '#0369a1',
      800: '#075985',
      900: '#0c4a6e',
      950: '#082f49',
    },
    secondary: {
      50: '#faf5ff',
      100: '#f3e8ff',
      200: '#e9d5ff',
      300: '#d8b4fe',
      400: '#c084fc',
      500: '#a855f7',
      600: '#9333ea',
      700: '#7e22ce',
      800: '#6b21a8',
      900: '#581c87',
      950: '#3b0764',
    },
    dark: {
      bg: '#0a0a0f',
      surface: '#1a1a1f',
      border: '#2a2a2f',
    }
  },

  gradients: {
    primary: 'linear-gradient(135deg, #38bdf8 0%, #0ea5e9 100%)',
    secondary: 'linear-gradient(135deg, #c084fc 0%, #a855f7 100%)',
    hero: 'linear-gradient(135deg, #38bdf8 0%, #0ea5e9 50%, #a855f7 100%)',
    neon: 'linear-gradient(135deg, #06b6d4 0%, #0ea5e9 50%, #ec4899 100%)',
  },

  shadows: {
    sm: '0 1px 2px 0 rgb(0 0 0 / 0.05)',
    md: '0 4px 6px -1px rgb(0 0 0 / 0.1)',
    lg: '0 10px 15px -3px rgb(0 0 0 / 0.1)',
    xl: '0 20px 25px -5px rgb(0 0 0 / 0.1)',
    glow: '0 0 20px rgba(14, 165, 233, 0.3)',
    glowHover: '0 0 30px rgba(14, 165, 233, 0.5)',
  },

  spacing: {
    section: {
      sm: '3rem',
      md: '5rem',
      lg: '7rem',
    },
    container: {
      padding: '1rem',
      maxWidth: '1280px',
    }
  },

  transitions: {
    fast: '150ms',
    normal: '300ms',
    slow: '500ms',
    ease: 'cubic-bezier(0.4, 0, 0.2, 1)',
  },

  animations: {
    blob: 'blob 7s infinite',
    gradient: 'gradient 8s ease infinite',
    pulse: 'pulse 2s cubic-bezier(0.4, 0, 0.6, 1) infinite',
  }
} as const

export type Theme = typeof theme
