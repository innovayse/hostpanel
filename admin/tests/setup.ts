/**
 * Runs before every test file.
 *
 * Every component a test mounts is unmounted when the test ends, so its timers and in-flight
 * requests cannot outlive the test and fail after the environment is torn down.
 *
 * jsdom has no layout and so no `ResizeObserver`; a component that watches an element's size gets an
 * observer that never fires. A test about resizing stubs its own with `vi.stubGlobal`.
 */
import { afterEach } from 'vitest'
import { enableAutoUnmount } from '@vue/test-utils'

enableAutoUnmount(afterEach)

/** Observer that never reports a size change, because jsdom never lays anything out. */
class NoLayoutResizeObserver {
  /** Accepts the element and never reports on it. */
  observe = (): void => {}
  /** Nothing is watched, so there is nothing to stop. */
  unobserve = (): void => {}
  /** Nothing is watched, so there is nothing to stop. */
  disconnect = (): void => {}
}

if (typeof globalThis.ResizeObserver === 'undefined') {
  globalThis.ResizeObserver = NoLayoutResizeObserver
}
