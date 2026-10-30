/**
 * Pinia store for admin services management: the service list with enriched client, domain and
 * pricing data, one service's detail, the per-section saves and the provisioning commands.
 *
 * Owns every request the service pages make and the sentence for a refusal, so the pages hold
 * no `catch`. A load puts its sentence in `error`; a mutation puts its sentence in `actionError`.
 */
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useServicesApi } from '@/composables/apis/useServicesApi'
import { runAction } from '@/lib/runAction'
import type { PaginatedList } from '@/types/paginatedlist'
import type {
  AddExistingServicePayload,
  ServiceAutomationPayload,
  ServiceBillingTermsPayload,
  ServiceDetail,
  ServiceHostingAccountPayload,
  ServiceListItem,
  ServiceNotesPayload,
  ServiceStatus,
} from '@/types/service'
import { DEFAULT_PAGE_SIZE, LOOKUP_PAGE_SIZE } from '@/lib/limits'

/** Loads and changes services. */
export const useServicesStore = defineStore('services', () => {
  const api = useServicesApi()

  /** Loaded services list for the current page. */
  const services = ref<ServiceListItem[]>([])

  /** Current page (1-based). */
  const page = ref(1)

  /** Items per page. */
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  /** Total number of matching items across all pages. */
  const totalCount = ref(0)

  /** True while a load is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed load, null when none. */
  const error = ref<string | null>(null)

  /** Service detail currently viewed, null until loaded. */
  const current = ref<ServiceDetail | null>(null)

  /** Sentence for the last failed save or provisioning command, null when none. */
  const actionError = ref<string | null>(null)

  /** Per-field messages of the last refused save, keyed by camelCase field name. */
  const fieldErrors = ref<Record<string, string>>({})

  /**
   * Loads one page of services.
   *
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchAll = async (): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () => api.listServices({ page: page.value, pageSize: pageSize.value }))
    loading.value = false
    if (result.ok) {
      services.value = result.value.items
      totalCount.value = result.value.totalCount
    }
    return result.ok
  }

  /**
   * Fetches one page of a client's services without touching the shared list state.
   *
   * @param clientId - The client identifier to filter by.
   * @param pageNumber - Page, 1-based.
   * @param size - Items per page.
   * @returns The page, or null when refused (see `error`).
   */
  const fetchClientPage = async (
    clientId: number | string,
    pageNumber: number,
    size: number,
  ): Promise<PaginatedList<ServiceListItem> | null> => {
    loading.value = true
    const result = await runAction(error, () =>
      api.listServices({ clientId, page: pageNumber, pageSize: size }),
    )
    loading.value = false
    return result.ok ? result.value : null
  }

  /**
   * Loads one service's detail into `current`.
   *
   * @param id - Service primary key.
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchById = async (id: number | string): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () => api.getService(id))
    loading.value = false
    if (result.ok) {
      current.value = result.value
    }
    return result.ok
  }

  /**
   * Loads a client's services for the service switcher. A failure leaves the list empty and
   * is not shown: the page works without the switcher.
   *
   * @param clientId - The client identifier.
   * @returns The client's services; empty when the call failed.
   */
  const fetchClientServices = async (clientId: number | string): Promise<ServiceListItem[]> => {
    /** Throwaway error ref: the switcher is optional, so its failure is deliberately silent. */
    const quiet = ref<string | null>(null)
    const result = await runAction(quiet, () => api.listServices({ clientId, page: 1, pageSize: LOOKUP_PAGE_SIZE }))
    return result.ok ? result.value.items : []
  }

  /**
   * Runs one mutating request, capturing a refusal into `actionError`.
   *
   * @param task - The request to run.
   * @returns True when the API accepted it; false when refused.
   */
  const mutate = async (task: () => Promise<unknown>): Promise<boolean> =>
    (await runAction(actionError, task, fieldErrors)).ok

  /**
   * Saves what a service is billed.
   *
   * @param id - Service primary key.
   * @param payload - The billing terms.
   * @returns True when saved; false when refused (see `actionError` and `fieldErrors`).
   */
  const updateBillingTerms = (id: number | string, payload: ServiceBillingTermsPayload): Promise<boolean> =>
    mutate(() => api.updateBillingTerms(id, payload))

  /**
   * Adds a service for a hosting account that already exists on a server.
   *
   * @param payload - The client, product, billing terms and account.
   * @returns True when added; false when refused (see `actionError` and `fieldErrors`).
   */
  const addExisting = (payload: AddExistingServicePayload): Promise<boolean> =>
    mutate(() => api.addExistingService(payload))

  /**
   * Saves the hosting account record of a service.
   *
   * @param id - Service primary key.
   * @param payload - The hosting account details.
   * @returns True when saved; false when refused (see `actionError` and `fieldErrors`).
   */
  const updateHostingAccount = (id: number | string, payload: ServiceHostingAccountPayload): Promise<boolean> =>
    mutate(() => api.updateHostingAccount(id, payload))

  /**
   * Saves the suspension hold and the end-of-cycle termination of a service.
   *
   * @param id - Service primary key.
   * @param payload - The automation settings.
   * @returns True when saved; false when refused (see `actionError` and `fieldErrors`).
   */
  const updateAutomation = (id: number | string, payload: ServiceAutomationPayload): Promise<boolean> =>
    mutate(() => api.updateAutomation(id, payload))

  /**
   * Saves the admin notes of a service.
   *
   * @param id - Service primary key.
   * @param payload - The notes.
   * @returns True when saved; false when refused (see `actionError` and `fieldErrors`).
   */
  const updateNotes = (id: number | string, payload: ServiceNotesPayload): Promise<boolean> =>
    mutate(() => api.updateNotes(id, payload))

  /**
   * Moves a service to another status.
   *
   * @param id - Service primary key.
   * @param status - The status asked for.
   * @returns True when moved; false when refused (see `actionError`).
   */
  const changeStatus = (id: number | string, status: ServiceStatus): Promise<boolean> =>
    mutate(() => api.changeStatus(id, { status }))

  /**
   * Corrects the termination date of a terminated service.
   *
   * @param id - Service primary key.
   * @param terminatedAt - ISO 8601 date the service really ended.
   * @returns True when saved; false when refused (see `actionError`).
   */
  const correctTerminationDate = (id: number | string, terminatedAt: string): Promise<boolean> =>
    mutate(() => api.correctTerminationDate(id, { terminatedAt }))

  /**
   * Asks the provisioning server for a one-time control-panel login URL.
   *
   * @param id - Service primary key.
   * @returns The URL, or null when refused (see `actionError`).
   */
  const fetchPanelLoginUrl = async (id: number | string): Promise<string | null> => {
    const result = await runAction(actionError, () => api.getPanelLoginUrl(id))
    return result.ok ? result.value.url : null
  }

  /**
   * Provisions a pending service on the hosting server.
   *
   * @param id - Service primary key.
   * @returns True when accepted; false when refused (see `actionError`).
   */
  const provision = (id: number | string): Promise<boolean> => mutate(() => api.provisionService(id))

  /**
   * Changes the hosting account password via the provisioning server.
   *
   * @param id - Service primary key.
   * @param newPassword - The new password.
   * @returns True when accepted; false when refused (see `actionError`).
   */
  const changePassword = (id: number | string, newPassword: string): Promise<boolean> =>
    mutate(() => api.changePassword(id, newPassword))

  /**
   * Changes the hosting package via the provisioning server.
   *
   * @param id - Service primary key.
   * @param newPackage - The new package name.
   * @returns True when accepted; false when refused (see `actionError`).
   */
  const changePackage = (id: number | string, newPackage: string): Promise<boolean> =>
    mutate(() => api.changePackage(id, newPackage))

  /**
   * Suspends a service.
   *
   * @param id - Service primary key.
   * @param reason - Reason recorded with the suspension.
   * @returns True when accepted; false when refused (see `actionError`).
   */
  const suspend = (id: number | string, reason: string): Promise<boolean> =>
    mutate(() => api.suspendService(id, { reason }))

  /**
   * Unsuspends a service.
   *
   * @param id - Service primary key.
   * @returns True when accepted; false when refused (see `actionError`).
   */
  const unsuspend = (id: number | string): Promise<boolean> => mutate(() => api.unsuspendService(id))

  /**
   * Terminates a service.
   *
   * @param id - Service primary key.
   * @param reason - Reason recorded with the termination.
   * @returns True when accepted; false when refused (see `actionError`).
   */
  const terminate = (id: number | string, reason: string): Promise<boolean> =>
    mutate(() => api.terminateService(id, { reason }))

  return {
    services, current, page, pageSize, totalCount, loading, error, actionError, fieldErrors,
    fetchAll, fetchClientPage, fetchById, fetchClientServices,
    addExisting, updateBillingTerms, updateHostingAccount, updateAutomation, updateNotes, changeStatus, correctTerminationDate,
    fetchPanelLoginUrl, provision, changePassword, changePackage, suspend, unsuspend, terminate,
  }
})
