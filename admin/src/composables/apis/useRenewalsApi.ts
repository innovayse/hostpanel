/**
 * Endpoints behind the renewals store: the upcoming services and domains, the auto-renew switches, the manual
 * renewal invoice run and the release of a claimed domain renewal line.
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */
import { buildQuery, useApi } from '@/composables/useApi'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'
import type { DomainRegistration } from '@/types/domain'
import type { PaginatedList } from '@/types/paginatedlist'
import type { ClaimedDomainRenewal, RenewalWindow, UpcomingServiceRenewal } from '@/types/renewal'

/**
 * Endpoint functions for the renewals screen.
 *
 * @returns One typed function per endpoint.
 */
export const useRenewalsApi = () => {
  /** Transport call of the shared API module. */
  const { request } = useApi()

  /**
   * Lists the services renewing inside a window, soonest first.
   *
   * @param window - How far ahead to look.
   * @param page - Page number (1-based).
   * @param pageSize - Items per page.
   * @returns One page of services.
   */
  const listServices = (window: RenewalWindow, page: number, pageSize: number): Promise<PaginatedList<UpcomingServiceRenewal>> =>
    request<PaginatedList<UpcomingServiceRenewal>>(`${ApiPath.Services}/renewals/upcoming${buildQuery({ window, page, pageSize })}`)

  /**
   * Lists the domains expiring inside a window, soonest first.
   *
   * @param window - How far ahead to look.
   * @param page - Page number (1-based).
   * @param pageSize - Items per page.
   * @returns One page of domains.
   */
  const listDomains = (window: RenewalWindow, page: number, pageSize: number): Promise<PaginatedList<DomainRegistration>> =>
    request<PaginatedList<DomainRegistration>>(`${ApiPath.Domains}/renewals/upcoming${buildQuery({ window, page, pageSize })}`)

  /**
   * Switches the automatic billing of a service on or off.
   *
   * @param id - Service id.
   * @param value - The wanted state.
   * @returns Nothing.
   */
  const setServiceAutoRenew = (id: number, value: boolean): Promise<void> =>
    request<void>(`${ApiPath.Services}/${id}/auto-renew`, { method: HttpMethod.Put, body: JSON.stringify({ value }) })

  /**
   * Switches the automatic renewal of a domain on or off.
   *
   * @param id - Domain id.
   * @param value - The wanted state.
   * @returns Nothing.
   */
  const setDomainAutoRenew = (id: number, value: boolean): Promise<void> =>
    request<void>(`${ApiPath.Domains}/${id}/auto-renew`, { method: HttpMethod.Put, body: JSON.stringify({ value }) })

  /**
   * Raises the renewal invoices a client is due now.
   *
   * @param clientId - Client id.
   * @returns How many invoices were raised; 0 when nothing is due yet.
   */
  const generateInvoices = (clientId: number): Promise<number> =>
    request<number>(`${ApiPath.Billing}/client/${clientId}/renewal-invoices`, { method: HttpMethod.Post })

  /**
   * Lists the domain renewal lines left claimed after an unknown registrar answer.
   *
   * @returns The claimed lines, oldest claim first.
   */
  const listClaims = (): Promise<ClaimedDomainRenewal[]> =>
    request<ClaimedDomainRenewal[]>(`${ApiPath.Billing}/renewal-claims`)

  /**
   * Releases a claimed domain renewal line so the next renewal run sends it again.
   *
   * @param invoiceId - Invoice id.
   * @param itemId - Line id.
   * @returns Nothing.
   */
  const releaseClaim = (invoiceId: number, itemId: number): Promise<void> =>
    request<void>(`${ApiPath.Billing}/${invoiceId}/items/${itemId}/release-renewal-claim`, { method: HttpMethod.Post })

  return { listServices, listDomains, setServiceAutoRenew, setDomainAutoRenew, generateInvoices, listClaims, releaseClaim }
}
