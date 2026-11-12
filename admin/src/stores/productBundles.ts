/**
 * Pinia store for the product bundles staff offer.
 *
 * Owns the bundle list the Product bundles screen reads, and the create, update and delete calls.
 * It is the only caller of `useProductBundlesApi`; a refusal becomes the API sentence in the
 * matching error ref (`error`, `saveError`, `actionError`), so no view holds a `catch`.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useProductBundlesApi } from '@/composables/apis/useProductBundlesApi'
import { runAction } from '@/lib/runAction'
import type { ProductBundle, ProductBundlePayload } from '@/types/productbundle'

/** Lists, creates, updates and deletes product bundles for the admin screen. */
export const useProductBundlesStore = defineStore('productBundles', () => {
  const api = useProductBundlesApi()

  /** Loaded product bundles. */
  const bundles = ref<ProductBundle[]>([])

  /** True while the list is loading. */
  const loading = ref(false)

  /** True while a create or update is in flight. */
  const saving = ref(false)

  /** Sentence for the last failed load, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last refused create or update, null when none. */
  const saveError = ref<string | null>(null)

  /** Per-field messages of the last refused create or update, keyed by camelCase field name. */
  const fieldErrors = ref<Record<string, string>>({})

  /** Sentence for the last refused delete, null when none. */
  const actionError = ref<string | null>(null)

  /**
   * Re-reads the list after a write, so the screen shows what the API now holds.
   *
   * @returns Promise that resolves when the list is replaced; rejects with the API refusal.
   */
  const reload = async (): Promise<void> => {
    bundles.value = await api.listBundles()
  }

  /**
   * Loads every bundle, replacing the list.
   *
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchAll = async (): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, api.listBundles)
    loading.value = false
    if (result.ok) {
      bundles.value = result.value
    }
    return result.ok
  }

  /**
   * Clears the form refusal, so a previous dialog's messages do not show on the next one.
   *
   * @returns Nothing.
   */
  const resetSaveError = (): void => {
    saveError.value = null
    fieldErrors.value = {}
  }

  /**
   * Clears the delete refusal, so a previous visit's sentence does not show on the list.
   *
   * @returns Nothing.
   */
  const clearActionError = (): void => {
    actionError.value = null
  }

  /**
   * Creates a bundle, then re-reads the list.
   *
   * @param payload - The bundle's fields and components.
   * @returns True when created; false when refused (see `saveError`, `fieldErrors`).
   */
  const create = async (payload: ProductBundlePayload): Promise<boolean> => {
    saving.value = true
    const result = await runAction(
      saveError,
      async () => {
        await api.createBundle(payload)
        await reload()
      },
      fieldErrors,
    )
    saving.value = false
    return result.ok
  }

  /**
   * Replaces a bundle's fields and components, then re-reads the list.
   *
   * @param id - The bundle id.
   * @param payload - The bundle's new fields and components.
   * @returns True when saved; false when refused (see `saveError`, `fieldErrors`).
   */
  const update = async (id: number, payload: ProductBundlePayload): Promise<boolean> => {
    saving.value = true
    const result = await runAction(
      saveError,
      async () => {
        await api.updateBundle(id, payload)
        await reload()
      },
      fieldErrors,
    )
    saving.value = false
    return result.ok
  }

  /**
   * Deletes a bundle, then re-reads the list.
   *
   * @param id - The bundle id.
   * @returns True when deleted; false when refused (see `actionError`).
   */
  const remove = async (id: number): Promise<boolean> => {
    const result = await runAction(actionError, async () => {
      await api.removeBundle(id)
      await reload()
    })
    return result.ok
  }

  return {
    bundles,
    loading,
    saving,
    error,
    saveError,
    fieldErrors,
    actionError,
    fetchAll,
    resetSaveError,
    clearActionError,
    create,
    update,
    remove,
  }
})
