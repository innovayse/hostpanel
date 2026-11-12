/**
 * Pinia store for domain actions: availability search, transfer and renewal orders, the
 * per-domain settings (auto-renew, lock, WHOIS privacy, nameservers, WHOIS contacts) and the
 * EPP code.
 *
 * The store owns every in-flight flag and every failure sentence, so a page only renders them
 * and branches on the returned {@link DomainActionOutcome}; no page wraps an API call in
 * try/catch. Sentences come from `lib/errorHandler.ts`: the wording is the API's own.
 *
 * It also owns the storefront's domain lookup (the TLD price rows, their categories and the
 * availability search across the offered extensions) on top of the price table that
 * `stores/catalog.ts` reads. Prices are quoted in the payer's own currency, never the page's
 * language.
 *
 * URLs and transport belong to {@link useDomainsApi}, {@link useCatalogApi} and
 * {@link useBillingApi}; this store owns only the state.
 *
 * @module stores/domains
 */

import { defineStore } from 'pinia'
import { useBillingApi } from '~/composables/apis/useBillingApi'
import { useClientApi } from '~/composables/apis/useClientApi'
import { useCatalogApi } from '~/composables/apis/useCatalogApi'
import { useDomainsApi } from '~/composables/apis/useDomainsApi'
import { useCatalogStore } from '~/stores/catalog'
import { useCurrencyStore } from '~/stores/currency'
import { ALL_CATEGORY } from '~/templates/aurora/types/allcategory'
import { DomainResultStatus } from '~/types/domainresultstatus'
import type { DomainResult } from '~/types/domainresult'
import type { DomainCheckResult } from '~/types/domaincheckresult'
import type { TldPriceRow } from '~/types/tldpricerow'
import type { TldPeriodPrice } from '~/types/tldperiodprice'
import { ONE_YEAR, priceForYears } from '~/utils/tldPeriodPrice'
import { EMPTY_AMOUNT } from '~/utils/formatCurrency'
import { formatMoney } from '~/utils/formatMoney'
import { contactFromRegistrant } from '~/utils/domainContacts'
import { DomainActionOutcome } from '~/types/domainactionoutcome'
import { ProductType } from '~/types/producttype'
import type { AccountContact } from '~/types/accountcontact'
import type { ClientUser } from '~/types/clientuser'
import type { DomainContactFields } from '~/types/domaincontactfields'
import type { DomainDetail } from '~/types/domaindetail'
import type { WhoisRecord } from '~/types/whoisrecord'
import type { DomainOrderResult } from '~/types/domainorderresult'
import type { GatewayMethod } from '~/types/gatewaymethod'
import { apiErrorMessage } from '~/lib/errorHandler'

/** How many extensions the storefront search offers. */
const OFFERED_TLD_COUNT = 4

/**
 * Central store for domain actions.
 *
 * @returns The domain state and the actions that change it.
 */
export const useDomainsStore = defineStore('domains', () => {
  const api = useDomainsApi()
  const catalog = useCatalogApi()
  const billing = useBillingApi()
  const client = useClientApi()
  const catalogStore = useCatalogStore()
  const currencyStore = useCurrencyStore()

  // -- Availability search ---------------------------------------------------------------

  /** The last availability answer, or null before a search or after a failed one. */
  const searchResult = ref<DomainCheckResult | null>(null)

  /** The sentence for the last failed search; empty when there is none to show. */
  const searchError = ref('')

  /** Whether an availability search is in flight. */
  const searching = ref(false)

  /**
   * Asks whether one domain is available.
   *
   * @param domain - Fully qualified domain name.
   * @returns How the search ended; on failure {@link searchError} holds the sentence.
   */
  const checkAvailability = async (domain: string): Promise<DomainActionOutcome> => {
    searching.value = true
    searchResult.value = null
    searchError.value = ''
    try {
      searchResult.value = await catalog.checkDomain(domain)
      return DomainActionOutcome.Done
    } catch (e: unknown) {
      searchError.value = apiErrorMessage(e)
      return DomainActionOutcome.Failed
    } finally {
      searching.value = false
    }
  }

  // -- Storefront lookup -----------------------------------------------------------------

  /** The availability rows of the last storefront search, one per offered extension. */
  const lookupResults = ref<DomainResult[]>([])

  /** Whether a storefront search is in flight. */
  const lookupPending = ref(false)

  /** The name the last storefront search was run for, so the search field can show it. */
  const lookupTerm = ref('')

  /** The currency the price rows are quoted in, shaped for {@link formatMoney}. */
  const rowCurrency = computed(() =>
    currencyStore.moneyCurrencyFor(catalogStore.tldPricing?.currency?.code))

  /** Currency code the prices are quoted in, as the API reports it: the payer's own currency. */
  const currencyCode = computed(() => catalogStore.tldPricing?.currency?.code ?? '')

  /**
   * Formats the one-year price of an action, falling back to a dash.
   *
   * @param prices - The action's price list as the API returned it.
   * @returns The one-year price formatted in the row currency, or a dash.
   */
  const oneYear = (prices: TldPeriodPrice[] | undefined): string => {
    const amount = priceForYears(prices, ONE_YEAR)
    return amount === null ? EMPTY_AMOUNT : formatMoney(amount, rowCurrency.value)
  }

  /** Every priced extension, for the full price table. */
  const priceRows = computed<TldPriceRow[]>(() =>
    (catalogStore.tldPricing?.pricing ?? []).map(entry => ({
      tld: entry.tld.startsWith('.') ? entry.tld : `.${entry.tld}`,
      register: oneYear(entry.register),
      renew: oneYear(entry.renew ?? entry.register),
      transfer: oneYear(entry.transfer ?? entry.register),
      categories: entry.categories ?? [],
      registerAmount: priceForYears(entry.register, ONE_YEAR) ?? 0,
      transferAmount: priceForYears(entry.transfer ?? entry.register, ONE_YEAR) ?? 0,
      sellCurrency: entry.sellCurrency,
    })))

  /**
   * Every category the priced zones carry, prefixed with the "all" sentinel. Derived from the
   * registrar data rather than a fixed list, so an operator's own categories drive the filter.
   */
  const categories = computed(() => {
    const found = new Set<string>()
    for (const row of priceRows.value) {
      for (const category of row.categories) {
        found.add(category)
      }
    }
    return [ALL_CATEGORY, ...[...found].sort()]
  })

  /** The subset the search card offers. */
  const offeredTlds = computed(() => priceRows.value.slice(0, OFFERED_TLD_COUNT))

  /**
   * Checks a name against every offered extension.
   *
   * A check that fails is reported as `unknown` rather than dropped or shown as taken: the row
   * count stays stable, and a registrar that is unconfigured or unreachable reads as a fault
   * instead of as a name somebody owns.
   *
   * @param term - Raw text the visitor typed.
   * @returns Nothing; the rows land in {@link lookupResults}.
   */
  const searchOfferedTlds = async (term: string): Promise<void> => {
    const base = term.trim().toLowerCase().replace(/\s+/g, '-').replace(/\.[^.]*$/, '')
    if (!base) {
      return
    }
    lookupTerm.value = term.trim()

    lookupPending.value = true
    try {
      lookupResults.value = await Promise.all(
        offeredTlds.value.map(async ({ tld, register }): Promise<DomainResult> => {
          const name = `${base}${tld}`
          try {
            const response = await catalog.checkDomain(name)
            return {
              name,
              price: response.available ? register : '—',
              status: response.available ? DomainResultStatus.Available : DomainResultStatus.Taken,
            }
          } catch {
            return { name, price: '—', status: DomainResultStatus.Unknown }
          }
        })
      )
    } finally {
      lookupPending.value = false
    }
  }

  // -- One domain ------------------------------------------------------------------------

  /** The domain the detail page shows; null until {@link loadDomain} or after a failed read. */
  const domain = ref<DomainDetail | null>(null)

  /** Whether the domain is being read. */
  const domainLoading = ref(false)

  /** The sentence for the last failed domain read; empty when there is none to show. */
  const domainError = ref('')

  /**
   * Reads one domain.
   *
   * @param id - Domain id.
   * @returns The domain; null when it could not be read ({@link domainError} holds the sentence).
   */
  const loadDomain = async (id: string): Promise<DomainDetail | null> => {
    domainLoading.value = true
    domainError.value = ''
    domain.value = null
    try {
      domain.value = await api.fetchDomain(id)
    } catch (e: unknown) {
      domain.value = null
      domainError.value = apiErrorMessage(e)
    } finally {
      domainLoading.value = false
    }
    return domain.value
  }

  /** The account's primary profile the WHOIS form is pre-filled from; null until asked for. */
  const primaryProfile = ref<ClientUser | null>(null)

  /**
   * Reads the account's primary profile once; later calls reuse the answer.
   *
   * @returns The profile, or null when it could not be read.
   */
  const loadPrimaryProfile = async (): Promise<ClientUser | null> => {
    if (!primaryProfile.value) {
      try {
        primaryProfile.value = await client.fetchMeOnce()
      } catch {
        return null
      }
    }
    return primaryProfile.value
  }

  /** The gateways the order forms may bill through; empty until {@link loadPaymentMethods}. */
  const paymentMethods = ref<GatewayMethod[]>([])

  /** Whether the saved payment methods are being read. */
  const paymentMethodsLoading = ref(false)

  /**
   * Reads the gateways an order form may bill through. The order endpoints expect a gateway
   * module name, which a saved card does not carry, so this reads the gateway list.
   *
   * @returns The gateways; an empty list when they could not be read, so the form stays blank.
   */
  const loadPaymentMethods = async (): Promise<GatewayMethod[]> => {
    paymentMethodsLoading.value = true
    try {
      paymentMethods.value = await billing.fetchGatewayMethods()
    } catch {
      paymentMethods.value = []
    } finally {
      paymentMethodsLoading.value = false
    }
    return paymentMethods.value
  }

  /**
   * Looks up the id of the product a domain cart line is sold under.
   *
   * @returns The id, or null when there is no domain product or the catalogue was unreachable
   * (the cart then simply cannot add a domain, which the page already handles).
   */
  const fetchDomainProductId = async (): Promise<number | null> => {
    try {
      const products = await catalog.fetchProducts()
      return products.find(p => p.type === ProductType.Domain)?.id ?? null
    } catch {
      return null
    }
  }

  // -- Transfer order ---------------------------------------------------------------------

  /** The order a successful transfer raised, or null. */
  const transferResult = ref<DomainOrderResult | null>(null)

  /** The sentence for the last failed transfer order; empty when there is none. */
  const transferError = ref('')

  /** Whether a transfer order is in flight. */
  const transferSaving = ref(false)

  /**
   * Raises an order to transfer a domain in.
   *
   * @param transfer - Domain, auth code, period and payment method.
   * @returns How the order ended; on failure {@link transferError} holds the sentence.
   */
  const placeTransfer = async (transfer: Record<string, unknown>): Promise<DomainActionOutcome> => {
    transferSaving.value = true
    transferError.value = ''
    try {
      transferResult.value = await api.createTransferOrder(transfer)
      return DomainActionOutcome.Done
    } catch (e: unknown) {
      transferError.value = apiErrorMessage(e)
      return DomainActionOutcome.Failed
    } finally {
      transferSaving.value = false
    }
  }

  // -- Renewal order ----------------------------------------------------------------------

  /** The order a successful renewal raised, or null. */
  const renewResult = ref<DomainOrderResult | null>(null)

  /** The sentence for the last failed renewal; empty when there is none. */
  const renewError = ref('')

  /** Whether a renewal order is in flight. */
  const renewSaving = ref(false)

  /**
   * Raises a renewal order. {@link renewResult} is only set from the API's own answer, since
   * the success panel is built out of an order id and an invoice id.
   *
   * @param id - Domain id.
   * @param years - How many years to renew for.
   * @param paymentMethod - Gateway module to bill through.
   * @returns How the order ended; on failure {@link renewError} holds the sentence.
   */
  const placeRenewal = async (
    id: string,
    years: number,
    paymentMethod: string
  ): Promise<DomainActionOutcome> => {
    renewSaving.value = true
    renewError.value = ''
    try {
      renewResult.value = await api.createRenewOrder(id, years, paymentMethod)
      return DomainActionOutcome.Done
    } catch (e: unknown) {
      renewError.value = apiErrorMessage(e)
      return DomainActionOutcome.Failed
    } finally {
      renewSaving.value = false
    }
  }

  // -- Toggles ----------------------------------------------------------------------------

  /** Whether an auto-renew change is in flight. */
  const autoRenewSaving = ref(false)

  /** Whether a registrar-lock change is in flight. */
  const lockSaving = ref(false)

  /** Whether a WHOIS-privacy change is in flight. */
  const idProtectSaving = ref(false)

  /** The sentence for the last failed toggle; empty when there is none. */
  const toggleError = ref('')

  /**
   * Runs one toggle request, keeping its flag and failure as state.
   *
   * @param flag - The in-flight flag of this toggle.
   * @param call - The API request.
   * @returns How the request ended.
   */
  const runToggle = async (
    flag: { value: boolean },
    call: () => Promise<unknown>
  ): Promise<DomainActionOutcome> => {
    flag.value = true
    toggleError.value = ''
    try {
      await call()
      return DomainActionOutcome.Done
    } catch (e: unknown) {
      toggleError.value = apiErrorMessage(e)
      return DomainActionOutcome.Failed
    } finally {
      flag.value = false
    }
  }

  /**
   * Switches automatic renewal on or off.
   *
   * @param id - Domain id.
   * @param enabled - True to renew automatically.
   * @returns How the request ended; the page keeps its current state on failure.
   */
  const setAutoRenew = (id: string, enabled: boolean): Promise<DomainActionOutcome> =>
    runToggle(autoRenewSaving, () => api.setAutoRenew(id, enabled))

  /**
   * Locks or unlocks the domain against transfer away.
   *
   * @param id - Domain id.
   * @param enabled - True to lock.
   * @returns How the request ended.
   */
  const setLock = (id: string, enabled: boolean): Promise<DomainActionOutcome> =>
    runToggle(lockSaving, () => api.setLock(id, enabled))

  /**
   * Switches WHOIS privacy on or off.
   *
   * @param id - Domain id.
   * @param enabled - True to hide the registrant details.
   * @returns How the request ended.
   */
  const setIdProtect = (id: string, enabled: boolean): Promise<DomainActionOutcome> =>
    runToggle(idProtectSaving, () => api.setIdProtect(id, enabled))

  // -- Nameservers ------------------------------------------------------------------------

  /** Whether the nameservers are being saved. */
  const nsSaving = ref(false)

  /** The sentence for the last failed nameserver save; empty when there is none. */
  const nsError = ref('')

  /**
   * Replaces the domain's nameservers. The nameservers themselves are read from
   * {@link domain}'s `management`, so after a save the domain is read again, without clearing
   * it, to keep that list the one the registrar now holds.
   *
   * @param id - Domain id.
   * @param nameservers - The complete list to set.
   * @returns How the save ended; on failure {@link nsError} holds the sentence.
   */
  const saveNameservers = async (id: string, nameservers: string[]): Promise<DomainActionOutcome> => {
    nsSaving.value = true
    nsError.value = ''
    try {
      await api.setNameservers(id, nameservers)
    } catch (e: unknown) {
      nsError.value = apiErrorMessage(e)
      nsSaving.value = false
      return DomainActionOutcome.Failed
    }
    try {
      domain.value = await api.fetchDomain(id)
    } catch {
      // The save itself succeeded; a failed re-read only leaves the previous copy in place, and
      // the form still shows what was just saved, so it is not reported as a failed save.
    } finally {
      nsSaving.value = false
    }
    return DomainActionOutcome.Done
  }

  // -- EPP code ---------------------------------------------------------------------------

  /** Whether an EPP code request is in flight. */
  const eppSending = ref(false)

  /** The code the registrar returned; empty until one is requested. */
  const eppCode = ref('')

  /** The sentence for the last failed EPP request; empty when there is none. */
  const eppError = ref('')

  /**
   * Asks the registrar for the transfer authorisation code.
   *
   * @param id - Domain id.
   * @returns How the request ended; {@link eppCode} or {@link eppError} holds the result.
   */
  const requestEpp = async (id: string): Promise<DomainActionOutcome> => {
    eppSending.value = true
    eppCode.value = ''
    eppError.value = ''
    try {
      eppCode.value = (await api.requestEppCode(id)).eppCode
      return DomainActionOutcome.Done
    } catch (e: unknown) {
      eppError.value = apiErrorMessage(e)
      return DomainActionOutcome.Failed
    } finally {
      eppSending.value = false
    }
  }

  // -- WHOIS contacts ---------------------------------------------------------------------

  /** Whether the WHOIS contacts are being read. */
  const whoisLoading = ref(false)

  /** Whether the WHOIS contacts are being saved. */
  const whoisSaving = ref(false)

  /** The sentence for the last failed WHOIS save; empty when there is none. */
  const whoisError = ref('')

  /**
   * Reads the registry's WHOIS record of the domain.
   *
   * @param id - Domain id.
   * @returns The record, or null when it could not be read.
   */
  const loadWhois = async (id: string): Promise<WhoisRecord | null> => {
    whoisLoading.value = true
    try {
      return await api.fetchWhois(id)
    } catch {
      return null
    } finally {
      whoisLoading.value = false
    }
  }

  /**
   * Reads the registrant contact the registrar currently holds for the domain.
   *
   * @param id - Domain id.
   * @returns The contact as form fields, or null when the registrar holds none or it could not be read.
   */
  const loadRegistrant = async (id: string): Promise<DomainContactFields | null> => {
    try {
      return contactFromRegistrant(await api.fetchContact(id))
    } catch {
      return null
    }
  }

  /**
   * Reads the account's sub-contacts the WHOIS form can be pre-filled from.
   *
   * @returns The contacts, or null when they could not be read.
   */
  const loadContacts = async (): Promise<AccountContact[] | null> => {
    try {
      return await client.fetchContacts()
    } catch {
      return null
    }
  }

  /**
   * Replaces the domain's WHOIS registrant contact.
   *
   * @param id - Domain id.
   * @param contact - The registrant contact to write.
   * @returns How the save ended; on failure {@link whoisError} holds the sentence.
   */
  const saveWhois = async (id: string, contact: DomainContactFields): Promise<DomainActionOutcome> => {
    whoisSaving.value = true
    whoisError.value = ''
    try {
      await api.setWhois(id, contact)
      return DomainActionOutcome.Done
    } catch (e: unknown) {
      whoisError.value = apiErrorMessage(e)
      return DomainActionOutcome.Failed
    } finally {
      whoisSaving.value = false
    }
  }

  /**
   * Clears the order, search and error state so a page opens clean.
   *
   * @returns Nothing.
   */
  const reset = (): void => {
    searchResult.value = null
    searchError.value = ''
    transferResult.value = null
    transferError.value = ''
    renewResult.value = null
    renewError.value = ''
    toggleError.value = ''
    nsError.value = ''
    eppCode.value = ''
    eppError.value = ''
    whoisError.value = ''
  }

  return {
    lookupTerm,
    searchResult,
    searchError,
    searching,
    checkAvailability,
    domain,
    domainLoading,
    domainError,
    loadDomain,
    primaryProfile,
    loadPrimaryProfile,
    paymentMethods,
    paymentMethodsLoading,
    loadPaymentMethods,
    lookupResults,
    lookupPending,
    currencyCode,
    priceRows,
    categories,
    offeredTlds,
    searchOfferedTlds,
    fetchDomainProductId,
    transferResult,
    transferError,
    transferSaving,
    placeTransfer,
    renewResult,
    renewError,
    renewSaving,
    placeRenewal,
    autoRenewSaving,
    lockSaving,
    idProtectSaving,
    toggleError,
    setAutoRenew,
    setLock,
    setIdProtect,
    nsSaving,
    nsError,
    saveNameservers,
    eppSending,
    eppCode,
    eppError,
    requestEpp,
    whoisLoading,
    whoisSaving,
    whoisError,
    loadWhois,
    loadRegistrant,
    loadContacts,
    saveWhois,
    reset
  }
})
