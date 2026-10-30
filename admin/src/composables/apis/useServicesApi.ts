/**
 * Endpoints behind the services store: the service list, one service's detail, the per-section saves and the
 * provisioning commands that act on the hosting server.
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import type { PaginatedList } from '@/types/paginatedlist'
import {
  type AddExistingServicePayload,
  type ProvisioningReasonPayload,
  type ServiceAutomationPayload,
  type ServiceBillingTermsPayload,
  type ServiceDetail,
  type ServiceFilter,
  type ServiceHostingAccountPayload,
  type ServiceListItem,
  type ServiceNotesPayload,
  ServiceSection,
  type ServiceStatusPayload,
  type ServiceTerminationDatePayload,
} from '@/types/service'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'

/**
 * Endpoint functions for `/services` and `/provisioning`.
 *
 * @returns One typed function per endpoint.
 */
export const useServicesApi = () => {
  const { request } = useApi()

  /**
   * Lists services, one page.
   *
   * @param filter - Page, page size and owning client.
   * @returns The page and the total count.
   */
  const listServices = (filter: ServiceFilter): Promise<PaginatedList<ServiceListItem>> =>
    request<PaginatedList<ServiceListItem>>(
      `${ApiPath.Services}${buildQuery({ clientId: filter.clientId, page: filter.page, pageSize: filter.pageSize })}`,
    )

  /**
   * Reads one service.
   *
   * @param id - Service id.
   * @returns The service detail.
   */
  const getService = (id: number | string): Promise<ServiceDetail> => request<ServiceDetail>(`${ApiPath.Services}/${id}`)

  /**
   * PUTs one section of a service to its own endpoint.
   *
   * @param id - Service id.
   * @param section - The section's path segment.
   * @param payload - The section's fields.
   * @returns Nothing.
   */
  const putSection = (id: number | string, section: ServiceSection, payload: object): Promise<void> =>
    request<void>(`${ApiPath.Services}/${id}/${section}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Saves what a service is billed.
   *
   * @param id - Service id.
   * @param payload - The billing terms.
   * @returns Nothing.
   */
  const updateBillingTerms = (id: number | string, payload: ServiceBillingTermsPayload): Promise<void> =>
    putSection(id, ServiceSection.Billing, payload)

  /**
   * Saves the hosting account record of a service.
   *
   * @param id - Service id.
   * @param payload - The hosting account details.
   * @returns Nothing.
   */
  const updateHostingAccount = (id: number | string, payload: ServiceHostingAccountPayload): Promise<void> =>
    putSection(id, ServiceSection.HostingAccount, payload)

  /**
   * Saves the suspension hold and the end-of-cycle termination of a service.
   *
   * @param id - Service id.
   * @param payload - The automation settings.
   * @returns Nothing.
   */
  const updateAutomation = (id: number | string, payload: ServiceAutomationPayload): Promise<void> =>
    putSection(id, ServiceSection.Automation, payload)

  /**
   * Saves the admin notes of a service.
   *
   * @param id - Service id.
   * @param payload - The notes.
   * @returns Nothing.
   */
  const updateNotes = (id: number | string, payload: ServiceNotesPayload): Promise<void> =>
    putSection(id, ServiceSection.Notes, payload)

  /**
   * Moves a service to another status.
   *
   * @param id - Service id.
   * @param payload - The status asked for.
   * @returns Nothing.
   */
  const changeStatus = (id: number | string, payload: ServiceStatusPayload): Promise<void> =>
    putSection(id, ServiceSection.Status, payload)

  /**
   * Corrects the termination date of a terminated service.
   *
   * @param id - Service id.
   * @param payload - The date the service really ended.
   * @returns Nothing.
   */
  const correctTerminationDate = (id: number | string, payload: ServiceTerminationDatePayload): Promise<void> =>
    putSection(id, ServiceSection.TerminationDate, payload)

  /**
   * Asks the provisioning server for a one-time control-panel login URL.
   *
   * @param id - Service id.
   * @returns The URL.
   */
  const getPanelLoginUrl = (id: number | string): Promise<{ url: string }> =>
    request<{ url: string }>(`${ApiPath.Provisioning}/${id}/cpanel-sso`)

  /**
   * Provisions a pending service.
   *
   * @param id - Service id.
   * @returns Nothing.
   */
  const provisionService = (id: number | string): Promise<void> =>
    request<void>(`${ApiPath.Provisioning}/${id}/provision`, { method: HttpMethod.Post })

  /**
   * Changes the hosting account password.
   *
   * @param id - Service id.
   * @param newPassword - The new password.
   * @returns Nothing.
   */
  const changePassword = (id: number | string, newPassword: string): Promise<void> =>
    request<void>(`${ApiPath.Provisioning}/${id}/change-password`, { method: HttpMethod.Post, body: JSON.stringify({ newPassword }) })

  /**
   * Changes the hosting package.
   *
   * @param id - Service id.
   * @param newPackage - The new package name.
   * @returns Nothing.
   */
  const changePackage = (id: number | string, newPackage: string): Promise<void> =>
    request<void>(`${ApiPath.Provisioning}/${id}/change-package`, { method: HttpMethod.Post, body: JSON.stringify({ newPackage }) })

  /**
   * Suspends a service.
   *
   * @param id - Service id.
   * @param payload - The reason recorded with the suspension.
   * @returns Nothing.
   */
  const suspendService = (id: number | string, payload: ProvisioningReasonPayload): Promise<void> =>
    request<void>(`${ApiPath.Provisioning}/${id}/suspend`, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Unsuspends a service.
   *
   * @param id - Service id.
   * @returns Nothing.
   */
  const unsuspendService = (id: number | string): Promise<void> =>
    request<void>(`${ApiPath.Provisioning}/${id}/unsuspend`, { method: HttpMethod.Post })

  /**
   * Terminates a service.
   *
   * @param id - Service id.
   * @param payload - The reason recorded with the termination.
   * @returns Nothing.
   */
  const terminateService = (id: number | string, payload: ProvisioningReasonPayload): Promise<void> =>
    request<void>(`${ApiPath.Provisioning}/${id}/terminate`, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Adds a service for a hosting account that already exists on a server; nothing is provisioned.
   *
   * @param payload - The client, product, billing terms and account.
   * @returns The new service's id.
   */
  const addExistingService = (payload: AddExistingServicePayload): Promise<number> =>
    request<number>(`${ApiPath.Services}/existing`, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  return {
    addExistingService, listServices, getService, updateBillingTerms, updateHostingAccount, updateAutomation, updateNotes,
    changeStatus, correctTerminationDate, getPanelLoginUrl,
    provisionService, changePassword, changePackage, suspendService, unsuspendService, terminateService,
  }
}
