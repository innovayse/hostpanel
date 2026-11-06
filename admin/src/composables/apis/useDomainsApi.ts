/**
 * Endpoints behind the domains store: the registration list, one domain's detail and every
 * change made to it (fields, registrar commands, DNS records, email forwarding).
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import type {
  DnsRecordPayload,
  DomainContactPayload,
  DomainDetail,
  DomainFilter,
  DomainRegistration,
  DomainToggle,
  ForwardingRulePayload,
  UpdateDomainPayload,
} from '@/types/domain'
import type { PaginatedList } from '@/types/paginatedlist'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'

/**
 * Endpoint functions for `/domains`.
 *
 * @returns One typed function per endpoint.
 */
export const useDomainsApi = () => {
  const { request } = useApi()

  /**
   * Lists domain registrations, one page.
   *
   * @param filter - Page, page size, name search and owning client.
   * @returns The page and the total count.
   */
  const listDomains = (filter: DomainFilter): Promise<PaginatedList<DomainRegistration>> =>
    request<PaginatedList<DomainRegistration>>(
      `/domains${buildQuery({
        clientId: filter.clientId,
        page: filter.page,
        pageSize: filter.pageSize,
        search: filter.search,
      })}`,
    )

  /**
   * Reads one domain with its nameservers, DNS records and forwarding rules.
   *
   * @param id - Domain id.
   * @returns The domain detail.
   */
  const getDomain = (id: number | string): Promise<DomainDetail> => request<DomainDetail>(`${ApiPath.Domains}/${id}`)

  /**
   * Saves the editable fields of a domain.
   *
   * @param id - Domain id.
   * @param payload - The editable fields.
   * @returns Nothing.
   */
  const updateDomain = (id: number | string, payload: UpdateDomainPayload): Promise<void> =>
    request<void>(`${ApiPath.Domains}/${id}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Sends the register command to the registrar.
   *
   * @param id - Domain id.
   * @returns Nothing.
   */
  const registerDomain = (id: number | string): Promise<void> =>
    request<void>(`${ApiPath.Domains}/${id}/register`, { method: HttpMethod.Post })

  /**
   * Renews a domain for more years.
   *
   * @param id - Domain id.
   * @param years - Number of years to add.
   * @returns Nothing.
   */
  const renewDomain = (id: number | string, years: number): Promise<void> =>
    request<void>(`${ApiPath.Domains}/${id}/renew`, { method: HttpMethod.Post, body: JSON.stringify({ years }) })

  /**
   * Turns auto-renew on or off.
   *
   * The API reads this switch from `value`, unlike the other domain toggles that read `enabled`.
   * Sending `enabled` binds nothing, so the flag silently falls back to false and switches
   * auto-renew off whatever the admin chose.
   *
   * @param id - Domain id.
   * @param toggle - The wanted state.
   * @returns Nothing.
   */
  const setAutoRenew = (id: number | string, toggle: DomainToggle): Promise<void> =>
    request<void>(`${ApiPath.Domains}/${id}/auto-renew`, {
      method: HttpMethod.Put,
      body: JSON.stringify({ value: toggle.enabled }),
    })

  /**
   * Turns WHOIS privacy on or off.
   *
   * @param id - Domain id.
   * @param toggle - The wanted state.
   * @returns Nothing.
   */
  const setWhoisPrivacy = (id: number | string, toggle: DomainToggle): Promise<void> =>
    request<void>(`${ApiPath.Domains}/${id}/whois-privacy`, { method: HttpMethod.Put, body: JSON.stringify(toggle) })

  /**
   * Turns the registrar lock on or off.
   *
   * @param id - Domain id.
   * @param toggle - The wanted state.
   * @returns Nothing.
   */
  const setLock = (id: number | string, toggle: DomainToggle): Promise<void> =>
    request<void>(`${ApiPath.Domains}/${id}/lock`, { method: HttpMethod.Put, body: JSON.stringify(toggle) })

  /**
   * Turns DNS management on or off.
   *
   * @param id - Domain id.
   * @param toggle - The wanted state.
   * @returns Nothing.
   */
  const setDnsManagement = (id: number | string, toggle: DomainToggle): Promise<void> =>
    request<void>(`${ApiPath.Domains}/${id}/dns-management`, { method: HttpMethod.Put, body: JSON.stringify(toggle) })

  /**
   * Turns email forwarding on or off.
   *
   * @param id - Domain id.
   * @param toggle - The wanted state.
   * @returns Nothing.
   */
  const setEmailForwarding = (id: number | string, toggle: DomainToggle): Promise<void> =>
    request<void>(`${ApiPath.Domains}/${id}/email-forwarding-toggle`, { method: HttpMethod.Put, body: JSON.stringify(toggle) })

  /**
   * Changes the WHOIS registrant contact.
   *
   * @param id - Domain id.
   * @param contact - The contact fields.
   * @returns Nothing.
   */
  const modifyContact = (id: number | string, contact: DomainContactPayload): Promise<void> =>
    request<void>(`${ApiPath.Domains}/${id}/modify-contact`, { method: HttpMethod.Post, body: JSON.stringify(contact) })

  /**
   * Starts an outgoing transfer and reads back the EPP code.
   *
   * @param id - Domain id.
   * @returns The EPP authorization code.
   */
  const initiateOutgoingTransfer = (id: number | string): Promise<{ eppCode: string }> =>
    request<{ eppCode: string }>(`${ApiPath.Domains}/${id}/initiate-outgoing-transfer`, { method: HttpMethod.Post })

  /**
   * Adds a DNS record.
   *
   * @param id - Domain id.
   * @param payload - The record.
   * @returns Nothing.
   */
  const addDnsRecord = (id: number | string, payload: DnsRecordPayload): Promise<void> =>
    request<void>(`${ApiPath.Domains}/${id}/dns`, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Replaces a DNS record.
   *
   * @param id - Domain id.
   * @param recordId - Record id.
   * @param payload - The record.
   * @returns Nothing.
   */
  const updateDnsRecord = (id: number | string, recordId: number, payload: DnsRecordPayload): Promise<void> =>
    request<void>(`${ApiPath.Domains}/${id}/dns/${recordId}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Deletes a DNS record.
   *
   * @param id - Domain id.
   * @param recordId - Record id.
   * @returns Nothing.
   */
  const deleteDnsRecord = (id: number | string, recordId: number): Promise<void> =>
    request<void>(`${ApiPath.Domains}/${id}/dns/${recordId}`, { method: HttpMethod.Delete })

  /**
   * Adds an email forwarding rule.
   *
   * @param id - Domain id.
   * @param payload - Source alias and destination address.
   * @returns Nothing.
   */
  const addForwardingRule = (id: number | string, payload: ForwardingRulePayload): Promise<void> =>
    request<void>(`${ApiPath.Domains}/${id}/email-forwarding`, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Replaces an email forwarding rule.
   *
   * @param id - Domain id.
   * @param ruleId - Rule id.
   * @param payload - Source, destination and active flag.
   * @returns Nothing.
   */
  const updateForwardingRule = (id: number | string, ruleId: number, payload: ForwardingRulePayload): Promise<void> =>
    request<void>(`${ApiPath.Domains}/${id}/email-forwarding/${ruleId}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Deletes an email forwarding rule.
   *
   * @param id - Domain id.
   * @param ruleId - Rule id.
   * @returns Nothing.
   */
  const deleteForwardingRule = (id: number | string, ruleId: number): Promise<void> =>
    request<void>(`${ApiPath.Domains}/${id}/email-forwarding/${ruleId}`, { method: HttpMethod.Delete })

  return {
    listDomains, getDomain, updateDomain, registerDomain, renewDomain,
    setAutoRenew, setWhoisPrivacy, setLock, setDnsManagement, setEmailForwarding,
    modifyContact, initiateOutgoingTransfer,
    addDnsRecord, updateDnsRecord, deleteDnsRecord,
    addForwardingRule, updateForwardingRule, deleteForwardingRule,
  }
}
