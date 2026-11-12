/**
 * Tests for `stores/domains.ts`.
 *
 * The store, not the domain pages, owns the in-flight flags and the refusal sentences, so these
 * assertions pin the contract the pages render from.
 *
 * @module tests/stores/domains.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { ref } from 'vue'
import { emptyContact } from '~/utils/domainContacts'
import { DomainAvailabilityStatus } from '~/types/domainavailabilitystatus'
import { ProductType } from '~/types/producttype'
import { makeClientUser } from '~/tests/helpers/clientUser'
import type { DomainDetail } from '~/types/domaindetail'

/** The mocked domains endpoints, re-created for every test. */
const domainsApi = {
  createTransferOrder: vi.fn(),
  createRenewOrder: vi.fn(),
  setAutoRenew: vi.fn(),
  setLock: vi.fn(),
  setIdProtect: vi.fn(),
  setNameservers: vi.fn(),
  requestEppCode: vi.fn(),
  fetchWhois: vi.fn(),
  fetchContact: vi.fn(),
  setWhois: vi.fn(),
  fetchDomain: vi.fn()
}

/** The mocked catalogue endpoints. */
const catalogApi = { checkDomain: vi.fn(), fetchProducts: vi.fn() }

/** The mocked billing endpoints. */
const billingApi = { fetchGatewayMethods: vi.fn() }

/** The mocked client endpoints. */
const clientApi = { fetchContacts: vi.fn(), fetchMeOnce: vi.fn() }

vi.stubGlobal('ref', ref)

vi.mock('~/composables/apis/useDomainsApi', () => ({
  /**
   * Stands in for the domains API composable.
   *
   * @returns The mocked endpoints.
   */
  useDomainsApi: () => domainsApi
}))
vi.mock('~/composables/apis/useCatalogApi', () => ({
  /**
   * Stands in for the catalogue API composable.
   *
   * @returns The mocked endpoints.
   */
  useCatalogApi: () => catalogApi
}))
vi.mock('~/composables/apis/useBillingApi', () => ({
  /**
   * Stands in for the billing API composable.
   *
   * @returns The mocked endpoints.
   */
  useBillingApi: () => billingApi
}))
vi.mock('~/composables/apis/useClientApi', () => ({
  /**
   * Stands in for the client API composable.
   *
   * @returns The mocked endpoints.
   */
  useClientApi: () => clientApi
}))

const { useDomainsStore } = await import('~/stores/domains')
const { DomainActionOutcome } = await import('~/types/domainactionoutcome')

/** An API refusal as `apiErrorMessage` reads it. */
const refusal = { data: { detail: 'Refused by the registrar.' } }

describe('useDomainsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    for (const api of [domainsApi, catalogApi, billingApi, clientApi]) {
      for (const fn of Object.values(api)) {
        fn.mockReset()
      }
    }
  })

  it('stores the availability answer and clears the flag', async () => {
    catalogApi.checkDomain.mockResolvedValue({ domain: 'a.com', available: true, status: DomainAvailabilityStatus.Available })
    const store = useDomainsStore()

    const outcome = await store.checkAvailability('a.com')

    expect(outcome).toBe(DomainActionOutcome.Done)
    expect(store.searchResult).toMatchObject({ domain: 'a.com' })
    expect(store.searchError).toBe('')
    expect(store.searching).toBe(false)
  })

  it('keeps the API sentence when the availability check is refused', async () => {
    catalogApi.checkDomain.mockRejectedValue(refusal)
    const store = useDomainsStore()

    const outcome = await store.checkAvailability('a.com')

    expect(outcome).toBe(DomainActionOutcome.Failed)
    expect(store.searchError).toBe('Refused by the registrar.')
    expect(store.searchResult).toBeNull()
    expect(store.searching).toBe(false)
  })

  it('returns the domain product id, or null when the catalogue fails', async () => {
    catalogApi.fetchProducts.mockResolvedValueOnce([{ id: 1, type: ProductType.SharedHosting }, { id: 7, type: ProductType.Domain }])
    const store = useDomainsStore()
    expect(await store.fetchDomainProductId()).toBe(7)

    catalogApi.fetchProducts.mockRejectedValueOnce(refusal)
    expect(await store.fetchDomainProductId()).toBeNull()
  })

  it('stores the transfer order on success', async () => {
    domainsApi.createTransferOrder.mockResolvedValue({ orderId: 1, invoiceId: 2 })
    const store = useDomainsStore()

    const outcome = await store.placeTransfer({ domain: 'a.com' })

    expect(outcome).toBe(DomainActionOutcome.Done)
    expect(store.transferResult).toEqual({ orderId: 1, invoiceId: 2 })
    expect(store.transferSaving).toBe(false)
  })

  it('keeps the transfer refusal as an error and sets no result', async () => {
    domainsApi.createTransferOrder.mockRejectedValue(refusal)
    const store = useDomainsStore()

    const outcome = await store.placeTransfer({ domain: 'a.com' })

    expect(outcome).toBe(DomainActionOutcome.Failed)
    expect(store.transferError).toBe('Refused by the registrar.')
    expect(store.transferResult).toBeNull()
  })

  it('stores the renewal order on success and the sentence on refusal', async () => {
    domainsApi.createRenewOrder.mockResolvedValueOnce({ orderId: 3, invoiceId: 4 })
    const store = useDomainsStore()
    expect(await store.placeRenewal('9', 1, 'gw')).toBe(DomainActionOutcome.Done)
    expect(store.renewResult).toEqual({ orderId: 3, invoiceId: 4 })

    domainsApi.createRenewOrder.mockRejectedValueOnce(refusal)
    expect(await store.placeRenewal('9', 1, 'gw')).toBe(DomainActionOutcome.Failed)
    expect(store.renewError).toBe('Refused by the registrar.')
    expect(store.renewSaving).toBe(false)
  })

  it('reports a toggle outcome and clears its flag either way', async () => {
    domainsApi.setLock.mockResolvedValueOnce(undefined)
    const store = useDomainsStore()
    expect(await store.setLock('9', true)).toBe(DomainActionOutcome.Done)
    expect(store.lockSaving).toBe(false)

    domainsApi.setAutoRenew.mockRejectedValueOnce(refusal)
    expect(await store.setAutoRenew('9', true)).toBe(DomainActionOutcome.Failed)
    expect(store.toggleError).toBe('Refused by the registrar.')
    expect(store.autoRenewSaving).toBe(false)

    domainsApi.setIdProtect.mockResolvedValueOnce(undefined)
    expect(await store.setIdProtect('9', false)).toBe(DomainActionOutcome.Done)
    expect(store.toggleError).toBe('')
  })

  it('saves nameservers and re-reads the domain, keeping the sentence on refusal', async () => {
    const store = useDomainsStore()

    domainsApi.setNameservers.mockRejectedValueOnce(refusal)
    expect(await store.saveNameservers('9', ['ns1.a.com'])).toBe(DomainActionOutcome.Failed)
    expect(store.nsError).toBe('Refused by the registrar.')
    expect(domainsApi.fetchDomain).not.toHaveBeenCalled()

    const saved = { id: 9, management: { nameservers: [{ id: 1, host: 'ns1.a.com' }] } }
    domainsApi.setNameservers.mockResolvedValueOnce(undefined)
    domainsApi.fetchDomain.mockResolvedValueOnce(saved)
    expect(await store.saveNameservers('9', ['ns1.a.com'])).toBe(DomainActionOutcome.Done)
    expect(store.nsError).toBe('')
    expect(store.nsSaving).toBe(false)
    expect(store.domain).toEqual(saved)
  })

  it('still reports a nameserver save as done when only the re-read fails', async () => {
    const store = useDomainsStore()
    const loaded = { id: 9, management: { nameservers: [] } }
    store.domain = loaded as unknown as DomainDetail

    domainsApi.setNameservers.mockResolvedValueOnce(undefined)
    domainsApi.fetchDomain.mockRejectedValueOnce(refusal)
    expect(await store.saveNameservers('9', ['ns1.a.com'])).toBe(DomainActionOutcome.Done)
    expect(store.nsError).toBe('')
    expect(store.nsSaving).toBe(false)
    expect(store.domain).toEqual(loaded)
  })

  it('keeps the EPP code on success and the sentence on refusal', async () => {
    domainsApi.requestEppCode.mockResolvedValueOnce({ eppCode: 'abc' })
    const store = useDomainsStore()
    expect(await store.requestEpp('9')).toBe(DomainActionOutcome.Done)
    expect(store.eppCode).toBe('abc')

    domainsApi.requestEppCode.mockRejectedValueOnce(refusal)
    expect(await store.requestEpp('9')).toBe(DomainActionOutcome.Failed)
    expect(store.eppCode).toBe('')
    expect(store.eppError).toBe('Refused by the registrar.')
    expect(store.eppSending).toBe(false)
  })

  it('loads the registrant as form fields, and null when the registrar has none', async () => {
    domainsApi.fetchContact.mockResolvedValueOnce({
      firstName: 'Ada', lastName: 'Lovelace', organization: null, email: 'ada@example.com', phone: '+44 1',
      address1: '1 Main St', address2: null, city: 'London', state: 'LDN', postalCode: 'N1', country: 'GB'
    })
    const store = useDomainsStore()
    expect(await store.loadRegistrant('9')).toEqual({
      firstName: 'Ada', lastName: 'Lovelace', organization: '', email: 'ada@example.com',
      address1: '1 Main St', address2: '', city: 'London', state: 'LDN',
      postalCode: 'N1', country: 'GB', phone: '+44 1'
    })

    domainsApi.fetchContact.mockRejectedValueOnce(refusal)
    expect(await store.loadRegistrant('9')).toBeNull()
  })

  it('loads WHOIS and contacts as null on failure, and saves WHOIS', async () => {
    domainsApi.fetchWhois.mockResolvedValueOnce({ registrar: 'Reg', registrant: 'Ada', createdAt: '2026-01-01T00:00:00Z', expiresAt: '2027-01-01T00:00:00Z' })
    const store = useDomainsStore()
    expect(await store.loadWhois('9')).toEqual({ registrar: 'Reg', registrant: 'Ada', createdAt: '2026-01-01T00:00:00Z', expiresAt: '2027-01-01T00:00:00Z' })

    domainsApi.fetchWhois.mockRejectedValueOnce(refusal)
    expect(await store.loadWhois('9')).toBeNull()
    expect(store.whoisLoading).toBe(false)

    clientApi.fetchContacts.mockRejectedValueOnce(refusal)
    expect(await store.loadContacts()).toBeNull()

    domainsApi.setWhois.mockRejectedValueOnce(refusal)
    expect(await store.saveWhois('9', emptyContact())).toBe(DomainActionOutcome.Failed)
    expect(store.whoisError).toBe('Refused by the registrar.')
    expect(store.whoisSaving).toBe(false)
  })

  it('reset clears results and errors', async () => {
    domainsApi.createTransferOrder.mockRejectedValue(refusal)
    const store = useDomainsStore()
    await store.placeTransfer({})

    store.reset()

    expect(store.transferError).toBe('')
    expect(store.transferResult).toBeNull()
  })
})

describe('useDomainsStore storefront lookup', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    for (const api of [domainsApi, catalogApi, billingApi, clientApi]) {
      for (const fn of Object.values(api)) {
        fn.mockReset()
      }
    }
  })

  /**
   * Loads a TLD price table into the catalogue store the lookup reads.
   *
   * @returns The domains store.
   */
  const withPricing = async () => {
    const { useCatalogStore } = await import('~/stores/catalog')
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

  it('turns the price table into rows with dotted extensions and one-year prices', async () => {
    const store = await withPricing()

    expect(store.priceRows[0]).toMatchObject({ tld: '.com', register: '12.50', renew: '12.50', transfer: '12.50', registerAmount: 12.5, sellCurrency: 'USD' })
    expect(store.priceRows[1]).toMatchObject({ tld: '.net', renew: '11.00' })
    expect(store.currencyCode).toBe('USD')
  })

  it('collects the categories behind the all sentinel, sorted', async () => {
    const store = await withPricing()

    expect(store.categories).toEqual(['__all__', 'popular', 'tech'])
  })

  it('offers only the first four extensions', async () => {
    const store = await withPricing()

    expect(store.offeredTlds.map(r => r.tld)).toEqual(['.com', '.net', '.org', '.io'])
  })

  it('checks a name against every offered extension, marking a failed check unknown', async () => {
    catalogApi.checkDomain.mockImplementation(async (name: string) => {
      if (name === 'my-site.net') {
        throw new Error('registrar down')
      }
      return { domain: name, available: name === 'my-site.com', status: 'x' }
    })
    const store = await withPricing()

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
    const store = await withPricing()

    await store.searchOfferedTlds('   ')

    expect(catalogApi.checkDomain).not.toHaveBeenCalled()
    expect(store.lookupResults).toEqual([])
    expect(store.lookupTerm).toBe('')
  })
})

describe('useDomainsStore detail reads', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    for (const api of [domainsApi, catalogApi, billingApi, clientApi]) {
      for (const fn of Object.values(api)) {
        fn.mockReset()
      }
    }
  })

  it('keeps the domain and clears the previous one while the next loads', async () => {
    domainsApi.fetchDomain.mockResolvedValue({ id: 4, name: 'a.com' })
    const store = useDomainsStore()

    expect(await store.loadDomain('4')).toMatchObject({ name: 'a.com' })
    expect(store.domain).toMatchObject({ id: 4 })
    expect(store.domainLoading).toBe(false)
    expect(store.domainError).toBe('')
  })

  it('keeps the API sentence and no domain when the read is refused', async () => {
    domainsApi.fetchDomain.mockRejectedValue(refusal)
    const store = useDomainsStore()

    expect(await store.loadDomain('4')).toBeNull()
    expect(store.domain).toBeNull()
    expect(store.domainError).toBe('Refused by the registrar.')
  })

  it('reads the primary profile once and reuses it', async () => {
    const profile = makeClientUser()
    clientApi.fetchMeOnce.mockResolvedValue(profile)
    const store = useDomainsStore()

    await store.loadPrimaryProfile()
    const again = await store.loadPrimaryProfile()

    expect(again).toEqual(profile)
    expect(clientApi.fetchMeOnce).toHaveBeenCalledTimes(1)
  })

  it('returns null for the primary profile when it cannot be read', async () => {
    clientApi.fetchMeOnce.mockRejectedValue(refusal)

    expect(await useDomainsStore().loadPrimaryProfile()).toBeNull()
  })

  it('keeps the gateways, and an empty list when they cannot be read', async () => {
    billingApi.fetchGatewayMethods.mockResolvedValueOnce([{ module: 'card_gateway', displayName: 'Card gateway', supportsStoredCredentials: false }]).mockRejectedValueOnce(refusal)
    const store = useDomainsStore()

    await store.loadPaymentMethods()
    expect(store.paymentMethods).toHaveLength(1)

    await store.loadPaymentMethods()
    expect(store.paymentMethods).toEqual([])
    expect(store.paymentMethodsLoading).toBe(false)
  })
})
