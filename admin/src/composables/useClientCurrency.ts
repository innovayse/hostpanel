/**
 * Follows the billing currency of the client a screen is pricing something for.
 *
 * A new quote is stamped with its client's billing currency when it is saved, so the editor shows
 * that currency while the record is still being drafted. The currency is read through the clients
 * store; the client picker list carries no currency.
 */

import { ref, watch } from 'vue'
import type { Ref } from 'vue'
import { useClientsStore } from '@/stores/clients'

/**
 * Creates the currency of a client that may change as the picker changes.
 *
 * @param clientId - Id of the chosen client; 0 or NaN while none is chosen.
 * @returns The ISO 4217 code of the client's billing currency; null while none is known.
 */
export const useClientCurrency = (clientId: Ref<number>): Ref<string | null> => {
  const store = useClientsStore()

  /** Billing currency of the chosen client, null until it is read. */
  const currency = ref<string | null>(null)

  watch(
    clientId,
    async (id) => {
      currency.value = null
      if (!Number.isInteger(id) || id <= 0) {
        return
      }
      const code = await store.fetchClientCurrency(id)
      // A later pick may have replaced this client while the read was in flight.
      // A write after the owner unmounted only touches a ref nobody reads any more, so it is harmless.
      if (clientId.value === id) {
        currency.value = code
      }
    },
    { immediate: true },
  )

  return currency
}
