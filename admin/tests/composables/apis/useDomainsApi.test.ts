/**
 * Tests for the domains API composable: paths, verbs, query strings and bodies through the real
 * `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useDomainsApi } from '@/composables/apis/useDomainsApi'
import { DomainStatus } from '@/types/domain'
import { json, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

/** A valid DNS record body. */
const record = { type: 'MX', host: '@', value: 'mail.example.com', ttl: 3600, priority: 10 }

describe('useDomainsApi', () => {
  it('listDomains sends the client, paging and search, dropping empties', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))
    const api = useDomainsApi()

    await api.listDomains({ clientId: 7, page: 2, pageSize: 20 })
    await api.listDomains({ page: 1, pageSize: 20, search: 'exa' })
    await api.listDomains({ page: 1, pageSize: 20, search: '' })
    expect(callOf(mock, 0).url).toBe('/api/domains?clientId=7&page=2&pageSize=20')
    expect(callOf(mock, 1).url).toBe('/api/domains?page=1&pageSize=20&search=exa')
    expect(callOf(mock, 2).url).toBe('/api/domains?page=1&pageSize=20')
  })

  it('getDomain reads the detail and updateDomain PUTs the fields', async () => {
    const mock = stubFetch(json({ id: 3 }))
    const api = useDomainsApi()
    const fields = {
      firstPaymentAmount: 10, recurringAmount: 12, paymentMethod: null, promotionCode: null,
      subscriptionId: null, adminNotes: null, expiresAt: null, nextDueDate: null,
      registrationPeriod: 1, status: DomainStatus.Active, nameservers: ['ns1.example.com'],
    }

    expect(await api.getDomain(3)).toEqual({ id: 3 })
    await api.updateDomain(3, fields)
    expect(callOf(mock, 0).url).toBe('/api/domains/3')
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/domains/3', method: 'PUT', body: fields })
  })

  it('registrar commands POST and the switches PUT { enabled }', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useDomainsApi()

    await api.registerDomain(3)
    await api.renewDomain(3, 2)
    await api.setAutoRenew(3, { enabled: true })
    await api.setWhoisPrivacy(3, { enabled: false })
    await api.setLock(3, { enabled: true })
    await api.setDnsManagement(3, { enabled: true })
    await api.setEmailForwarding(3, { enabled: false })
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/domains/3/register', method: 'POST' })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/domains/3/renew', method: 'POST', body: { years: 2 } })
    expect(callOf(mock, 2)).toMatchObject({ url: '/api/domains/3/auto-renew', method: 'PUT', body: { value: true } })
    expect(callOf(mock, 3)).toMatchObject({ url: '/api/domains/3/whois-privacy', method: 'PUT', body: { enabled: false } })
    expect(callOf(mock, 4)).toMatchObject({ url: '/api/domains/3/lock', method: 'PUT', body: { enabled: true } })
    expect(callOf(mock, 5)).toMatchObject({ url: '/api/domains/3/dns-management', method: 'PUT', body: { enabled: true } })
    expect(callOf(mock, 6)).toMatchObject({ url: '/api/domains/3/email-forwarding-toggle', method: 'PUT', body: { enabled: false } })
  })

  it('setAutoRenew sends exactly { value }, the field the API binds, never { enabled }', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useDomainsApi()
    await api.setAutoRenew(3, { enabled: true })
    await api.setAutoRenew(3, { enabled: false })
    expect(callOf(mock, 0).body).toEqual({ value: true })
    expect(callOf(mock, 1).body).toEqual({ value: false })
  })

  it('modifyContact POSTs the contact and initiateOutgoingTransfer returns the code', async () => {
    const mock = stubFetch(json({ eppCode: 'abc123' }))
    const api = useDomainsApi()

    await api.modifyContact(3, { firstName: 'Ann' })
    expect(await api.initiateOutgoingTransfer(3)).toEqual({ eppCode: 'abc123' })
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/domains/3/modify-contact', method: 'POST', body: { firstName: 'Ann' } })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/domains/3/initiate-outgoing-transfer', method: 'POST' })
  })

  it('DNS record calls hit /dns', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useDomainsApi()

    await api.addDnsRecord(3, record)
    await api.updateDnsRecord(3, 9, record)
    await api.deleteDnsRecord(3, 9)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/domains/3/dns', method: 'POST', body: record })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/domains/3/dns/9', method: 'PUT', body: record })
    expect(callOf(mock, 2)).toMatchObject({ url: '/api/domains/3/dns/9', method: 'DELETE' })
  })

  it('forwarding rule calls hit /email-forwarding', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useDomainsApi()
    const rule = { source: 'info', destination: 'me@example.com' }

    await api.addForwardingRule(3, rule)
    await api.updateForwardingRule(3, 4, { ...rule, isActive: false })
    await api.deleteForwardingRule(3, 4)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/domains/3/email-forwarding', method: 'POST', body: rule })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/domains/3/email-forwarding/4', method: 'PUT', body: { ...rule, isActive: false } })
    expect(callOf(mock, 2)).toMatchObject({ url: '/api/domains/3/email-forwarding/4', method: 'DELETE' })
  })
})
