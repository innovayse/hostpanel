/**
 * Pinia store for the Hostpanel release status.
 *
 * Fetched once after sign-in; the sidebar reads it to show the upgrade notice when a newer
 * version is available. No action throws — a refusal is kept as a sentence in `error`.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useReleaseStatusApi } from '@/composables/apis/useReleaseStatusApi'
import { runAction } from '@/lib/runAction'
import type { ReleaseStatusDto } from '@/types/releaseStatus'

/**
 * Manages the cached release status fetched from the backend.
 *
 * Call {@link fetch} once after sign-in; the result is available in {@link status} until the
 * page is refreshed.
 */
export const useReleaseStatusStore = defineStore('releaseStatus', () => {
  const api = useReleaseStatusApi()

  /** The release status returned by the API; null until {@link fetch} resolves. */
  const status = ref<ReleaseStatusDto | null>(null)

  /** True while the request is in flight. */
  const loading = ref(false)

  /** Human-readable error sentence from the last failed fetch; null when no error. */
  const error = ref<string | null>(null)

  /**
   * Loads the release status from the backend and populates {@link status}.
   *
   * @returns Resolves when the request is over; a refusal is left in {@link error}.
   */
  const fetch = async (): Promise<void> => {
    loading.value = true
    const result = await runAction(error, api.getReleaseStatus)
    loading.value = false
    if (result.ok) {
      status.value = result.value
    }
  }

  return { status, loading, error, fetch }
})
