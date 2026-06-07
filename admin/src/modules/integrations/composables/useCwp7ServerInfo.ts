import { ref } from 'vue'
import type { Cwp7ServerInfoDto } from '../types/cwp7.types'
import { useApi } from '@/composables/useApi'

/**
 * Fetches and manages live CWP7 server status from the server-info endpoint.
 *
 * Handles loading and error states automatically. Call `fetch()` to load data.
 *
 * @returns Reactive server info, loading flag, error message, and fetch action.
 */
export function useCwp7ServerInfo() {
  /** CWP7 server info data, null until first successful fetch. */
  const info = ref<Cwp7ServerInfoDto | null>(null)

  /** True while a fetch is in progress. */
  const loading = ref(false)

  /** Human-readable error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches live CWP7 server status from the backend.
   *
   * On network failure, sets error and leaves info as previous value.
   * Never throws — safe to call without try/catch.
   *
   * @returns Promise that resolves when the fetch completes.
   */
  async function fetch(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const { request } = useApi()
      info.value = await request<Cwp7ServerInfoDto>('/admin/integrations/cwp7/server-info')
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch CWP7 server info.'
    } finally {
      loading.value = false
    }
  }

  return { info, loading, error, fetch }
}
