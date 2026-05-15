import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type {
  ClientListItem,
  ClientDetail,
  PagedResult,
  AdminCreateClientPayload,
  CountryOption,
  CurrencyOption,
} from '../../../types/models'

/** Filter parameters for the clients list endpoint. */
export interface ClientFilters {
  /** Name/company search term. */
  search?: string
  /** Email partial match. */
  email?: string
  /** Phone partial match. */
  phone?: string
  /** Status filter (Active, Inactive, Suspended, Closed). */
  status?: string
}

/**
 * Pinia store for admin clients management.
 *
 * Handles paginated list with advanced filters and single-client fetching.
 */
export const useClientsStore = defineStore('clients', () => {
  const { request } = useApi()

  /** Loaded clients list. */
  const clients = ref<ClientListItem[]>([])

  /** Total clients matching the current query. */
  const totalCount = ref(0)

  /** Current page number (1-based). */
  const page = ref(1)

  /** Items per page. */
  const pageSize = ref(20)

  /** Currently viewed client detail, null until loaded. */
  const current = ref<ClientDetail | null>(null)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Error message, null when no error. */
  const error = ref<string | null>(null)

  /** Cached list of countries from the reference endpoint. */
  const countries = ref<CountryOption[]>([])

  /** Cached list of currencies from the reference endpoint. */
  const currencies = ref<CurrencyOption[]>([])

  /**
   * Fetches a paginated, filtered client list from the backend.
   *
   * @param filters - Optional filter parameters.
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchAll(filters?: ClientFilters): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const params = new URLSearchParams({
        page: page.value.toString(),
        pageSize: pageSize.value.toString(),
      })
      if (filters?.search) params.set('search', filters.search)
      if (filters?.email) params.set('email', filters.email)
      if (filters?.phone) params.set('phone', filters.phone)
      if (filters?.status) params.set('status', filters.status)

      const result = await request<PagedResult<ClientListItem>>(`/clients?${params}`)
      clients.value = result.items
      totalCount.value = result.totalCount
    } catch {
      error.value = 'Failed to load clients.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches a single client by ID.
   *
   * @param id - The client identifier.
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchById(id: number | string): Promise<void> {
    loading.value = true
    error.value = null
    current.value = null
    try {
      current.value = await request<ClientDetail>(`/clients/${id}`)
    } catch {
      error.value = 'Failed to load client details.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Creates a new client via the admin API, then refreshes the list.
   *
   * @param payload - Client creation payload.
   * @returns Promise that resolves when the client is created and list is refreshed.
   */
  async function createClient(payload: AdminCreateClientPayload): Promise<void> {
    await request('/clients/admin', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
    await fetchAll()
  }

  /**
   * Updates an existing client's profile, address, notifications, settings, and status.
   *
   * @param id - The client identifier.
   * @param payload - Updated client fields.
   * @returns Promise that resolves when the client is updated and detail is refreshed.
   */
  async function updateClient(id: number | string, payload: Record<string, unknown>): Promise<void> {
    await request(`/clients/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    })
    await fetchById(id)
  }

  /**
   * Searches Identity users by a term (name or email).
   *
   * @param term - Search string.
   * @returns Matching user items (max 10).
   */
  async function searchUsers(term: string): Promise<{ id: string; email: string; firstName: string; lastName: string }[]> {
    const params = new URLSearchParams({ search: term, pageSize: '10', page: '1' })
    const result = await request<PagedResult<{ id: string; email: string; firstName: string; lastName: string }>>(`/admin/users?${params}`)
    return result.items
  }

  /**
   * Fetches the list of countries from the reference endpoint.
   * Caches the result so subsequent calls are no-ops.
   *
   * @returns Promise that resolves when countries are loaded.
   */
  async function fetchCountries(): Promise<void> {
    if (countries.value.length > 0) return
    try {
      countries.value = await request<CountryOption[]>('/reference/countries')
    } catch {
      countries.value = []
    }
  }

  /**
   * Fetches the list of currencies from the reference endpoint.
   * Caches the result so subsequent calls are no-ops.
   *
   * @returns Promise that resolves when currencies are loaded.
   */
  async function fetchCurrencies(): Promise<void> {
    if (currencies.value.length > 0) return
    try {
      currencies.value = await request<CurrencyOption[]>('/reference/currencies')
    } catch {
      currencies.value = []
    }
  }

  /**
   * Adds a new contact to a client.
   *
   * @param clientId - The client identifier.
   * @param payload - Contact creation data.
   * @returns Promise that resolves when the contact is added and client is refreshed.
   */
  async function addContact(clientId: number | string, payload: Record<string, unknown>): Promise<void> {
    await request(`/clients/${clientId}/contacts`, {
      method: 'POST',
      body: JSON.stringify(payload),
    })
    await fetchById(clientId)
  }

  /**
   * Updates an existing contact on a client.
   *
   * @param clientId - The client identifier.
   * @param contactId - The contact identifier.
   * @param payload - Updated contact data.
   * @returns Promise that resolves when the contact is updated and client is refreshed.
   */
  async function updateContact(clientId: number | string, contactId: number, payload: Record<string, unknown>): Promise<void> {
    await request(`/clients/${clientId}/contacts/${contactId}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    })
    await fetchById(clientId)
  }

  /**
   * Removes a contact from a client.
   *
   * @param clientId - The client identifier.
   * @param contactId - The contact identifier.
   * @returns Promise that resolves when the contact is removed and client is refreshed.
   */
  async function removeContact(clientId: number | string, contactId: number): Promise<void> {
    await request(`/clients/${clientId}/contacts/${contactId}`, {
      method: 'DELETE',
    })
    await fetchById(clientId)
  }

  return {
    clients, totalCount, page, pageSize, current, loading, error,
    countries, currencies,
    fetchAll, fetchById, createClient, updateClient, searchUsers, fetchCountries, fetchCurrencies,
    addContact, updateContact, removeContact,
  }
})
