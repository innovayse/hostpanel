/**
 * Pinia store for the integration list, the per-integration config page and the live CWP server
 * status.
 *
 * The only caller of `useIntegrationsApi` for these screens. A refusal is kept as the API's own
 * sentence in `error`; a failed connection test is reported as an unsuccessful `testResult`
 * carrying that sentence. Nothing throws.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useIntegrationsApi } from '@/composables/apis/useIntegrationsApi'
import { runAction } from '@/lib/runAction'
import { IntegrationCategory, IntegrationCategoryName } from '@/types/integration'
import { PluginType } from '@/types/plugin'
import type { CwpServerInfoDto } from '@/types/cwp'
import type {
  IntegrationConfigPayload,
  IntegrationDetailDto,
  IntegrationDto,
  IntegrationTestResult,
} from '@/types/integration'

/** Maps backend category strings to frontend IntegrationCategory keys. */
const CATEGORY_MAP: Record<string, IntegrationCategory> = {
  [IntegrationCategoryName.PaymentGateways]: IntegrationCategory.Payments,
  [IntegrationCategoryName.DomainRegistrars]: IntegrationCategory.Registrars,
  [IntegrationCategoryName.Provisioning]: IntegrationCategory.Provisioning,
  [IntegrationCategoryName.Email]: IntegrationCategory.Email,
  [IntegrationCategoryName.FraudProtection]: IntegrationCategory.Fraud,
}

/**
 * The category a plugin of each type is listed under. A plugin's category name is free text from its
 * manifest, so its declared type decides; fulfilment, identity and hosted plugins have no built-in category
 * name that could hold them.
 */
const PLUGIN_TYPE_CATEGORY: Record<PluginType, IntegrationCategory> = {
  [PluginType.Payment]: IntegrationCategory.Payments,
  [PluginType.Registrar]: IntegrationCategory.Registrars,
  [PluginType.Provisioning]: IntegrationCategory.Provisioning,
  [PluginType.Fulfilment]: IntegrationCategory.Fulfilment,
  [PluginType.Identity]: IntegrationCategory.Identity,
  [PluginType.Hosted]: IntegrationCategory.Hosted,
}

/**
 * Normalises an integration's category to the frontend IntegrationCategory key: a plugin by its declared
 * type, a built-in integration by the API's category name.
 *
 * @param integration - The summary as returned by the backend.
 * @returns Mapped frontend category key, or the original string cast as IntegrationCategory.
 */
const normaliseCategory = (integration: IntegrationDto): IntegrationCategory => {
  const byType = integration.pluginType === null ? undefined : PLUGIN_TYPE_CATEGORY[integration.pluginType]
  return byType ?? CATEGORY_MAP[integration.category] ?? integration.category
}

/** Manages the integrations summary list and the config of the integration being edited. */
export const useIntegrationsStore = defineStore('integrations', () => {
  const api = useIntegrationsApi()

  /** All integrations summary list. */
  const integrations = ref<IntegrationDto[]>([])

  /** Currently loaded integration detail (config page). */
  const current = ref<IntegrationDetailDto | null>(null)

  /** True while any request is in flight. */
  const loading = ref(false)

  /**
   * True while a connection test is in flight. Kept apart from `loading` on purpose: that flag
   * also drives the Save button's "Saving" label and the initial page skeleton, so sharing it
   * would make the Save button claim it was saving and leave the status sidebar unable to tell
   * "testing now" from "not tested yet".
   */
  const testing = ref(false)

  /** Sentence for the last failed load or save, null when none. */
  const error = ref<string | null>(null)

  /** Result of the last connection test. */
  const testResult = ref<IntegrationTestResult | null>(null)

  /** Live status of the CWP server, null until the first successful read. */
  const cwpServerInfo = ref<CwpServerInfoDto | null>(null)

  /**
   * Sentence for the last failed CWP status read, null when none. Kept apart from `error` so a
   * status refusal never hides the config page's own load or save refusal.
   */
  const cwpServerInfoError = ref<string | null>(null)

  /**
   * Fetches the summary list of all integrations.
   *
   * @returns Resolves when the request is over; a refusal is left in `error`.
   */
  const fetchAll = async (): Promise<void> => {
    loading.value = true
    const result = await runAction(error, api.listIntegrations)
    loading.value = false
    if (result.ok) {
      integrations.value = result.value.map(i => ({ ...i, category: normaliseCategory(i) }))
    }
  }

  /**
   * Fetches full config detail for a single integration by slug.
   *
   * @param slug - Integration slug, as used in the admin routes.
   * @returns Resolves when the request is over; a refusal is left in `error`.
   */
  const fetchOne = async (slug: string): Promise<void> => {
    loading.value = true
    current.value = null
    const result = await runAction(error, () => api.getIntegration(slug))
    loading.value = false
    if (result.ok) {
      current.value = result.value
    }
  }

  /**
   * Saves updated config for an integration, then re-reads it.
   *
   * The PUT answers 204 No Content, so there is no detail in its response to show. Assigning
   * it to `current` blanked the whole page: the form is rendered from `current`, and one
   * successful save replaced it with undefined. Re-reading is also the only way to see what the
   * server actually stored, since secrets come back masked and the enabled state can be refused
   * when required fields are missing.
   *
   * @param slug - Integration slug.
   * @param payload - Updated enabled state and config values.
   * @returns True when the save was accepted (after the re-read); false when refused, with the
   * sentence left in `error`.
   */
  const saveConfig = async (slug: string, payload: IntegrationConfigPayload): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () => api.saveIntegration(slug, payload))
    loading.value = false
    if (result.ok) {
      await fetchOne(slug)
    }
    return result.ok
  }

  /**
   * Runs a live connection test for an integration.
   *
   * @param slug - Integration slug.
   * @returns Resolves when the test is over; the outcome is in `testResult`.
   */
  const testConnection = async (slug: string): Promise<void> => {
    testing.value = true
    testResult.value = null
    /** Local sink for the refusal sentence, which becomes the result's message. */
    const refusal = ref<string | null>(null)
    const startedAt = performance.now()
    const result = await runAction(refusal, () => api.testIntegration(slug))
    const durationMs = Math.round(performance.now() - startedAt)
    testing.value = false
    if (!result.ok) {
      testResult.value = { success: false, message: refusal.value ?? '', durationMs }
      return
    }
    testResult.value = { ...result.value, durationMs }
  }

  /**
   * Reads the live status of the CWP server.
   *
   * On failure the previous status stays in `cwpServerInfo` and the refusal is left in
   * `cwpServerInfoError`.
   *
   * @returns Resolves when the request is over.
   */
  const fetchCwpServerInfo = async (): Promise<void> => {
    const result = await runAction(cwpServerInfoError, api.getCwpServerInfo)
    if (result.ok) {
      cwpServerInfo.value = result.value
    }
  }

  return {
    integrations,
    current,
    loading,
    testing,
    error,
    testResult,
    cwpServerInfo,
    cwpServerInfoError,
    fetchAll,
    fetchOne,
    saveConfig,
    testConnection,
    fetchCwpServerInfo,
  }
})
