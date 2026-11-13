/**
 * Tests for the demo store: values and accounts kept, one load only, silent on failure.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useDemoStore } from '@/stores/demo'
import { DemoAccountRole } from '@/types/demo'
import { json, problem, stubFetch } from '../helpers/fetch'

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('demoStore', () => {
  it('load keeps the description and the published accounts', async () => {
    stubFetch(json({
      enabled: true,
      nextResetAt: '2030-01-01T00:00:00Z',
      accounts: [
        { role: DemoAccountRole.Client, email: 'c@x.test', password: 'c' },
        { role: DemoAccountRole.Admin, email: 'a@x.test', password: 'a' },
      ],
    }))
    const store = useDemoStore()

    await store.load()
    expect(store.enabled).toBe(true)
    expect(store.nextResetAt).toBe('2030-01-01T00:00:00Z')
    expect(store.accounts.map(a => a.email)).toEqual(['c@x.test', 'a@x.test'])
  })

  it('load reaches the network once', async () => {
    const mock = stubFetch(json({ enabled: false, nextResetAt: null, accounts: [] }))
    const store = useDemoStore()

    await store.load()
    await store.load()
    expect(mock).toHaveBeenCalledTimes(1)
    expect(store.enabled).toBe(false)
    expect(store.accounts).toEqual([])
  })

  it('forced load reaches the network again', async () => {
    const mock = stubFetch(json({ enabled: true, nextResetAt: '2030-01-01T00:00:00Z', accounts: [] }))
    const store = useDemoStore()

    await store.load()
    await store.load(true)
    expect(mock).toHaveBeenCalledTimes(2)
  })

  it('load means not a demo when the lookup fails', async () => {
    stubFetch(problem('Down.', 500))
    const store = useDemoStore()

    await store.load()
    expect(store.enabled).toBe(false)
    expect(store.loaded).toBe(true)
  })
})
