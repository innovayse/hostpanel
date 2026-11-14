/**
 * Pinia store for the admin-wide billable items list.
 *
 * It is the only caller of `useBillableItemsApi` for this screen; a refusal becomes the API
 * sentence in `error` (and the per-field messages in `fieldErrors`).
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useBillableItemsApi } from '@/composables/apis/useBillableItemsApi'
import { runAction } from '@/lib/runAction'
import type { BillableItem, BillableItemListFilters, CreateBillableItemPayload } from '@/types/billableitem'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'

/**
 * Loads, creates and deletes billable items across all clients. The page and the filters are the address of the list,
 * so the store keeps no copy of them, and a write does not reload: the list screen does.
 */
export const useBillableItemsStore = defineStore('billableItems', () => {
  const api = useBillableItemsApi()

  /** Loaded billable items of the current page. */
  const items = ref<BillableItem[]>([])

  /** Items per page. */
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  /** Total number of matching items across all pages. */
  const totalCount = ref(0)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed request, null when none. */
  const error = ref<string | null>(null)

  /** Per-field messages of the last refused request, keyed by camelCase field name. */
  const fieldErrors = ref<Record<string, string>>({})

  /**
   * Loads one page of billable items.
   *
   * @param page - Page to load, 1-based.
   * @param filters - The search text, client, type and invoiced filters; empty for none.
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchAll = async (page: number, filters: BillableItemListFilters = {}): Promise<boolean> => {
    loading.value = true
    const result = await runAction(
      error,
      () => api.list(page, pageSize.value, { ...filters, search: filters.search?.trim() }),
      fieldErrors,
    )
    loading.value = false
    if (result.ok) {
      items.value = result.value.items
      totalCount.value = result.value.totalCount
    }
    return result.ok
  }

  /**
   * Creates a billable item.
   *
   * @param item - Item fields.
   * @returns True when created; false when refused (see `error`).
   */
  const create = async (item: CreateBillableItemPayload): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () => api.create(item), fieldErrors)
    loading.value = false
    return result.ok
  }

  /**
   * Deletes a billable item.
   *
   * @param id - The item id.
   * @returns True when deleted; false when refused (see `error`).
   */
  const removeItem = async (id: number): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () => api.remove(id), fieldErrors)
    loading.value = false
    return result.ok
  }

  return { items, pageSize, totalCount, loading, error, fieldErrors, fetchAll, create, removeItem }
})
