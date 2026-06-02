import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { Quote, QuoteListItem, QuoteStage, PagedResult } from '../../../types/models'

/**
 * Pinia store for admin quote management.
 *
 * Handles client-scoped quote listing, single quote detail,
 * and all quote mutations (create, update, duplicate, convert, delete).
 */
export const useQuoteStore = defineStore('quotes', () => {
  const { request } = useApi()

  /** Global quotes list (billing section). */
  const quotes = ref<QuoteListItem[]>([])

  /** Total count for global quotes. */
  const totalCount = ref(0)

  /** Page size for global quotes. */
  const pageSize = ref(25)

  /** Client-scoped quotes list. */
  const clientQuotes = ref<QuoteListItem[]>([])

  /** Total number of client-scoped quotes across all pages. */
  const clientQuotesTotal = ref(0)

  /** Currently loaded quote detail. */
  const currentQuote = ref<Quote | null>(null)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches all quotes (global billing view) with pagination.
   *
   * @param page - Page number (1-based).
   * @param size - Items per page.
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchAll(page = 1, size?: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const ps = size ?? pageSize.value
      const result = await request<PagedResult<QuoteListItem>>(`/billing/quotes?page=${page}&pageSize=${ps}`)
      quotes.value = result.items
      totalCount.value = result.totalCount
    } catch {
      error.value = 'Failed to load quotes.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches quotes for a specific client with pagination.
   *
   * @param clientId - The client ID.
   * @param page - Page number (1-based).
   * @param pageSize - Items per page.
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchClientQuotes(
    clientId: number | string,
    page = 1,
    pageSize = 25,
  ): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const url = `/billing/quotes/client/${clientId}?page=${page}&pageSize=${pageSize}`
      const result = await request<PagedResult<QuoteListItem>>(url)
      clientQuotes.value = result.items
      clientQuotesTotal.value = result.totalCount
    } catch {
      error.value = 'Failed to load quotes.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches a single quote by ID and sets currentQuote.
   *
   * @param id - The quote ID to fetch.
   * @returns Promise that resolves when the quote is loaded.
   */
  async function fetchById(id: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      currentQuote.value = await request<Quote>(`/quotes/${id}`)
    } catch {
      error.value = 'Failed to load quote.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Creates a new quote.
   *
   * @param payload - Quote creation payload.
   * @returns The new quote ID, or null on failure.
   */
  async function createQuote(payload: {
    clientId: number
    subject: string
    stage: QuoteStage
    validUntil: string
    proposalText?: string
    customerNotes?: string
    adminNotes?: string
    items: Array<{ quantity: number; description: string; unitPrice: number; discountPercent: number; taxed: boolean }>
  }): Promise<number | null> {
    loading.value = true
    error.value = null
    try {
      const result = await request<{ id: number }>('/quotes', {
        method: 'POST',
        body: JSON.stringify(payload),
      })
      return result.id
    } catch {
      error.value = 'Failed to create quote.'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Updates an existing quote.
   *
   * @param id - The quote ID to update.
   * @param payload - Updated quote data.
   * @returns Promise that resolves when the update completes.
   */
  async function updateQuote(
    id: number,
    payload: {
      subject: string
      stage: QuoteStage
      validUntil: string
      proposalText?: string
      customerNotes?: string
      adminNotes?: string
      items: Array<{ id?: number; quantity: number; description: string; unitPrice: number; discountPercent: number; taxed: boolean }>
    },
  ): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request(`/quotes/${id}`, {
        method: 'PUT',
        body: JSON.stringify(payload),
      })
      await fetchById(id)
    } catch {
      error.value = 'Failed to update quote.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Duplicates an existing quote.
   *
   * @param id - The quote ID to duplicate.
   * @returns The new quote ID, or null on failure.
   */
  async function duplicateQuote(id: number): Promise<number | null> {
    loading.value = true
    error.value = null
    try {
      const result = await request<{ id: number }>(`/quotes/${id}/duplicate`, { method: 'POST' })
      return result.id
    } catch {
      error.value = 'Failed to duplicate quote.'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Converts a quote to an invoice.
   *
   * @param id - The quote ID to convert.
   * @returns The new invoice ID, or null on failure.
   */
  async function convertToInvoice(id: number): Promise<number | null> {
    loading.value = true
    error.value = null
    try {
      const result = await request<{ id: number }>(`/quotes/${id}/convert-to-invoice`, { method: 'POST' })
      return result.id
    } catch {
      error.value = 'Failed to convert quote to invoice.'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Deletes a quote by ID.
   *
   * @param id - The quote ID to delete.
   * @returns Promise that resolves when the quote is deleted.
   */
  async function deleteQuote(id: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request(`/quotes/${id}`, { method: 'DELETE' })
    } catch {
      error.value = 'Failed to delete quote.'
    } finally {
      loading.value = false
    }
  }

  return {
    quotes,
    totalCount,
    pageSize,
    clientQuotes,
    clientQuotesTotal,
    currentQuote,
    loading,
    error,
    fetchAll,
    fetchClientQuotes,
    fetchById,
    createQuote,
    updateQuote,
    duplicateQuote,
    convertToInvoice,
    deleteQuote,
  }
})
