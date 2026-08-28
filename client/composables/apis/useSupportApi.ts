/**
 * Endpoints for getting help — support tickets under `/api/portal/client/tickets`, and the
 * public contact form at `/api/contact`.
 *
 * Two naming shapes, as elsewhere in `composables/apis/`: `load*` returns the `useApi()`
 * handle for a server-rendered read, `fetch*` / verb-named functions return a plain `Promise`
 * for a one-shot call. Nothing here is cached or held.
 *
 * @module composables/apis/useSupportApi
 */

import { apiFetch, useApi } from '~/composables/useApi'
import type { ContactMessage } from '~/types/contactmessage'
import type { NewTicket } from '~/types/newticket'
import type { TicketThread } from '~/types/ticketthread'

/**
 * The support surface, one function per endpoint.
 *
 * @returns The support endpoint functions.
 */
export function useSupportApi() {
  /**
   * Reads one ticket with its whole reply thread.
   *
   * @param id - Ticket id; a getter so the page re-reads when the route changes.
   * @returns The `useApi()` handle for the ticket.
   */
  const loadTicket = (id: () => string) =>
    useApi<TicketThread>(() => `/api/portal/client/tickets/${id()}`)

  /**
   * Server-rendered read of the departments a ticket can be routed to.
   *
   * Under `/api/portal/public`, but it belongs to this file rather than to `useCatalogApi`:
   * the list only exists to fill the ticket form's department picker.
   *
   * @returns The `useApi()` handle for the department list, defaulting to an empty list.
   */
  const loadDepartments = <T>() =>
    useApi<T[]>('/api/portal/public/departments', { default: () => [] })

  /**
   * Opens a support ticket.
   *
   * @param ticket - Department, subject, first message and priority.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const createTicket = (ticket: NewTicket): Promise<unknown> =>
    apiFetch('/api/portal/client/tickets', { method: 'POST', body: ticket })

  /**
   * Posts a reply onto an existing ticket.
   *
   * @param id - Ticket id.
   * @param message - The reply body.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const replyToTicket = (id: string, message: string): Promise<unknown> =>
    apiFetch(`/api/portal/client/tickets/${id}/reply`, { method: 'POST', body: { message } })

  /**
   * Sends a message from the public contact form.
   *
   * Not under `/api/portal`: this one is answered by the Nuxt server itself, which relays to
   * mail and Telegram rather than to the C# backend.
   *
   * @param message - The form's fields.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const sendContactMessage = (message: ContactMessage): Promise<unknown> =>
    apiFetch('/api/contact', { method: 'POST', body: message })

  return { loadTicket, loadDepartments, createTicket, replyToTicket, sendContactMessage }
}
