/**
 * Endpoints under `/api/portal/client` — the signed-in client's own account and the four
 * collections hanging off it.
 *
 * This file is the list of what that backend area offers, and nothing else: no `ref`, no
 * `computed`, no caching, no error handling. Loading flags, cached results and error strings
 * live in `stores/client.ts`, which is the only intended caller.
 *
 * @module composables/apis/useClientApi
 */

import { apiFetch, useApi } from '~/composables/useApi'
import type { ClientUser } from '~/types/clientuser'
import type { ClientService } from '~/types/clientservice'
import type { ClientInvoice } from '~/types/clientinvoice'
import type { ClientDomain } from '~/types/clientdomain'
import type { ClientTicket } from '~/types/clientticket'

/**
 * The `/api/portal/client` surface, one function per endpoint.
 *
 * @returns The client-area endpoint functions.
 */
export function useClientApi() {
  /**
   * Reads the signed-in client's profile.
   *
   * @returns The client profile.
   * @throws Whatever `apiFetch` throws — notably the `client_profile_not_found` answer for an
   * identity that has no client record. Callers decide what that means; this layer does not.
   */
  const fetchMe = (): Promise<ClientUser> =>
    apiFetch<ClientUser>('/api/portal/client/me')

  /**
   * Lists the client's hosting services.
   *
   * @returns Every service on the account.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchServices = (): Promise<ClientService[]> =>
    apiFetch<ClientService[]>('/api/portal/client/services')

  /**
   * Lists the client's invoices.
   *
   * @returns Every invoice on the account, paid and unpaid alike.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchInvoices = (): Promise<ClientInvoice[]> =>
    apiFetch<ClientInvoice[]>('/api/portal/client/invoices')

  /**
   * Lists the client's domains.
   *
   * @returns Every domain on the account.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchDomains = (): Promise<ClientDomain[]> =>
    apiFetch<ClientDomain[]>('/api/portal/client/domains')

  /**
   * Lists the client's support tickets.
   *
   * @returns Every ticket on the account, open and closed alike.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchTickets = (): Promise<ClientTicket[]> =>
    apiFetch<ClientTicket[]>('/api/portal/client/tickets')


  /**
   * Updates the signed-in client's profile.
   *
   * @param profile - The fields to change, including the email preference flags.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const updateMe = (profile: Record<string, unknown>): Promise<unknown> =>
    apiFetch('/api/portal/client/me', { method: 'PUT', body: profile })

  /**
   * Reads the signed-in client's profile as a one-shot call.
   *
   * @returns The client profile.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchMeOnce = (): Promise<Record<string, string>> =>
    apiFetch<Record<string, string>>('/api/portal/client/me')

  /**
   * Reads the people who may sign in to this client account.
   *
   * @returns The `useApi()` handle for the user list, defaulting to an empty list.
   */
  const loadUsers = <T>() =>
    useApi<T[]>('/api/portal/client/users', { default: () => [] })

  /**
   * Invites somebody to this client account.
   *
   * @param invite - Email and the permissions to grant.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const inviteUser = (invite: Record<string, unknown>): Promise<unknown> =>
    apiFetch('/api/portal/client/users/invite', { method: 'POST', body: invite })

  /**
   * Removes somebody's access to this client account.
   *
   * @param id - The user to remove.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const removeUser = (id: string): Promise<unknown> =>
    apiFetch(`/api/portal/client/users/${id}`, { method: 'DELETE' })

  /**
   * Reads the account's sub-contacts.
   *
   * @returns The `useApi()` handle for the contact list, defaulting to an empty list.
   */
  const loadContacts = <T>() =>
    useApi<T[]>('/api/portal/client/contacts', { default: () => [] })

  /**
   * Adds a sub-contact.
   *
   * @param contact - The contact's details.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const createContact = (contact: Record<string, unknown>): Promise<unknown> =>
    apiFetch('/api/portal/client/contacts', { method: 'POST', body: contact })

  /**
   * Updates a sub-contact.
   *
   * @param id - The contact to update.
   * @param contact - The fields to change.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const updateContact = (id: string | number, contact: Record<string, unknown>): Promise<unknown> =>
    apiFetch(`/api/portal/client/contacts/${id}`, { method: 'PUT', body: contact })

  /**
   * Removes a sub-contact.
   *
   * @param id - The contact to delete.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const removeContact = (id: string | number): Promise<unknown> =>
    apiFetch(`/api/portal/client/contacts/${id}`, { method: 'DELETE' })

  /**
   * Reads the billing addresses saved on the account.
   *
   * @returns Every saved address.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchAddresses = <T>(): Promise<T[]> =>
    apiFetch<T[]>('/api/portal/client/addresses')

  /**
   * Saves a new billing address.
   *
   * @param address - The address fields.
   * @returns The new address's id, so the caller can select it.
   * @throws Whatever `apiFetch` throws.
   */
  const createAddress = (address: Record<string, unknown>): Promise<{ addressid: string }> =>
    apiFetch<{ addressid: string }>('/api/portal/client/addresses', {
      method: 'POST',
      body: address
    })

  /**
   * Reads the sub-contacts as a one-shot call.
   *
   * @returns Every sub-contact on the account.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchContacts = <T>(): Promise<T[]> =>
    apiFetch<T[]>('/api/portal/client/contacts')

  /**
   * Reads the mail the system has sent this account.
   *
   * @returns The `useApi()` handle for the message list, defaulting to an empty list.
   */
  const loadEmails = <T>() =>
    useApi<T[]>('/api/portal/client/emails', { default: () => [] })

  /**
   * Reads one of the messages the system sent this account.
   *
   * @param id - Message id; a getter so the page re-reads when the route changes.
   * @returns The `useApi()` handle for the message.
   */
  const loadEmail = <T>(id: () => string) =>
    useApi<T>(() => `/api/portal/client/emails/${id()}`)

  /**
   * Reads one hosting service.
   *
   * @param id - Service id; a getter so the page re-reads when the route changes.
   * @returns The `useApi()` handle for the service.
   */
  const loadService = <T>(id: () => string) =>
    useApi<T>(() => `/api/portal/client/services/${id()}`)

  /**
   * Reads a sub-resource of one hosting service — `hosting-info`, `ssh-info`,
   * `cancellation-status` and the like.
   *
   * @param id - Service id; a getter so the page re-reads when the route changes.
   * @param resource - Path segment under the service, e.g. "hosting-info".
   * @param defaultValue - What `data` holds before the first response. These sub-resources
   * are all optional extras, so a page renders "not available" rather than guarding on null.
   * @returns The `useApi()` handle for that sub-resource.
   */
  const loadServiceResource = <T>(id: () => string, resource: string, defaultValue?: () => T) =>
    useApi<T>(() => `/api/portal/client/services/${id()}/${resource}`, { default: defaultValue })

  /**
   * Reads the invoices raised for one hosting service.
   *
   * @param id - Service id.
   * @returns The service's invoices.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchServiceInvoices = <T>(id: string): Promise<T> =>
    apiFetch<T>(`/api/portal/client/services/${id}/invoices`)

  /**
   * Changes the control-panel password on one hosting service.
   *
   * @param id - Service id.
   * @param password - The password to set.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const changeServicePassword = (id: string, password: string): Promise<unknown> =>
    apiFetch(`/api/portal/client/services/${id}/change-password`, {
      method: 'POST',
      body: { password }
    })

  /**
   * Requests cancellation of one hosting service.
   *
   * @param id - Service id.
   * @param body - Reason and whether to cancel now or at the end of the term.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const cancelService = (id: string, body: Record<string, unknown>): Promise<unknown> =>
    apiFetch(`/api/portal/client/services/${id}/cancel`, { method: 'POST', body })

  /**
   * Mints a single-sign-on URL into the service's cPanel.
   *
   * @param id - Service id.
   * @returns The URL to open; it is short-lived, so it is fetched at click time rather than
   * rendered into the page.
   * @throws Whatever `apiFetch` throws — the caller falls back to the plain login page.
   */
  const fetchCpanelSsoUrl = (id: string): Promise<{ url: string }> =>
    apiFetch<{ url: string }>(`/api/portal/client/services/${id}/cpanel-sso`)

  /**
   * Completes first-time setup on a newly provisioned hosting service.
   *
   * @param id - Service id.
   * @param body - The setup answers.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const setUpService = (id: string, body: Record<string, unknown>): Promise<unknown> =>
    apiFetch(`/api/portal/client/services/${id}/setup`, { method: 'POST', body })

  return {
    fetchMe,
    fetchMeOnce,
    updateMe,
    fetchServices,
    fetchInvoices,
    fetchDomains,
    fetchTickets,
    loadUsers,
    inviteUser,
    removeUser,
    loadContacts,
    fetchContacts,
    createContact,
    updateContact,
    removeContact,
    fetchAddresses,
    createAddress,
    loadEmails,
    loadEmail,
    loadService,
    loadServiceResource,
    fetchServiceInvoices,
    changeServicePassword,
    cancelService,
    fetchCpanelSsoUrl,
    setUpService
  }
}
