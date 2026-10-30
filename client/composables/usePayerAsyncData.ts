/**
 * `useAsyncData` for reads whose result is quoted in the payer's currency.
 *
 * @module composables/usePayerAsyncData
 */

import type { AsyncDataOptions } from 'nuxt/app'
import { useCurrencyStore } from '~/stores/currency'

/**
 * Runs `useAsyncData` under a key that carries the payer's currency.
 *
 * The server render never knows the signed-in client (their profile loads in the browser only,
 * see `plugins/client-auth.ts`), so it reads prices in the guest's or base currency. In the browser
 * the profile loads before the page's setup runs, so the payer currency is already the client's own
 * when the read starts: a fixed key would hydrate from the server's payload in the other currency,
 * and a `watch` on the currency would never fire because it never changes in the browser (#662).
 * With the currency in the key, the browser's key misses that payload and the read runs again, and
 * a later currency change re-keys it the same way, so callers need no currency `watch`.
 *
 * @param key - The read's own key, without the currency.
 * @param handler - The read.
 * @param options - Options passed through to `useAsyncData`, e.g. a `watch` on the locale.
 * @returns What `useAsyncData` returns.
 */
export const usePayerAsyncData = <T>(key: string, handler: () => Promise<T>, options?: AsyncDataOptions<T>) => {
  const currencyStore = useCurrencyStore()
  return useAsyncData(() => `${key}:${currencyStore.payerCode ?? ''}`, handler, options)
}
