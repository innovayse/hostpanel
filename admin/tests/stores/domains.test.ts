/**
 * Tests for the domains store loads and typed writes: a load puts the API's sentence into
 * `error`, a mutation into `actionError`, a refused save exposes the field messages, and the
 * request bodies match what the API expects. The requests go through the real `useApi` transport.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { translate } from '@/lib/translate'
import { useDomainsStore } from '@/stores/domains'
import { DomainStatus } from '@/types/domain'
import { json, problem, stubFetch, stubOffline, callOf, invalid } from '../helpers/fetch'

describe('domainsStore loads', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetchAll sends the trimmed search and fills the list', async () => {
    const mock = stubFetch(json({ items: [{ id: 1 }], totalCount: 31 }))
    const store = useDomainsStore()
    store.search = '  exa  '

    expect(await store.fetchAll()).toBe(true)
    expect(callOf(mock).url).toBe('/api/domains?page=1&pageSize=20&search=exa')
    expect(store.domains).toEqual([{ id: 1 }])
    expect(store.totalCount).toBe(31)
    expect(store.loading).toBe(false)
  })

  it('fetchAll reports the API sentence, and the no-answer sentence offline', async () => {
    const store = useDomainsStore()
    stubFetch(problem('Not allowed.', 403))
    expect(await store.fetchAll()).toBe(false)
    expect(store.error).toBe('Not allowed.')

    stubOffline()
    expect(await store.fetchAll()).toBe(false)
    expect(store.error).toBe(translate('errors.noAnswer'))
    expect(store.loading).toBe(false)
  })

  it('fetchByClient fills clientDomains, not the shared list', async () => {
    const mock = stubFetch(json({ items: [{ id: 2 }], totalCount: 1 }))
    const store = useDomainsStore()

    expect(await store.fetchByClient(7)).toBe(true)
    expect(callOf(mock).url).toBe('/api/domains?clientId=7&page=1&pageSize=20')
    expect(store.clientDomains).toEqual([{ id: 2 }])
    expect(store.domains).toEqual([])
  })

  it('fetchById stores the detail and reports a refusal', async () => {
    const store = useDomainsStore()
    stubFetch(json({ id: 3 }))
    expect(await store.fetchById(3)).toBe(true)
    expect(store.current).toEqual({ id: 3 })

    stubFetch(problem('No such domain.', 404))
    expect(await store.fetchById(4)).toBe(false)
    expect(store.error).toBe('No such domain.')
  })
})

describe('domainsStore writes', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('a refused save returns false with the sentence and the field messages', async () => {
    const store = useDomainsStore()
    stubFetch(invalid('Check the form.', { RegistrationPeriod: ['Must be at least 1.'] }))

    expect(await store.update(3, {
      firstPaymentAmount: 0, recurringAmount: 0, paymentMethod: null, promotionCode: null,
      subscriptionId: null, adminNotes: null, expiresAt: null, nextDueDate: null,
      registrationPeriod: 0, status: DomainStatus.Active, nameservers: [],
    })).toBe(false)
    expect(store.actionError).toBe('Check the form.')
    expect(store.fieldErrors).toEqual({ registrationPeriod: 'Must be at least 1.' })

    stubFetch(new Response(null, { status: 204 }))
    expect(await store.register(3)).toBe(true)
    expect(store.actionError).toBeNull()
    expect(store.fieldErrors).toEqual({})
  })

  it('switches send { enabled } and rule calls send their bodies', async () => {
    const store = useDomainsStore()
    const mock = stubFetch(new Response(null, { status: 204 }))

    expect(await store.setAutoRenew(3, true)).toBe(true)
    expect(await store.addForwardingRule(3, 'info', 'me@example.com')).toBe(true)
    expect(await store.addDnsRecord(3, { type: 'A', host: '@', value: '1.2.3.4', ttl: 3600, priority: null })).toBe(true)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/domains/3/auto-renew', method: 'PUT', body: { value: true } })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/domains/3/email-forwarding', body: { source: 'info', destination: 'me@example.com' } })
    expect(callOf(mock, 2)).toMatchObject({ url: '/api/domains/3/dns', method: 'POST' })
  })

  it('getEppCode returns the code, null with actionError when refused', async () => {
    const store = useDomainsStore()
    stubFetch(json({ eppCode: 'abc123' }))
    expect(await store.getEppCode(3)).toBe('abc123')

    stubFetch(problem('Domain is locked.', 409))
    expect(await store.getEppCode(3)).toBeNull()
    expect(store.actionError).toBe('Domain is locked.')
  })
})

describe('domainsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetchClientPage returns the page without touching the shared list', async () => {
    stubFetch(json({ items: [{ id: 1 }], totalCount: 1 }))
    const store = useDomainsStore()

    expect(await store.fetchClientPage(7, 1, 20)).toEqual({ items: [{ id: 1 }], totalCount: 1 })
    expect(store.domains).toEqual([])
    expect(store.loading).toBe(false)
  })

  it('fetchClientPage returns null and sets error on refusal', async () => {
    stubFetch(problem('No access.', 403))
    const store = useDomainsStore()

    expect(await store.fetchClientPage(7, 1, 20)).toBeNull()
    expect(store.error).toBe('No access.')
  })

  it('mutations send the verb and body, and report the outcome', async () => {
    const mock = stubFetch(json({}))
    const store = useDomainsStore()

    expect(await store.renew(3, 2)).toBe(true)
    expect(mock.mock.calls[0]![0]).toBe('/api/domains/3/renew')
    expect(JSON.parse(mock.mock.calls[0]![1].body as string)).toEqual({ years: 2 })
    expect(await store.setLock(3, true)).toBe(true)
    expect(await store.deleteDnsRecord(3, 9)).toBe(true)
  })

  it('a refused mutation sets actionError, and the next success clears it', async () => {
    const store = useDomainsStore()
    stubFetch(problem('Registrar is down.', 502))
    expect(await store.register(3)).toBe(false)
    expect(store.actionError).toBe('Registrar is down.')

    stubFetch(json({}))
    expect(await store.register(3)).toBe(true)
    expect(store.actionError).toBeNull()
  })

  it('getEppCode returns the code, null on failure', async () => {
    const store = useDomainsStore()
    stubFetch(json({ eppCode: 'ABC123' }))
    expect(await store.getEppCode(3)).toBe('ABC123')

    stubOffline()
    expect(await store.getEppCode(3)).toBeNull()
    expect(store.actionError).toBe('Could not reach the server. Check your connection and try again.')
  })
})
