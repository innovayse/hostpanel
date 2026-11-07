/**
 * Minimal stand-in for VitePress `useData`, for tests that import docs theme components.
 *
 * Returns an empty theme so a component that calls `useData()` does not throw. A test that needs
 * theme fields mocks `vitepress` itself and returns its own `theme` ref.
 *
 * @returns An object whose `theme.value` is empty.
 */
export const useData = () => ({ theme: { value: {} } })
