/**
 * Endpoints under `/api/portal/client/domains`: one domain and everything that can be
 * changed about it.
 *
 * The public availability check is *not* here: it lives in `useCatalogApi` with the rest of
 * `/api/portal/public`, because an API file is named for the backend surface it covers rather
 * than for the screen that happens to call it.
 *
 * Every function returns a plain `Promise`. Nothing here is cached or held.
 *
 * @module composables/apis/useDomainsApi
 */

import { apiFetch } from '~/composables/useApi'
import type { DomainContactFields } from '~/types/domaincontactfields'
import type { DomainDetail } from '~/types/domaindetail'
import type { RegistrantContact } from '~/types/registrantcontact'
import type { WhoisRecord } from '~/types/whoisrecord'
import { HttpMethod } from '~/types/httpmethod'

/**
 * The client-domain surface, one function per endpoint.
 *
 * @returns The domain endpoint functions.
 */
export const useDomainsApi = () => {
  /**
   * Reads one domain.
   *
   * @param id - Domain id.
   * @returns The domain.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchDomain = (id: string): Promise<DomainDetail> =>
    apiFetch<DomainDetail>(`/api/portal/client/domains/${id}`)

  /**
   * Switches automatic renewal on or off.
   *
   * The backend reads this switch from `value` and refuses a body without it.
   *
   * @param id - Domain id.
   * @param enabled - True to renew automatically at expiry.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const setAutoRenew = (id: string, enabled: boolean): Promise<unknown> =>
    apiFetch(`/api/portal/client/domains/${id}/autorenew`, {
      method: HttpMethod.Put,
      body: { value: enabled }
    })

  /**
   * Locks or unlocks the domain against transfer away.
   *
   * The field is `value`, not `locked`: the three client toggles on a domain share one request
   * shape, and the backend reads the same key for each. A body without `value` is refused with 400,
   * so a wrong key can never switch the lock off.
   *
   * @param id - Domain id.
   * @param enabled - True to lock.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const setLock = (id: string, enabled: boolean): Promise<unknown> =>
    apiFetch(`/api/portal/client/domains/${id}/lock`, {
      method: HttpMethod.Put,
      body: { value: enabled }
    })

  /**
   * Switches WHOIS privacy on or off.
   *
   * The backend reads this switch from `value` and refuses a body without it.
   *
   * @param id - Domain id.
   * @param enabled - True to hide the registrant's details.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const setIdProtect = (id: string, enabled: boolean): Promise<unknown> =>
    apiFetch(`/api/portal/client/domains/${id}/idprotect`, {
      method: HttpMethod.Put,
      body: { value: enabled }
    })

  /**
   * Replaces the domain's nameservers.
   *
   * @param id - Domain id.
   * @param nameservers - The nameservers to set, in order. Blank slots are the caller's to
   * drop: the backend takes the list it is given as the complete set.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const setNameservers = (id: string, nameservers: string[]): Promise<unknown> =>
    apiFetch(`/api/portal/client/domains/${id}/nameservers`, {
      method: HttpMethod.Put,
      body: { nameservers }
    })

  /**
   * Replaces the domain's WHOIS registrant contact.
   *
   * @param id - Domain id.
   * @param contact - The registrant contact; sent as `{ contact }`, the backend's
   * `ModifyMyDomainContactCommand` body.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const setWhois = (id: string, contact: DomainContactFields): Promise<unknown> =>
    apiFetch(`/api/portal/client/domains/${id}/whois`, {
      method: HttpMethod.Put,
      body: { contact }
    })

  /**
   * Reads the registry's WHOIS record of the domain.
   *
   * @param id - Domain id.
   * @returns The record: registrar, registrant and the registration dates.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchWhois = (id: string): Promise<WhoisRecord> =>
    apiFetch<WhoisRecord>(`/api/portal/client/domains/${id}/whois`)

  /**
   * Reads the WHOIS registrant contact the registrar currently holds for the domain.
   *
   * @param id - Domain id.
   * @returns The registrant contact, the backend's `DomainContactDto`.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchContact = (id: string): Promise<RegistrantContact> =>
    apiFetch<RegistrantContact>(`/api/portal/client/domains/${id}/contact`)

  /**
   * Asks the registrar for the domain's transfer authorisation code.
   *
   * A POST rather than a GET because most registrars mail the code and rate-limit the
   * request; asking for it is an action, not a read.
   *
   * @param id - Domain id.
   * @returns The EPP code.
   * @throws Whatever `apiFetch` throws.
   */
  const requestEppCode = (id: string): Promise<{ eppCode: string }> =>
    apiFetch<{ eppCode: string }>(`/api/portal/client/domains/${id}/epp`, { method: HttpMethod.Post })

  /**
   * Raises a renewal order for the domain.
   *
   * @param id - Domain id.
   * @param years - How many years to renew for.
   * @param paymentMethod - Gateway module to bill through.
   * @returns The new order and invoice ids.
   * @throws Whatever `apiFetch` throws.
   */
  const createRenewOrder = (
    id: string,
    years: number,
    paymentMethod: string
  ): Promise<{ orderId: number, invoiceId: number }> =>
    apiFetch<{ orderId: number, invoiceId: number }>(
      `/api/portal/client/domains/${id}/renew-order`,
      { method: HttpMethod.Post, body: { years, paymentmethod: paymentMethod } }
    )

  /**
   * Raises an order to transfer a domain in from another registrar.
   *
   * @param transfer - Domain, auth code, period and payment method.
   * @returns The new order and invoice ids.
   * @throws Whatever `apiFetch` throws.
   */
  const createTransferOrder = (
    transfer: Record<string, unknown>
  ): Promise<{ orderId: number, invoiceId: number }> =>
    apiFetch<{ orderId: number, invoiceId: number }>(
      '/api/portal/client/domains/transfer-order',
      { method: HttpMethod.Post, body: transfer }
    )

  return {
    fetchDomain,
    setAutoRenew,
    setLock,
    setIdProtect,
    setNameservers,
    fetchWhois,
    fetchContact,
    setWhois,
    requestEppCode,
    createRenewOrder,
    createTransferOrder
  }
}
