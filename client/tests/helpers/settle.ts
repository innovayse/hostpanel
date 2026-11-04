/**
 * Waits for real socket work while `setTimeout` is faked.
 *
 * `vi.waitFor` polls on `setTimeout`, so under `vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })` it never ticks,
 * and advancing the clock to make it tick would also fire the very time limit a test is measuring. This helper yields to
 * the event loop with `setImmediate` (never faked here), which lets the stub's replies arrive and the promises behind them
 * run, without moving the faked clock at all.
 *
 * @module tests/helpers/settle
 */

import { flushPromises } from '@vue/test-utils'

/** How many event-loop turns {@link until} waits before it gives up: a few seconds of real I/O. */
const MAX_TURNS = 5000

/**
 * Yields one turn of the event loop and flushes the promises it released.
 *
 * @returns Resolves after the turn.
 */
const turn = async (): Promise<void> => {
  await new Promise<void>((resolve) => {
    setImmediate(resolve)
  })
  await flushPromises()
}

/**
 * Waits, without moving the faked clock, until a condition holds.
 *
 * @param condition - What must become true, e.g. "the stub received two requests".
 * @param what - Named in the failure, so a timeout says what never happened.
 * @returns Resolves once the condition holds.
 * @throws When it still does not hold after {@link MAX_TURNS} turns.
 */
export const until = async (condition: () => boolean, what: string): Promise<void> => {
  for (let i = 0; i < MAX_TURNS; i += 1) {
    if (condition()) {
      return
    }
    await turn()
  }
  throw new Error(`Never happened: ${what}`)
}

/**
 * Lets every reply already on its way arrive and be handled, without moving the faked clock.
 *
 * @param turns - How many event-loop turns to give it.
 * @returns Resolves after them.
 */
export const settle = async (turns = 50): Promise<void> => {
  for (let i = 0; i < turns; i += 1) {
    await turn()
  }
}
