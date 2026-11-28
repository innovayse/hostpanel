/**
 * Tests for the integrations store: list with category normalisation, detail, save-then-reread
 * and the connection test, through the real `useApi` transport. A refusal keeps the API's own
 * sentence; nothing throws.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useIntegrationsStore } from '@/stores/integrations'
import { json, problem, stubFetch, stubOffline } from '../helpers/fetch'

/** The sentence shown when nothing answered. */
const NO_ANSWER = 'Could not reach the server. Check your connection and try again.'

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('integrationsStore.fetchAll', () => {
  it('maps the backend category strings to the frontend keys', async () => {
    stubFetch(json([
      { slug: 'bank-transfer', name: null, category: 'Payment Gateways' },
      { slug: 'x', name: 'X', category: 'Unknown Group' },
    ]))
    const store = useIntegrationsStore()

    await store.fetchAll()
    expect(store.integrations.map(i => i.category)).toEqual(['payments', 'Unknown Group'])
    expect(store.loading).toBe(false)
  })

  it('lists a plugin under the category of its declared type, whatever its manifest category says', async () => {
    stubFetch(json([
      { slug: 'deliver', name: 'Deliver', category: 'Extensions', pluginType: 'Fulfilment' },
      { slug: 'sign-in', name: 'Sign in', category: 'Extensions', pluginType: 'Identity' },
      { slug: 'worker', name: 'Worker', category: 'Payment Gateways', pluginType: 'Hosted' },
      { slug: 'bank', name: 'Bank', category: 'Payment', pluginType: 'Payment' },
      { slug: 'smtp', name: null, category: 'Email / SMTP', pluginType: null },
    ]))
    const store = useIntegrationsStore()

    await store.fetchAll()
    expect(store.integrations.map(i => i.category)).toEqual(['fulfilment', 'identity', 'hosted', 'payments', 'email'])
  })

  it('keeps the API sentence on refusal', async () => {
    stubFetch(problem('Integrations unavailable.', 503))
    const store = useIntegrationsStore()

    await store.fetchAll()
    expect(store.error).toBe('Integrations unavailable.')
    expect(store.integrations).toEqual([])
  })
})

describe('integrationsStore.fetchOne', () => {
  it('loads the detail', async () => {
    stubFetch(json({ slug: 'bank-transfer', name: 'Bank Transfer', fieldDefinitions: [] }))
    const store = useIntegrationsStore()

    await store.fetchOne('bank-transfer')
    expect(store.current).toEqual({ slug: 'bank-transfer', name: 'Bank Transfer', fieldDefinitions: [] })
  })

  it('empties current and keeps the no-answer sentence offline', async () => {
    stubOffline()
    const store = useIntegrationsStore()
    store.current = { slug: 'old' } as never

    await store.fetchOne('bank-transfer')
    expect(store.current).toBeNull()
    expect(store.error).toBe(NO_ANSWER)
  })
})

describe('integrationsStore.saveConfig', () => {
  it('re-reads the detail after the 204 instead of blanking the page', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }), json({ slug: 'bank-transfer', name: 'Bank Transfer', fieldDefinitions: [], enabled: true }))
    const store = useIntegrationsStore()

    expect(await store.saveConfig('bank-transfer', { enabled: true, config: {} } as never)).toBe(true)
    expect(mock).toHaveBeenCalledTimes(2)
    expect(store.current).toEqual({ slug: 'bank-transfer', name: 'Bank Transfer', fieldDefinitions: [], enabled: true })
  })

  it('keeps the API sentence and does not re-read on refusal', async () => {
    const mock = stubFetch(problem('Required field missing.', 422))
    const store = useIntegrationsStore()

    expect(await store.saveConfig('bank-transfer', {} as never)).toBe(false)
    expect(store.error).toBe('Required field missing.')
    expect(mock).toHaveBeenCalledTimes(1)
  })
})

describe('integrationsStore.testConnection', () => {
  it('keeps the result with its test time and a duration', async () => {
    stubFetch(json({ success: true, message: 'ok', testedAt: '2026-01-01T00:00:00Z' }))
    const store = useIntegrationsStore()
    await store.testConnection('bank-transfer')
    expect(store.testResult?.success).toBe(true)
    expect(store.testResult?.durationMs).toBeTypeOf('number')
    expect(store.testResult?.testedAt).toBe('2026-01-01T00:00:00Z')
    expect(store.testing).toBe(false)
  })

  it('reports a refused test as an unsuccessful result carrying the API sentence', async () => {
    stubFetch(problem('Credentials rejected.', 502))
    const store = useIntegrationsStore()

    await store.testConnection('bank-transfer')
    expect(store.testResult).toMatchObject({ success: false, message: 'Credentials rejected.' })
    expect(store.testing).toBe(false)
  })

  it('reports an unreachable server with the no-answer sentence', async () => {
    stubOffline()
    const store = useIntegrationsStore()

    await store.testConnection('bank-transfer')
    expect(store.testResult).toMatchObject({ success: false, message: NO_ANSWER })
  })
})
