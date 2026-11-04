/**
 * Minimal vitepress stub for tests that import VitePress components.
 *
 * Returns an empty theme so components that call `useData()` do not throw.
 * Override `theme.value` in a test to set specific theme fields.
 */
export const useData = () => ({ theme: { value: {} } })
