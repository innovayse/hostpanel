/**
 * Pinia store for the admin tax rules page.
 *
 * Holds the paged rule list and the create, update and delete of one rule. The only caller of
 * `useTaxRulesApi`; a refusal is kept as the API's own sentence, never thrown. A write does not
 * re-read the list: the screen does, because only it knows the page and the filters.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useTaxRulesApi } from '@/composables/apis/useTaxRulesApi'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { runAction } from '@/lib/runAction'
import type { SaveTaxRulePayload, TaxRuleDto, TaxRuleListFilters } from '@/types/taxrule'

/** Manages the tax rule list and the admin tax rule operations. */
export const useTaxRulesStore = defineStore('taxRules', () => {
  const api = useTaxRulesApi()

  /** The tax rules of the loaded page, as the API lists them. */
  const taxRules = ref<TaxRuleDto[]>([])

  /** Items per page. */
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  /** Total number of matching tax rules across all pages. */
  const totalCount = ref(0)

  /** Whether a fetch is in progress. */
  const loading = ref(false)

  /** Sentence for the last failed load, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last failed mutation (create, update, delete), null when none. */
  const actionError = ref<string | null>(null)

  /**
   * Loads one page of tax rules. The page, the search and the filters come from the list's address, so the store
   * keeps no copy of them.
   *
   * @param page - Page to load, 1-based.
   * @param filters - The search text and the enabled filter; empty for none.
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchAll = async (page: number, filters: TaxRuleListFilters = {}): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () =>
      api.listTaxRules({ ...filters, page, pageSize: pageSize.value, search: filters.search?.trim() }),
    )
    loading.value = false
    if (result.ok) {
      taxRules.value = result.value.items
      totalCount.value = result.value.totalCount
    }
    return result.ok
  }

  /**
   * Creates a tax rule.
   *
   * @param payload - The rule's name, country, region, rate and switch.
   * @returns True when created; false when refused (see `actionError`).
   */
  const create = async (payload: SaveTaxRulePayload): Promise<boolean> => {
    return (await runAction(actionError, () => api.createTaxRule(payload))).ok
  }

  /**
   * Replaces a tax rule's fields.
   *
   * @param id - The rule's id.
   * @param payload - The rule's new fields.
   * @returns True when saved; false when refused (see `actionError`).
   */
  const update = async (id: number, payload: SaveTaxRulePayload): Promise<boolean> => {
    return (await runAction(actionError, () => api.updateTaxRule(id, payload))).ok
  }

  /**
   * Deletes a tax rule.
   *
   * @param id - The rule's id.
   * @returns True when deleted; false when refused (see `actionError`).
   */
  const remove = async (id: number): Promise<boolean> => {
    return (await runAction(actionError, () => api.deleteTaxRule(id))).ok
  }

  return { taxRules, pageSize, totalCount, loading, error, actionError, fetchAll, create, update, remove }
})
