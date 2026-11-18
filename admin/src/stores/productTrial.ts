/**
 * Pinia store for the free-trial settings of a product.
 *
 * The product form reads the defaults and limits of the trial settings through this store, which owns
 * the request and the sentence for a refusal, so the form holds no `catch`.
 */
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useProductsApi } from '@/composables/apis/useProductsApi'
import { runAction } from '@/lib/runAction'
import type { ProductTrialDefaults } from '@/types/product'

/** Loads the defaults and limits of the free-trial settings. */
export const useProductTrialStore = defineStore('productTrial', () => {
  const api = useProductsApi()

  /** Sentence for the last failed call, null when none. */
  const error = ref<string | null>(null)

  /**
   * Loads the defaults and limits from the API.
   *
   * @returns The defaults, or null when refused (see `error`).
   */
  const fetchDefaults = async (): Promise<ProductTrialDefaults | null> => {
    const result = await runAction(error, () => api.getTrialDefaults())
    return result.ok ? result.value : null
  }

  return { error, fetchDefaults }
})
