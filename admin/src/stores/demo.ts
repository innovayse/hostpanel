/**
 * Demo-mode state: whether this deployment is a public demo, when its data resets next, and the
 * accounts it publishes.
 *
 * Loaded at app start and reloaded (forced) when the reset countdown reaches zero. Deliberately failure-tolerant: a backend that is
 * down, or too old to serve `/public/demo`, simply means "not a demo", so the panel never shows a
 * banner it cannot vouch for.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useDemoApi } from '@/composables/apis/useDemoApi'
import { runAction } from '@/lib/runAction'
import type { DemoAccount } from '@/types/demo'

/** Pinia store holding the demo-mode description. */
export const useDemoStore = defineStore('demo', () => {
  const api = useDemoApi()

  /** True when this deployment runs as a public demo. */
  const enabled = ref(false)

  /** ISO timestamp of the next data reset, or null when unknown. */
  const nextResetAt = ref<string | null>(null)

  /** Published demo accounts. */
  const accounts = ref<DemoAccount[]>([])

  /** True once a load has been attempted, successfully or not. */
  const loaded = ref(false)

  /** Sentence of the last failed lookup, or null. A failure still means "not a demo". */
  const error = ref<string | null>(null)

  /**
   * Reads the demo description. Without `force` only the first call reaches the network.
   *
   * @param force - Reload even though a load already happened (used after a data reset).
   * @returns Resolves when the attempt is over, whether or not it found a demo.
   */
  const load = async (force = false): Promise<void> => {
    if (loaded.value && !force) {
      return
    }
    loaded.value = true

    const result = await runAction(error, () => api.fetchDemo())
    if (!result.ok) {
      return
    }
    enabled.value = result.value.enabled
    nextResetAt.value = result.value.nextResetAt
    accounts.value = result.value.accounts ?? []
  }

  return { enabled, nextResetAt, accounts, loaded, error, load }
})
