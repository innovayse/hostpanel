/**
 * Pinia store for managing TLD pricing configurations.
 *
 * Provides CRUD operations, provider import, and price sync
 * for the admin TLD pricing settings page.
 */
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type {
  TldConfigListItem,
  TldConfig,
  CreateTldConfigPayload,
  UpdateTldConfigPayload,
  TldImportResult,
} from '../../../types/models'

/** Manages TLD pricing configuration state and admin CRUD operations. */
export const useTldConfigsStore = defineStore('tldConfigs', () => {
  const { request } = useApi()

  /** All TLD configs. */
  const configs = ref<TldConfigListItem[]>([])

  /** Whether a fetch is in progress. */
  const loading = ref(false)

  /** Error message from the last operation. */
  const error = ref<string | null>(null)

  /**
   * Fetches all TLD configs from the backend.
   *
   * @returns Promise that resolves when configs are loaded.
   */
  async function fetchAll(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      configs.value = await request<TldConfigListItem[]>('/admin/tld-configs')
    } catch {
      error.value = 'Failed to load TLD configurations.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches a single TLD config by ID.
   *
   * @param id - TLD config identifier.
   * @returns The full TLD config, or undefined on error.
   */
  async function fetchById(id: number): Promise<TldConfig | undefined> {
    try {
      return await request<TldConfig>(`/admin/tld-configs/${id}`)
    } catch {
      error.value = 'Failed to load TLD configuration.'
      return undefined
    }
  }

  /**
   * Creates a new TLD config and refreshes the list.
   *
   * @param payload - TLD config data.
   * @returns The new config's ID.
   */
  async function create(payload: CreateTldConfigPayload): Promise<number> {
    const id = await request<number>('/admin/tld-configs', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
    await fetchAll()
    return id
  }

  /**
   * Updates an existing TLD config and refreshes the list.
   *
   * @param id - TLD config ID.
   * @param payload - Updated TLD config data.
   */
  async function update(id: number, payload: UpdateTldConfigPayload): Promise<void> {
    await request(`/admin/tld-configs/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    })
    await fetchAll()
  }

  /**
   * Deletes a TLD config and refreshes the list.
   *
   * @param id - TLD config ID to delete.
   */
  async function remove(id: number): Promise<void> {
    await request(`/admin/tld-configs/${id}`, {
      method: 'DELETE',
    })
    await fetchAll()
  }

  /**
   * Imports TLDs from a registrar provider and refreshes the list.
   *
   * @param module - Registrar module name (e.g. "NameAm", "Namecheap").
   * @returns Import result with counts.
   */
  async function importFromProvider(module: string): Promise<TldImportResult> {
    const result = await request<TldImportResult>(`/admin/tld-configs/import/${module}`, {
      method: 'POST',
    })
    await fetchAll()
    return result
  }

  /**
   * Triggers a manual cost price sync from all providers and refreshes the list.
   *
   * @returns Promise that resolves when sync and refresh are complete.
   */
  async function syncPrices(): Promise<void> {
    await request('/admin/tld-configs/sync', { method: 'POST' })
    await fetchAll()
  }

  return { configs, loading, error, fetchAll, fetchById, create, update, remove, importFromProvider, syncPrices }
})
