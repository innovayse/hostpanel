/**
 * Tests for the shared wait: it settles only after the time has passed, under fake timers.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { pause } from '@/lib/pause'

afterEach(() => {
  vi.useRealTimers()
})

describe('pause', () => {
  it('resolves only once the given milliseconds have passed', async () => {
    vi.useFakeTimers()
    let done = false
    const waiting = pause(500).then(() => {
      done = true
    })

    await vi.advanceTimersByTimeAsync(499)
    expect(done).toBe(false)
    await vi.advanceTimersByTimeAsync(1)
    await waiting
    expect(done).toBe(true)
  })
})
