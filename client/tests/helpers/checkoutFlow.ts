/**
 * Support for the checkout page tests (both templates) against the real cart, currency, checkout,
 * auth and client stores, the real API composables and transport, with a stub BFF answering on a
 * real socket: the replies a checkout reaches, a cart line, and the wait until the page has read
 * its currencies and gateways.
 *
 * @module tests/helpers/checkoutFlow
 */

import { flushPromises } from '@vue/test-utils'
import { AccountEndpoint } from '~/tests/helpers/accountPaths'
import { bankTransferPath, BillingEndpoint } from '~/tests/helpers/billingPaths'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { USD_CURRENCY } from '~/tests/helpers/payerCatalogue'
import { until } from '~/tests/helpers/settle'
import { readOf, writeOf } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import { useCurrencyStore } from '~/stores/currency'
import type { CartItem } from '~/types/cartitem'
import type { GatewayMethod } from '~/types/gatewaymethod'
import type { PlaceOrderResult } from '~/types/placeorderresult'
import { PricingCycle } from '~/types/pricingcycle'

/** The order the stub answers a placed order with: an invoice of 12 dollars. */
export const PLACED_ORDER: PlaceOrderResult = { orderId: 7, orderNumber: 'A 7', invoiceId: 12, paymentToken: 'token', total: 12, currency: 'USD' }

/**
 * The replies a checkout reaches: the billing currencies, the gateways on offer, placing the order,
 * the guest's automatic sign-in and choosing the bank transfer. A test changes entries on the table
 * it gets back before it mounts or acts.
 *
 * @param methods - The gateways the stub offers, in the order the page lists them (the first is preselected).
 * @returns The route table.
 */
export const checkoutReplies = (methods: GatewayMethod[]): StubRouteTable => ({
  [readOf(CatalogPath.BillingCurrencies)]: { body: [USD_CURRENCY] },
  [readOf(BillingEndpoint.OrderPaymentMethods)]: { body: methods },
  [writeOf(BillingEndpoint.OrderCreate)]: { body: PLACED_ORDER },
  [writeOf(AccountEndpoint.Login)]: { body: makeClientUser() },
  [writeOf(AccountEndpoint.Logout)]: { body: {} },
  [writeOf(bankTransferPath(PLACED_ORDER.invoiceId))]: { body: {} },
})

/**
 * Builds a monthly hosting cart line.
 *
 * @param pid - Product id.
 * @param amount - Price of the line.
 * @param currency - Currency the line is priced in.
 * @returns The line.
 */
export const cartLine = (pid: number, amount: number, currency = 'USD'): CartItem => ({
  pid,
  name: `Plan ${pid}`,
  billingcycle: PricingCycle.Monthly,
  cycleLabel: 'Monthly',
  amount,
  currency,
})

/**
 * Waits until a mounted checkout page has read its gateways and its currencies.
 *
 * The page reads the gateways before it renders, then (once mounted) the currencies, so the currency
 * store being loaded means both reads have been answered and the page is on screen.
 *
 * @returns Resolves once the currencies are in the store and what they changed has rendered.
 */
export const awaitCheckoutLoaded = async (): Promise<void> => {
  await until(() => useCurrencyStore().loaded, 'the billing currencies to be read')
  await flushPromises()
}
