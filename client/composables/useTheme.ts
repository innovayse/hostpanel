/**
 * Theme composable for accessing design tokens
 * Provides easy access to theme constants throughout the app
 */

import { theme } from '~/constants/theme'

export const useTheme = () => {
  /**
   * Get color by path (e.g., 'primary.500')
   */
  const getColor = (path: string): string => {
    const keys = path.split('.')
    let value: any = theme.colors

    for (const key of keys) {
      value = value[key]
      if (!value) break
    }

    return value || ''
  }

  /**
   * Get gradient by name
   */
  const getGradient = (name: keyof typeof theme.gradients): string => {
    return theme.gradients[name]
  }

  /**
   * Get shadow by name
   */
  const getShadow = (name: keyof typeof theme.shadows): string => {
    return theme.shadows[name]
  }

  /**
   * Generate dynamic gradient from two colors
   */
  const createGradient = (from: string, to: string, direction: number = 135): string => {
    return `linear-gradient(${direction}deg, ${from} 0%, ${to} 100%)`
  }

  /**
   * Get transition timing
   */
  const getTransition = (property: string, duration: keyof typeof theme.transitions = 'normal'): string => {
    return `${property} ${theme.transitions[duration]} ${theme.transitions.ease}`
  }

  return {
    theme,
    getColor,
    getGradient,
    getShadow,
    createGradient,
    getTransition,
  }
}
