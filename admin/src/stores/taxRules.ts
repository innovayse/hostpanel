/**
 * Pinia store for the admin tax rules page.
 *
 * Holds the rule list and the create, update and delete of one rule. The only caller of
 * `useTaxRulesApi`; a refusal is kept as the API's own sentence, never thrown, and a write is
 * followed by a re-read of the list.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useTaxRulesApi } from '@/composables/apis/useTaxRulesApi'
import { runAction } from '@/lib/runAction'
import type { SaveTaxRulePayload, TaxRuleDto } from '@/types/taxrule'

/** Manages the tax rule list and the admin tax rule operations. */
export const useTaxRulesStore = defineStore('taxRules', () => {
  const api = useTaxRulesApi()

  /** Every tax rule, as the API lists them. */
  const taxRules = ref<TaxRuleDto[]>([])

  /** Whether a fetch is in progress. */
  const loading = ref(false)

  /** Sentence for the last failed load, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last failed mutation (create, update, delete), null when none. */
  const actionError = ref<string | null>(null)

  /**
   * Fetches every tax rule from the backend.
   *
   * @returns Resolves when the request is over; a refusal is left in `error`.
   */
  const fetchAll = async (): Promise<void> => {
    loading.value = true
    const result = await runAction(error, api.listTaxRules)
    loading.value = false
    if (result.ok) {
      taxRules.value = result.value
    }
  }

  /**
   * Creates a tax rule, then refreshes the list.
   *
   * @param payload - The rule's name, country, region, rate and switch.
   * @returns True when created; false when refused (see `actionError`).
   */
  const create = async (payload: SaveTaxRulePayload): Promise<boolean> => {
    const result = await runAction(actionError, () => api.createTaxRule(payload))
    if (result.ok) {
      await fetchAll()
    }
    return result.ok
  }

  /**
   * Replaces a tax rule's fields, then refreshes the list.
   *
   * @param id - The rule's id.
   * @param payload - The rule's new fields.
   * @returns True when saved; false when refused (see `actionError`).
   */
  const update = async (id: number, payload: SaveTaxRulePayload): Promise<boolean> => {
    const result = await runAction(actionError, () => api.updateTaxRule(id, payload))
    if (result.ok) {
      await fetchAll()
    }
    return result.ok
  }

  /**
   * Deletes a tax rule, then refreshes the list.
   *
   * @param id - The rule's id.
   * @returns True when deleted; false when refused (see `actionError`).
   */
  const remove = async (id: number): Promise<boolean> => {
    const result = await runAction(actionError, () => api.deleteTaxRule(id))
    if (result.ok) {
      await fetchAll()
    }
    return result.ok
  }

  return { taxRules, loading, error, actionError, fetchAll, create, update, remove }
})
