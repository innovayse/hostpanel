import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type {
  ClientSummaryData,
  ServiceListItem,
  DomainRegistration,
  QuoteListItem,
  PagedResult,
} from '../../../types/models'

/**
 * Manages aggregated summary data for the client profile dashboard.
 *
 * Lazy-loaded — call fetchAll before reading any state.
 */
export const useClientSummaryStore = defineStore('clientSummary', () => {
  const { request } = useApi()

  /** Aggregated summary data from the backend. */
  const summary = ref<ClientSummaryData | null>(null)

  /** Recent services for the bottom table. */
  const services = ref<ServiceListItem[]>([])

  /** Recent domains for the bottom table. */
  const domains = ref<DomainRegistration[]>([])

  /** Recent quotes for the bottom table. */
  const quotes = ref<QuoteListItem[]>([])

  /** True while any request is in flight. */
  const loading = ref(false)

  /** Human-readable error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches the aggregated summary data for a client.
   *
   * @param clientId - The client's primary key.
   * @returns Promise that resolves when summary is loaded.
   */
  async function fetchSummary(clientId: number): Promise<void> {
    try {
      summary.value = await request<ClientSummaryData>(`/clients/${clientId}/summary`)
    } catch {
      error.value = 'Failed to load summary data.'
    }
  }

  /**
   * Fetches the 5 most recent services for a client.
   *
   * @param clientId - The client's primary key.
   * @returns Promise that resolves when services are loaded.
   */
  async function fetchRecentServices(clientId: number): Promise<void> {
    try {
      const result = await request<PagedResult<ServiceListItem>>(
        `/services?clientId=${clientId}&page=1&pageSize=5`,
      )
      services.value = result.items
    } catch {
      // Non-critical — silently ignore
    }
  }

  /**
   * Fetches the 5 most recent domains for a client.
   *
   * @param clientId - The client's primary key.
   * @returns Promise that resolves when domains are loaded.
   */
  async function fetchRecentDomains(clientId: number): Promise<void> {
    try {
      const result = await request<PagedResult<DomainRegistration>>(
        `/domains?clientId=${clientId}&page=1&pageSize=5`,
      )
      domains.value = result.items
    } catch {
      // Non-critical — silently ignore
    }
  }

  /**
   * Fetches the 5 most recent quotes for a client.
   *
   * @param clientId - The client's primary key.
   * @returns Promise that resolves when quotes are loaded.
   */
  async function fetchRecentQuotes(clientId: number): Promise<void> {
    try {
      const result = await request<PagedResult<QuoteListItem>>(
        `/billing/quotes/client/${clientId}?page=1&pageSize=5`,
      )
      quotes.value = result.items
    } catch {
      // Non-critical — silently ignore
    }
  }

  /**
   * Fetches all summary data in parallel: summary stats, services, domains, and quotes.
   *
   * @param clientId - The client's primary key.
   * @returns Promise that resolves when all data is loaded.
   */
  async function fetchAll(clientId: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await Promise.all([
        fetchSummary(clientId),
        fetchRecentServices(clientId),
        fetchRecentDomains(clientId),
        fetchRecentQuotes(clientId),
      ])
    } finally {
      loading.value = false
    }
  }

  /**
   * Resets the store to its initial state.
   *
   * @returns void
   */
  function reset(): void {
    summary.value = null
    services.value = []
    domains.value = []
    quotes.value = []
    loading.value = false
    error.value = null
  }

  return { summary, services, domains, quotes, loading, error, fetchAll, reset }
})
