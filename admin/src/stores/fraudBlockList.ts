/**
 * Pinia store for the fraud block lists.
 *
 * It is the only caller of the block list endpoints of `useFraudApi`. A refusal becomes the API
 * sentence in `error` (a failed read) or `actionError` (a failed add or delete), and the per-field
 * messages of a refused add land in `fieldErrors`.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useFraudApi } from '@/composables/apis/useFraudApi'
import { runAction } from '@/lib/runAction'
import type { AddBlockListEntryPayload, BlockListEntry, BlockListType } from '@/types/fraud'

/** Lists one block list a page at a time and adds and removes its entries. */
export const useFraudBlockListStore = defineStore('fraud-block-list', () => {
  const api = useFraudApi()

  /** Entries of the current page. */
  const entries = ref<BlockListEntry[]>([])

  /** Total number of entries in the list, across all pages. */
  const totalCount = ref(0)

  /** True while a page is being read. */
  const loading = ref(false)

  /** Sentence for the last failed read, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last failed add or delete, null when none. */
  const actionError = ref<string | null>(null)

  /** Messages of the fields the last add was refused for, by field name. */
  const fieldErrors = ref<Record<string, string>>({})

  /**
   * Reads one page of a block list.
   *
   * @param type - Which block list to read.
   * @param page - Page number (1-based).
   * @param pageSize - Entries per page.
   * @returns Promise that resolves when the page is read; a refusal is left in `error`.
   */
  const fetchPage = async (type: BlockListType, page: number, pageSize: number): Promise<void> => {
    loading.value = true
    const result = await runAction(error, () => api.listBlockList(type, page, pageSize))
    loading.value = false
    if (result.ok) {
      entries.value = result.value.items
      totalCount.value = result.value.totalCount
    }
  }

  /**
   * Adds an entry to a block list.
   *
   * @param payload - The type, the value to block and an optional note.
   * @returns True when added; false when refused (see `actionError` and `fieldErrors`).
   */
  const add = async (payload: AddBlockListEntryPayload): Promise<boolean> => {
    const result = await runAction(actionError, () => api.addBlockListEntry(payload), fieldErrors)
    return result.ok
  }

  /**
   * Removes an entry from its block list.
   *
   * @param id - Entry id.
   * @returns True when removed; false when refused (see `actionError`).
   */
  const remove = async (id: number): Promise<boolean> => {
    const result = await runAction(actionError, () => api.deleteBlockListEntry(id))
    return result.ok
  }

  /** Forgets the refusal of the last add or delete, for a screen that moves to another list. */
  const clearAction = (): void => {
    actionError.value = null
    fieldErrors.value = {}
  }

  return { entries, totalCount, loading, error, actionError, fieldErrors, fetchPage, add, remove, clearAction }
})
