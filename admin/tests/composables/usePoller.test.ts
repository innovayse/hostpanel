/**
 * Tests for the shared poller: it reads at once or after one interval, never overlaps its own reads, pauses while the
 * tab is hidden and reads once when it is shown again, shares one visibility listener, and stops with its scope.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { effectScope } from 'vue'
import { usePoller } from '@/composables/usePoller'
import type { Poller } from '@/composables/usePoller'
import { POLL_INTERVALS_MS } from '@/lib/limits'
import { PollKind } from '@/types/poller'

/** The interval of the kind these tests poll. */
const EVERY = POLL_INTERVALS_MS[PollKind.MigrationStatus]

/** What the page reports as its visibility; the tests flip it. */
let visibility: DocumentVisibilityState = 'visible'

/** The pollers a test started, stopped afterwards so the shared listener never leaks into the next test. */
let started: Poller[] = []

/**
 * Creates a poller and remembers it for cleanup.
 *
 * @param tick - The read.
 * @param immediate - Whether the first read is at once.
 * @returns The poller.
 */
const make = (tick: () => void | Promise<void>, immediate?: boolean): Poller => {
  const poller = usePoller({ kind: PollKind.MigrationStatus, tick, immediate })
  started.push(poller)
  return poller
}

/**
 * Changes the page visibility and fires the event the browser would.
 *
 * @param next - The new visibility.
 * @returns Nothing.
 */
const setVisibility = (next: DocumentVisibilityState): void => {
  visibility = next
  document.dispatchEvent(new Event('visibilitychange'))
}

beforeEach(() => {
  vi.useFakeTimers()
  visibility = 'visible'
  Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => visibility })
})

afterEach(() => {
  started.forEach(poller => poller.stop())
  started = []
  vi.useRealTimers()
  vi.restoreAllMocks()
})

describe('usePoller', () => {
  it('reads at once and then every interval of its kind', async () => {
    const tick = vi.fn()
    make(tick).start()
    expect(tick).toHaveBeenCalledTimes(1)

    await vi.advanceTimersByTimeAsync(EVERY - 1)
    expect(tick).toHaveBeenCalledTimes(1)
    await vi.advanceTimersByTimeAsync(1)
    expect(tick).toHaveBeenCalledTimes(2)
    await vi.advanceTimersByTimeAsync(EVERY)
    expect(tick).toHaveBeenCalledTimes(3)
  })

  it('waits one interval for the first read when it is not immediate', async () => {
    const tick = vi.fn()
    make(tick, false).start()
    expect(tick).not.toHaveBeenCalled()

    await vi.advanceTimersByTimeAsync(EVERY)
    expect(tick).toHaveBeenCalledTimes(1)
  })

  it('never starts a read while the previous one is still in flight', async () => {
    let finish: () => void = () => undefined
    const tick = vi.fn(() => new Promise<void>((resolve) => {
      finish = resolve
    }))
    make(tick).start()

    await vi.advanceTimersByTimeAsync(EVERY * 10)
    expect(tick).toHaveBeenCalledTimes(1)

    finish()
    await vi.advanceTimersByTimeAsync(EVERY)
    expect(tick).toHaveBeenCalledTimes(2)
  })

  it('stops reading while the tab is hidden and reads once when it is shown again', async () => {
    const tick = vi.fn()
    make(tick).start()
    expect(tick).toHaveBeenCalledTimes(1)

    setVisibility('hidden')
    await vi.advanceTimersByTimeAsync(EVERY * 20)
    expect(tick).toHaveBeenCalledTimes(1)

    setVisibility('visible')
    // The listener only schedules: nothing is read inside the event handler itself.
    expect(tick).toHaveBeenCalledTimes(1)
    await vi.advanceTimersByTimeAsync(0)
    expect(tick).toHaveBeenCalledTimes(2)

    await vi.advanceTimersByTimeAsync(EVERY)
    expect(tick).toHaveBeenCalledTimes(3)
  })

  it('does not read at all when started in a hidden tab until it is shown', async () => {
    const tick = vi.fn()
    visibility = 'hidden'
    make(tick).start()
    await vi.advanceTimersByTimeAsync(EVERY * 5)
    expect(tick).not.toHaveBeenCalled()

    setVisibility('visible')
    await vi.advanceTimersByTimeAsync(0)
    expect(tick).toHaveBeenCalledTimes(1)
  })

  it('does not add a second read when the tab comes back during one in flight', async () => {
    let finish: () => void = () => undefined
    const tick = vi.fn(() => new Promise<void>((resolve) => {
      finish = resolve
    }))
    make(tick).start()

    setVisibility('hidden')
    setVisibility('visible')
    await vi.advanceTimersByTimeAsync(0)
    expect(tick).toHaveBeenCalledTimes(1)

    finish()
    await vi.advanceTimersByTimeAsync(EVERY)
    expect(tick).toHaveBeenCalledTimes(2)
  })

  it('stops: no further read, and a read in flight schedules nothing after it', async () => {
    let finish: () => void = () => undefined
    const tick = vi.fn(() => new Promise<void>((resolve) => {
      finish = resolve
    }))
    const poller = make(tick)
    poller.start()
    poller.stop()
    finish()
    await vi.advanceTimersByTimeAsync(EVERY * 5)
    expect(tick).toHaveBeenCalledTimes(1)
  })

  it('lets a read end the poll itself, and a stopped poll stays quiet when the tab changes', async () => {
    const tick = vi.fn(() => poller.stop())
    const poller: Poller = make(tick)
    poller.start()
    await vi.advanceTimersByTimeAsync(EVERY * 3)
    setVisibility('hidden')
    setVisibility('visible')
    await vi.advanceTimersByTimeAsync(EVERY)
    expect(tick).toHaveBeenCalledTimes(1)
  })

  it('replaces a running poll when started again, so only one chain of reads runs', async () => {
    const tick = vi.fn()
    const poller = make(tick)
    poller.start()
    poller.start()
    expect(tick).toHaveBeenCalledTimes(2)

    await vi.advanceTimersByTimeAsync(EVERY)
    expect(tick).toHaveBeenCalledTimes(3)
  })

  it('shares one visibility listener among all pollers and removes it with the last one', () => {
    const add = vi.spyOn(document, 'addEventListener')
    const remove = vi.spyOn(document, 'removeEventListener')
    const first = make(vi.fn())
    const second = make(vi.fn())

    first.start()
    second.start()
    expect(add.mock.calls.filter(call => call[0] === 'visibilitychange')).toHaveLength(1)

    first.stop()
    expect(remove.mock.calls.filter(call => call[0] === 'visibilitychange')).toHaveLength(0)
    second.stop()
    expect(remove.mock.calls.filter(call => call[0] === 'visibilitychange')).toHaveLength(1)
  })

  it('stops with the scope that created it', async () => {
    const tick = vi.fn()
    const scope = effectScope()
    scope.run(() => usePoller({ kind: PollKind.MigrationStatus, tick }).start())
    expect(tick).toHaveBeenCalledTimes(1)

    scope.stop()
    await vi.advanceTimersByTimeAsync(EVERY * 3)
    expect(tick).toHaveBeenCalledTimes(1)
  })
})
