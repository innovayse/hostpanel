/**
 * Waits for an assertion that depends on real socket work to hold.
 *
 * `vi.waitFor` gives up after one second unless told otherwise, which a busy CI machine can exceed
 * for a reply that crosses a real socket and the render that follows it. Every test waits through
 * this wrapper instead, so the limit is one named value and not a default each call inherits.
 *
 * This is the wait for tests that run on real timers: it polls on `setTimeout`, so it never ticks while
 * `setTimeout` is faked. A test that fakes the clock waits with `until`, `settle` or `untilStable` from
 * `tests/helpers/settle` instead, which poll on `setImmediate` and never move the faked clock.
 *
 * @module tests/helpers/waitUntil
 */

import { vi } from 'vitest'

/** How long a wait for a socket reply and its render may take before the test fails, in milliseconds. */
const WAIT_TIMEOUT_MS = 5000

/**
 * Retries a callback until it stops throwing (or returns a defined value), as `vi.waitFor` does,
 * with a limit sized for real socket work.
 *
 * @param callback - The assertion or read to retry.
 * @returns What the callback returned once it succeeded.
 * @throws The callback's last error when it still fails after {@link WAIT_TIMEOUT_MS}.
 */
export const waitUntil = <T>(callback: () => T | Promise<T>): Promise<T> => vi.waitFor(callback, { timeout: WAIT_TIMEOUT_MS })
