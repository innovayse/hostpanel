/**
 * Provides currency lookup and formatting helpers sourced from WHMCS GetCurrencies.
 *
 * Usage in <script setup>:
 *   const { format } = useCurrency()
 *   // template: {{ format(service.recurringamount, store.user?.currency) }}
 *
 * Currencies are fetched once and cached by useFetch's key deduplication.
 */

interface WhmcsCurrency {
  id: number
  code: string
  prefix: string
  suffix: string
}

export function useCurrency() {
  const { data: currencies } = useApi<WhmcsCurrency[]>(
    '/api/portal/public/currencies',
    { server: false, default: () => [] }
  )

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
