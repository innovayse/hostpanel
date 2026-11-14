/**
 * Pinia store for managing TLD pricing configurations.
 *
 * Provides the paged list, CRUD operations, provider import, and price sync for the admin TLD pricing
 * settings page. The only caller of `useTldConfigsApi`; a refusal is kept as the API's own sentence,
 * never thrown.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useTldConfigsApi } from '@/composables/apis/useTldConfigsApi'
import { runAction } from '@/lib/runAction'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import type {
  CreateTldConfigPayload,
  TldConfig,
  TldConfigListFilters,
  TldConfigListItem,
  TldImportResult,
  UpdateTldConfigPayload,
} from '@/types/tldconfig'

/** Manages TLD pricing configuration state and admin CRUD operations. */
export const useTldConfigsStore = defineStore('tldConfigs', () => {
  const api = useTldConfigsApi()

  /** The TLD configs of the loaded page. */
  const configs = ref<TldConfigListItem[]>([])

  /** Items per page. */
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  /** Total number of matching TLD configs across all pages. */
  const totalCount = ref(0)

  /** Whether a fetch is in progress. */
  const loading = ref(false)

  /** Sentence for the last failed load, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last failed create, update, delete, import or sync, null when none. */
  const actionError = ref<string | null>(null)

  /**
   * Loads one page of TLD configs. The page, the search and the filters come from the list's address, so the
   * store keeps no copy of them.
   *
   * @param page - Page to load, 1-based.
   * @param filters - The TLD search text and the registrar and enabled filters; empty for none.
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchAll = async (page: number, filters: TldConfigListFilters = {}): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () =>
      api.listTldConfigs({ ...filters, page, pageSize: pageSize.value, search: filters.search?.trim() }),
    )
    loading.value = false
    if (result.ok) {
      configs.value = result.value.items
      totalCount.value = result.value.totalCount
    }
    return result.ok
  }

  /**
   * Fetches a single TLD config by ID.
   *
   * @param id - TLD config identifier.
   * @returns The full TLD config, or undefined when refused (see `error`).
   */
  const fetchById = async (id: number): Promise<TldConfig | undefined> => {
    const result = await runAction(error, () => api.getTldConfig(id))
    return result.ok ? result.value : undefined
  }

  /**
   * Runs one mutating request. The screen re-reads the list it is showing afterwards, because only it knows
   * the page and the filters.
   *
   * @param task - The request to run.
   * @returns The request's value, or null when refused (see `actionError`).
   */
  const mutate = async <T>(task: () => Promise<T>): Promise<{ value: T } | null> => {
    const result = await runAction(actionError, task)
    if (!result.ok) {
      return null
    }
    return { value: result.value }
  }

  /**
   * Creates a new TLD config.
   *
   * @param payload - TLD config data.
   * @returns The new config's ID, or null when refused (see `actionError`).
   */
  const create = async (payload: CreateTldConfigPayload): Promise<number | null> => {
    const done = await mutate(() => api.createTldConfig(payload))
    return done ? done.value : null
  }

  /**
   * Updates an existing TLD config.
   *
   * @param id - TLD config ID.
   * @param payload - Updated TLD config data.
   * @returns True when updated; false when refused (see `actionError`).
   */
  const update = async (id: number, payload: UpdateTldConfigPayload): Promise<boolean> => {
    const done = await mutate(() => api.updateTldConfig(id, payload))
    return done !== null
  }

  /**
   * Deletes a TLD config.
   *
   * @param id - TLD config ID to delete.
   * @returns True when deleted; false when refused (see `actionError`).
   */
  const remove = async (id: number): Promise<boolean> => {
    const done = await mutate(() => api.deleteTldConfig(id))
    return done !== null
  }

  /**
   * Imports TLDs from a registrar provider.
   *
   * @param module - Registrar module name, as the backend's registrar module enum spells it.
   * @returns Import result with counts, or null when refused (see `actionError`).
   */
  const importFromProvider = async (module: string): Promise<TldImportResult | null> => {
    const done = await mutate(() => api.importTldConfigs(module))
    return done ? done.value : null
  }

  /**
   * Triggers a manual cost price sync from all providers.
   *
   * @returns True when synced; false when refused (see `actionError`).
   */
  const syncPrices = async (): Promise<boolean> => {
    const done = await mutate(api.syncTldPrices)
    return done !== null
  }

  return { configs, pageSize, totalCount, loading, error, actionError, fetchAll, fetchById, create, update, remove, importFromProvider, syncPrices }
})
