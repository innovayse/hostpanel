import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { Ticket, TicketListItem, Department, PagedResult, SupportOverviewStats } from '../../../types/models'

/**
 * Pinia store for the global Support module.
 *
 * Provides paginated ticket listing with status/search filters,
 * overview stats, CRUD operations, reply management, flagging,
 * and bulk actions for the admin support section.
 */
export const useSupportStore = defineStore('support', () => {
  const { request } = useApi()

  /** Paginated list of tickets. */
  const tickets = ref<TicketListItem[]>([])

  /** Total ticket count matching the current filter. */
  const totalCount = ref(0)

  /** Currently loaded ticket detail. */
  const currentTicket = ref<Ticket | null>(null)

  /** Available support departments. */
  const departments = ref<Department[]>([])

  /** Support overview statistics. */
  const overviewStats = ref<SupportOverviewStats | null>(null)

  /** True while any API request is in flight. */
  const loading = ref(false)

  /** Human-readable error message, null when no error. */
  const error = ref<string | null>(null)

  /** Current page (1-based). */
  const page = ref(1)

  /** Items per page. */
  const pageSize = ref(20)

  /** Active status filter (null = all). */
  const statusFilter = ref<string | null>(null)

  /** Subject search term. */
  const search = ref('')

  /**
   * Fetches a paginated, filtered list of all tickets.
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchAll(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const params = new URLSearchParams({
        page: String(page.value),
        pageSize: String(pageSize.value),
      })
      if (statusFilter.value) params.set('status', statusFilter.value)
      if (search.value) params.set('search', search.value)

      const result = await request<PagedResult<TicketListItem>>(`/tickets?${params}`)
      tickets.value = result.items
      totalCount.value = result.totalCount
    } catch {
      error.value = 'Failed to load tickets.'
    } finally {
      loading.value = false
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
   * Fetches support overview statistics for a given period.
   *
   * @param period - Time period: "today", "yesterday", "last7days", "last30days".
   */
  async function fetchOverview(period: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      overviewStats.value = await request<SupportOverviewStats>(`/tickets/overview?period=${period}`)
    } catch {
      error.value = 'Failed to load overview stats.'
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
      // Non-critical
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

  /**
   * Toggles the flagged state of a ticket.
   *
   * @param id - The ticket identifier.
   * @returns True on success, false on failure.
   */
  async function toggleFlag(id: number | string): Promise<boolean> {
    error.value = null
    try {
      await request(`/tickets/${id}/toggle-flag`, { method: 'POST' })
      return true
    } catch {
      error.value = 'Failed to toggle flag.'
      return false
    }
  }

  /**
   * Performs a bulk action on multiple tickets.
   *
   * @param ticketIds - Array of ticket IDs.
   * @param action - Action: "close", "delete", "flag", "unflag".
   * @returns True on success, false on failure.
   */
  async function bulkAction(ticketIds: number[], action: string): Promise<boolean> {
    error.value = null
    try {
      await request('/tickets/bulk', {
        method: 'POST',
        body: JSON.stringify({ ticketIds, action }),
      })
      return true
    } catch {
      error.value = 'Bulk action failed.'
      return false
    }
  }

  return {
    tickets,
    totalCount,
    currentTicket,
    departments,
    overviewStats,
    loading,
    error,
    page,
    pageSize,
    statusFilter,
    search,
    fetchAll,
    fetchById,
    fetchOverview,
    fetchDepartments,
    createTicket,
    updateTicket,
    deleteTicket,
    replyToTicket,
    toggleFlag,
    bulkAction,
  }
})
