import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { Ticket, TicketListItem, ClientTicketStats, Department, PagedResult } from '../../../types/models'

/**
 * Manages client-scoped ticket state for the admin panel.
 *
 * Provides actions for listing, creating, updating, deleting tickets,
 * and managing replies within the client profile context.
 */
export const useClientTicketStore = defineStore('clientTickets', () => {
  const { request } = useApi()

  /** Paginated list of tickets for the current client. */
  const tickets = ref<TicketListItem[]>([])

  /** Total count of tickets matching the current query. */
  const totalCount = ref(0)

  /** Ticket statistics for summary cards. */
  const stats = ref<ClientTicketStats>({ openedThisMonth: 0, openedLastMonth: 0, openedThisYear: 0, openedLastYear: 0 })

  /** Currently loaded ticket detail. */
  const currentTicket = ref<Ticket | null>(null)

  /** Available support departments. */
  const departments = ref<Department[]>([])

  /** True while any API request is in flight. */
  const loading = ref(false)

  /** Human-readable error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches a paginated list of tickets for a client.
   *
   * @param clientId - The client identifier.
   * @param page - One-based page number.
   * @param pageSize - Number of items per page.
   * @param search - Optional subject search term.
   */
  async function fetchClientTickets(clientId: number | string, page = 1, pageSize = 20, search?: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
      if (search) params.set('search', search)
      const result = await request<PagedResult<TicketListItem>>(`/tickets/client/${clientId}?${params}`)
      tickets.value = result.items
      totalCount.value = result.totalCount
    } catch {
      error.value = 'Failed to load tickets.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches ticket statistics for a client's summary cards.
   *
   * @param clientId - The client identifier.
   */
  async function fetchStats(clientId: number | string): Promise<void> {
    try {
      stats.value = await request<ClientTicketStats>(`/tickets/client/${clientId}/stats`)
    } catch {
      // Stats failure is non-critical
    }
  }

  /**
   * Fetches a single ticket by ID with all its replies.
   *
   * @param id - The ticket identifier.
   */
  async function fetchById(id: number | string): Promise<void> {
    loading.value = true
    error.value = null
    currentTicket.value = null
    try {
      currentTicket.value = await request<Ticket>(`/tickets/${id}`)
    } catch {
      error.value = 'Failed to load ticket details.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches all support departments for dropdown options.
   */
  async function fetchDepartments(): Promise<void> {
    try {
      departments.value = await request<Department[]>('/tickets/departments')
    } catch {
      // Department fetch failure is non-critical
    }
  }

  /**
   * Creates a new ticket on behalf of a client.
   *
   * @param payload - Ticket creation data.
   * @returns The ID of the newly created ticket, or null on failure.
   */
  async function createTicket(payload: {
    clientId: number
    subject: string
    message: string
    departmentId: number
    priority: string
  }): Promise<number | null> {
    loading.value = true
    error.value = null
    try {
      const id = await request<number>('/tickets', {
        method: 'POST',
        body: JSON.stringify(payload),
      })
      return id
    } catch {
      error.value = 'Failed to create ticket.'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Updates a ticket's metadata.
   *
   * @param id - The ticket identifier.
   * @param payload - Fields to update.
   * @returns True on success, false on failure.
   */
  async function updateTicket(id: number | string, payload: {
    status?: string
    priority?: string
    departmentId?: number
    assignedToStaffId?: number
  }): Promise<boolean> {
    error.value = null
    try {
      await request(`/tickets/${id}`, {
        method: 'PUT',
        body: JSON.stringify(payload),
      })
      return true
    } catch {
      error.value = 'Failed to update ticket.'
      return false
    }
  }

  /**
   * Permanently deletes a ticket.
   *
   * @param id - The ticket identifier.
   * @returns True on success, false on failure.
   */
  async function deleteTicket(id: number | string): Promise<boolean> {
    error.value = null
    try {
      await request(`/tickets/${id}`, { method: 'DELETE' })
      return true
    } catch {
      error.value = 'Failed to delete ticket.'
      return false
    }
  }

  /**
   * Adds a staff reply to a ticket.
   *
   * @param id - The ticket identifier.
   * @param message - The reply message body.
   * @param authorName - Display name of the replying staff member.
   * @returns True on success, false on failure.
   */
  async function replyToTicket(id: number | string, message: string, authorName: string): Promise<boolean> {
    error.value = null
    try {
      await request(`/tickets/${id}/reply`, {
        method: 'POST',
        body: JSON.stringify({ message, authorName }),
      })
      return true
    } catch {
      error.value = 'Failed to send reply.'
      return false
    }
  }

  return {
    tickets,
    totalCount,
    stats,
    currentTicket,
    departments,
    loading,
    error,
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
