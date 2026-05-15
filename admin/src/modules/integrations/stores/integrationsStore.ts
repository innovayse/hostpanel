import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type {
  IntegrationDto,
  IntegrationCategory,
  IntegrationDetailDto,
  IntegrationConfigPayload,
  IntegrationTestResult,
} from '../types/integration.types'

/** Maps backend category strings to frontend IntegrationCategory keys. */
const CATEGORY_MAP: Record<string, IntegrationCategory> = {
  'Payment Gateways': 'payments',
  'Domain Registrars': 'registrars',
  'Hosting / Provisioning': 'provisioning',
  'Email / SMTP': 'email',
  'Fraud Protection': 'fraud',
}

/**
 * Normalises a raw API category string to the frontend IntegrationCategory key.
 *
 * @param raw - Category string as returned by the backend.
 * @returns Mapped frontend category key, or the original string cast as IntegrationCategory.
 */
function normaliseCategory(raw: string): IntegrationCategory {
  return CATEGORY_MAP[raw] ?? (raw as IntegrationCategory)
}

/**
 * Pinia store for managing integration list and per-integration config.
 */
export const useIntegrationsStore = defineStore('integrations', () => {
  const { request } = useApi()

  /** All integrations summary list. */
  const integrations = ref<IntegrationDto[]>([])

  /** Currently loaded integration detail (config page). */
  const current = ref<IntegrationDetailDto | null>(null)

  /** True while any request is in flight. */
  const loading = ref(false)

  /** Error message, null when no error. */
  const error = ref<string | null>(null)

  /** Result of the last connection test. */
  const testResult = ref<IntegrationTestResult | null>(null)

  /**
   * Fetches the summary list of all integrations.
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchAll(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const raw = await request<IntegrationDto[]>('/admin/integrations')
      integrations.value = raw.map(i => ({ ...i, category: normaliseCategory(i.category) }))
    } catch {
      error.value = 'Failed to load integrations.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches full config detail for a single integration by slug.
   *
   * @param slug - Integration slug (e.g. "stripe", "cpanel").
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchOne(slug: string): Promise<void> {
    loading.value = true
    error.value = null
    current.value = null
    try {
      current.value = await request<IntegrationDetailDto>(`/admin/integrations/${slug}`)
    } catch {
      error.value = 'Failed to load integration config.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Saves updated config for an integration.
   *
   * @param slug - Integration slug.
   * @param payload - Updated enabled state and config values.
   * @returns Promise that resolves when save is complete.
   */
  async function saveConfig(slug: string, payload: IntegrationConfigPayload): Promise<void> {
    loading.value = true
    error.value = null
    try {
      current.value = await request<IntegrationDetailDto>(`/admin/integrations/${slug}`, {
        method: 'PUT',
        body: JSON.stringify(payload),
      })
    } catch {
      error.value = 'Failed to save integration config.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Runs a live connection test for an integration.
   *
   * @param slug - Integration slug.
   * @returns Promise that resolves with the test result.
   */
  async function testConnection(slug: string): Promise<void> {
    loading.value = true
    error.value = null
    testResult.value = null
    try {
      testResult.value = await request<IntegrationTestResult>(`/admin/integrations/${slug}/test`, {
        method: 'POST',
      })
    } catch {
      testResult.value = { success: false, message: 'Connection test failed.' }
    } finally {
      loading.value = false
    }
  }

  return {
    integrations,
    current,
    loading,
    error,
    testResult,
    fetchAll,
    fetchOne,
    saveConfig,
    testConnection,
  }
})
