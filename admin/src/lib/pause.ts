/**
 * The one wait the app shares: a promise that settles after a number of milliseconds, driven by `setTimeout`
 * so tests can move it with fake timers.
 */

/**
 * Waits for a number of milliseconds.
 *
 * @param ms - How long to wait.
 * @returns A promise that resolves after that time.
 */
export const pause = (ms: number): Promise<void> => new Promise((resolve) => setTimeout(resolve, ms))
