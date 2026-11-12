/**
 * Tests for `stores/demo.ts`, through the real API composable and transport against a stub server:
 * the status is read once, the published customer account is picked by role, and a failed read
 * leaves the demo off instead of throwing.
 *
 * @module tests/stores/demo.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { startTransport } from '../helpers/stubTransport'
import type { StubServer } from '../helpers/stubServer'
import { useDemoStore } from '~/stores/demo'
import { DemoAccountRole } from '~/types/demoaccountrole'

/** The path the demo status is read from. */
const DEMO_PATH = '/api/portal/demo'

/** The stub playing the BFF. */
let stub: StubServer

/** A demo status with both published accounts. */
const STATUS = {
  enabled: true,
  nextResetAt: '2030-01-01T00:00:00Z',
  accounts: [
    { role: DemoAccountRole.Admin, email: 'admin@demo.test', password: 'a' },
    { role: DemoAccountRole.Client, email: 'client@demo.test', password: 'c' }
  ]
}

describe('useDemoStore', () => {
  beforeEach(async () => {
    setActivePinia(createPinia())
    stub = await startTransport(() => ({ body: STATUS }))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('starts as not a demo', () => {
    const store = useDemoStore()

    expect(store.enabled).toBe(false)
    expect(store.clientAccount).toBeUndefined()
  })

  it('stores the status and exposes the client account', async () => {
    const store = useDemoStore()

    await store.load()

    expect(store.enabled).toBe(true)
    expect(store.nextResetAt).toBe('2030-01-01T00:00:00Z')
    expect(store.clientAccount?.email).toBe('client@demo.test')
  })

  it('reads the status only once', async () => {
    const store = useDemoStore()

    await store.load()
    await store.load()

    expect(stub.requests).toHaveLength(1)
    expect(stub.requests[0]).toMatchObject({ method: 'GET', url: DEMO_PATH })
  })

  it('stays off and does not throw when the read fails', async () => {
    stub.respondWith(() => ({ status: 404, body: { detail: 'Not found.' } }))
    const store = useDemoStore()

    await expect(store.load()).resolves.toBeUndefined()

    expect(store.enabled).toBe(false)
    expect(store.accounts).toEqual([])
    expect(store.loaded).toBe(true)
  })
})
