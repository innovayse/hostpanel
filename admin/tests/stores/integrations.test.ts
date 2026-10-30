/**
 * Tests for the integrations store: list with category normalisation, detail, save-then-reread
 * the connection test and the live CWP status, through the real `useApi` transport. A refusal keeps the API's own
 * sentence; nothing throws.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useIntegrationsStore } from '@/stores/integrations'
import { callOf, json, problem, stubFetch, stubOffline } from '../helpers/fetch'

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
      { slug: 'stripe', category: 'Payment Gateways' },
      { slug: 'x', category: 'Unknown Group' },
    ]))
    const store = useIntegrationsStore()

    await store.fetchAll()
    expect(store.integrations.map(i => i.category)).toEqual(['payments', 'Unknown Group'])
    expect(store.loading).toBe(false)
  })

  it('lists a plugin under the category of its declared type, whatever its manifest category says', async () => {
    stubFetch(json([
      { slug: 'deliver', category: 'Extensions', pluginType: 'Fulfilment' },
      { slug: 'sign-in', category: 'Extensions', pluginType: 'Identity' },
      { slug: 'worker', category: 'Payment Gateways', pluginType: 'Hosted' },
      { slug: 'bank', category: 'Payment', pluginType: 'Payment' },
      { slug: 'smtp', category: 'Email / SMTP', pluginType: null },
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
    stubFetch(json({ slug: 'stripe' }))
    const store = useIntegrationsStore()

    await store.fetchOne('stripe')
    expect(store.current).toEqual({ slug: 'stripe' })
  })

  it('empties current and keeps the no-answer sentence offline', async () => {
    stubOffline()
    const store = useIntegrationsStore()
    store.current = { slug: 'old' } as never

    await store.fetchOne('stripe')
    expect(store.current).toBeNull()
    expect(store.error).toBe(NO_ANSWER)
  })
})

describe('integrationsStore.saveConfig', () => {
  it('re-reads the detail after the 204 instead of blanking the page', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }), json({ slug: 'stripe', enabled: true }))
    const store = useIntegrationsStore()

    expect(await store.saveConfig('stripe', { enabled: true, config: {} } as never)).toBe(true)
    expect(mock).toHaveBeenCalledTimes(2)
    expect(store.current).toEqual({ slug: 'stripe', enabled: true })
  })

  it('keeps the API sentence and does not re-read on refusal', async () => {
    const mock = stubFetch(problem('Required field missing.', 422))
    const store = useIntegrationsStore()

    expect(await store.saveConfig('stripe', {} as never)).toBe(false)
    expect(store.error).toBe('Required field missing.')
    expect(mock).toHaveBeenCalledTimes(1)
  })
})

describe('integrationsStore.testConnection', () => {
  it('keeps the result with its test time and a duration', async () => {
    stubFetch(json({ success: true, message: 'ok', testedAt: '2026-01-01T00:00:00Z' }))
    const store = useIntegrationsStore()
    await store.testConnection('stripe')
    expect(store.testResult?.success).toBe(true)
    expect(store.testResult?.durationMs).toBeTypeOf('number')
    expect(store.testResult?.testedAt).toBe('2026-01-01T00:00:00Z')
    expect(store.testing).toBe(false)
  })

  it('reports a refused test as an unsuccessful result carrying the API sentence', async () => {
    stubFetch(problem('Credentials rejected.', 502))
    const store = useIntegrationsStore()

    await store.testConnection('stripe')
    expect(store.testResult).toMatchObject({ success: false, message: 'Credentials rejected.' })
    expect(store.testing).toBe(false)
  })

  it('reports an unreachable server with the no-answer sentence', async () => {
    stubOffline()
    const store = useIntegrationsStore()

    await store.testConnection('stripe')
    expect(store.testResult).toMatchObject({ success: false, message: NO_ANSWER })
  })
})

describe('integrationsStore.fetchCwpServerInfo', () => {
  it('reads the CWP endpoint and fills the status', async () => {
    const mock = stubFetch(json({ connected: true }))
    const store = useIntegrationsStore()

    await store.fetchCwpServerInfo()
    expect(callOf(mock).url).toBe('/api/admin/integrations/cwp/server-info')
    expect(store.cwpServerInfo).toEqual({ connected: true })
    expect(store.cwpServerInfoError).toBeNull()
  })

  it('keeps the API sentence and the previous status on refusal', async () => {
    const store = useIntegrationsStore()
    stubFetch(json({ connected: true }))
    await store.fetchCwpServerInfo()

    stubFetch(problem('Server unreachable.', 502))
    await store.fetchCwpServerInfo()
    expect(store.cwpServerInfoError).toBe('Server unreachable.')
    expect(store.cwpServerInfo).toEqual({ connected: true })
    expect(store.error).toBeNull()
  })

  it('keeps the no-answer sentence offline', async () => {
    stubOffline()
    const store = useIntegrationsStore()

    await store.fetchCwpServerInfo()
    expect(store.cwpServerInfoError).toBe(NO_ANSWER)
  })
})
