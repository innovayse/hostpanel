/**
 * Unit tests for AuroraCheckout: the empty cart, the guest form with its placeholders, the
 * signed-in account line, payment method choice, the order summary and the place-order flow,
 * with the real cart, currency, checkout, auth and client stores, the real API composables and
 * the real transport; only the wire is a stub server that plays the BFF.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { VueWrapper } from '@vue/test-utils'
import { mountAsyncPage } from '~/tests/helpers/mountWithKit'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { heldReply, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { settle, until } from '~/tests/helpers/settle'
import { startTransport } from '~/tests/helpers/stubTransport'
import AuroraCheckout from '~/templates/aurora/pages/AuroraCheckout.vue'
import GatewayOption from '~/components/checkout/GatewayOption.vue'
import SaveOption from '~/components/checkout/SaveOption.vue'
import SubmitHint from '~/components/checkout/SubmitHint.vue'
import { clientOrderSuccessPath } from '~/constants/clientRoutes'
import { useAuthStore } from '~/stores/auth'
import { useCartStore } from '~/stores/cart'
import { useClientStore } from '~/stores/client'
import { useCurrencyStore } from '~/stores/currency'
import type { CartItem } from '~/types/cartitem'
import type { ClientUser } from '~/types/clientuser'
import type { BillingCurrency } from '~/types/currency'
import { Permission } from '~/types/permission'
import { PricingCycle } from '~/types/pricingcycle'
import { stubAuroraGlobals } from '../auroraGlobals'

/** The gateway list the BFF answers the checkout with. */
const GATEWAYS_PATH = '/api/portal/order/payment-methods'

/** The catalogue read that re-prices the cart when the page opens. */
const PRODUCTS_PATH = '/api/portal/public/products'

/** The order creation the place-order button ends in. */
const CREATE_ORDER_PATH = '/api/portal/order/create'

/** The sign-out the "not you" button ends in. */
const LOGOUT_PATH = '/api/portal/auth/logout'

/** The gateways offered unless a test says otherwise; the first one is preselected, and both are hosted gateways. */
const GATEWAYS = [{ module: 'example_gateway', displayName: 'Example Gateway' }, { module: 'card_gateway', displayName: 'Card' }]

/** What the BFF answers a placed order with when nothing is due, so the payer goes straight to the confirmation. */
const SETTLED_ORDER = { orderId: 41, orderNumber: '1001', invoiceId: 5, paymentToken: 'token', total: 0, currency: 'USD' }

/** The base currency of the stand-in billing currencies. */
const USD: BillingCurrency = { code: 'USD', numeric: '840', prefix: '$', suffix: '', decimals: 2, isBase: true }

/** The other currency the guest may pick. */
const EUR: BillingCurrency = { code: 'EUR', numeric: '978', prefix: '€', suffix: '', decimals: 2, isBase: false }

/** One cart line as the summary renders it. */
const ITEM: CartItem = { pid: 7, name: 'Business hosting', billingcycle: PricingCycle.Monthly, cycleLabel: 'Monthly', amount: 9.99, currency: 'USD' }

/** The stub playing the BFF. */
let stub: StubServer

/** What the stub answers, by route; the tests below change entries for their scenario. */
const replies: StubRouteTable = {}

/** What a test can set before the page mounts. */
interface CheckoutSetup {
  /** The cart lines; defaults to one line. */
  items?: CartItem[]
  /** Whether a client is signed in, so the account line shows instead of the guest form. */
  signedIn?: boolean
  /** The person the client store holds, which decides what they may do on the account. */
  person?: ClientUser
}

/**
 * Mounts the page under a Suspense boundary, because its setup awaits the payment methods, with
 * the stores filled as the setup says, and waits until the page has rendered and re-priced its cart.
 *
 * @param setup What the stores hold before the page opens.
 * @returns The wrapper once the page and the reads it starts have settled.
 */
const mountCheckout = async (setup: CheckoutSetup = {}): Promise<VueWrapper> => {
  const pinia = createPinia()
  setActivePinia(pinia)
  const items = setup.items ?? [ITEM]
  useCartStore(pinia).items = items
  const currency = useCurrencyStore(pinia)
  currency.currencies = [USD, EUR]
  currency.loaded = true
  if (setup.signedIn) {
    useAuthStore(pinia).user = makeClientUser({ firstName: 'Ann', lastName: 'Lee', email: 'ann@acme.test' })
  }
  if (setup.person) {
    useClientStore(pinia).user = setup.person
  }
  const pricesBefore = requestsTo(stub.requests, PRODUCTS_PATH).length
  const wrapper = await mountAsyncPage(AuroraCheckout, {
    pinia,
    components: { CheckoutGatewayOption: GatewayOption, CheckoutSaveOption: SaveOption, CheckoutSubmitHint: SubmitHint },
  })
  await until(() => wrapper.find('[data-testid="checkout-title"]').exists(), 'the checkout page to render')
  if (items.length > 0) {
    await until(() => requestsTo(stub.requests, PRODUCTS_PATH).length > pricesBefore, 'the cart prices to be read')
  }
  await settle(10)
  return wrapper
}

/**
 * Clicks the place-order button.
 *
 * @param wrapper The mounted page.
 */
const placeOrder = async (wrapper: VueWrapper): Promise<void> => {
  await wrapper.find('[data-testid="checkout-place-order"]').trigger('click')
}

describe('AuroraCheckout', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    Object.assign(replies, {
      [`GET ${GATEWAYS_PATH}`]: { body: GATEWAYS },
      [`GET ${PRODUCTS_PATH}`]: { body: [] },
      [`POST ${CREATE_ORDER_PATH}`]: { body: SETTLED_ORDER },
      [`POST ${LOGOUT_PATH}`]: { body: {} },
    })
    window.localStorage.clear()
    vi.stubGlobal('useHead', vi.fn())
    stubAuroraGlobals()
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('offers the way back to hosting instead of a form when the cart is empty', async () => {
    const wrapper = await mountCheckout({ items: [] })

    expect(wrapper.find('[data-testid="checkout-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="checkout-browse-hosting"]').attributes('href')).toBe('/hosting')
    expect(wrapper.find('[data-testid="checkout-account"]').exists()).toBe(false)
  })

  it('offers the renewal-save box, unticked, only when the store says so', async () => {
    const offered = await mountCheckout({ signedIn: true })

    expect(offered.find('[data-testid="checkout-save-for-renewals"]').attributes('aria-checked')).toBe('false')
    expect(offered.text()).toContain('checkout.saveForRenewalsAgreement')

    const hidden = await mountCheckout()

    expect(hidden.find('[data-testid="checkout-save-for-renewals"]').exists()).toBe(false)
  })

  it('gives every guest field a translated placeholder', async () => {
    const wrapper = await mountCheckout()

    for (const id of ['firstname', 'lastname', 'email', 'password']) {
      expect(wrapper.find(`[data-testid="checkout-${id}"]`).attributes('placeholder')).toBe(`checkout.placeholders.${id}`)
    }
    expect(wrapper.find('[data-testid="checkout-sign-in"]').attributes('href')).toBe('/client/login')
  })

  it('shows the signed-in account line instead of the guest form, and signs out on "not you"', async () => {
    const wrapper = await mountCheckout({ signedIn: true })

    expect(wrapper.find('[data-testid="checkout-logged-in"]').text()).toContain('ann@acme.test')
    expect(wrapper.find('[data-testid="checkout-firstname"]').exists()).toBe(false)

    await wrapper.find('[data-testid="checkout-not-you"]').trigger('click')
    await until(() => requestsTo(stub.requests, LOGOUT_PATH).length > 0, 'the sign-out to reach the BFF')

    const signOuts = requestsTo(stub.requests, LOGOUT_PATH)
    expect(signOuts).toHaveLength(1)
    expect(signOuts[0]?.method).toBe('POST')
  })

  it('lists the gateway methods and marks the selected one', async () => {
    const wrapper = await mountCheckout()

    expect(wrapper.find('[data-testid="checkout-method-example_gateway"]').classes()).toContain('border-ac1')
    expect(wrapper.find('[data-testid="checkout-method-card_gateway"]').classes()).not.toContain('border-ac1')
    expect(wrapper.find('[data-testid="checkout-method-card_gateway"]').text()).toContain('Card')
  })

  it('says so when no payment method is available', async () => {
    replies[`GET ${GATEWAYS_PATH}`] = { body: [] }
    const wrapper = await mountCheckout()

    expect(wrapper.find('[data-testid="checkout-no-methods"]').text()).toBe('checkout.noPaymentMethods')
  })

  it('summarises the order with each line and the total', async () => {
    const wrapper = await mountCheckout()

    expect(wrapper.find('[data-testid="checkout-item-7"]').text()).toContain('Business hosting')
    expect(wrapper.find('[data-testid="checkout-item-7"]').text()).toContain('$9.99')
    expect(wrapper.find('[data-testid="checkout-total"]').text()).toBe('$9.99')
  })

  it('shows the store error as an alert above the button', async () => {
    replies[`POST ${CREATE_ORDER_PATH}`] = { status: 400, body: { detail: 'Card declined' } }
    const wrapper = await mountCheckout()

    await placeOrder(wrapper)
    await until(() => requestsTo(stub.requests, CREATE_ORDER_PATH).length > 0, 'the order to reach the BFF')
    await settle(10)

    expect(wrapper.find('[data-testid="checkout-error"]').text()).toBe('Card declined')
  })

  it('places the order and follows the destination the store returns', async () => {
    const wrapper = await mountCheckout()

    await placeOrder(wrapper)
    await until(() => vi.mocked(navigateTo).mock.calls.length > 0, 'the page to follow the destination')

    const orders = requestsTo(stub.requests, CREATE_ORDER_PATH)
    expect(orders).toHaveLength(1)
    expect(orders[0]?.method).toBe('POST')
    expect(JSON.parse(orders[0]?.body ?? '')).toMatchObject({ paymentmethod: 'example_gateway' })
    expect(navigateTo).toHaveBeenCalledWith(clientOrderSuccessPath({ order: '1001', invoice: 5 }))
  })

  it('keeps the busy label short on the button and puts the sentence in a polite line under it', async () => {
    const held = heldReply()
    replies[`POST ${CREATE_ORDER_PATH}`] = held.reply
    const wrapper = await mountCheckout()

    await placeOrder(wrapper)
    await until(() => held.waiting() > 0, 'the order to wait at the BFF')
    await settle(5)

    const button = wrapper.find('[data-testid="checkout-place-order"]')
    expect(button.text()).toBe('checkout.redirecting')
    expect(button.text()).not.toContain('checkout.redirectingToBank')
    expect(button.classes()).toContain('text-base')
    expect(button.classes()).not.toContain('text-lg')
    const hint = wrapper.find('[data-testid="checkout-submit-hint"]')
    expect(hint.text()).toBe('checkout.redirectingToBank')
    expect(hint.attributes('aria-live')).toBe('polite')

    held.release({ status: 400, body: { detail: 'Stopped.' } })
    await settle(5)
  })

  it('disables the button while no payment method is selected', async () => {
    replies[`GET ${GATEWAYS_PATH}`] = { body: [] }
    const wrapper = await mountCheckout()

    expect(wrapper.find('[data-testid="checkout-place-order"]').attributes('disabled')).toBeDefined()
  })

  describe('purchase permission', () => {
    it('offers the gateways and the order button to a member who may place orders', async () => {
      const wrapper = await mountCheckout({ person: makeClientUser({ permissions: Permission.PlaceNewOrders }) })

      expect(wrapper.find('[data-testid="checkout-permission-denied"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="checkout-method-card_gateway-input"]').attributes('disabled')).toBeUndefined()
      expect(wrapper.find('[data-testid="checkout-place-order"]').attributes('disabled')).toBeUndefined()
    })

    it('disables the gateways and the order button, and says why, for a member who may not place orders', async () => {
      const wrapper = await mountCheckout({ person: makeClientUser({ permissions: Permission.ViewPayInvoices }) })

      expect(wrapper.find('[data-testid="checkout-permission-denied"]').text()).toContain('checkout.permissionDenied')
      expect(wrapper.find('[data-testid="checkout-method-card_gateway-input"]').attributes('disabled')).toBeDefined()
      expect(wrapper.find('[data-testid="checkout-place-order"]').attributes('disabled')).toBeDefined()

      await placeOrder(wrapper)
      await settle(10)
      expect(requestsTo(stub.requests, CREATE_ORDER_PATH)).toHaveLength(0)
    })

    it('keeps the checkout open for a signed-in first-time buyer who acts for no account', async () => {
      const wrapper = await mountCheckout({ signedIn: true, person: makeClientUser({ permissions: 0, actsForAccount: false }) })

      expect(wrapper.find('[data-testid="checkout-permission-denied"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="checkout-place-order"]').attributes('disabled')).toBeUndefined()
    })
  })
})
