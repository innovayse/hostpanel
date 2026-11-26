/**
 * Tests for the registrar domains API composable: paths, verbs, query string and body through the real `useApi` transport
 * with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useRegistrarDomainsApi } from '@/composables/apis/useRegistrarDomainsApi'
import { json, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useRegistrarDomainsApi', () => {
  it('listDomains reads the registrar page, sending the cursor only when there is one', async () => {
    const mock = stubFetch(json({ domains: [], nextCursor: null }))
    const api = useRegistrarDomainsApi()

    await api.listDomains('registrar-one', null)
    await api.listDomains('registrar-one', 'page 2/x')

    expect(callOf(mock, 0).url).toBe('/api/domains/registrar/registrar-one')
    expect(callOf(mock, 1).url).toBe('/api/domains/registrar/registrar-one?cursor=page+2%2Fx')
  })

  it('listDomains answers the page as the API sends it', async () => {
    const page = { domains: [{ name: 'example.com', expiresAt: '2027-01-01T00:00:00Z', inPanel: false }], nextCursor: 'c-2' }
    stubFetch(json(page))

    expect(await useRegistrarDomainsApi().listDomains('registrar-one', null)).toEqual(page)
  })

  it('importDomain POSTs the client and the name to the registrar import route', async () => {
    const mock = stubFetch(new Response(null, { status: 201 }))

    await useRegistrarDomainsApi().importDomain('registrar-one', { clientId: 7, domainName: 'example.com' })

    expect(callOf(mock)).toMatchObject({
      url: '/api/domains/registrar/registrar-one/import',
      method: 'POST',
      body: { clientId: 7, domainName: 'example.com' },
    })
  })

  it('encodes the registrar id in the path', async () => {
    const mock = stubFetch(json({ domains: [], nextCursor: null }))

    await useRegistrarDomainsApi().listDomains('a b', null)

    expect(callOf(mock).url).toBe('/api/domains/registrar/a%20b')
  })
})
