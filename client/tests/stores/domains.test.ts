/**
 * Tests for `stores/domains.ts`, through the real domains, catalogue, billing and client API composables, the real
 * catalogue and currency stores and the transport against a stub server.
 *
 * The store, not the domain pages, owns the in-flight flags and the refusal sentences, so these
 * assertions pin the contract the pages render from. Nothing of our own is replaced: the stub plays the BFF.
 *
 * @module tests/stores/domains.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { requestsTo, routeTable } from '../helpers/stubRoutes'
import type { StubRouteTable } from '../helpers/stubRoutes'
import type { StubReply, StubServer } from '../helpers/stubServer'
import { startTransport } from '../helpers/stubTransport'
import { emptyContact } from '~/utils/domainContacts'
import { DomainActionOutcome } from '~/types/domainactionoutcome'
import type { DomainDetail } from '~/types/domaindetail'
import { DomainAvailabilityStatus } from '~/types/domainavailabilitystatus'
import { HttpMethod } from '~/types/httpmethod'
import { ProductType } from '~/types/producttype'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { useCatalogStore } from '~/stores/catalog'
import { useDomainsStore } from '~/stores/domains'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies the stub gives, by `METHOD /path`; each test fills the ones it needs. */
let table: StubRouteTable

/** The base path of domain 9. */
const DOMAIN_9 = '/api/portal/client/domains/9'

/** The availability check. */
const CHECK_ROUTE = `${HttpMethod.Post} /api/portal/public/domain-check`

/** An API refusal as `apiErrorMessage` reads it. */
const refusal: StubReply = { status: 400, body: { detail: 'Refused by the registrar.' } }

/**
 * The JSON body of a request the stub received.
 *
 * @param path - The path the request went to.
 * @returns The parsed body of the first such request.
 */
const bodyTo = (path: string): unknown => JSON.parse(requestsTo(stub.requests, path)[0]?.body ?? '')

beforeEach(async () => {
  setActivePinia(createPinia())
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('useDomainsStore', () => {
  it('stores the availability answer and clears the flag', async () => {
    table[CHECK_ROUTE] = { body: { domain: 'a.com', available: true, status: DomainAvailabilityStatus.Available } }
    const store = useDomainsStore()

    const outcome = await store.checkAvailability('a.com')

    expect(outcome).toBe(DomainActionOutcome.Done)
    expect(bodyTo('/api/portal/public/domain-check')).toEqual({ domain: 'a.com' })
    expect(store.searchResult).toMatchObject({ domain: 'a.com' })
    expect(store.searchError).toBe('')
    expect(store.searching).toBe(false)
  })

  it('keeps the API sentence when the availability check is refused', async () => {
    table[CHECK_ROUTE] = refusal
    const store = useDomainsStore()

    const outcome = await store.checkAvailability('a.com')

    expect(outcome).toBe(DomainActionOutcome.Failed)
    expect(store.searchError).toBe('Refused by the registrar.')
    expect(store.searchResult).toBeNull()
    expect(store.searching).toBe(false)
  })

  it('returns the domain product id, or null when the catalogue fails', async () => {
    const productsRoute = `${HttpMethod.Get} /api/portal/public/products`
    table[productsRoute] = { body: [{ id: 1, type: ProductType.SharedHosting }, { id: 7, type: ProductType.Domain }] }
    const store = useDomainsStore()
    expect(await store.fetchDomainProductId()).toBe(7)

    table[productsRoute] = refusal
    expect(await store.fetchDomainProductId()).toBeNull()
  })

  it('stores the transfer order on success', async () => {
    table[`${HttpMethod.Post} /api/portal/client/domains/transfer-order`] = { body: { orderId: 1, invoiceId: 2 } }
    const store = useDomainsStore()

    const outcome = await store.placeTransfer({ domain: 'a.com' })

    expect(outcome).toBe(DomainActionOutcome.Done)
    expect(bodyTo('/api/portal/client/domains/transfer-order')).toEqual({ domain: 'a.com' })
    expect(store.transferResult).toEqual({ orderId: 1, invoiceId: 2 })
    expect(store.transferSaving).toBe(false)
  })

  it('keeps the transfer refusal as an error and sets no result', async () => {
    table[`${HttpMethod.Post} /api/portal/client/domains/transfer-order`] = refusal
    const store = useDomainsStore()

    const outcome = await store.placeTransfer({ domain: 'a.com' })

    expect(outcome).toBe(DomainActionOutcome.Failed)
    expect(store.transferError).toBe('Refused by the registrar.')
    expect(store.transferResult).toBeNull()
  })

  it('stores the renewal order on success and the sentence on refusal', async () => {
    const renewRoute = `${HttpMethod.Post} ${DOMAIN_9}/renew-order`
    table[renewRoute] = { body: { orderId: 3, invoiceId: 4 } }
    const store = useDomainsStore()
    expect(await store.placeRenewal('9', 1, 'gw')).toBe(DomainActionOutcome.Done)
    expect(bodyTo(`${DOMAIN_9}/renew-order`)).toEqual({ years: 1, paymentmethod: 'gw' })
    expect(store.renewResult).toEqual({ orderId: 3, invoiceId: 4 })

    table[renewRoute] = refusal
    expect(await store.placeRenewal('9', 1, 'gw')).toBe(DomainActionOutcome.Failed)
    expect(store.renewError).toBe('Refused by the registrar.')
    expect(store.renewSaving).toBe(false)
  })

  it('reports a toggle outcome and clears its flag either way', async () => {
    table[`${HttpMethod.Put} ${DOMAIN_9}/lock`] = { body: {} }
    const store = useDomainsStore()
    expect(await store.setLock('9', true)).toBe(DomainActionOutcome.Done)
    expect(bodyTo(`${DOMAIN_9}/lock`)).toEqual({ value: true })
    expect(store.lockSaving).toBe(false)

    table[`${HttpMethod.Put} ${DOMAIN_9}/autorenew`] = refusal
    expect(await store.setAutoRenew('9', true)).toBe(DomainActionOutcome.Failed)
    expect(store.toggleError).toBe('Refused by the registrar.')
    expect(store.autoRenewSaving).toBe(false)

    table[`${HttpMethod.Put} ${DOMAIN_9}/idprotect`] = { body: {} }
    expect(await store.setIdProtect('9', false)).toBe(DomainActionOutcome.Done)
    expect(bodyTo(`${DOMAIN_9}/idprotect`)).toEqual({ value: false })
    expect(store.toggleError).toBe('')
  })

  it('saves nameservers and re-reads the domain, keeping the sentence on refusal', async () => {
    const store = useDomainsStore()

    table[`${HttpMethod.Put} ${DOMAIN_9}/nameservers`] = refusal
    expect(await store.saveNameservers('9', ['ns1.a.com'])).toBe(DomainActionOutcome.Failed)
    expect(store.nsError).toBe('Refused by the registrar.')
    expect(requestsTo(stub.requests, DOMAIN_9)).toHaveLength(0)

    const saved = { id: 9, management: { nameservers: [{ id: 1, host: 'ns1.a.com' }] } }
    table[`${HttpMethod.Put} ${DOMAIN_9}/nameservers`] = { body: {} }
    table[`${HttpMethod.Get} ${DOMAIN_9}`] = { body: saved }
    expect(await store.saveNameservers('9', ['ns1.a.com'])).toBe(DomainActionOutcome.Done)
    expect(bodyTo(`${DOMAIN_9}/nameservers`)).toEqual({ nameservers: ['ns1.a.com'] })
    expect(store.nsError).toBe('')
    expect(store.nsSaving).toBe(false)
    expect(store.domain).toEqual(saved)
  })

  it('still reports a nameserver save as done when only the re-read fails', async () => {
    const store = useDomainsStore()
    const loaded = { id: 9, management: { nameservers: [] } }
    store.domain = loaded as unknown as DomainDetail

    table[`${HttpMethod.Put} ${DOMAIN_9}/nameservers`] = { body: {} }
    table[`${HttpMethod.Get} ${DOMAIN_9}`] = refusal
    expect(await store.saveNameservers('9', ['ns1.a.com'])).toBe(DomainActionOutcome.Done)
    expect(store.nsError).toBe('')
    expect(store.nsSaving).toBe(false)
    expect(store.domain).toEqual(loaded)
  })

  it('keeps the EPP code on success and the sentence on refusal', async () => {
    const eppRoute = `${HttpMethod.Post} ${DOMAIN_9}/epp`
    table[eppRoute] = { body: { eppCode: 'abc' } }
    const store = useDomainsStore()
    expect(await store.requestEpp('9')).toBe(DomainActionOutcome.Done)
    expect(store.eppCode).toBe('abc')

    table[eppRoute] = refusal
    expect(await store.requestEpp('9')).toBe(DomainActionOutcome.Failed)
    expect(store.eppCode).toBe('')
    expect(store.eppError).toBe('Refused by the registrar.')
    expect(store.eppSending).toBe(false)
  })

  it('loads the registrant as form fields, and null when the registrar has none', async () => {
    const contactRoute = `${HttpMethod.Get} ${DOMAIN_9}/contact`
    table[contactRoute] = {
      body: {
        firstName: 'Ada', lastName: 'Lovelace', organization: null, email: 'ada@example.com', phone: '+44 1',
        address1: '1 Main St', address2: null, city: 'London', state: 'LDN', postalCode: 'N1', country: 'GB'
      }
    }
    const store = useDomainsStore()
    expect(await store.loadRegistrant('9')).toEqual({
      firstName: 'Ada', lastName: 'Lovelace', organization: '', email: 'ada@example.com',
      address1: '1 Main St', address2: '', city: 'London', state: 'LDN',
      postalCode: 'N1', country: 'GB', phone: '+44 1'
    })

    table[contactRoute] = refusal
    expect(await store.loadRegistrant('9')).toBeNull()
  })

  it('loads WHOIS and contacts as null on failure, and saves WHOIS', async () => {
    const whoisRoute = `${HttpMethod.Get} ${DOMAIN_9}/whois`
    const record = { registrar: 'Reg', registrant: 'Ada', createdAt: '2026-01-01T00:00:00Z', expiresAt: '2027-01-01T00:00:00Z' }
    table[whoisRoute] = { body: record }
    const store = useDomainsStore()
    expect(await store.loadWhois('9')).toEqual(record)

    table[whoisRoute] = refusal
    expect(await store.loadWhois('9')).toBeNull()
    expect(store.whoisLoading).toBe(false)

    table[`${HttpMethod.Get} /api/portal/client/contacts`] = refusal
    expect(await store.loadContacts()).toBeNull()

    table[`${HttpMethod.Put} ${DOMAIN_9}/whois`] = refusal
    expect(await store.saveWhois('9', emptyContact())).toBe(DomainActionOutcome.Failed)
    expect(store.whoisError).toBe('Refused by the registrar.')
    expect(store.whoisSaving).toBe(false)
  })

  it('reset clears results and errors', async () => {
    table[`${HttpMethod.Post} /api/portal/client/domains/transfer-order`] = refusal
    const store = useDomainsStore()
    await store.placeTransfer({})

    store.reset()

    expect(store.transferError).toBe('')
    expect(store.transferResult).toBeNull()
  })
})

describe('useDomainsStore storefront lookup', () => {
  /**
   * Loads a TLD price table into the catalogue store the lookup reads.
   *
   * @returns The domains store.
   */
  const withPricing = () => {
    useCatalogStore().tldPricing = {
      currency: { code: 'USD', prefix: '$' },
      pricing: [
        { tld: 'com', sellCurrency: 'USD', register: [{ years: 1, amount: 12.5 }], categories: ['popular'] },
        { tld: 'net', sellCurrency: 'USD', register: [{ years: 1, amount: 10 }], renew: [{ years: 1, amount: 11 }], categories: ['popular', 'tech'] },
        { tld: 'org', sellCurrency: 'USD', register: [{ years: 1, amount: 9 }], categories: [] },
        { tld: 'io', sellCurrency: 'USD', register: [{ years: 1, amount: 40 }], categories: ['tech'] },
        { tld: 'am', sellCurrency: 'AMD', register: [{ years: 1, amount: 25 }] }
      ]
    }
    return useDomainsStore()
  }

  it('turns the price table into rows with dotted extensions and one-year prices', () => {
    const store = withPricing()

    expect(store.priceRows[0]).toMatchObject({ tld: '.com', register: '12.50', renew: '12.50', transfer: '12.50', registerAmount: 12.5, sellCurrency: 'USD' })
    expect(store.priceRows[1]).toMatchObject({ tld: '.net', renew: '11.00' })
    expect(store.currencyCode).toBe('USD')
  })

  it('collects the categories behind the all sentinel, sorted', () => {
    const store = withPricing()

    expect(store.categories).toEqual(['__all__', 'popular', 'tech'])
  })

  it('offers only the first four extensions', () => {
    const store = withPricing()

    expect(store.offeredTlds.map(r => r.tld)).toEqual(['.com', '.net', '.org', '.io'])
  })

  it('checks a name against every offered extension, marking a failed check unknown', async () => {
    /**
     * Answers each name the way the registrar would: one available, one unreachable, the rest taken.
     *
     * @param request - The availability check.
     * @returns The reply.
     */
    table[CHECK_ROUTE] = (request) => {
      const name = (JSON.parse(request.body) as { domain: string }).domain
      if (name === 'my-site.net') {
        return { status: 502 }
      }
      return { body: { domain: name, available: name === 'my-site.com', status: 'x' } }
    }
    const store = withPricing()

    await store.searchOfferedTlds('My Site.xyz')

    expect(store.lookupResults).toEqual([
      { name: 'my-site.com', price: '12.50', status: 'available' },
      { name: 'my-site.net', price: '—', status: 'unknown' },
      { name: 'my-site.org', price: '—', status: 'taken' },
      { name: 'my-site.io', price: '—', status: 'taken' }
    ])
    expect(store.lookupPending).toBe(false)
    expect(store.lookupTerm).toBe('My Site.xyz')
  })

  it('does not search for blank text', async () => {
    const store = withPricing()

    await store.searchOfferedTlds('   ')

    expect(requestsTo(stub.requests, '/api/portal/public/domain-check')).toHaveLength(0)
    expect(store.lookupResults).toEqual([])
    expect(store.lookupTerm).toBe('')
  })
})

describe('useDomainsStore detail reads', () => {
  it('keeps the domain and clears the previous one while the next loads', async () => {
    table[`${HttpMethod.Get} /api/portal/client/domains/4`] = { body: { id: 4, name: 'a.com' } }
    const store = useDomainsStore()

    expect(await store.loadDomain('4')).toMatchObject({ name: 'a.com' })
    expect(store.domain).toMatchObject({ id: 4 })
    expect(store.domainLoading).toBe(false)
    expect(store.domainError).toBe('')
  })

  it('keeps the API sentence and no domain when the read is refused', async () => {
    table[`${HttpMethod.Get} /api/portal/client/domains/4`] = refusal
    const store = useDomainsStore()

    expect(await store.loadDomain('4')).toBeNull()
    expect(store.domain).toBeNull()
    expect(store.domainError).toBe('Refused by the registrar.')
  })

  it('reads the primary profile once and reuses it', async () => {
    const profile = makeClientUser()
    table[`${HttpMethod.Get} /api/portal/client/me`] = { body: profile }
    const store = useDomainsStore()

    await store.loadPrimaryProfile()
    const again = await store.loadPrimaryProfile()

    expect(again).toEqual(profile)
    expect(requestsTo(stub.requests, '/api/portal/client/me')).toHaveLength(1)
  })

  it('returns null for the primary profile when it cannot be read', async () => {
    table[`${HttpMethod.Get} /api/portal/client/me`] = refusal

    expect(await useDomainsStore().loadPrimaryProfile()).toBeNull()
  })

  it('keeps the gateways, and an empty list when they cannot be read', async () => {
    const methodsRoute = `${HttpMethod.Get} /api/portal/order/payment-methods`
    table[methodsRoute] = { body: [{ module: 'card_gateway', displayName: 'Card gateway' }] }
    const store = useDomainsStore()

    await store.loadPaymentMethods()
    expect(store.paymentMethods).toHaveLength(1)

    table[methodsRoute] = refusal
    await store.loadPaymentMethods()
    expect(store.paymentMethods).toEqual([])
    expect(store.paymentMethodsLoading).toBe(false)
  })
})
