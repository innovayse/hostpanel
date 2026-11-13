/**
 * Tests for ClassicCheckout against the real cart, currency, checkout, auth and client stores, the
 * real billing and auth API composables and the real transport, with a stub BFF answering on a real
 * socket (nothing of ours is mocked): the empty cart, the order summary, the account step in both
 * states, the payment-method choice, placing an order and a refused order.
 *
 * @module tests/templates/classic/pages/ClassicCheckout.test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { AccountEndpoint } from '~/tests/helpers/accountPaths'
import { bankTransferPath, BillingEndpoint } from '~/tests/helpers/billingPaths'
import { awaitCheckoutLoaded, cartLine, checkoutReplies } from '~/tests/helpers/checkoutFlow'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import { bodyOf } from '~/tests/helpers/requestBody'
import { until } from '~/tests/helpers/settle'
import { readOf, refusal, requestsTo, routeTable, writeOf } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { waitUntil } from '~/tests/helpers/waitUntil'
import ClassicCheckout from '~/templates/classic/pages/ClassicCheckout.vue'
import GatewayOption from '~/components/checkout/GatewayOption.vue'
import SaveOption from '~/components/checkout/SaveOption.vue'
import SubmitHint from '~/components/checkout/SubmitHint.vue'
import { useAuthStore } from '~/stores/auth'
import { useCartStore } from '~/stores/cart'
import { useCheckoutStore } from '~/stores/checkout'
import { useClientStore } from '~/stores/client'
import type { CartItem } from '~/types/cartitem'
import type { GatewayMethod } from '~/types/gatewaymethod'
import { Permission } from '~/types/permission'
import { PaymentModule } from '~/types/paymentmodule'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts or acts. */
let table: StubRouteTable

/** The gateways the stub offers unless a test says otherwise: the bank transfer first, then a card gateway that can keep a method. */
const DEFAULT_METHODS: GatewayMethod[] = [
  { module: PaymentModule.BankTransfer, displayName: 'Bank transfer', supportsStoredCredentials: false },
  { module: 'card_gateway', displayName: 'Card', supportsStoredCredentials: true },
]

/** Stand-in for the phone field, which keeps its country picker out of this test. */
const PhoneInputStub = defineComponent({
  name: 'UiPhoneInput',
  props: { modelValue: { type: String, default: '' } },
  emits: ['update:modelValue'],
  setup: (props, { emit }) => () => h('input', {
    'data-testid': 'checkout-phone',
    value: props.modelValue,
    onInput: (event: Event) => emit('update:modelValue', (event.target as HTMLInputElement).value),
  }),
})

/** A signed-in client. */
const client = makeClientUser()

/**
 * Mounts the checkout, waits for the currencies and gateways to load and fills the cart.
 *
 * @param items Lines to put in the cart.
 * @returns The wrapper.
 */
const mountCheckout = async (items: CartItem[] = [cartLine(1, 5), cartLine(2, 7)]) => {
  const wrapper = await mountPage(ClassicCheckout, {
    components: { CheckoutGatewayOption: GatewayOption, CheckoutSaveOption: SaveOption, CheckoutSubmitHint: SubmitHint, UiPhoneInput: PhoneInputStub },
  })
  await awaitCheckoutLoaded()
  useCartStore().items = items
  await flushPromises()
  return wrapper
}

/**
 * Fills the guest form.
 *
 * @param wrapper The mounted page.
 */
const fillGuestForm = async (wrapper: Awaited<ReturnType<typeof mountCheckout>>) => {
  await wrapper.find('[data-testid="checkout-firstname"]').setValue('Ada')
  await wrapper.find('[data-testid="checkout-lastname"]').setValue('Lovelace')
  await wrapper.find('[data-testid="checkout-email"]').setValue('ada@example.test')
  await wrapper.find('[data-testid="checkout-password"]').setValue('s3cret-pass')
  await wrapper.find('[data-testid="checkout-phone"]').setValue('+37400000000')
}

describe('ClassicCheckout', () => {
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

  it('shows the empty state with a way to the hosting plans when the cart is empty', async () => {
    const wrapper = await mountCheckout([])

    expect(wrapper.find('[data-testid="checkout-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="checkout-browse"]').attributes('href')).toBe('/en/hosting')
    expect(wrapper.find('[data-testid="checkout-grid"]').exists()).toBe(false)
  })

  it('lists the cart lines and the total in the order summary', async () => {
    const wrapper = await mountCheckout()

    const summary = wrapper.find('[data-testid="checkout-summary"]')
    expect(summary.findAll('[data-testid="checkout-item"]')).toHaveLength(2)
    expect(summary.text()).toContain('$5.00')
    expect(wrapper.find('[data-testid="checkout-total"]').text()).toBe('$12.00')
  })

  it('warns about lines left in another currency and leaves them out', async () => {
    const wrapper = await mountCheckout([cartLine(1, 5), cartLine(2, 2000, 'AMD')])

    expect(wrapper.find('[data-testid="checkout-stale"]').exists()).toBe(true)
    expect(wrapper.findAll('[data-testid="checkout-item"]')).toHaveLength(1)
  })

  it('shows the guest form to a visitor, every text field with its placeholder', async () => {
    const wrapper = await mountCheckout()

    expect(wrapper.find('[data-testid="checkout-guest-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="checkout-firstname"]').attributes('placeholder')).toBe('checkout.placeholders.firstname')
    expect(wrapper.find('[data-testid="checkout-lastname"]').attributes('placeholder')).toBe('checkout.placeholders.lastname')
    expect(wrapper.find('[data-testid="checkout-email"]').attributes('placeholder')).toBe('checkout.placeholders.email')
    expect(wrapper.find('[data-testid="checkout-password"]').attributes('placeholder')).toBe('checkout.placeholders.password')
    expect(wrapper.find('[data-testid="checkout-sign-in"]').attributes('href')).toBe('/en/client/login')
  })

  it('shows the signed-in client instead of the guest form', async () => {
    const wrapper = await mountCheckout()

    useAuthStore().user = client
    await flushPromises()

    expect(wrapper.find('[data-testid="checkout-guest-form"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="checkout-user-email"]').text()).toBe('ada@example.test')
  })

  it('lists the gateways with the first preselected', async () => {
    const wrapper = await mountCheckout()

    const bank = wrapper.find(`[data-testid="checkout-method-${PaymentModule.BankTransfer}"]`)
    const card = wrapper.find(`[data-testid="checkout-method-card_gateway"]`)
    expect(bank.text()).toContain('Bank transfer')
    expect(bank.text()).toContain('checkout.gatewayKinds.bankTransfer')
    expect(card.text()).toContain('Card')
    expect(card.text()).not.toContain('checkout.gatewayKinds')
    expect(bank.classes()).toContain('bg-classic-brand-soft')
    expect(card.classes()).not.toContain('bg-classic-brand-soft')
  })

  it('draws with the classic tokens only, no gradients, glows or cyan', async () => {
    const wrapper = await mountCheckout()

    const html = wrapper.html()
    expect(html).not.toContain('bg-gradient')
    expect(html).not.toContain('cyan-')
    expect(html).not.toContain('blur-')
    expect(wrapper.find('[data-testid="checkout-summary"]').classes()).toContain('border-t-classic-brand')
  })

  it('says so when no gateway is on, and cannot be submitted', async () => {
    table[readOf(BillingEndpoint.OrderPaymentMethods)] = { body: [] }
    const wrapper = await mountCheckout()

    expect(wrapper.find('[data-testid="checkout-no-methods"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="checkout-submit"]').attributes('disabled')).toBeDefined()
  })

  describe('save for automatic renewals', () => {
    it('is not offered to a guest', async () => {
      const wrapper = await mountCheckout()

      expect(wrapper.find('[data-testid="checkout-save-for-renewals"]').exists()).toBe(false)
    })

    it('is offered, unticked, to a signed-in client paying through a hosted gateway', async () => {
      const wrapper = await mountCheckout()
      useAuthStore().user = client
      useCheckoutStore().selectedMethod = 'card_gateway'
      await flushPromises()

      const box = wrapper.find('[data-testid="checkout-save-for-renewals"]')
      expect(box.exists()).toBe(true)
      expect(box.attributes('aria-checked')).toBe('false')
      expect(wrapper.text()).toContain('checkout.saveForRenewalsAgreement')
    })

    it('is hidden for bank transfer', async () => {
      const wrapper = await mountCheckout()
      useAuthStore().user = client
      useCheckoutStore().selectedMethod = PaymentModule.BankTransfer
      await flushPromises()

      expect(wrapper.find('[data-testid="checkout-save-for-renewals"]').exists()).toBe(false)
    })
  })

  describe('purchase permission', () => {
    it('offers the gateways and the order button to a member of the account who may place orders', async () => {
      const wrapper = await mountCheckout()

      useClientStore().user = makeClientUser({ permissions: Permission.PlaceNewOrders })
      await flushPromises()

      expect(wrapper.find('[data-testid="checkout-permission-denied"]').exists()).toBe(false)
      expect(wrapper.find(`[data-testid="checkout-method-card_gateway-input"]`).attributes('disabled')).toBeUndefined()
      expect(wrapper.find('[data-testid="checkout-submit"]').attributes('disabled')).toBeUndefined()
    })

    it('disables the gateways and the order button, and says why, for a member who may not place orders', async () => {
      const wrapper = await mountCheckout()

      useClientStore().user = makeClientUser({ permissions: Permission.ViewPayInvoices })
      await flushPromises()

      expect(wrapper.find('[data-testid="checkout-permission-denied"]').text()).toContain('checkout.permissionDenied')
      expect(wrapper.find(`[data-testid="checkout-method-card_gateway-input"]`).attributes('disabled')).toBeDefined()
      expect(wrapper.find('[data-testid="checkout-submit"]').attributes('disabled')).toBeDefined()
    })

    it('keeps the checkout open for a signed-in first-time buyer who acts for no account and holds no flag', async () => {
      const wrapper = await mountCheckout()

      useClientStore().user = makeClientUser({ id: null, permissions: Permission.None, actsForAccount: false })
      await flushPromises()

      expect(wrapper.find('[data-testid="checkout-permission-denied"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="checkout-submit"]').attributes('disabled')).toBeUndefined()
    })
  })

  it('places a bank-transfer order with the guest details and goes to the invoice', async () => {
    const wrapper = await mountCheckout()
    await fillGuestForm(wrapper)

    await wrapper.find('[data-testid="checkout-submit"]').trigger('click')
    await waitUntil(() => expect(navigateTo).toHaveBeenCalledWith('/en/client/invoices/12/pay'))

    expect(bodyOf(requestsTo(stub.requests, BillingEndpoint.OrderCreate)[0])).toEqual(expect.objectContaining({
      paymentmethod: PaymentModule.BankTransfer,
      currency: 'USD',
      firstname: 'Ada',
      lastname: 'Lovelace',
      email: 'ada@example.test',
      password: 's3cret-pass',
      phonenumber: '+37400000000',
      items: [
        expect.objectContaining({ productId: 1, billingcycle: 'monthly' }),
        expect.objectContaining({ productId: 2, billingcycle: 'monthly' }),
      ],
    }))
    expect(bodyOf(requestsTo(stub.requests, AccountEndpoint.Login)[0])).toEqual({ email: 'ada@example.test', password: 's3cret-pass' })
    expect(requestsTo(stub.requests, bankTransferPath(12)).map(request => request.method)).toEqual(['POST'])
    expect(useCartStore().items).toEqual([])
  })

  it('keeps the busy label short and explains the bank hand-off in a polite line under the button', async () => {
    table[readOf(BillingEndpoint.OrderPaymentMethods)] = { body: [{ module: 'hosted_gateway', displayName: 'Bank card', supportsStoredCredentials: false }] }
    // An order that never settles keeps the page in its busy state.
    table[writeOf(BillingEndpoint.OrderCreate)] = { hold: true }
    const wrapper = await mountCheckout()
    await fillGuestForm(wrapper)
    expect(wrapper.find('[data-testid="checkout-submit-hint"]').text()).toBe('')

    await wrapper.find('[data-testid="checkout-submit"]').trigger('click')
    await until(() => requestsTo(stub.requests, BillingEndpoint.OrderCreate).length === 1, 'the order to reach the backend')
    await flushPromises()

    const button = wrapper.find('[data-testid="checkout-submit"]')
    expect(button.text()).toBe('checkout.placeOrder')
    expect(button.attributes('aria-busy')).toBe('true')
    expect(button.classes()).toContain('text-base')
    expect(button.classes()).not.toContain('text-lg')
    const hint = wrapper.find('[data-testid="checkout-submit-hint"]')
    expect(hint.text()).toBe('checkout.redirectingToBank')
    expect(hint.attributes('aria-live')).toBe('polite')
  })

  it('shows the refusal sentence and stays on the page when the order is refused', async () => {
    table[writeOf(BillingEndpoint.OrderCreate)] = refusal('Email already registered.')
    const wrapper = await mountCheckout()
    await fillGuestForm(wrapper)

    await wrapper.find('[data-testid="checkout-submit"]').trigger('click')
    await waitUntil(() => expect(wrapper.find('[data-testid="checkout-error"]').text()).toBe('Email already registered.'))

    expect(navigateTo).not.toHaveBeenCalled()
    expect(useCartStore().items).toHaveLength(2)
  })

  it('offers the contact link in the help card', async () => {
    const wrapper = await mountCheckout()

    expect(wrapper.find('[data-testid="checkout-contact"]').attributes('href')).toBe('/en/contact')
  })
})
