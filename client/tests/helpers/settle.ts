/**
 * Waits for real socket work while `setTimeout` is faked.
 *
 * `vi.waitFor` polls on `setTimeout`, so under `vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })` it never ticks,
 * and advancing the clock to make it tick would also fire the very time limit a test is measuring. This helper yields to
 * the event loop with `setImmediate` (never faked here), which lets the stub's replies arrive and the promises behind them
 * run, without moving the faked clock at all.
 *
 * These are the waits for tests whose clock is faked. A test on real timers waits with `waitUntil` from
 * `tests/helpers/waitUntil`, which retries an assertion with `vi.waitFor` and a named limit; the two are not
 * interchangeable, because `vi.waitFor` cannot tick under a faked `setTimeout` and `until` has no assertion to retry.
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

/** How many consecutive event-loop turns a snapshot must stay unchanged for {@link untilStable} to call it settled. */
const STEADY_TURNS = 3

/**
 * Waits, without moving the faked clock, until what a snapshot reads stops changing.
 *
 * Replies that cross a real socket land in turns the test cannot count, and a render that follows
 * them lands in later ones. Instead of a guessed number of turns, this returns once the snapshot
 * (the rendered HTML, a store's state) has read the same for several turns in a row.
 *
 * @param snapshot - Reads the state that must settle, as a string that changes when the state does.
 * @param what - Named in the failure, so a timeout says what never settled.
 * @returns Resolves once the snapshot has stopped changing.
 * @throws When it is still changing after {@link MAX_TURNS} turns.
 */
export const untilStable = async (snapshot: () => string, what: string): Promise<void> => {
  let previous: string | undefined
  let steady = 0
  for (let i = 0; i < MAX_TURNS; i += 1) {
    const current = snapshot()
    steady = current === previous ? steady + 1 : 0
    previous = current
    if (steady >= STEADY_TURNS) {
      return
    }
    await turn()
  }
  throw new Error(`Never settled: ${what}`)
}
