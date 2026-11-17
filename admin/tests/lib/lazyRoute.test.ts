/**
 * Tests for lazy route loaders: a failed or stalled download is repeated with a growing pause, the plain import
 * behind a retrying loader stays reachable, and a route lists only the loaders the router still has
 * to call.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { RouteLocationNormalized } from 'vue-router'
import { LOAD_RETRIES, LOAD_TIMEOUT_MS, RETRY_STEP_MS, isLazyLoader, lazyLoadersOf, originalLoader, withRetry } from '@/lib/lazyRoute'

/**
 * Builds the slice of a resolved route the helpers read.
 *
 * @param components - One `components` map per matched record; null for a record without components.
 * @returns A route whose `matched` holds those records.
 */
const routeOf = (...components: Array<Record<string, unknown> | null>): Pick<RouteLocationNormalized, 'matched'> =>
  ({ matched: components.map((map) => ({ components: map })) }) as unknown as Pick<RouteLocationNormalized, 'matched'>

describe('withRetry', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('waits one step longer before each repeat and then gives up with the last error', async () => {
    const loader = vi.fn().mockRejectedValue(new Error('gone'))
    const result = withRetry(loader)()
    const settled = result.catch((error: Error) => error.message)

    await vi.advanceTimersByTimeAsync(RETRY_STEP_MS - 1)
    expect(loader).toHaveBeenCalledTimes(1)
    await vi.advanceTimersByTimeAsync(1)
    expect(loader).toHaveBeenCalledTimes(2)
    await vi.advanceTimersByTimeAsync(RETRY_STEP_MS * 2)

    expect(await settled).toBe('gone')
    expect(loader).toHaveBeenCalledTimes(LOAD_RETRIES + 1)
  })

  it('repeats a failed download and recovers', async () => {
    const loader = vi.fn()
      .mockRejectedValueOnce(new Error('504'))
      .mockResolvedValue('ok')
    const result = withRetry(loader)()

    await vi.advanceTimersByTimeAsync(RETRY_STEP_MS)

    expect(await result).toBe('ok')
    expect(loader).toHaveBeenCalledTimes(2)
  })

  it('rejects an attempt that stalls after the time limit and repeats it', async () => {
    const loader = vi.fn()
      .mockReturnValueOnce(new Promise<string>(() => undefined))
      .mockResolvedValue('ok')
    const result = withRetry(loader)()

    await vi.advanceTimersByTimeAsync(LOAD_TIMEOUT_MS - 1)
    expect(loader).toHaveBeenCalledTimes(1)
    await vi.advanceTimersByTimeAsync(1 + RETRY_STEP_MS)

    expect(await result).toBe('ok')
    expect(loader).toHaveBeenCalledTimes(2)
  })

  it('gives up with a timeout error when every attempt stalls', async () => {
    const loader = vi.fn(() => new Promise<string>(() => undefined))
    const settled = withRetry(loader)().catch((error: Error) => error.message)

    await vi.advanceTimersByTimeAsync(LOAD_TIMEOUT_MS * (LOAD_RETRIES + 1) + RETRY_STEP_MS * 3)

    expect(await settled).toContain('took longer than')
    expect(loader).toHaveBeenCalledTimes(LOAD_RETRIES + 1)
  })

  it('does not time out a download that finishes within the limit', async () => {
    const loader = vi.fn(() => new Promise<string>((resolve) => setTimeout(() => resolve('late'), LOAD_TIMEOUT_MS - 1)))
    const result = withRetry(loader)()

    await vi.advanceTimersByTimeAsync(LOAD_TIMEOUT_MS)

    expect(await result).toBe('late')
    expect(loader).toHaveBeenCalledTimes(1)
  })

  it('returns the result of a first-time success without waiting', async () => {
    const loader = vi.fn().mockResolvedValue('ok')

    expect(await withRetry(loader)()).toBe('ok')
    expect(loader).toHaveBeenCalledTimes(1)
  })

  it('makes a loader vue-router treats as lazy, not as a component', () => {
    expect(isLazyLoader(withRetry(() => Promise.resolve({})))).toBe(true)
  })
})

describe('originalLoader', () => {
  it('exposes the wrapped import, and passes any other loader through', () => {
    const loader = vi.fn(() => Promise.resolve('x'))

    expect(originalLoader(withRetry(loader))).toBe(loader)
    expect(originalLoader(loader)).toBe(loader)
  })
})

describe('isLazyLoader', () => {
  it('accepts a plain import function', () => {
    expect(isLazyLoader(() => Promise.resolve({}))).toBe(true)
  })

  it('rejects resolved components and anything that is not a function', () => {
    expect(isLazyLoader({ render: () => null })).toBe(false)
    expect(isLazyLoader(Object.assign(() => null, { displayName: 'Named' }))).toBe(false)
    expect(isLazyLoader(Object.assign(() => null, { props: {} }))).toBe(false)
    expect(isLazyLoader(Object.assign(() => null, { __vccOpts: {} }))).toBe(false)
    expect(isLazyLoader(undefined)).toBe(false)
  })
})

describe('lazyLoadersOf', () => {
  it('lists the loaders of every matched record, layout first', () => {
    /** Loader of the outer layout. */
    const layout = (): Promise<unknown> => Promise.resolve({})
    /** Loader of the view inside it. */
    const view = (): Promise<unknown> => Promise.resolve({})

    expect(lazyLoadersOf(routeOf({ default: layout }, null, { default: view }))).toEqual([layout, view])
  })

  it('leaves out components the router has already resolved', () => {
    /** Loader of the view still to download. */
    const view = (): Promise<unknown> => Promise.resolve({})

    expect(lazyLoadersOf(routeOf({ default: { render: () => null } }, { default: view }))).toEqual([view])
    expect(lazyLoadersOf(routeOf({ default: { render: () => null } }))).toEqual([])
  })

  it('finds nothing for a route that matched nothing', () => {
    expect(lazyLoadersOf(routeOf())).toEqual([])
  })
})
