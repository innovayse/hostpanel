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
  /**
   * True while a connection test is in flight. Kept apart from `loading` on purpose: that flag
   * also drives the Save button's "Saving…" label and the initial page skeleton, so reusing it
   * for the test made the Save button claim it was saving and gave the status sidebar nothing
   * to distinguish "testing now" from "not tested yet".
   */
  const testing = ref(false)

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
      await request(`/admin/integrations/${slug}`, {
        method: 'PUT',
        body: JSON.stringify(payload),
      })
      // The PUT answers 204 No Content, so there is no detail in its response to show. Assigning
      // it to `current` blanked the whole page: the form is rendered from `current`, and one
      // successful save replaced it with undefined. Re-read instead — which is also the only way
      // to see what the server actually stored, since secrets come back masked and the enabled
      // state can be refused when required fields are missing.
      await fetchOne(slug)
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
    testing.value = true
    error.value = null
    testResult.value = null
    const startedAt = performance.now()
    try {
      const result = await request<IntegrationTestResult>(`/admin/integrations/${slug}/test`, {
        method: 'POST',
      })
      testResult.value = { ...result, durationMs: Math.round(performance.now() - startedAt) }
      // The detail page's "Last tested" reads the persisted value, which the backend updates
      // on a successful probe. Mirror it here so the card does not keep quoting the previous
      // run until the next full reload.
      if (result.success && current.value && result.testedAt) {
        current.value = { ...current.value, lastTestedAt: result.testedAt }
      }
    } catch {
      testResult.value = {
        success: false,
        message: 'Connection test failed.',
        durationMs: Math.round(performance.now() - startedAt),
      }
    } finally {
      testing.value = false
    }
  }

  return {
    integrations,
    current,
    loading,
    testing,
    error,
    testResult,
    fetchAll,
    fetchOne,
    saveConfig,
    testConnection,
  }
})
