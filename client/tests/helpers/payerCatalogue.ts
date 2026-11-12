/**
 * Support for page tests that read the public catalogue in the payer's currency over a real socket.
 *
 * A storefront page reads the catalogue, then (once mounted) the billing currencies, and reads the
 * catalogue again when the payer's currency resolves. With the real stores and a stub BFF those reads
 * finish after the page's own promise flush, so a test waits here until the currency store is loaded
 * and no catalogue read is in flight, and only then asserts on what the page shows.
 *
 * @module tests/helpers/payerCatalogue
 */

import { flushPromises } from '@vue/test-utils'
import { until, untilStable } from '~/tests/helpers/settle'
import { useCatalogStore } from '~/stores/catalog'
import { useCurrencyStore } from '~/stores/currency'
import type { BillingCurrency } from '~/types/currency'

/** The one currency the stub's billing-currencies endpoint offers: it is the base, so it becomes the payer's. */
export const USD_CURRENCY: BillingCurrency = { code: 'USD', numeric: '840', prefix: '$', suffix: '', decimals: 2, isBase: true }

/**
 * Waits until the currencies are loaded and the catalogue is idle, in the active Pinia.
 *
 * Call it after the page has mounted. The re-read that follows the payer currency resolving can start
 * after the currencies land, so once they are loaded the wait goes on until both stores stop changing.
 *
 * @returns Resolves once the page has rendered the catalogue in the payer's currency.
 */
export const awaitCatalogue = async (): Promise<void> => {
  const currencyStore = useCurrencyStore()
  const catalogStore = useCatalogStore()
  await until(() => currencyStore.loaded && !catalogStore.loading, 'the currencies and the catalogue to be read in the payer currency')
  await untilStable(() => JSON.stringify([currencyStore.loaded, catalogStore.loading, catalogStore.$state]), 'the catalogue to stop changing')
  await flushPromises()
}
