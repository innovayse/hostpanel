/**
 * Minimal dark/light mode composable.
 * - Persists preference to localStorage under 'color-mode'
 * - Toggles `dark` class on <html> element
 * - Defaults to dark (matches the site's existing dark design)
 */

const COLOR_MODE_KEY = 'color-mode'

export const useAppColorMode = () => {
  const isDark = useState<boolean>('colorMode', () => true)

  function applyMode(dark: boolean) {
    if (!import.meta.client) return
    document.documentElement.classList.toggle('dark', dark)
    document.documentElement.classList.toggle('light', !dark)
    localStorage.setItem(COLOR_MODE_KEY, dark ? 'dark' : 'light')
  }

  function toggle() {
    isDark.value = !isDark.value
    applyMode(isDark.value)
  }

  function init() {
    if (!import.meta.client) return
    const saved = localStorage.getItem(COLOR_MODE_KEY)
    // Default to dark; only switch to light if explicitly saved
    isDark.value = saved !== 'light'
    applyMode(isDark.value)
  }

  return { isDark, toggle, init }
}
