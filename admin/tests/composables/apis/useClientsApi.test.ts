/**
 * Tests for the clients API composable: paths, verbs, query strings and bodies through the
 * real `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useClientsApi } from '@/composables/apis/useClientsApi'
import { ApiError } from '@/composables/useApi'
import { ClientStatus } from '@/types/client'
import type { UpdateClientPayload } from '@/types/updateclientpayload'
import { json, problem, stubFetch, callOf } from '../../helpers/fetch'

/** A complete client update, as the PUT endpoint takes it (a full replacement, never a patch). */
const update: UpdateClientPayload = {
  email: null,
  firstName: 'A',
  lastName: 'B',
  companyName: null,
  phone: null,
  address: {},
  preferences: {},
  notifications: { general: true, invoice: true, support: true, product: true, domain: true, affiliate: false },
  settings: {
    overdueNotices: true,
    taxExempt: false,
    separateInvoices: false,
    disableCcProcessing: false,
    marketingOptIn: false,
    statusUpdate: true,
    allowSso: true,
  },
  status: null,
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useClientsApi', () => {
  it('list sends page, size and only the filters that are set', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))

    await useClientsApi().list(2, 20, { search: 'acme', email: '', status: ClientStatus.Active })
    expect(callOf(mock).url).toBe('/api/clients?page=2&pageSize=20&search=acme&status=Active')
  })

  it('get reads one client', async () => {
    const mock = stubFetch(json({ id: 4 }))

    expect(await useClientsApi().get(4)).toEqual({ id: 4 })
    expect(callOf(mock).url).toBe('/api/clients/4')
  })

  it('create POSTs to the admin endpoint and update PUTs the client', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useClientsApi()

    await api.create({ email: 'a@b.c' } as never)
    await api.update(4, update)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/clients/admin', method: 'POST', body: { email: 'a@b.c' } })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/clients/4', method: 'PUT', body: update })
  })

  it('setExternalAccount PUTs the pair, and both null to clear', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useClientsApi()

    await api.setExternalAccount(4, { provider: 'crm', accountId: 'acct-1' })
    await api.setExternalAccount(4, { provider: null, accountId: null })
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/clients/4/external-account', method: 'PUT', body: { provider: 'crm', accountId: 'acct-1' } })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/clients/4/external-account', method: 'PUT', body: { provider: null, accountId: null } })
  })

  it('searchUsers asks the admin users endpoint for the first page', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))

    await useClientsApi().searchUsers('ann', 10)
    expect(callOf(mock).url).toBe('/api/admin/users?search=ann&pageSize=10&page=1')
  })

  it('countries and configuredCurrencies read the reference endpoints', async () => {
    const mock = stubFetch(json([]))
    const api = useClientsApi()

    await api.countries()
    await api.configuredCurrencies()
    expect(callOf(mock, 0).url).toBe('/api/reference/countries')
    expect(callOf(mock, 1).url).toBe('/api/admin/currencies')
  })

  it('contact calls use the client contacts path', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useClientsApi()

    await api.addContact(4, { email: 'c@d.e' })
    await api.updateContact(4, 9, { email: 'f@g.h' })
    await api.removeContact(4, 9)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/clients/4/contacts', method: 'POST', body: { email: 'c@d.e' } })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/clients/4/contacts/9', method: 'PUT', body: { email: 'f@g.h' } })
    expect(callOf(mock, 2)).toMatchObject({ url: '/api/clients/4/contacts/9', method: 'DELETE' })
  })

  it('exportData sends the sections as repeated fields keys', async () => {
    const mock = stubFetch(json({}))

    await useClientsApi().exportData(4, ['profileData', 'notes'])
    expect(callOf(mock).url).toBe('/api/clients/4/export?fields=profileData&fields=notes')
  })

  it('summary, recentServices and recentDomains read the summary data', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))
    const api = useClientsApi()

    await api.summary(4)
    await api.recentServices(4, 5)
    await api.recentDomains(4, 5)
    expect(callOf(mock, 0).url).toBe('/api/clients/4/summary')
    expect(callOf(mock, 1).url).toBe('/api/services?clientId=4&page=1&pageSize=5')
    expect(callOf(mock, 2).url).toBe('/api/domains?clientId=4&page=1&pageSize=5')
  })

  it('rejects with an ApiError carrying the refusal body', async () => {
    stubFetch(problem('Not found.', 404))

    await expect(useClientsApi().get(4)).rejects.toBeInstanceOf(ApiError)
  })
})
