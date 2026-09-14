/**
 * Endpoints under `/api/portal/public` that expose the operator's configured billing
 * currencies to an anonymous visitor.
 *
 * Like the other `apis/use*Api.ts` files, this one is a list of URLs and nothing else — no
 * `ref`, no caching, no error handling. `stores/currency.ts` is the only intended caller.
 *
 * @module composables/apis/useCurrencyApi
 */

import { apiFetch } from '~/composables/useApi'
import type { BillingCurrency } from '~/types/currency'

/**
 * The `/api/portal/public/billing-currencies` surface.
 *
 * @returns The currency endpoint functions.
 */
export function useCurrencyApi() {
  /**
   * Loads every currency a visitor may be billed in, base first.
   *
   * @returns The configured billing currencies.
   * @throws Whatever `apiFetch` throws.
   */
  const loadBillingCurrencies = (): Promise<BillingCurrency[]> =>
    apiFetch<BillingCurrency[]>('/api/portal/public/billing-currencies')

  return { loadBillingCurrencies }
}
