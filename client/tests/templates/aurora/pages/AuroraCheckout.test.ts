/**
 * Unit tests for AuroraCheckout: the empty cart, the guest form with its placeholders, the
 * signed-in account line, payment method choice, the order summary and the place-order flow,
 * with the three stores replaced by minimal fakes so no transport is involved.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { Suspense, defineComponent, h, reactive, ref } from 'vue'
import type { Ref } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import AuroraCheckout from '~/templates/aurora/pages/AuroraCheckout.vue'
import GatewayOption from '~/components/checkout/GatewayOption.vue'
import SubmitHint from '~/components/checkout/SubmitHint.vue'
import { Permission } from '~/composables/usePermissions'
import { CheckoutDestinationKind } from '~/types/checkoutdestinationkind'
import { stubAuroraGlobals } from '../auroraGlobals'

/** The fake stores and their spies, shared with the module mocks below. */
const fakes = vi.hoisted(() => ({
  cart: null as null | Record<string, unknown>,
  currency: null as null | Record<string, unknown>,
  checkout: null as null | Record<string, unknown>,
  /** The permissions the signed-in person holds; `null` stands for a first-time buyer who acts for no account. */
  held: null as number | null,
}))

vi.mock('~/stores/cart', () => ({ useCartStore: () => fakes.cart }))
vi.mock('~/stores/currency', () => ({ useCurrencyStore: () => fakes.currency }))
vi.mock('~/stores/checkout', () => ({ useCheckoutStore: () => fakes.checkout }))
// The client store's own decision is covered by its tests; the page only reads its answer.
vi.mock('~/stores/client', () => ({
  useClientStore: () => ({ mayDo: (flag: number) => fakes.held === null || (fakes.held & flag) === flag }),
}))
// The page reaches the session through the store module; the test's session is the stand-in
// `stubAuroraGlobals` registers as the global store accessor.
vi.mock('~/stores/auth', () => ({
  useAuthStore: () => (globalThis as unknown as { useAuthStore: () => unknown }).useAuthStore(),
}))

/** One cart line as the summary renders it. */
const ITEM = { pid: 7, name: 'Business hosting', cycleLabel: 'Monthly' }

/** The refs of the fake checkout store that a test sets before mounting. */
interface FakeCheckout {
  /** Sentence the store shows above the button. */
  error: Ref<string>
  /** Module of the chosen payment method; empty means none. */
  selectedMethod: Ref<string>
  /** Whether an order is in flight. */
  submitting: Ref<boolean>
  /** Short label the button shows while an order is in flight. */
  submittingLabel: Ref<string>
  /** Sentence shown under the button while an order is in flight. */
  submittingHint: Ref<string>
  /** Spy standing in for the store's order action. */
  placeOrder: ReturnType<typeof vi.fn>
}

/** Stand-in for the card form, which mounts the payment provider's element. */
const StripeCardFormStub = defineComponent({
  name: 'CheckoutStripeCardForm',
  setup: (_props, { expose }) => {
    expose({ confirmPayment: () => Promise.resolve({ id: 'pi_1' }) })
    return () => h('div', { 'data-testid': 'card-form-stub' })
  },
})

/**
 * Rebuilds the three fake stores.
 *
 * @param options Whether the cart is empty and which payment methods the gateway offers.
 * @returns The settable parts of the fake checkout store.
 */
const buildStores = (options: { empty?: boolean, methods?: { module: string, displayName: string }[] } = {}): FakeCheckout => {
  const placeOrder = vi.fn(async () => ({ kind: CheckoutDestinationKind.Route, target: '/client/order-success' }))
  fakes.cart = reactive({
    isEmpty: options.empty ?? false,
    init: vi.fn(),
    itemsInCurrency: () => (options.empty ? [] : [ITEM]),
  })
  fakes.currency = reactive({
    payerCode: 'USD',
    currencies: [{ code: 'USD' }, { code: 'EUR' }],
    init: vi.fn(),
    load: vi.fn(),
  })
  /** The sentence the fake store shows above the button. */
  const error = ref('')
  /** The payment method the fake store has selected. */
  const selectedMethod = ref('paypal')
  /** Whether the fake store is placing an order. */
  const submitting = ref(false)
  /** The button label the fake store shows while placing an order. */
  const submittingLabel = ref('checkout.processing')
  /** The line under the button the fake store shows while placing an order. */
  const submittingHint = ref('')
  fakes.checkout = {
    staleItems: ref<unknown[]>([]),
    totalLabel: ref('$9.99'),
    submittingLabel,
    submittingHint,
    paymentMethods: ref(options.methods ?? [{ module: 'stripe', displayName: 'Card' }, { module: 'paypal', displayName: 'PayPal' }]),
    selectedMethod,
    submitting,
    error,
    itemPrice: () => '$9.99',
    changeGuestCurrency: vi.fn(),
    loadPaymentMethods: vi.fn(async () => undefined),
    resetFlow: vi.fn(),
    refreshCartPrices: vi.fn(async () => undefined),
    placeOrder,
  }
  return { error, selectedMethod, submitting, submittingLabel, submittingHint, placeOrder }
}

/**
 * Mounts the page under a Suspense boundary, because its setup awaits the payment methods.
 *
 * @returns The wrapper once the async setup has settled.
 */
const mountPage = async () => {
  const Host = defineComponent({ render: () => h(Suspense, null, { default: () => h(AuroraCheckout) }) })
  const wrapper = mountWithKit(Host, { components: { CheckoutStripeCardForm: StripeCardFormStub, CheckoutGatewayOption: GatewayOption, CheckoutSubmitHint: SubmitHint } })
  await flushPromises()
  return wrapper
}

describe('AuroraCheckout', () => {
  beforeEach(() => {
    fakes.held = Permission.All
    vi.stubGlobal('useHead', vi.fn())
  })

  it('offers the way back to hosting instead of a form when the cart is empty', async () => {
    stubAuroraGlobals()
    buildStores({ empty: true })
    const wrapper = await mountPage()

    expect(wrapper.find('[data-testid="checkout-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="checkout-browse-hosting"]').attributes('href')).toBe('/hosting')
    expect(wrapper.find('[data-testid="checkout-account"]').exists()).toBe(false)
  })

  it('gives every guest field a translated placeholder', async () => {
    stubAuroraGlobals()
    buildStores()
    const wrapper = await mountPage()

    for (const id of ['firstname', 'lastname', 'email', 'password']) {
      expect(wrapper.find(`[data-testid="checkout-${id}"]`).attributes('placeholder')).toBe(`checkout.placeholders.${id}`)
    }
    expect(wrapper.find('[data-testid="checkout-sign-in"]').attributes('href')).toBe('/client/login')
  })

  it('shows the signed-in account line instead of the guest form, and signs out on "not you"', async () => {
    const { signOut } = stubAuroraGlobals({ user: { firstName: 'Ann', lastName: 'Lee', email: 'ann@acme.test' } })
    buildStores()
    const wrapper = await mountPage()

    expect(wrapper.find('[data-testid="checkout-logged-in"]').text()).toContain('ann@acme.test')
    expect(wrapper.find('[data-testid="checkout-firstname"]').exists()).toBe(false)

    await wrapper.find('[data-testid="checkout-not-you"]').trigger('click')
    expect(signOut).toHaveBeenCalledTimes(1)
  })

  it('lists the gateway methods and marks the selected one', async () => {
    stubAuroraGlobals()
    buildStores()
    const wrapper = await mountPage()

    expect(wrapper.find('[data-testid="checkout-method-paypal"]').classes()).toContain('border-ac1')
    expect(wrapper.find('[data-testid="checkout-method-stripe"]').classes()).not.toContain('border-ac1')
    expect(wrapper.find('[data-testid="checkout-method-stripe"]').text()).toContain('Card')
  })

  it('says so when no payment method is available', async () => {
    stubAuroraGlobals()
    buildStores({ methods: [] })
    const wrapper = await mountPage()

    expect(wrapper.find('[data-testid="checkout-no-methods"]').text()).toBe('checkout.noPaymentMethods')
  })

  it('summarises the order with each line and the total', async () => {
    stubAuroraGlobals()
    buildStores()
    const wrapper = await mountPage()

    expect(wrapper.find('[data-testid="checkout-item-7"]').text()).toContain('Business hosting')
    expect(wrapper.find('[data-testid="checkout-item-7"]').text()).toContain('$9.99')
    expect(wrapper.find('[data-testid="checkout-total"]').text()).toBe('$9.99')
  })

  it('shows the store error as an alert above the button', async () => {
    stubAuroraGlobals()
    buildStores().error.value = 'Card declined'
    const wrapper = await mountPage()

    expect(wrapper.find('[data-testid="checkout-error"]').text()).toBe('Card declined')
  })

  it('places the order and follows the destination the store returns', async () => {
    stubAuroraGlobals()
    const { placeOrder } = buildStores()
    const wrapper = await mountPage()

    await wrapper.find('[data-testid="checkout-place-order"]').trigger('click')
    await flushPromises()

    expect(placeOrder).toHaveBeenCalledTimes(1)
    expect(navigateTo).toHaveBeenCalledWith('/client/order-success')
  })

  it('keeps the busy label short on the button and puts the sentence in a polite line under it', async () => {
    stubAuroraGlobals()
    const store = buildStores()
    store.submitting.value = true
    store.submittingLabel.value = 'checkout.redirecting'
    store.submittingHint.value = 'checkout.redirectingToBank'
    const wrapper = await mountPage()

    const button = wrapper.find('[data-testid="checkout-place-order"]')
    expect(button.text()).toBe('checkout.redirecting')
    expect(button.text()).not.toContain('checkout.redirectingToBank')
    expect(button.classes()).toContain('text-base')
    expect(button.classes()).not.toContain('text-lg')
    const hint = wrapper.find('[data-testid="checkout-submit-hint"]')
    expect(hint.text()).toBe('checkout.redirectingToBank')
    expect(hint.attributes('aria-live')).toBe('polite')
  })

  it('disables the button while no payment method is selected', async () => {
    stubAuroraGlobals()
    buildStores().selectedMethod.value = ''
    const wrapper = await mountPage()

    expect(wrapper.find('[data-testid="checkout-place-order"]').attributes('disabled')).toBeDefined()
  })

  describe('purchase permission', () => {
    it('offers the gateways and the order button to a member who may place orders', async () => {
      stubAuroraGlobals()
      fakes.held = Permission.PlaceNewOrders
      buildStores()
      const wrapper = await mountPage()

      expect(wrapper.find('[data-testid="checkout-permission-denied"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="checkout-method-stripe-input"]').attributes('disabled')).toBeUndefined()
      expect(wrapper.find('[data-testid="checkout-place-order"]').attributes('disabled')).toBeUndefined()
    })

    it('disables the gateways and the order button, and says why, for a member who may not place orders', async () => {
      stubAuroraGlobals()
      fakes.held = Permission.ViewPayInvoices
      const { placeOrder } = buildStores()
      const wrapper = await mountPage()

      expect(wrapper.find('[data-testid="checkout-permission-denied"]').text()).toContain('checkout.permissionDenied')
      expect(wrapper.find('[data-testid="checkout-method-stripe-input"]').attributes('disabled')).toBeDefined()
      expect(wrapper.find('[data-testid="checkout-place-order"]').attributes('disabled')).toBeDefined()

      await wrapper.find('[data-testid="checkout-place-order"]').trigger('click')
      await flushPromises()
      expect(placeOrder).not.toHaveBeenCalled()
    })

    it('does not open the card form for a member who may not place orders', async () => {
      stubAuroraGlobals()
      fakes.held = Permission.None
      buildStores().selectedMethod.value = 'stripe'
      const wrapper = await mountPage()

      expect(wrapper.find('[data-testid="card-form-stub"]').exists()).toBe(false)
    })

    it('keeps the checkout open for a signed-in first-time buyer who acts for no account', async () => {
      stubAuroraGlobals({ user: { firstName: 'Ann', lastName: 'Lee', email: 'ann@acme.test' } })
      fakes.held = null
      buildStores()
      const wrapper = await mountPage()

      expect(wrapper.find('[data-testid="checkout-permission-denied"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="checkout-place-order"]').attributes('disabled')).toBeUndefined()
    })
  })
})
