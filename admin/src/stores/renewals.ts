/**
 * Pinia store for the upcoming renewals screen: the chosen window, the services and domains renewing inside it,
 * the domain renewal lines left claimed, and the staff actions on them.
 *
 * It is the only caller of `useRenewalsApi`. A load puts its sentence in `error`; an action puts its sentence in
 * `actionError`, or a finished result sentence key in `notice`.
 */
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useRenewalsApi } from '@/composables/apis/useRenewalsApi'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { runAction } from '@/lib/runAction'
import type { DomainRegistration } from '@/types/domain'
import { RenewalWindow } from '@/types/renewal'
import type { ClaimedDomainRenewal, UpcomingServiceRenewal } from '@/types/renewal'

/** What an action ended with, for the screen to word. */
export enum RenewalNotice {
  /** Invoices were raised; `noticeCount` says how many. */
  InvoicesRaised = 'InvoicesRaised',
  /** The client had nothing inside the invoicing lead time. */
  NothingDue = 'NothingDue',
  /** A claim was released. */
  ClaimReleased = 'ClaimReleased',
}

/** Loads and changes what is about to renew. */
export const useRenewalsStore = defineStore('renewals', () => {
  /** The renewals endpoints; this store is their only caller. */
  const api = useRenewalsApi()

  /** The window staff look ahead over. */
  const window = ref<RenewalWindow>(RenewalWindow.Next30Days)

  /** Services renewing inside the window, current page. */
  const services = ref<UpcomingServiceRenewal[]>([])

  /** Current page of services (1-based). */
  const servicesPage = ref(1)

  /** Total services inside the window. */
  const servicesTotal = ref(0)

  /** Domains expiring inside the window, current page. */
  const domains = ref<DomainRegistration[]>([])

  /** Current page of domains (1-based). */
  const domainsPage = ref(1)

  /** Total domains inside the window. */
  const domainsTotal = ref(0)

  /** Claimed domain renewal lines. */
  const claims = ref<ClaimedDomainRenewal[]>([])

  /** Items per page of both lists. */
  const pageSize = DEFAULT_PAGE_SIZE

  /** True while a load is in flight. */
  const loading = ref(false)

  /** True while a staff action is in flight. */
  const acting = ref(false)

  /** Sentence for the last failed load, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last refused action, null when none. */
  const actionError = ref<string | null>(null)

  /** What the last action ended with, null when none. */
  const notice = ref<RenewalNotice | null>(null)

  /** How many invoices the last run raised. */
  const noticeCount = ref(0)

  /**
   * Loads one page of services for the current window.
   *
   * @param page - Page number (1-based).
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchServices = async (page: number): Promise<boolean> => {
    const result = await runAction(error, () => api.listServices(window.value, page, pageSize))
    if (result.ok) {
      services.value = result.value.items
      servicesTotal.value = result.value.totalCount
      servicesPage.value = page
    }
    return result.ok
  }

  /**
   * Loads one page of domains for the current window.
   *
   * @param page - Page number (1-based).
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchDomains = async (page: number): Promise<boolean> => {
    const result = await runAction(error, () => api.listDomains(window.value, page, pageSize))
    if (result.ok) {
      domains.value = result.value.items
      domainsTotal.value = result.value.totalCount
      domainsPage.value = page
    }
    return result.ok
  }

  /**
   * Loads the claimed domain renewal lines.
   *
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchClaims = async (): Promise<boolean> => {
    const result = await runAction(error, () => api.listClaims())
    if (result.ok) {
      claims.value = result.value
    }
    return result.ok
  }

  /**
   * Loads everything the screen shows, for the current window.
   *
   * @returns True when all three loads worked.
   */
  const fetchAll = async (): Promise<boolean> => {
    loading.value = true
    // Each load clears `error` before it runs, so the first refusal is kept and put back after the last load.
    let firstFailure: string | null = null
    for (const load of [() => fetchServices(1), () => fetchDomains(1), () => fetchClaims()]) {
      if (!(await load()) && firstFailure === null) {
        firstFailure = error.value
      }
    }
    error.value = firstFailure
    loading.value = false
    return firstFailure === null
  }

  /**
   * Chooses another window and loads it.
   *
   * @param next - The new window.
   * @returns True when loaded.
   */
  const setWindow = (next: RenewalWindow): Promise<boolean> => {
    window.value = next
    return fetchAll()
  }

  /**
   * Runs a staff action, clearing the last result first.
   *
   * @param task - The request.
   * @returns True when accepted; false when refused (see `actionError`).
   */
  const act = async (task: () => Promise<unknown>): Promise<boolean> => {
    acting.value = true
    notice.value = null
    const result = await runAction(actionError, task)
    acting.value = false
    return result.ok
  }

  /**
   * Switches the automatic billing of a listed service.
   *
   * @param id - Service id.
   * @param value - The wanted state.
   * @returns True when accepted.
   */
  const setServiceAutoRenew = async (id: number, value: boolean): Promise<boolean> => {
    const ok = await act(() => api.setServiceAutoRenew(id, value))
    const row = services.value.find(s => s.id === id)
    if (ok && row) {
      row.autoRenew = value
    }
    return ok
  }

  /**
   * Switches the automatic renewal of a listed domain.
   *
   * @param id - Domain id.
   * @param value - The wanted state.
   * @returns True when accepted.
   */
  const setDomainAutoRenew = async (id: number, value: boolean): Promise<boolean> => {
    const ok = await act(() => api.setDomainAutoRenew(id, value))
    const row = domains.value.find(d => d.id === id)
    if (ok && row) {
      row.autoRenew = value
    }
    return ok
  }

  /**
   * Raises the renewal invoices a client is due now and records the outcome in `notice`.
   *
   * @param clientId - Client id.
   * @returns True when the run worked, also when nothing was due.
   */
  const generateInvoices = async (clientId: number): Promise<boolean> => {
    let raised = 0
    const ok = await act(async () => {
      raised = await api.generateInvoices(clientId)
    })
    if (ok) {
      noticeCount.value = raised
      notice.value = raised > 0 ? RenewalNotice.InvoicesRaised : RenewalNotice.NothingDue
    }
    return ok
  }

  /**
   * Releases a claimed line, then reloads the claims.
   *
   * @param invoiceId - Invoice id.
   * @param itemId - Line id.
   * @returns True when released.
   */
  const releaseClaim = async (invoiceId: number, itemId: number): Promise<boolean> => {
    const ok = await act(() => api.releaseClaim(invoiceId, itemId))
    if (ok) {
      notice.value = RenewalNotice.ClaimReleased
    }
    await fetchClaims()
    return ok
  }

  return {
    window, services, servicesPage, servicesTotal, domains, domainsPage, domainsTotal, claims, pageSize,
    loading, acting, error, actionError, notice, noticeCount,
    fetchServices, fetchDomains, fetchClaims, fetchAll, setWindow,
    setServiceAutoRenew, setDomainAutoRenew, generateInvoices, releaseClaim,
  }
})
