import { useCatalogApi } from '~/composables/apis/useCatalogApi'

/**
 * Provides currency lookup and formatting helpers sourced from WHMCS GetCurrencies.
 *
 * Usage in <script setup>:
 *   const { format } = useCurrency()
 *   // template: {{ format(service.recurringamount, store.user?.currency) }}
 *
 * Currencies are fetched once and cached by useFetch's key deduplication.
 */

/**
 * Currency lookup and formatting helpers.
 *
 * @returns The prefix/suffix lookups and the amount formatter.
 */
export function useCurrency() {
  // `server: false` — the amounts this decorates are themselves client-side, so the
  // storefront does not pay for the currency list on every server render.
  const { data: currencies } = useCatalogApi().loadCurrencies(false)

  /** Return the prefix string for a given WHMCS currency ID */
  function prefixFor(id?: number): string {
    return (currencies.value ?? []).find(c => c.id === id)?.prefix ?? ''
  }

  /** Return the suffix string for a given WHMCS currency ID */
  function suffixFor(id?: number): string {
    return (currencies.value ?? []).find(c => c.id === id)?.suffix ?? ''
  }

  /**
   * Format a WHMCS amount string with its currency prefix/suffix.
   * Returns "Free" for zero amounts.
   *
   * @param amount     - Raw amount string from WHMCS, e.g. "9.99"
   * @param currencyId - WHMCS currency ID from client profile (store.user?.currency)
   */
  function format(amount: string | number, currencyId?: number): string {
    if (!amount || amount === '0.00' || amount === 0) return 'Free'
    return `${prefixFor(currencyId)}${amount}${suffixFor(currencyId)}`.trim()
  }

  return { currencies, prefixFor, suffixFor, format }
}
