/**
 * Unit tests for the payment result page: it verifies a hosted-gateway payment from the address and
 * shows the paid, pending, declined, missing or unknown outcome, never asserting money moved unless
 * the bank said so.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import type { Pinia } from 'pinia'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import ResultPage from '~/pages/payment/result.vue'
import { useAuthStore } from '~/stores/auth'
import { useCartStore } from '~/stores/cart'
import { ConsentTextVersion } from '~/types/consenttextversion'
import { GatewayPaymentState } from '~/types/gatewaypaymentstate'
import { PricingCycle } from '~/types/pricingcycle'
import { consumeCheckoutSave, rememberCheckoutSave } from '~/utils/pendingCheckoutSave'
import { ProductType } from '~/types/producttype'

/** The mocked billing endpoints, re-created for every test. */
const billing = { completeGatewayPayment: vi.fn() }

vi.mock('~/composables/apis/useBillingApi', () => ({
  /**
   * Stands in for the billing API composable so the store runs without transport.
   *
   * @returns The mocked endpoints.
   */
  useBillingApi: () => billing,
}))

/** The shared stored-method session starter; a test makes it answer. */
const storedStart = vi.hoisted(() => vi.fn())

vi.mock('~/composables/useStoredMethodStart', () => ({
  /**
   * Stands in for the shared session starter so no stored-method call is made.
   *
   * @returns The shared stand-in.
   */
  useStoredMethodStart: () => ({ start: storedStart }),
}))

/** The order payment token helpers of the current test. */
const tokens = { recall: vi.fn(), recallInvoiceId: vi.fn(), forget: vi.fn() }

/**
 * Mounts the page for one address query and lets the check finish.
 *
 * @param query The address query string.
 * @param pinia A Pinia the test filled before mounting; a fresh one when omitted.
 * @returns The wrapper.
 */
const mountResult = async (query: Record<string, string | string[]> = {}, pinia?: Pinia) => {
  stubPublicPageRuntime({ query })
  const wrapper = await mountPage(ResultPage, { pinia })
  await flushPromises()
  return wrapper
}

/** The account that made the checkout choice. */
const ADA = 'ada@example.com'

/**
 * Builds a Pinia in which the given account is already loaded as the signed-in one.
 *
 * @param email The signed-in account's email.
 * @returns The Pinia to mount the page with.
 */
const signedInAs = (email: string): Pinia => {
  const pinia = createPinia()
  setActivePinia(pinia)
  useAuthStore().user = { email } as never
  return pinia
}

describe('payment result page', () => {
  beforeEach(() => {
    billing.completeGatewayPayment.mockReset()
    tokens.recall.mockReset()
    tokens.recall.mockReturnValue('token-1')
    tokens.recallInvoiceId.mockReset()
    tokens.recallInvoiceId.mockReturnValue(77)
    tokens.forget.mockReset()
    storedStart.mockReset()
    window.sessionStorage.clear()
    vi.stubGlobal('useOrderPaymentToken', () => tokens)
  })

  it('says there is nothing to check, and does not call the backend, when the address names no payment', async () => {
    const wrapper = await mountResult()

    expect(wrapper.find('[data-testid="payment-title"]').text()).toBe('paymentResult.missingTitle')
    expect(wrapper.find('[data-testid="payment-invoices"]').attributes('href')).toBe('/en/client/invoices')
    expect(billing.completeGatewayPayment).not.toHaveBeenCalled()
  })

  it('verifies an order with its payment token and offers the order page once paid', async () => {
    billing.completeGatewayPayment.mockResolvedValue({ state: GatewayPaymentState.Paid, orderNumber: 'INV-77' })

    const wrapper = await mountResult({ order: '5' })

    expect(billing.completeGatewayPayment).toHaveBeenCalledWith('order', '5', 'token-1')
    expect(wrapper.find('[data-testid="payment-title"]').text()).toBe('paymentResult.successTitle')
    expect(wrapper.find('[data-testid="payment-continue"]').attributes('href')).toBe('/en/client/order-success?order=INV-77')
    expect(tokens.forget).toHaveBeenCalledWith('5')
  })

  it('verifies an invoice without a token and offers the invoice once paid', async () => {
    billing.completeGatewayPayment.mockResolvedValue({ state: GatewayPaymentState.Paid })

    const wrapper = await mountResult({ invoice: '9' })

    expect(billing.completeGatewayPayment).toHaveBeenCalledWith('invoice', '9', undefined)
    expect(wrapper.find('[data-testid="payment-continue"]').attributes('href')).toBe('/en/client/invoices/9')
  })

  it('keeps the token and lets the payer check again while the bank has not settled', async () => {
    billing.completeGatewayPayment.mockResolvedValue({ state: GatewayPaymentState.Pending })
    const wrapper = await mountResult({ order: '5' })
    expect(wrapper.find('[data-testid="payment-title"]').text()).toBe('paymentResult.pendingTitle')
    expect(tokens.forget).not.toHaveBeenCalled()

    billing.completeGatewayPayment.mockResolvedValue({ state: GatewayPaymentState.Paid })
    await wrapper.find('[data-testid="payment-retry"]').trigger('click')
    await flushPromises()

    expect(billing.completeGatewayPayment).toHaveBeenCalledTimes(2)
    expect(wrapper.find('[data-testid="payment-title"]').text()).toBe('paymentResult.successTitle')
  })

  it('sends a declined payer to pay the same invoice again, never to a fresh cart', async () => {
    billing.completeGatewayPayment.mockResolvedValue({ state: GatewayPaymentState.Declined })

    const order = await mountResult({ order: '5' })
    expect(order.find('[data-testid="payment-try-again"]').attributes('href')).toBe('/en/client/invoices/77/pay')

    const invoice = await mountResult({ invoice: '9' })
    expect(invoice.find('[data-testid="payment-try-again"]').attributes('href')).toBe('/en/client/invoices/9/pay')
  })

  it('sends a declined order payer to the invoice list when the billed invoice is not remembered', async () => {
    billing.completeGatewayPayment.mockResolvedValue({ state: GatewayPaymentState.Declined })
    tokens.recallInvoiceId.mockReturnValue(undefined)

    const order = await mountResult({ order: '5' })

    expect(order.find('[data-testid="payment-try-again"]').attributes('href')).toBe('/en/client/invoices')
  })

  it('empties the cart only once an order payment is confirmed', async () => {
    const cartItem = { pid: 1, name: 'Plan', billingcycle: PricingCycle.Monthly, cycleLabel: 'Monthly', amount: 5, currency: 'USD' }

    /**
     * Builds a Pinia whose cart already holds one line.
     *
     * @returns The active Pinia.
     */
    const filledPinia = (): Pinia => {
      const pinia = createPinia()
      setActivePinia(pinia)
      useCartStore().items = [cartItem]
      return pinia
    }

    billing.completeGatewayPayment.mockResolvedValue({ state: GatewayPaymentState.Declined })
    const declinedPinia = filledPinia()
    await mountResult({ order: '5' }, declinedPinia)
    expect(useCartStore(declinedPinia).items).toHaveLength(1)

    billing.completeGatewayPayment.mockResolvedValue({ state: GatewayPaymentState.Paid, orderNumber: 'ORD-5' })
    const paidPinia = filledPinia()
    await mountResult({ order: '5' }, paidPinia)
    expect(useCartStore(paidPinia).items).toEqual([])
  })

  it('offers the order page of digital goods, which asks for no hosting setup, when the paid cart held only those', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    useCartStore().items = [{ pid: 1, name: 'Credits', billingcycle: PricingCycle.Monthly, cycleLabel: 'Monthly', amount: 5, currency: 'USD', productType: ProductType.Digital }]
    billing.completeGatewayPayment.mockResolvedValue({ state: GatewayPaymentState.Paid, orderNumber: 'ORD-5' })

    const wrapper = await mountResult({ order: '5' }, pinia)

    expect(wrapper.find('[data-testid="payment-continue"]').attributes('href')).toBe('/en/client/order-success?order=ORD-5&digital=1')
  })

  it('treats a repeated or malformed id as missing and does not call the backend', async () => {
    /** Queries whose order or invoice id is repeated or malformed. */
    const queries: Array<Record<string, string | string[]>> = [{ order: ['1', '2'] }, { invoice: ['3', '4'] }, { order: '../x' }, { order: '5', invoice: ['9', '10'] }]
    for (const query of queries) {
      const wrapper = await mountResult(query)

      expect(wrapper.find('[data-testid="payment-title"]').text()).toBe('paymentResult.missingTitle')
    }
    expect(billing.completeGatewayPayment).not.toHaveBeenCalled()
  })

  it('does not assert an outcome when the backend cannot be reached, and keeps the token', async () => {
    rememberCheckoutSave('5', 'card_gateway', ADA)
    billing.completeGatewayPayment.mockRejectedValue(new Error('offline'))

    const wrapper = await mountResult({ order: '5' }, signedInAs(ADA))

    expect(wrapper.find('[data-testid="payment-title"]').text()).toBe('paymentResult.unknownTitle')
    expect(wrapper.find('[data-testid="payment-status"]').exists()).toBe(true)
    expect(tokens.forget).not.toHaveBeenCalled()
    // Nothing was confirmed or declined, so the renewal-save choice is neither acted on nor dropped.
    expect(storedStart).not.toHaveBeenCalled()
    expect(consumeCheckoutSave('5', ADA)).toBe('card_gateway')
  })

  describe('save for automatic renewals', () => {
    it('starts the stored-method session once the payment is confirmed and the payer asked for it', async () => {
      rememberCheckoutSave('5', 'card_gateway', ADA)
      storedStart.mockResolvedValue(true)
      billing.completeGatewayPayment.mockResolvedValue({ state: GatewayPaymentState.Paid, orderNumber: 'ORD-5' })

      const wrapper = await mountResult({ order: '5' }, signedInAs(ADA))

      expect(storedStart).toHaveBeenCalledWith('card_gateway', true, ConsentTextVersion.CheckoutAgreement)
      expect(wrapper.find('[data-testid="payment-save-starting"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="payment-save-failed"]').exists()).toBe(false)
    })

    it('starts nothing, and drops the choice, when another account opens the page on the same browser', async () => {
      rememberCheckoutSave('5', 'card_gateway', ADA)
      billing.completeGatewayPayment.mockResolvedValue({ state: GatewayPaymentState.Paid, orderNumber: 'ORD-5' })

      const wrapper = await mountResult({ order: '5' }, signedInAs('bob@example.com'))

      expect(storedStart).not.toHaveBeenCalled()
      expect(wrapper.find('[data-testid="payment-save-starting"]').exists()).toBe(false)
      expect(consumeCheckoutSave('5', ADA)).toBeNull()
    })

    it('starts nothing when the payer did not ask for it', async () => {
      billing.completeGatewayPayment.mockResolvedValue({ state: GatewayPaymentState.Paid, orderNumber: 'ORD-5' })

      await mountResult({ order: '5' }, signedInAs(ADA))

      expect(storedStart).not.toHaveBeenCalled()
    })

    it('starts one session only, even when the paid page is opened again', async () => {
      rememberCheckoutSave('5', 'card_gateway', ADA)
      storedStart.mockResolvedValue(true)
      billing.completeGatewayPayment.mockResolvedValue({ state: GatewayPaymentState.Paid, orderNumber: 'ORD-5' })

      await mountResult({ order: '5' }, signedInAs(ADA))
      await mountResult({ order: '5' }, signedInAs(ADA))

      expect(storedStart).toHaveBeenCalledTimes(1)
    })

    it('does not start on a declined payment, and drops the choice', async () => {
      rememberCheckoutSave('5', 'card_gateway', ADA)
      billing.completeGatewayPayment.mockResolvedValue({ state: GatewayPaymentState.Declined })

      await mountResult({ order: '5' }, signedInAs(ADA))

      expect(storedStart).not.toHaveBeenCalled()
      expect(consumeCheckoutSave('5', ADA)).toBeNull()
    })

    it('does not start on a pending payment, and keeps the choice for the next check', async () => {
      rememberCheckoutSave('5', 'card_gateway', ADA)
      billing.completeGatewayPayment.mockResolvedValue({ state: GatewayPaymentState.Pending })

      await mountResult({ order: '5' }, signedInAs(ADA))

      expect(storedStart).not.toHaveBeenCalled()
      expect(consumeCheckoutSave('5', ADA)).toBe('card_gateway')
    })

    it('does not start when the outcome could not be confirmed', async () => {
      rememberCheckoutSave('5', 'card_gateway', ADA)
      billing.completeGatewayPayment.mockRejectedValue(new Error('offline'))

      await mountResult({ order: '5' }, signedInAs(ADA))

      expect(storedStart).not.toHaveBeenCalled()
    })

    it('starts nothing for an invoice payment', async () => {
      rememberCheckoutSave('9', 'card_gateway', ADA)
      billing.completeGatewayPayment.mockResolvedValue({ state: GatewayPaymentState.Paid })

      await mountResult({ invoice: '9' }, signedInAs(ADA))

      expect(storedStart).not.toHaveBeenCalled()
    })

    it('keeps the paid result and shows a notice with a link to the saved methods when the session cannot be started', async () => {
      rememberCheckoutSave('5', 'card_gateway', ADA)
      storedStart.mockResolvedValue(false)
      billing.completeGatewayPayment.mockResolvedValue({ state: GatewayPaymentState.Paid, orderNumber: 'ORD-5' })

      const wrapper = await mountResult({ order: '5' }, signedInAs(ADA))

      expect(wrapper.find('[data-testid="payment-title"]').text()).toBe('paymentResult.successTitle')
      expect(wrapper.find('[data-testid="payment-continue"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="payment-save-failed"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="payment-save-failed-link"]').attributes('href')).toBe('/en/client/account?tab=payment')
      expect(wrapper.find('[data-testid="payment-save-starting"]').exists()).toBe(false)
    })
  })
})
