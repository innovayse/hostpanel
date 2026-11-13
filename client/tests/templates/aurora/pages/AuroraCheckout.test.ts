/**
 * Tests for AuroraCheckout against the real cart, currency, checkout, auth and client stores, the
 * real billing and auth API composables and the real transport, with a stub BFF answering on a real
 * socket (nothing of ours is mocked): the empty cart, the guest form with its placeholders, the
 * signed-in account line, payment method choice, the order summary and the place-order flow.
 *
 * @module tests/templates/aurora/pages/AuroraCheckout.test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { AccountEndpoint } from '~/tests/helpers/accountPaths'
import { BillingEndpoint } from '~/tests/helpers/billingPaths'
import { awaitCheckoutLoaded, cartLine, checkoutReplies, PLACED_ORDER } from '~/tests/helpers/checkoutFlow'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import { until } from '~/tests/helpers/settle'
import { readOf, refusal, requestsTo, routeTable, writeOf } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { waitUntil } from '~/tests/helpers/waitUntil'
import AuroraCheckout from '~/templates/aurora/pages/AuroraCheckout.vue'
import GatewayOption from '~/components/checkout/GatewayOption.vue'
import SaveOption from '~/components/checkout/SaveOption.vue'
import SubmitHint from '~/components/checkout/SubmitHint.vue'
import { clientInvoicePayPath } from '~/constants/clientRoutes'
import { useAuthStore } from '~/stores/auth'
import { useCartStore } from '~/stores/cart'
import { useCheckoutStore } from '~/stores/checkout'
import { useClientStore } from '~/stores/client'
import type { CartItem } from '~/types/cartitem'
import type { GatewayMethod } from '~/types/gatewaymethod'
import { PaymentModule } from '~/types/paymentmodule'
import { Permission } from '~/types/permission'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts or acts. */
let table: StubRouteTable

/** The gateways the stub offers unless a test says otherwise: the example gateway first (so it is the selected one), then a card gateway. */
const DEFAULT_METHODS: GatewayMethod[] = [
  { module: 'example_gateway', displayName: 'Example Gateway', supportsStoredCredentials: false },
  { module: 'card_gateway', displayName: 'Card', supportsStoredCredentials: false },
]

/** The one cart line the summary shows. */
const ITEM: CartItem = cartLine(7, 9.99)

/** The signed-in customer. */
const ann = makeClientUser({ firstName: 'Ann', lastName: 'Lee', email: 'ann@acme.test' })

/**
 * Mounts the page, waits for the currencies and gateways to load and fills the cart.
 *
 * @param items The lines to put in the cart; the one line of the summary by default.
 * @returns The wrapper.
 */
const mountCheckout = async (items: CartItem[] = [ITEM]) => {
  const wrapper = await mountPage(AuroraCheckout, {
    components: { CheckoutGatewayOption: GatewayOption, CheckoutSaveOption: SaveOption, CheckoutSubmitHint: SubmitHint },
  })
  await awaitCheckoutLoaded()
  useCartStore().items = items
  await flushPromises()
  return wrapper
}

/**
 * How many orders reached the backend.
 *
 * @returns The count.
 */
const placedOrders = (): number => requestsTo(stub.requests, BillingEndpoint.OrderCreate).length

describe('AuroraCheckout', () => {
  beforeEach(async () => {
    stubPublicPageRuntime()
    vi.mocked(navigateTo).mockClear()
    window.localStorage.clear()
    table = checkoutReplies(DEFAULT_METHODS)
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('offers the way back to hosting instead of a form when the cart is empty', async () => {
    const wrapper = await mountCheckout([])

    expect(wrapper.find('[data-testid="checkout-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="checkout-browse-hosting"]').attributes('href')).toBe('/en/hosting')
    expect(wrapper.find('[data-testid="checkout-account"]').exists()).toBe(false)
  })

  it('offers the renewal-save box, unticked, only when the signed-in payer can have the method kept', async () => {
    table[readOf(BillingEndpoint.OrderPaymentMethods)] = { body: [{ module: 'example_gateway', displayName: 'Example Gateway', supportsStoredCredentials: true }] }
    const offered = await mountCheckout()
    useAuthStore().user = ann
    await flushPromises()

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
    expect(wrapper.find('[data-testid="checkout-sign-in"]').attributes('href')).toBe('/en/client/login')
  })

  it('shows the signed-in account line instead of the guest form, and signs out on "not you"', async () => {
    const wrapper = await mountCheckout()
    useAuthStore().user = ann
    await flushPromises()

    expect(wrapper.find('[data-testid="checkout-logged-in"]').text()).toContain('ann@acme.test')
    expect(wrapper.find('[data-testid="checkout-firstname"]').exists()).toBe(false)

    await wrapper.find('[data-testid="checkout-not-you"]').trigger('click')
    await waitUntil(() => expect(useAuthStore().user).toBeNull())

    expect(requestsTo(stub.requests, AccountEndpoint.Logout).map(request => request.method)).toEqual(['POST'])
    expect(wrapper.find('[data-testid="checkout-logged-in"]').exists()).toBe(false)
  })

  it('lists the gateway methods and marks the selected one', async () => {
    const wrapper = await mountCheckout()

    expect(wrapper.find('[data-testid="checkout-method-example_gateway"]').classes()).toContain('border-ac1')
    expect(wrapper.find('[data-testid="checkout-method-card_gateway"]').classes()).not.toContain('border-ac1')
    expect(wrapper.find('[data-testid="checkout-method-card_gateway"]').text()).toContain('Card')
  })

  it('says so when no payment method is available', async () => {
    table[readOf(BillingEndpoint.OrderPaymentMethods)] = { body: [] }
    const wrapper = await mountCheckout()

    expect(wrapper.find('[data-testid="checkout-no-methods"]').text()).toBe('checkout.noPaymentMethods')
  })

  it('summarises the order with each line and the total', async () => {
    const wrapper = await mountCheckout()

    expect(wrapper.find('[data-testid="checkout-item-7"]').text()).toContain(ITEM.name)
    expect(wrapper.find('[data-testid="checkout-item-7"]').text()).toContain('$9.99')
    expect(wrapper.find('[data-testid="checkout-total"]').text()).toBe('$9.99')
  })

  it('shows the order refusal as an alert above the button', async () => {
    table[writeOf(BillingEndpoint.OrderCreate)] = refusal('Card declined')
    const wrapper = await mountCheckout()

    await wrapper.find('[data-testid="checkout-place-order"]').trigger('click')
    await waitUntil(() => expect(wrapper.find('[data-testid="checkout-error"]').text()).toBe('Card declined'))
  })

  it('places the order and follows the destination the store returns', async () => {
    table[readOf(BillingEndpoint.OrderPaymentMethods)] = { body: [{ module: PaymentModule.BankTransfer, displayName: 'Bank transfer', supportsStoredCredentials: false }] }
    // The cart's total is the placed order's total, so the payer is not asked to accept a changed total.
    const wrapper = await mountCheckout([{ ...ITEM, amount: PLACED_ORDER.total }])

    await wrapper.find('[data-testid="checkout-place-order"]').trigger('click')
    await waitUntil(() => expect(navigateTo).toHaveBeenCalledWith(`/en${clientInvoicePayPath(PLACED_ORDER.invoiceId)}`))

    expect(placedOrders()).toBe(1)
  })

  it('keeps the button label, marks it busy and puts the sentence in a polite line under it', async () => {
    // An order that never settles keeps the page in its busy state.
    table[writeOf(BillingEndpoint.OrderCreate)] = { hold: true }
    const wrapper = await mountCheckout()

    await wrapper.find('[data-testid="checkout-place-order"]').trigger('click')
    await until(() => placedOrders() === 1, 'the order to reach the backend')
    await flushPromises()

    const button = wrapper.find('[data-testid="checkout-place-order"]')
    expect(button.text()).toBe('checkout.placeOrder')
    expect(button.attributes('aria-busy')).toBe('true')
    expect(button.text()).not.toContain('checkout.redirectingToBank')
    expect(button.classes()).toContain('text-base')
    expect(button.classes()).not.toContain('text-lg')
    const hint = wrapper.find('[data-testid="checkout-submit-hint"]')
    expect(hint.text()).toBe('checkout.redirectingToBank')
    expect(hint.attributes('aria-live')).toBe('polite')
  })

  it('disables the button while no payment method is selected', async () => {
    const wrapper = await mountCheckout()

    useCheckoutStore().selectedMethod = ''
    await flushPromises()

    expect(wrapper.find('[data-testid="checkout-place-order"]').attributes('disabled')).toBeDefined()
  })

  describe('purchase permission', () => {
    it('offers the gateways and the order button to a member who may place orders', async () => {
      const wrapper = await mountCheckout()

      useClientStore().user = makeClientUser({ permissions: Permission.PlaceNewOrders })
      await flushPromises()

      expect(wrapper.find('[data-testid="checkout-permission-denied"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="checkout-method-card_gateway-input"]').attributes('disabled')).toBeUndefined()
      expect(wrapper.find('[data-testid="checkout-place-order"]').attributes('disabled')).toBeUndefined()
    })

    it('disables the gateways and the order button, and says why, for a member who may not place orders', async () => {
      const wrapper = await mountCheckout()

      useClientStore().user = makeClientUser({ permissions: Permission.ViewPayInvoices })
      await flushPromises()

      expect(wrapper.find('[data-testid="checkout-permission-denied"]').text()).toContain('checkout.permissionDenied')
      expect(wrapper.find('[data-testid="checkout-method-card_gateway-input"]').attributes('disabled')).toBeDefined()
      expect(wrapper.find('[data-testid="checkout-place-order"]').attributes('disabled')).toBeDefined()

      await wrapper.find('[data-testid="checkout-place-order"]').trigger('click')
      await flushPromises()
      expect(placedOrders()).toBe(0)
    })

    it('keeps the checkout open for a signed-in first-time buyer who acts for no account', async () => {
      const wrapper = await mountCheckout()

      useAuthStore().user = ann
      useClientStore().user = makeClientUser({ id: null, permissions: Permission.None, actsForAccount: false })
      await flushPromises()

      expect(wrapper.find('[data-testid="checkout-permission-denied"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="checkout-place-order"]').attributes('disabled')).toBeUndefined()
    })
  })
})
