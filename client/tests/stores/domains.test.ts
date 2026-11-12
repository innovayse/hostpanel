/**
 * Tests for `stores/domains.ts`, through the real domains, catalogue, billing and client API composables and the transport against a stub server.
 *
 * The store, not the domain pages, owns the in-flight flags and the refusal sentences, so these
 * assertions pin the contract the pages render from. Nothing of our own is replaced: the stub plays the BFF, and
 * "the API was called with X" is an assertion on the request that reached it.
 *
 * @module tests/stores/domains.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { AccountEndpoint } from '../helpers/accountPaths'
import { DOMAIN_ID, DomainAction, domainPath } from '../helpers/domainPanelPaths'
import { bodyOf } from '../helpers/requestBody'
import { readOf, refusal, requestsTo, routeTable } from '../helpers/stubRoutes'
import type { StubRouteTable } from '../helpers/stubRoutes'
import type { StubRequest, StubServer } from '../helpers/stubServer'
import { startTransport } from '../helpers/stubTransport'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { useCatalogStore } from '~/stores/catalog'
import { useDomainsStore } from '~/stores/domains'
import { DomainActionOutcome } from '~/types/domainactionoutcome'
import { DomainAvailabilityStatus } from '~/types/domainavailabilitystatus'
import type { DomainDetail } from '~/types/domaindetail'
import { HttpMethod } from '~/types/httpmethod'
import { ProductType } from '~/types/producttype'
import { emptyContact } from '~/utils/domainContacts'
import { EMPTY_AMOUNT } from '~/utils/formatCurrency'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies the stub gives, by `METHOD /path`; each test fills the ones it needs. */
let table: StubRouteTable

/** The availability check. */
const CHECK_ROUTE = 'POST /api/portal/public/domain-check'

/** The catalogue read. */
const PRODUCTS_ROUTE = readOf('/api/portal/public/products')

/** The placing of a transfer order. */
const TRANSFER_ROUTE = 'POST /api/portal/client/domains/transfer-order'

/**
 * The key of an action on the domain every test opens in a stub route table.
 *
 * @param method - The verb of the action.
 * @param action - The action, or an empty string for the domain itself.
 * @returns The `METHOD /path` key.
 */
const on = (method: HttpMethod, action: Parameters<typeof domainPath>[0]): string => `${method} ${domainPath(action)}`

/** The refusal the registrar answers with. */
const REFUSED = refusal('Refused by the registrar.')

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
    expect(bodyOf(requestsTo(stub.requests, '/api/portal/public/domain-check')[0])).toEqual({ domain: 'a.com' })
    expect(store.searchResult).toMatchObject({ domain: 'a.com' })
    expect(store.searchError).toBe('')
    expect(store.searching).toBe(false)
  })

  it('keeps the API sentence when the availability check is refused', async () => {
    table[CHECK_ROUTE] = REFUSED
    const store = useDomainsStore()

    const outcome = await store.checkAvailability('a.com')

    expect(outcome).toBe(DomainActionOutcome.Failed)
    expect(store.searchError).toBe('Refused by the registrar.')
    expect(store.searchResult).toBeNull()
    expect(store.searching).toBe(false)
  })

  it('returns the domain product id, or null when the catalogue fails', async () => {
    table[PRODUCTS_ROUTE] = { body: [{ id: 1, type: ProductType.SharedHosting }, { id: 7, type: ProductType.Domain }] }
    const store = useDomainsStore()
    expect(await store.fetchDomainProductId()).toBe(7)

    table[PRODUCTS_ROUTE] = REFUSED
    expect(await store.fetchDomainProductId()).toBeNull()
  })

  it('stores the transfer order on success', async () => {
    table[TRANSFER_ROUTE] = { body: { orderId: 1, invoiceId: 2 } }
    const store = useDomainsStore()

    const outcome = await store.placeTransfer({ domain: 'a.com' })

    expect(outcome).toBe(DomainActionOutcome.Done)
    expect(bodyOf(requestsTo(stub.requests, '/api/portal/client/domains/transfer-order')[0])).toEqual({ domain: 'a.com' })
    expect(store.transferResult).toEqual({ orderId: 1, invoiceId: 2 })
    expect(store.transferSaving).toBe(false)
  })

  it('keeps the transfer refusal as an error and sets no result', async () => {
    table[TRANSFER_ROUTE] = REFUSED
    const store = useDomainsStore()

    const outcome = await store.placeTransfer({ domain: 'a.com' })

    expect(outcome).toBe(DomainActionOutcome.Failed)
    expect(store.transferError).toBe('Refused by the registrar.')
    expect(store.transferResult).toBeNull()
  })

  it('stores the renewal order on success and the sentence on refusal', async () => {
    table[on(HttpMethod.Post, DomainAction.RenewOrder)] = { body: { orderId: 3, invoiceId: 4 } }
    const store = useDomainsStore()
    expect(await store.placeRenewal(DOMAIN_ID, 1, 'gw')).toBe(DomainActionOutcome.Done)
    expect(bodyOf(requestsTo(stub.requests, domainPath(DomainAction.RenewOrder))[0])).toEqual({ years: 1, paymentmethod: 'gw' })
    expect(store.renewResult).toEqual({ orderId: 3, invoiceId: 4 })

    table[on(HttpMethod.Post, DomainAction.RenewOrder)] = REFUSED
    expect(await store.placeRenewal(DOMAIN_ID, 1, 'gw')).toBe(DomainActionOutcome.Failed)
    expect(store.renewError).toBe('Refused by the registrar.')
    expect(store.renewSaving).toBe(false)
  })

  it('reports a toggle outcome and clears its flag either way', async () => {
    table[on(HttpMethod.Put, DomainAction.Lock)] = { body: {} }
    const store = useDomainsStore()
    expect(await store.setLock(DOMAIN_ID, true)).toBe(DomainActionOutcome.Done)
    expect(bodyOf(requestsTo(stub.requests, domainPath(DomainAction.Lock))[0])).toEqual({ value: true })
    expect(store.lockSaving).toBe(false)

    table[on(HttpMethod.Put, DomainAction.AutoRenew)] = REFUSED
    expect(await store.setAutoRenew(DOMAIN_ID, true)).toBe(DomainActionOutcome.Failed)
    expect(store.toggleError).toBe('Refused by the registrar.')
    expect(store.autoRenewSaving).toBe(false)

    table[on(HttpMethod.Put, DomainAction.IdProtect)] = { body: {} }
    expect(await store.setIdProtect(DOMAIN_ID, false)).toBe(DomainActionOutcome.Done)
    expect(bodyOf(requestsTo(stub.requests, domainPath(DomainAction.IdProtect))[0])).toEqual({ value: false })
    expect(store.toggleError).toBe('')
  })

  it('saves nameservers and re-reads the domain, keeping the sentence on refusal', async () => {
    const store = useDomainsStore()

    table[on(HttpMethod.Put, DomainAction.Nameservers)] = REFUSED
    expect(await store.saveNameservers(DOMAIN_ID, ['ns1.a.com'])).toBe(DomainActionOutcome.Failed)
    expect(store.nsError).toBe('Refused by the registrar.')
    expect(requestsTo(stub.requests, domainPath(''))).toHaveLength(0)

    const saved = { id: 7, management: { nameservers: [{ id: 1, host: 'ns1.a.com' }] } }
    table[on(HttpMethod.Put, DomainAction.Nameservers)] = { body: {} }
    table[on(HttpMethod.Get, '')] = { body: saved }
    expect(await store.saveNameservers(DOMAIN_ID, ['ns1.a.com'])).toBe(DomainActionOutcome.Done)
    expect(bodyOf(requestsTo(stub.requests, domainPath(DomainAction.Nameservers)).at(-1))).toEqual({ nameservers: ['ns1.a.com'] })
    expect(store.nsError).toBe('')
    expect(store.nsSaving).toBe(false)
    expect(store.domain).toEqual(saved)
  })

  it('still reports a nameserver save as done when only the re-read fails', async () => {
    const store = useDomainsStore()
    const loaded = { id: 7, management: { nameservers: [] } }
    store.domain = loaded as unknown as DomainDetail

    table[on(HttpMethod.Put, DomainAction.Nameservers)] = { body: {} }
    table[on(HttpMethod.Get, '')] = REFUSED
    expect(await store.saveNameservers(DOMAIN_ID, ['ns1.a.com'])).toBe(DomainActionOutcome.Done)
    expect(requestsTo(stub.requests, domainPath(''))).toHaveLength(1)
    expect(store.nsError).toBe('')
    expect(store.nsSaving).toBe(false)
    expect(store.domain).toEqual(loaded)
  })

  it('keeps the EPP code on success and the sentence on refusal', async () => {
    table[on(HttpMethod.Post, DomainAction.Epp)] = { body: { eppCode: 'abc' } }
    const store = useDomainsStore()
    expect(await store.requestEpp(DOMAIN_ID)).toBe(DomainActionOutcome.Done)
    expect(store.eppCode).toBe('abc')

    table[on(HttpMethod.Post, DomainAction.Epp)] = REFUSED
    expect(await store.requestEpp(DOMAIN_ID)).toBe(DomainActionOutcome.Failed)
    expect(store.eppCode).toBe('')
    expect(store.eppError).toBe('Refused by the registrar.')
    expect(store.eppSending).toBe(false)
  })

  it('loads the registrant as form fields, and null when the registrar has none', async () => {
    table[on(HttpMethod.Get, DomainAction.Contact)] = {
      body: {
        firstName: 'Ada', lastName: 'Lovelace', organization: null, email: 'ada@example.com', phone: '+44 1',
        address1: '1 Main St', address2: null, city: 'London', state: 'LDN', postalCode: 'N1', country: 'GB'
      }
    }
    const store = useDomainsStore()
    expect(await store.loadRegistrant(DOMAIN_ID)).toEqual({
      firstName: 'Ada', lastName: 'Lovelace', organization: '', email: 'ada@example.com',
      address1: '1 Main St', address2: '', city: 'London', state: 'LDN',
      postalCode: 'N1', country: 'GB', phone: '+44 1'
    })

    table[on(HttpMethod.Get, DomainAction.Contact)] = REFUSED
    expect(await store.loadRegistrant(DOMAIN_ID)).toBeNull()
  })

  it('loads WHOIS and contacts as null on failure, and saves WHOIS', async () => {
    const record = { registrar: 'Reg', registrant: 'Ada', createdAt: '2026-01-01T00:00:00Z', expiresAt: '2027-01-01T00:00:00Z' }
    table[on(HttpMethod.Get, DomainAction.Whois)] = { body: record }
    const store = useDomainsStore()
    expect(await store.loadWhois(DOMAIN_ID)).toEqual(record)

    table[on(HttpMethod.Get, DomainAction.Whois)] = REFUSED
    expect(await store.loadWhois(DOMAIN_ID)).toBeNull()
    expect(store.whoisLoading).toBe(false)

    table[readOf(AccountEndpoint.Contacts)] = REFUSED
    expect(await store.loadContacts()).toBeNull()

    table[on(HttpMethod.Put, DomainAction.Whois)] = REFUSED
    expect(await store.saveWhois(DOMAIN_ID, emptyContact())).toBe(DomainActionOutcome.Failed)
    expect(bodyOf(requestsTo(stub.requests, domainPath(DomainAction.Whois)).at(-1))).toEqual({ contact: emptyContact() })
    expect(store.whoisError).toBe('Refused by the registrar.')
    expect(store.whoisSaving).toBe(false)
  })

  it('reset clears results and errors', async () => {
    table[TRANSFER_ROUTE] = REFUSED
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
    table[CHECK_ROUTE] = (request: StubRequest) => {
      const { domain } = bodyOf(request) as { domain: string }
      if (domain === 'my-site.net') {
        return { status: 502, body: { detail: 'registrar down' } }
      }
      return { body: { domain, available: domain === 'my-site.com', status: 'x' } }
    }
    const store = withPricing()

    await store.searchOfferedTlds('My Site.xyz')

    expect(store.lookupResults).toEqual([
      { name: 'my-site.com', price: '12.50', status: 'available' },
      { name: 'my-site.net', price: EMPTY_AMOUNT, status: 'unknown' },
      { name: 'my-site.org', price: EMPTY_AMOUNT, status: 'taken' },
      { name: 'my-site.io', price: EMPTY_AMOUNT, status: 'taken' }
    ])
    expect(requestsTo(stub.requests, '/api/portal/public/domain-check')).toHaveLength(4)
    expect(store.lookupPending).toBe(false)
    expect(store.lookupTerm).toBe('My Site.xyz')
  })

  it('does not search for blank text', async () => {
    const store = withPricing()

    await store.searchOfferedTlds('   ')

    expect(stub.requests).toHaveLength(0)
    expect(store.lookupResults).toEqual([])
    expect(store.lookupTerm).toBe('')
  })
})

describe('useDomainsStore detail reads', () => {
  it('keeps the domain and clears the previous one while the next loads', async () => {
    table[on(HttpMethod.Get, '')] = { body: { id: 7, name: 'a.com' } }
    const store = useDomainsStore()

    expect(await store.loadDomain(DOMAIN_ID)).toMatchObject({ name: 'a.com' })
    expect(store.domain).toMatchObject({ id: 7 })
    expect(store.domainLoading).toBe(false)
    expect(store.domainError).toBe('')
  })

  it('keeps the API sentence and no domain when the read is refused', async () => {
    table[on(HttpMethod.Get, '')] = REFUSED
    const store = useDomainsStore()

    expect(await store.loadDomain(DOMAIN_ID)).toBeNull()
    expect(store.domain).toBeNull()
    expect(store.domainError).toBe('Refused by the registrar.')
  })

  it('reads the primary profile once and reuses it', async () => {
    const profile = makeClientUser()
    table[readOf(AccountEndpoint.Me)] = { body: profile }
    const store = useDomainsStore()

    await store.loadPrimaryProfile()
    const again = await store.loadPrimaryProfile()

    expect(again).toEqual(profile)
    expect(requestsTo(stub.requests, AccountEndpoint.Me)).toHaveLength(1)
  })

  it('returns null for the primary profile when it cannot be read', async () => {
    table[readOf(AccountEndpoint.Me)] = REFUSED

    expect(await useDomainsStore().loadPrimaryProfile()).toBeNull()
  })

  it('keeps the gateways, and an empty list when they cannot be read', async () => {
    table[readOf(AccountEndpoint.PaymentMethods)] = { body: [{ module: 'card_gateway', displayName: 'Card gateway', supportsStoredCredentials: false }] }
    const store = useDomainsStore()

    await store.loadPaymentMethods()
    expect(store.paymentMethods).toHaveLength(1)

    table[readOf(AccountEndpoint.PaymentMethods)] = REFUSED
    await store.loadPaymentMethods()
    expect(store.paymentMethods).toEqual([])
    expect(store.paymentMethodsLoading).toBe(false)
  })
})
