/**
 * The one timer the panel polls with. A store or a view that has to read something again and again hands its read to
 * {@link usePoller}; it never holds a `setInterval` of its own. The poller waits for a read to finish before it
 * schedules the next one (so two reads never overlap and a slow server is never flooded), stops while the tab is
 * hidden, reads once at once when the tab is shown again, and stops with the scope that created it. All pollers share
 * one `visibilitychange` listener whose handler only moves timers, never reads or renders.
 */
import { getCurrentScope, onScopeDispose } from 'vue'
import { POLL_INTERVALS_MS } from '@/lib/limits'
import type { PollKind } from '@/types/poller'

/** What a poller is told. */
export interface PollerOptions {
  /** What is being polled; decides the interval. */
  kind: PollKind
  /** One read. It handles its own failures (a rejection is an unhandled error) and may call `stop` to end the poll. */
  tick: () => void | Promise<void>
  /** Whether {@link Poller.start} reads at once instead of waiting one interval. Defaults to true. */
  immediate?: boolean
}

/** The handle of one poller. */
export interface Poller {
  /** Starts (or restarts) the poll; a poll already running is replaced. */
  start: () => void
  /** Ends the poll; a read in flight finishes but nothing follows it. Safe to call when not running. */
  stop: () => void
}

/** What the shared listener drives on every poller that is running. */
interface Pausable {
  /** Clears the pending wait because the tab went away. */
  hide: () => void
  /** Reads again soon because the tab came back. */
  show: () => void
}

/** The pollers that are running now; the shared listener is attached while this is not empty. */
const running = new Set<Pausable>()

/**
 * Tells whether the page is hidden from the user.
 *
 * @returns True while the tab is in the background.
 */
const isHidden = (): boolean => document.visibilityState === 'hidden'

/**
 * Moves every running poller when the tab is hidden or shown.
 *
 * @returns Nothing.
 */
const onVisibilityChange = (): void => {
  running.forEach((poller) => (isHidden() ? poller.hide() : poller.show()))
}

/**
 * Adds a poller to the running set, attaching the shared listener for the first one.
 *
 * @param poller - The poller that started.
 * @returns Nothing.
 */
const register = (poller: Pausable): void => {
  if (running.size === 0) {
    document.addEventListener('visibilitychange', onVisibilityChange)
  }
  running.add(poller)
}

/**
 * Removes a poller from the running set, detaching the shared listener with the last one.
 *
 * @param poller - The poller that stopped.
 * @returns Nothing.
 */
const unregister = (poller: Pausable): void => {
  running.delete(poller)
  if (running.size === 0) {
    document.removeEventListener('visibilitychange', onVisibilityChange)
  }
}

/**
 * Creates a poller. When called inside a component, a store or any effect scope it stops with that scope.
 *
 * @param options - What to read and how often (by kind).
 * @returns The handle that starts and stops the poll.
 */
export const usePoller = (options: PollerOptions): Poller => {
  /** The wait before the next read, undefined when none is pending. */
  let timer: ReturnType<typeof setTimeout> | undefined

  /** Whether the poll is on; a hidden tab keeps it on but pauses the wait. */
  let active = false

  /** Whether a read is in flight, so a returning tab does not start a second one beside it. */
  let reading = false

  /** The number of the current start; a read that finishes for an older one schedules nothing. */
  let generation = 0

  /**
   * Clears the pending wait.
   *
   * @returns Nothing.
   */
  const clear = (): void => {
    if (timer !== undefined) {
      clearTimeout(timer)
      timer = undefined
    }
  }

  /**
   * One read, unless the poll was stopped, the tab is hidden or a read is already in flight. It waits for the read to
   * finish and then schedules the next one a full interval later, so reads never overlap.
   *
   * @returns Resolves when the read has ended.
   */
  const run = async (): Promise<void> => {
    timer = undefined
    if (!active || isHidden() || reading) {
      return
    }
    const mine = generation
    reading = true
    try {
      await options.tick()
    } finally {
      // A read of an older start must not clear the flag of the read that replaced it.
      if (mine === generation) {
        reading = false
      }
    }
    if (active && mine === generation && !isHidden()) {
      timer = setTimeout(run, POLL_INTERVALS_MS[options.kind])
    }
  }

  /** What the shared listener calls. */
  const pausable: Pausable = {
    hide: clear,
    /** Resumes the poll with one refresh when the tab shows again. */
    show: () => {
      // The read itself runs from a timer, so the listener returns at once. One read stands for all the time away.
      if (!reading) {
        clear()
        timer = setTimeout(run, 0)
      }
    },
  }

  /**
   * Ends the poll.
   *
   * @returns Nothing.
   */
  const stop = (): void => {
    active = false
    reading = false
    generation += 1
    clear()
    unregister(pausable)
  }

  /**
   * Starts the poll, replacing one already running.
   *
   * @returns Nothing.
   */
  const start = (): void => {
    stop()
    active = true
    register(pausable)
    if (isHidden()) {
      // The shared listener starts the first read when the tab is shown.
      return
    }
    if (options.immediate === false) {
      timer = setTimeout(run, POLL_INTERVALS_MS[options.kind])
      return
    }
    void run()
  }

  if (getCurrentScope()) {
    onScopeDispose(stop)
  }

  return { start, stop }
}
