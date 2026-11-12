/**
 * Pinia store for the fraud settings.
 *
 * It is the only caller of the settings endpoints of `useFraudApi`. A refusal becomes the API
 * sentence in `error` (a failed read) or `actionError` (a failed save), and the per-field messages
 * of a refused save land in `fieldErrors`.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useFraudApi } from '@/composables/apis/useFraudApi'
import { runAction } from '@/lib/runAction'
import type { FraudSettings } from '@/types/fraud'

/** Holds the stored fraud settings and saves a new set. */
export const useFraudSettingsStore = defineStore('fraud-settings', () => {
  const api = useFraudApi()

  /** The stored settings, null until they are read. */
  const settings = ref<FraudSettings | null>(null)

  /** True while the settings are being read. */
  const loading = ref(false)

  /** True while a save is in flight. */
  const saving = ref(false)

  /** Sentence for the last failed read, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last failed save, null when none. */
  const actionError = ref<string | null>(null)

  /** Messages of the fields the last save was refused for, by field name. */
  const fieldErrors = ref<Record<string, string>>({})

  /**
   * Reads the stored settings.
   *
   * @returns Promise that resolves when the read is over; a refusal is left in `error`.
   */
  const fetchSettings = async (): Promise<void> => {
    loading.value = true
    const result = await runAction(error, api.getSettings)
    loading.value = false
    if (result.ok) {
      settings.value = result.value
    }
  }

  /**
   * Saves the settings, then reads them back so the screen shows what the API stored.
   *
   * @param next - The settings to store.
   * @returns True when saved; false when refused (see `actionError` and `fieldErrors`).
   */
  const save = async (next: FraudSettings): Promise<boolean> => {
    saving.value = true
    const result = await runAction(actionError, () => api.updateSettings(next), fieldErrors)
    saving.value = false
    if (result.ok) {
      await fetchSettings()
    }
    return result.ok
  }

  return { settings, loading, saving, error, actionError, fieldErrors, fetchSettings, save }
})
