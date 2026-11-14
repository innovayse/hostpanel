/**
 * Pinia store for admin quotes.
 *
 * Owns the global and client-scoped quote lists, the open quote and every quote mutation.
 * It is the only caller of `useQuotesApi`; a refusal becomes the API sentence in `error`
 * (and the per-field messages in `fieldErrors`), so no view holds a `catch`.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useQuotesApi } from '@/composables/apis/useQuotesApi'
import { runAction } from '@/lib/runAction'
import { translate } from '@/lib/translate'
import type { CreateQuotePayload, Quote, QuoteFilters, QuoteListItem, UpdateQuotePayload } from '@/types/quote'
import { LEDGER_PAGE_SIZE } from '@/lib/limits'

/** Loads, creates and changes quotes for the admin billing screens. */
export const useQuotesStore = defineStore('quotes', () => {
  const api = useQuotesApi()

  /** Global quotes list (billing section). */
  const quotes = ref<QuoteListItem[]>([])

  /** Total count for global quotes. */
  const totalCount = ref(0)

  /** Page size for global quotes. */
  const pageSize = ref(LEDGER_PAGE_SIZE)

  /** Client-scoped quotes list. */
  const clientQuotes = ref<QuoteListItem[]>([])

  /** Total number of client-scoped quotes across all pages. */
  const clientQuotesTotal = ref(0)

  /** Currently loaded quote detail. */
  const currentQuote = ref<Quote | null>(null)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed request, null when none. */
  const error = ref<string | null>(null)

  /** Per-field messages of the last refused request, keyed by camelCase field name. */
  const fieldErrors = ref<Record<string, string>>({})

  /** Confirmation line for the last duplicate or convert, null when none. */
  const successMessage = ref<string | null>(null)

  /**
   * Runs one request with the loading flag and the error state around it.
   *
   * @param task - The request to run.
   * @returns The outcome: the value on success, a failure whose sentence is in `error`.
   */
  const run = async <T>(task: () => Promise<T>) => {
    loading.value = true
    const result = await runAction(error, task, fieldErrors)
    loading.value = false
    return result
  }

  /**
   * Fetches a page of all quotes (global billing view), narrowed by the server. The page and the filters come from the
   * list's address, so the store keeps no copy of them.
   *
   * @param page - Page number (1-based).
   * @param filters - The search text and the client, stage and validity filters; a member left out does not narrow.
   * @param size - Items per page; the store page size when omitted.
   * @returns Promise that resolves when data is loaded.
   */
  const fetchAll = async (page = 1, filters: QuoteFilters = {}, size?: number): Promise<void> => {
    const result = await run(() => api.list(page, size ?? pageSize.value, filters))
    if (result.ok) {
      quotes.value = result.value.items
      totalCount.value = result.value.totalCount
    }
  }

  /**
   * Fetches a page of the quotes of one client, narrowed by the server.
   *
   * @param clientId - The client id.
   * @param page - Page number (1-based).
   * @param filters - The search text and the stage and validity filters; a member left out does not narrow.
   * @param size - Items per page.
   * @returns Promise that resolves when data is loaded.
   */
  const fetchClientQuotes = async (
    clientId: number | string,
    page = 1,
    filters: QuoteFilters = {},
    size = LEDGER_PAGE_SIZE,
  ): Promise<void> => {
    const result = await run(() => api.listByClient(clientId, page, size, filters))
    if (result.ok) {
      clientQuotes.value = result.value.items
      clientQuotesTotal.value = result.value.totalCount
    }
  }

  /**
   * Fetches a single quote by id and sets `currentQuote`.
   *
   * @param id - The quote id to fetch.
   * @returns Promise that resolves when the quote is loaded.
   */
  const fetchById = async (id: number): Promise<void> => {
    const result = await run(() => api.get(id))
    if (result.ok) {
      currentQuote.value = result.value
    }
  }

  /**
   * Creates a new quote.
   *
   * @param payload - Quote creation payload.
   * @returns The new quote id, or null when refused (see `error`).
   */
  const createQuote = async (payload: CreateQuotePayload): Promise<number | null> => {
    const result = await run(() => api.create(payload))
    return result.ok ? result.value : null
  }

  /**
   * Updates an existing quote, then reloads it and confirms the save in `successMessage`.
   *
   * @param id - The quote id to update.
   * @param payload - Updated quote data.
   * @returns True when saved; false when refused (see `error`).
   */
  const updateQuote = async (id: number, payload: UpdateQuotePayload): Promise<boolean> => {
    const result = await run(async () => {
      await api.update(id, payload)
      currentQuote.value = await api.get(id)
    })
    if (result.ok) {
      successMessage.value = translate('quotes.savedSuccess', { id })
    }
    return result.ok
  }

  /**
   * Duplicates an existing quote.
   *
   * @param id - The quote id to duplicate.
   * @returns The new quote id, or null when refused (see `error`).
   */
  const duplicateQuote = async (id: number): Promise<number | null> => {
    const result = await run(() => api.duplicate(id))
    if (!result.ok) {
      return null
    }
    successMessage.value = translate('quotes.duplicatedSuccess', { id: result.value })
    return result.value
  }

  /**
   * Converts a quote to an invoice.
   *
   * @param id - The quote id to convert.
   * @returns The new invoice id, or null when refused (see `error`).
   */
  const convertToInvoice = async (id: number): Promise<number | null> => {
    const result = await run(() => api.convertToInvoice(id))
    if (!result.ok) {
      return null
    }
    successMessage.value = translate('quotes.convertedSuccess', { id: result.value })
    return result.value
  }

  /**
   * Deletes a quote by id.
   *
   * @param id - The quote id to delete.
   * @returns True when deleted; false when refused (see `error`).
   */
  const deleteQuote = async (id: number): Promise<boolean> => {
    const result = await run(() => api.remove(id))
    return result.ok
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
    fieldErrors,
    successMessage,
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
