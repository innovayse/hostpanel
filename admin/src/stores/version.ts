/**
 * Pinia store for the installed Hostpanel version shown to every signed-in staff member.
 *
 * Kept apart from the release status store, whose endpoint is Admin only. The layout starts the
 * load; the labels only read. A failure is never cached, so the next call asks again, and the
 * value is dropped whenever the signed-in account or its roles change.
 */

import { defineStore } from 'pinia'
import { ref, watch } from 'vue'
import { useVersionApi } from '@/composables/apis/useVersionApi'
import { runAction } from '@/lib/runAction'
import { useAuthStore } from '@/stores/auth'

/**
 * Manages the installed version: one shared request, a kept value, no cached failure.
 *
 * Call {@link ensureLoaded} from one place; every label reads {@link version}.
 */
export const useVersionStore = defineStore('version', () => {
  const api = useVersionApi()

  /** The auth store, watched so another account or another set of roles never sees a stale answer. */
  const auth = useAuthStore()

  /** The installed version; empty while unknown or after a failed load. */
  const version = ref('')

  /** Human-readable sentence from the last failed load; null when there is none. */
  const error = ref<string | null>(null)

  /** The request in flight, shared by every caller; null when none is running. */
  let inFlight: Promise<void> | null = null

  /** Bumped on every reset, so an answer to a request started before it is thrown away. */
  let generation = 0

  /**
   * Loads the version unless it is already known or a request is already running.
   *
   * @returns Resolves when the shared request is over; a refusal leaves {@link version} empty and
   * the next call asks again.
   */
  const ensureLoaded = (): Promise<void> => {
    if (version.value !== '') {
      return Promise.resolve()
    }
    if (inFlight !== null) {
      return inFlight
    }
    const started = generation
    const request = (async (): Promise<void> => {
      const result = await runAction(error, api.getInstalledVersion)
      if (started !== generation) {
        return
      }
      inFlight = null
      if (result.ok) {
        version.value = result.value.installedVersion
      }
    })()
    inFlight = request
    return request
  }

  /** Forgets the version, the error and any request in flight (its answer will be ignored). */
  const reset = (): void => {
    generation += 1
    inFlight = null
    version.value = ''
    error.value = null
  }

  // Sign-out clears the user, and a role change replaces it: either way the answer may no longer
  // be one this account may see. Synchronous, so the reset lands before anything reloads.
  watch(
    () => auth.accountKey,
    reset,
    { flush: 'sync' },
  )

  return { version, error, ensureLoaded, reset }
})
