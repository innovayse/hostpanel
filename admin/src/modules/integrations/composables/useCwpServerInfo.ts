import { ref } from 'vue'
import type { CwpServerInfoDto } from '../types/cwp.types'
import { useApi } from '@/composables/useApi'

/**
 * Fetches and manages live CWP server status from the server-info endpoint.
 *
 * Handles loading and error states automatically. Call `fetch()` to load data.
 *
 * @returns Reactive server info, loading flag, error message, and fetch action.
 */
export function useCwpServerInfo() {
  /** CWP server info data, null until first successful fetch. */
  const info = ref<CwpServerInfoDto | null>(null)

  /** True while a fetch is in progress. */
  const loading = ref(false)

  /** Human-readable error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches live CWP server status from the backend.
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
      info.value = await request<CwpServerInfoDto>('/admin/integrations/cwp/server-info')
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch CWP server info.'
    } finally {
      loading.value = false
    }
  }

  return { info, loading, error, fetch }
}
