/**
 * Tests for `composables/apis/useDomainsApi.ts`.
 *
 * Each endpoint function is called for real through `apiFetch` against a stub server, and the
 * test asserts on the path, verb, query and body that arrived and on how the reply was typed.
 *
 * @module tests/composables/apis/useDomainsApi.test
 */

import { afterEach, describe, expect, it } from 'vitest'
import { expectLastRequest, startTransport } from '../../helpers/stubTransport'
import type { ExpectedRequest } from '../../helpers/stubTransport'
import type { StubServer } from '../../helpers/stubServer'
import { useDomainsApi } from '~/composables/apis/useDomainsApi'
import { emptyContact } from '~/utils/domainContacts'
import { HttpMethod } from '~/types/httpmethod'

/** The stub started by the current test. */
let stub: StubServer | undefined

afterEach(async () => {
  await stub?.close()
  stub = undefined
})

/** The api surface under test. */
const api = useDomainsApi()

/** Every endpoint function with the request it must send. */
const rows: ExpectedRequest[] = [
  { name: 'fetchDomain reads one domain', call: () => api.fetchDomain('4'), method: HttpMethod.Get, url: '/api/portal/client/domains/4' },
  { name: 'setAutoRenew puts the flag', call: () => api.setAutoRenew('4', true), method: HttpMethod.Put, url: '/api/portal/client/domains/4/autorenew', body: { value: true } },
  { name: 'setLock puts the flag', call: () => api.setLock('4', false), method: HttpMethod.Put, url: '/api/portal/client/domains/4/lock', body: { value: false } },
  { name: 'setIdProtect puts the flag', call: () => api.setIdProtect('4', true), method: HttpMethod.Put, url: '/api/portal/client/domains/4/idprotect', body: { value: true } },
  { name: 'setNameservers puts the list', call: () => api.setNameservers('4', ['ns1.a.com', 'ns2.a.com']), method: HttpMethod.Put, url: '/api/portal/client/domains/4/nameservers', body: { nameservers: ['ns1.a.com', 'ns2.a.com'] } },
  { name: 'setWhois puts the registrant contact wrapped in `contact`', call: () => api.setWhois('4', { ...emptyContact(), firstName: 'A' }), method: HttpMethod.Put, url: '/api/portal/client/domains/4/whois', body: { contact: { ...emptyContact(), firstName: 'A' } } },
  { name: 'fetchWhois reads the record', call: () => api.fetchWhois('4'), method: HttpMethod.Get, url: '/api/portal/client/domains/4/whois' },
  { name: 'fetchContact reads the registrant contact', call: () => api.fetchContact('4'), method: HttpMethod.Get, url: '/api/portal/client/domains/4/contact' },
  { name: 'requestEppCode posts without a body', call: () => api.requestEppCode('4'), method: HttpMethod.Post, url: '/api/portal/client/domains/4/epp' },
]

describe('useDomainsApi', () => {
  it.each(rows)('$name', async (row) => {
    stub = await startTransport()

    await row.call()

    expectLastRequest(stub, row)
  })

  it('returns the EPP code the endpoint answers with', async () => {
    stub = await startTransport(() => ({ body: { eppCode: 'abc' } }))

    expect(await api.requestEppCode('4')).toEqual({ eppCode: 'abc' })
  })
})
