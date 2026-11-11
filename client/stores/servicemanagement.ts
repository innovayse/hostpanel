/**
 * Pinia store for managing one hosting service from `pages/client/services/[id]/index.vue`
 * and `pages/client/services/[id]/setup.vue`.
 *
 * Owns every in-flight flag, success flag and refusal sentence for the service actions
 * (password change, cancellation, related invoices, control-panel sign-in, first-time setup),
 * so the pages render them and never wrap an API call in their own try/catch. URLs and
 * transport belong to {@link useClientApi}.
 *
 * @module stores/servicemanagement
 */

import { defineStore } from 'pinia'
import { useClientApi } from '~/composables/apis/useClientApi'
import { useClientStore } from '~/stores/client'
import { ActionOutcome } from '~/types/actionoutcome'
import type { CancellationStatus } from '~/types/cancellationstatus'
import type { CancellationType } from '~/types/cancellationtype'
import type { HostingInfo } from '~/types/hostinginfo'
import type { ClientService } from '~/types/clientservice'
import type { ServiceInvoice } from '~/types/serviceinvoice'
import type { ServiceInvoices } from '~/types/serviceinvoices'
import { apiErrorCode, apiErrorMessage, PortalErrorCode } from '~/lib/errorHandler'
import { controlPanelUrl } from '~/utils/controlPanel'

/**
 * Service-management state for the service pages.
 *
 * @returns The state and the actions that change it.
 */
export const useServiceManagementStore = defineStore('serviceManagement', () => {
  const api = useClientApi()

  /** The service on screen; null until {@link loadService} or after a failed read. */
  const service = ref<ClientService | null>(null)
  /** True while the service is being read. */
  const serviceLoading = ref(false)
  /** The sentence for the last failed service read; empty when there is none to show. */
  const serviceError = ref('')
  /** Whether the last service read was refused because the client has no such service. */
  const serviceNotFound = ref(false)

  /** Nameservers and certificate of the service; empty until {@link loadHostingInfo}. */
  const hostingInfo = ref<HostingInfo>({ nameservers: [], ssl: null })

  /** Whether a cancellation is pending on the service; "not pending" until {@link loadCancellationStatus}. */
  const cancelStatus = ref<CancellationStatus>({ pending: false })

  /** True while a password change is in flight. */
  const passwordChanging = ref(false)
  /** True once the password change was accepted. */
  const passwordSuccess = ref(false)
  /** The sentence for the last refused password change; empty when there is nothing to report. */
  const passwordError = ref('')

  /** True while a cancellation request is in flight. */
  const cancelSubmitting = ref(false)
  /** The sentence for the last refused cancellation; empty when there is nothing to report. */
  const cancelError = ref('')
  /** True once the cancellation request was accepted. */
  const cancelDone = ref(false)

  /** Invoices raised against the service. */
  const relatedInvoices = ref<ServiceInvoice[]>([])
  /**
   * How many of the client's invoices carry no service link on any line. Reported beside the
   * list because an empty list only means "nothing is recorded against this service".
   */
  const unattributedInvoices = ref(0)
  /** True while the invoice list is loading. */
  const invoicesLoading = ref(true)
  /** True once the invoice list was read successfully. */
  const invoicesLoaded = ref(false)
  /** The sentence for the last failed invoice read; null when there is nothing to report. */
  const invoicesError = ref<string | null>(null)

  /** True while the automatic control-panel sign-in is in flight. */
  const ssoLoading = ref(false)
  /** Why the last automatic control-panel sign-in failed; empty when there is nothing to report. */
  const ssoError = ref('')

  /** True while first-time setup is in flight. */
  const setupSubmitting = ref(false)
  /** The sentence for the last refused setup; empty when there is nothing to report. */
  const setupError = ref('')

  /**
   * Clears everything left by a previous service, called when a service page opens.
   *
   * @returns Nothing.
   */
  const reset = (): void => {
    service.value = null
    serviceLoading.value = false
    serviceError.value = ''
    serviceNotFound.value = false
    hostingInfo.value = { nameservers: [], ssl: null }
    cancelStatus.value = { pending: false }
    passwordChanging.value = false
    passwordSuccess.value = false
    passwordError.value = ''
    cancelSubmitting.value = false
    cancelError.value = ''
    cancelDone.value = false
    relatedInvoices.value = []
    unattributedInvoices.value = 0
    invoicesLoading.value = true
    invoicesLoaded.value = false
    invoicesError.value = null
    ssoLoading.value = false
    ssoError.value = ''
    setupSubmitting.value = false
    setupError.value = ''
  }

  /**
   * Reads one service.
   *
   * @param serviceId - Service id.
   * @param keepCurrent - Keep the service on screen while it is read again, for a refresh after a change.
   * @returns The service; null when it could not be read ({@link serviceError} holds the sentence).
   */
  const loadService = async (serviceId: string, keepCurrent = false): Promise<ClientService | null> => {
    serviceLoading.value = true
    serviceError.value = ''
    serviceNotFound.value = false
    if (!keepCurrent) {
      service.value = null
    }
    try {
      service.value = await api.fetchService(serviceId)
    } catch (e: unknown) {
      serviceError.value = apiErrorMessage(e)
      serviceNotFound.value = apiErrorCode(e) === PortalErrorCode.ServiceNotFound
    } finally {
      serviceLoading.value = false
    }
    return service.value
  }

  /**
   * Reads the service's nameservers and certificate. These are optional extras, so a failure
   * leaves the empty default and the page renders "not available".
   *
   * @param serviceId - Service id.
   * @returns Nothing.
   */
  const loadHostingInfo = async (serviceId: string): Promise<void> => {
    try {
      hostingInfo.value = await api.fetchHostingInfo(serviceId)
    } catch {
      hostingInfo.value = { nameservers: [], ssl: null }
    }
  }

  /**
   * Reads whether a cancellation is pending. A failure leaves "not pending": the banner is an
   * optional extra and the cancel action itself is refused by the API when it cannot proceed.
   *
   * @param serviceId - Service id.
   * @returns Nothing.
   */
  const loadCancellationStatus = async (serviceId: string): Promise<void> => {
    try {
      cancelStatus.value = await api.fetchCancellationStatus(serviceId)
    } catch {
      cancelStatus.value = { pending: false }
    }
  }

  /**
   * Changes the control-panel password on a service.
   *
   * @param serviceId - Service id.
   * @param password - The new password.
   * @returns How the attempt ended; on failure {@link passwordError} holds the sentence to show.
   */
  const changePassword = async (serviceId: string, password: string): Promise<ActionOutcome> => {
    passwordError.value = ''
    passwordChanging.value = true
    try {
      await api.changeServicePassword(serviceId, password)
      passwordSuccess.value = true
      return ActionOutcome.Succeeded
    } catch (e: unknown) {
      passwordError.value = apiErrorMessage(e)
      return ActionOutcome.Failed
    } finally {
      passwordChanging.value = false
    }
  }

  /**
   * Requests cancellation of a service.
   *
   * @param serviceId - Service id.
   * @param type - When the cancellation takes effect.
   * @param reason - Free-text reason from the customer.
   * @returns How the attempt ended; on failure {@link cancelError} holds the sentence to show.
   */
  const cancelService = async (
    serviceId: string,
    type: CancellationType,
    reason: string
  ): Promise<ActionOutcome> => {
    cancelError.value = ''
    cancelSubmitting.value = true
    try {
      await api.cancelService(serviceId, { type, reason })
      cancelDone.value = true
      return ActionOutcome.Succeeded
    } catch (e: unknown) {
      cancelError.value = apiErrorMessage(e)
      return ActionOutcome.Failed
    } finally {
      cancelSubmitting.value = false
    }
  }

  /**
   * Reads the invoices raised against a service, once.
   *
   * The failure is kept in {@link invoicesError} rather than swallowed, so the page never claims
   * "no invoices" without evidence.
   *
   * @param serviceId - Service id.
   * @returns Nothing; the outcome is read from the state.
   */
  const loadInvoices = async (serviceId: string): Promise<void> => {
    if (invoicesLoaded.value) {
      return
    }
    invoicesLoading.value = true
    invoicesError.value = null
    try {
      const result = await api.fetchServiceInvoices<ServiceInvoices>(serviceId)
      relatedInvoices.value = result.invoices
      unattributedInvoices.value = result.unattributedInvoiceCount
      invoicesLoaded.value = true
    } catch (e: unknown) {
      invoicesError.value = apiErrorMessage(e)
      relatedInvoices.value = []
      unattributedInvoices.value = 0
    } finally {
      invoicesLoading.value = false
    }
  }

  /**
   * Asks the API for a one-time control-panel sign-in address.
   *
   * @param serviceId - Service id.
   * @returns The address to open, or null when none could be obtained
   * ({@link ssoError} holds the sentence to show). A call made while one is in flight returns null.
   */
  const fetchCpanelUrl = async (serviceId: string): Promise<string | null> => {
    if (ssoLoading.value) {
      return null
    }
    ssoLoading.value = true
    ssoError.value = ''
    try {
      const { url } = await api.fetchCpanelSsoUrl(serviceId)
      // An empty address would open a blank tab, which reads as a panel that failed to load.
      if (!url) {
        throw new Error('No control-panel address was returned.')
      }
      return url
    } catch (e: unknown) {
      ssoError.value = apiErrorMessage(e)
      return null
    } finally {
      ssoLoading.value = false
    }
  }

  /**
   * Opens a service's control panel in a new tab: through single sign-on when the API gives an
   * address, else the panel's plain sign-in page on the service's server. The one place this
   * choice is made, so every button that opens the panel behaves the same.
   *
   * @param serviceId - Service id.
   * @param hostname - The service's server host name; null or absent when the server is not known.
   * @returns Nothing; a failed single sign-in leaves its sentence in {@link ssoError}.
   */
  const openControlPanel = async (serviceId: string, hostname?: string | null): Promise<void> => {
    const url = await fetchCpanelUrl(serviceId)
    if (url) {
      window.open(url, '_blank', 'noopener')
      return
    }
    if (hostname) {
      window.open(controlPanelUrl(hostname), '_blank', 'noopener')
    }
  }

  /**
   * Completes first-time setup on a newly provisioned service.
   *
   * On success the cached service list is marked stale so list pages refetch.
   *
   * @param serviceId - Service id.
   * @param data - Domain and control-panel credentials.
   * @returns How the attempt ended; on failure {@link setupError} holds the sentence to show.
   */
  const setUpService = async (
    serviceId: string,
    data: Record<string, unknown>
  ): Promise<ActionOutcome> => {
    setupSubmitting.value = true
    setupError.value = ''
    try {
      await api.setUpService(serviceId, data)
      useClientStore().servicesLoaded = false
      return ActionOutcome.Succeeded
    } catch (e: unknown) {
      setupError.value = apiErrorMessage(e)
      return ActionOutcome.Failed
    } finally {
      setupSubmitting.value = false
    }
  }

  return {
    service,
    serviceLoading,
    serviceError,
    serviceNotFound,
    hostingInfo,
    cancelStatus,
    loadService,
    loadHostingInfo,
    loadCancellationStatus,
    passwordChanging,
    passwordSuccess,
    passwordError,
    cancelSubmitting,
    cancelError,
    cancelDone,
    relatedInvoices,
    unattributedInvoices,
    invoicesLoading,
    invoicesLoaded,
    invoicesError,
    ssoLoading,
    ssoError,
    setupSubmitting,
    setupError,
    reset,
    changePassword,
    cancelService,
    loadInvoices,
    fetchCpanelUrl,
    openControlPanel,
    setUpService
  }
})
