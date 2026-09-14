/**
 * Pinia store for admin currency configuration.
 *
 * Provides the configured currency list, per-currency upsert, base switching,
 * exchange-rate refresh, and the bulk product-price recompute.
 */
import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { CurrencyDto, UpdateExchangeRatesResult, UpsertCurrencyPayload } from '../../../types/currency'

/** Manages the configured-currency list and admin currency operations. */
export const useCurrenciesStore = defineStore('currencies', () => {
  const { request } = useApi()

  /** All configured currencies, base first. */
  const currencies = ref<CurrencyDto[]>([])

  /** Whether a fetch is in progress. */
  const loading = ref(false)

  /** Error message from the last operation, null when no error. */
  const error = ref<string | null>(null)

  /** Currencies a payer may currently be billed in. */
  const enabledCurrencies = computed<CurrencyDto[]>(() => currencies.value.filter(c => c.isEnabled))

  /**
   * Fetches all configured currencies from the backend.
   *
   * @returns Promise that resolves when currencies are loaded.
   */
  async function fetchAll(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      currencies.value = await request<CurrencyDto[]>('/admin/currencies')
    } catch {
      error.value = 'Failed to load currencies.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Creates a currency under the code, or rewrites the one already stored there, then refreshes.
   *
   * @param code - ISO 4217 alpha code, any case.
   * @param payload - The currency's numeric code, display, rate and switch.
   * @returns Promise that resolves when the currency is saved and the list is refreshed.
   */
  async function upsert(code: string, payload: UpsertCurrencyPayload): Promise<void> {
    await request(`/admin/currencies/${code}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    })
    await fetchAll()
  }

  /**
   * Makes the given currency the base and refreshes the list.
   *
   * @param code - ISO 4217 alpha code, any case.
   * @returns Promise that resolves when the switch is applied and the list is refreshed.
   */
  async function makeBase(code: string): Promise<void> {
    await request(`/admin/currencies/${code}/make-base`, { method: 'POST' })
    await fetchAll()
  }

  /**
   * Refreshes every non-base rate from the exchange-rate source and refreshes the list.
   *
   * @returns Which codes were updated and which the source did not quote.
   */
  async function updateRates(): Promise<UpdateExchangeRatesResult> {
    const result = await request<UpdateExchangeRatesResult>('/admin/currencies/update-rates', { method: 'POST' })
    await fetchAll()
    return result
  }

  /**
   * Recomputes every product's prices in the given currencies from their base prices.
   *
   * @param codes - Non-base currency codes to recompute.
   * @returns How many price rows were written.
   */
  async function updateProductPrices(codes: string[]): Promise<number> {
    return await request<number>('/admin/currencies/update-product-prices', {
      method: 'POST',
      body: JSON.stringify({ currencies: codes }),
    })
  }

  return {
    currencies, loading, error, enabledCurrencies,
    fetchAll, upsert, makeBase, updateRates, updateProductPrices,
  }
})
