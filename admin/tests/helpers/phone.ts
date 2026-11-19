/**
 * Test helper that makes the screen a phone (below the `sm` breakpoint) or not for the components that pick a bottom
 * sheet on a phone (`useDrawerPresentation`). jsdom has no `matchMedia`, so every query otherwise answers "no match".
 */
import { vi } from 'vitest'

/**
 * Stubs `matchMedia` so every media query answers the same.
 *
 * Pair it with `vi.unstubAllGlobals()` in an `afterEach`.
 *
 * @param phone - Whether the below-`sm` query matches.
 * @returns Nothing; the global is stubbed.
 */
export const stubPhone = (phone: boolean): void => {
  vi.stubGlobal('matchMedia', () => ({ matches: phone, addEventListener: vi.fn(), removeEventListener: vi.fn() }))
}
