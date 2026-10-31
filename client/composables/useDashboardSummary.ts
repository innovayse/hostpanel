/**
 * What the client dashboard derives from the client store: the lists behind its alert banners,
 * the loading flag for its stat tiles and the de-duplicated load errors.
 *
 * Reads the store only; the data is fetched by the page. Kept out of the page so the
 * derivations can be tested without mounting it.
 *
 * @module composables/useDashboardSummary
 */

import { useClientStore } from '~/stores/client'
import { EXPIRY_WARNING_DAYS, isExpiringWithin } from '~/utils/domainExpiry'
import { isInvoiceOutstanding } from '~/utils/invoice'
import { serviceAwaitsSetup } from '~/utils/serviceSetup'

/**
 * Builds the dashboard's derived state.
 *
 * @returns Computed lists and flags over the client store.
 */
export const useDashboardSummary = () => {
  const store = useClientStore()

  /** True while any of the four sections the stat tiles count is still loading. */
  const statsLoading = computed(() =>
    store.servicesLoading || store.invoicesLoading || store.domainsLoading || store.ticketsLoading)

  /**
   * What the API said about each section that failed to load, without repeats.
   *
   * Deduplicated because the four calls share one cause more often than not, and printing the
   * same sentence four times reads as four separate faults.
   */
  const loadErrors = computed(() => [...new Set(
    [store.servicesError, store.invoicesError, store.domainsError, store.ticketsError]
      .filter((message): message is string => Boolean(message))
  )])

  /** Domains whose expiry falls within {@link EXPIRY_WARNING_DAYS} days (or has passed). */
  const expiringDomains = computed(() =>
    store.domains.filter((domain) => isExpiringWithin(domain.expiresAt, EXPIRY_WARNING_DAYS, true)))

  /** Hosting services ordered but not yet set up; a digital service has nothing to set up ({@link serviceAwaitsSetup}). */
  const servicesNeedingSetup = computed(() => store.services.filter(serviceAwaitsSetup))

  /**
   * Invoices still awaiting payment.
   *
   * Shares {@link isInvoiceOutstanding} with the unpaid stat tile, so the two cannot disagree.
   */
  const unpaidInvoices = computed(() => store.invoices.filter(isInvoiceOutstanding))

  return { statsLoading, loadErrors, expiringDomains, servicesNeedingSetup, unpaidInvoices }
}
