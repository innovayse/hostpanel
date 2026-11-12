/**
 * Endpoints behind the tax rules store: the rule list and the create, update and delete of one rule.
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { useApi } from '@/composables/useApi'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'
import type { SaveTaxRulePayload, TaxRuleDto } from '@/types/taxrule'

/**
 * Endpoint functions for `/admin/tax-rules`.
 *
 * @returns One typed function per endpoint.
 */
export const useTaxRulesApi = () => {
  const { request } = useApi()

  /**
   * Lists every tax rule.
   *
   * @returns The rules.
   */
  const listTaxRules = (): Promise<TaxRuleDto[]> => request<TaxRuleDto[]>(ApiPath.AdminTaxRules)

  /**
   * Creates a tax rule for a country, or for one region of it.
   *
   * @param payload - The rule's name, country, region, rate and switch.
   * @returns The new rule's id.
   */
  const createTaxRule = (payload: SaveTaxRulePayload): Promise<number> =>
    request<number>(ApiPath.AdminTaxRules, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Replaces a tax rule's fields.
   *
   * @param id - The rule's id.
   * @param payload - The rule's new fields.
   * @returns Nothing; the API answers 204.
   */
  const updateTaxRule = (id: number, payload: SaveTaxRulePayload): Promise<void> =>
    request<void>(`${ApiPath.AdminTaxRules}/${id}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Deletes a tax rule; invoices already issued keep the rate they were taxed at.
   *
   * @param id - The rule's id.
   * @returns Nothing; the API answers 204.
   */
  const deleteTaxRule = (id: number): Promise<void> =>
    request<void>(`${ApiPath.AdminTaxRules}/${id}`, { method: HttpMethod.Delete })

  return { listTaxRules, createTaxRule, updateTaxRule, deleteTaxRule }
}
