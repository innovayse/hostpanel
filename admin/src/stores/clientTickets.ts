/**
 * Pinia store for the tickets of one client profile.
 *
 * Lists, opens, creates, updates, deletes and answers tickets through `useClientTicketsApi`.
 * A refusal becomes the API sentence in `error` (and the per-field messages in
 * `fieldErrors`); the statistics lookup is non-critical and fails silently, while a failed
 * department lookup is kept in `departmentsError` so a form can offer a retry.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useClientTicketsApi } from '@/composables/apis/useClientTicketsApi'
import { runAction } from '@/lib/runAction'
import type { ClientTicketStats } from '@/types/client'
import type { Department } from '@/types/department'
import type { ClientTicketListFilters, CreateTicketPayload, Ticket, TicketListItem, UpdateTicketPayload } from '@/types/ticket'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'

/** Loads and changes the support tickets of a client. */
export const useClientTicketsStore = defineStore('clientTickets', () => {
  const api = useClientTicketsApi()

  /** Paginated list of tickets for the current client. */
  const tickets = ref<TicketListItem[]>([])

  /** Total count of tickets matching the current query. */
  const totalCount = ref(0)

  /** Items per page. */
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  /** Ticket statistics for summary cards. */
  const stats = ref<ClientTicketStats>({ openedThisMonth: 0, openedLastMonth: 0, openedThisYear: 0, openedLastYear: 0 })

  /** Currently loaded ticket detail. */
  const currentTicket = ref<Ticket | null>(null)

  /** Available support departments. */
  const departments = ref<Department[]>([])

  /** True while any API request is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed request, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last failed department lookup, null when it succeeded. */
  const departmentsError = ref<string | null>(null)

  /** Per-field messages of the last refused request, keyed by camelCase field name. */
  const fieldErrors = ref<Record<string, string>>({})

  /** Throwaway error ref: the stats lookup is non-critical, so its failure is silent. */
  const quiet = ref<string | null>(null)

  /**
   * Fetches one page of a client's tickets. The page, the search and the filters come from the list's address, so
   * the store keeps no copy of them.
   *
   * @param clientId - The client identifier.
   * @param page - Page to load, 1-based.
   * @param filters - The subject search text and the status, priority and department filters; empty for none.
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchClientTickets = async (
    clientId: number | string,
    page: number,
    filters: ClientTicketListFilters = {},
  ): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () =>
      api.list(clientId, { ...filters, page, pageSize: pageSize.value, search: filters.search?.trim() }),
    )
    loading.value = false
    if (result.ok) {
      tickets.value = result.value.items
      totalCount.value = result.value.totalCount
    }
    return result.ok
  }

  /**
   * Fetches ticket statistics for a client summary cards.
   *
   * @param clientId - The client identifier.
   * @returns Promise that resolves when the statistics are loaded.
   */
  const fetchStats = async (clientId: number | string): Promise<void> => {
    const result = await runAction(quiet, () => api.stats(clientId))
    if (result.ok) {
      stats.value = result.value
    }
  }

  /**
   * Fetches a single ticket by id with all its replies.
   *
   * @param id - The ticket identifier.
   * @returns Promise that resolves when the ticket is loaded.
   */
  const fetchById = async (id: number | string): Promise<void> => {
    loading.value = true
    currentTicket.value = null
    const result = await runAction(error, () => api.get(id))
    loading.value = false
    if (result.ok) {
      currentTicket.value = result.value
    }
  }

  /**
   * Fetches all support departments for dropdown options; a failure lands in `departmentsError`.
   *
   * @returns Promise that resolves when the departments are loaded.
   */
  const fetchDepartments = async (): Promise<void> => {
    const result = await runAction(departmentsError, () => api.departments())
    if (result.ok) {
      departments.value = result.value
    }
  }

  /**
   * Creates a new ticket on behalf of a client.
   *
   * @param payload - Ticket creation data.
   * @returns The id of the new ticket, or null when refused (see `error`).
   */
  const createTicket = async (payload: CreateTicketPayload): Promise<number | null> => {
    loading.value = true
    const result = await runAction(error, () => api.create(payload), fieldErrors)
    loading.value = false
    return result.ok ? result.value : null
  }

  /**
   * Updates ticket metadata.
   *
   * @param id - The ticket identifier.
   * @param payload - Fields to update.
   * @returns True when saved; false when refused (see `error`).
   */
  const updateTicket = async (id: number | string, payload: UpdateTicketPayload): Promise<boolean> => {
    const result = await runAction(error, () => api.update(id, payload), fieldErrors)
    return result.ok
  }

  /**
   * Permanently deletes a ticket.
   *
   * @param id - The ticket identifier.
   * @returns True when deleted; false when refused (see `error`).
   */
  const deleteTicket = async (id: number | string): Promise<boolean> => {
    const result = await runAction(error, () => api.remove(id))
    return result.ok
  }

  /**
   * Adds a staff reply to a ticket.
   *
   * @param id - The ticket identifier.
   * @param message - The reply message body.
   * @returns True when sent; false when refused (see `error`).
   */
  const replyToTicket = async (id: number | string, message: string): Promise<boolean> => {
    const result = await runAction(error, () => api.reply(id, message), fieldErrors)
    return result.ok
  }

  return {
    tickets,
    totalCount,
    pageSize,
    stats,
    currentTicket,
    departments,
    loading,
    error,
    departmentsError,
    fieldErrors,
    fetchClientTickets,
    fetchStats,
    fetchById,
    fetchDepartments,
    createTicket,
    updateTicket,
    deleteTicket,
    replyToTicket,
  }
})
