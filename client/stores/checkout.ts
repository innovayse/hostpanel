/**
 * Pinia store for the one order flow both checkout templates share.
 *
 * It owns the gateway list, the chosen method, the in-flight flag and the failure sentence, and
 * walks the order through its steps: place the order, sign a guest in, then settle it by card
 * (the page supplies the card element through {@link PlaceOrderInput.confirmCard}), by hosted
 * gateway or by bank transfer. It reports where the payer goes next as a
 * {@link CheckoutDestination} instead of navigating, so the page keeps the router and a test
 * needs none. Templates differ only in markup; the flow is here once.
 *
 * Required guest fields are refused by the API (field errors land in {@link fieldErrors}); the
 * store does not author a sentence of its own for them.
 *
 * URLs and transport belong to {@link useBillingApi}; this store owns only the state.
 *
 * @module stores/checkout
 */

import { CLIENT_INVOICES_PATH, clientOrderSuccessPath } from '~/constants/clientRoutes'
import { defineStore } from 'pinia'
import { useBillingApi } from '~/composables/apis/useBillingApi'
import { useOrderPaymentToken } from '~/composables/useOrderPaymentToken'
import { apiErrorMessage, apiFieldErrors } from '~/lib/errorHandler'
import { useAuthStore } from '~/stores/auth'
import { useCartStore } from '~/stores/cart'
import { useCatalogStore } from '~/stores/catalog'
import { useCurrencyStore } from '~/stores/currency'
import type { CartItem } from '~/types/cartitem'
import type { CheckoutDestination } from '~/types/checkoutdestination'
import { CheckoutDestinationKind } from '~/types/checkoutdestinationkind'
import type { GatewayMethod } from '~/types/gatewaymethod'
import type { PlaceOrderInput } from '~/types/placeorderinput'
import type { PlaceOrderResult } from '~/types/placeorderresult'
import { isSafeRedirectUrl } from '~/utils/externalRedirect'
import { formatMoney } from '~/utils/formatMoney'
import { PaymentModule } from '~/types/paymentmodule'
import { isHostedGatewayModule } from '~/utils/paymentModules'
import { translate } from '~/utils/translate'
import { CartItemType } from '~/types/cartitemtype'

/** Two amounts closer than this are the same price; it absorbs float rounding, not a real change. */
const TOTAL_TOLERANCE = 0.005

/**
 * Central store for placing an order.
 *
 * @returns The checkout state and the actions that change it.
 */
export const useCheckoutStore = defineStore('checkout', () => {
  const billing = useBillingApi()
  const cart = useCartStore()
  const currencyStore = useCurrencyStore()
  const catalog = useCatalogStore()
  const authStore = useAuthStore()
  const { remember: rememberPaymentToken } = useOrderPaymentToken()

  /** The gateways the order can be paid through; empty until {@link loadPaymentMethods}. */
  const paymentMethods = ref<GatewayMethod[]>([])

  /** Whether the gateway list is being read. */
  const methodsLoading = ref(false)

  /** The gateway module the payer picked; defaults to the first one offered. */
  const selectedMethod = ref('')

  /**
   * Whether an order is being placed and settled. It stays true once the order was handed on (the
   * payer is sent to the bank or to the next page) and is released only by a refusal or
   * {@link resetFlow}, so a second click during the hand-off cannot place or pay a second order.
   */
  const submitting = ref(false)

  /** The sentence for the last refused order; empty when there is none to show. */
  const error = ref('')

  /** Field-level messages of the last refused order, keyed by camelCase field name. */
  const fieldErrors = ref<Record<string, string>>({})

  /**
   * The order already placed for the current cart whose payment has not gone through yet. A retry
   * after a failed payment pays this order instead of placing a second one; any change to the
   * cart forgets it, because the order no longer matches what the payer is buying.
   */
  const pendingOrder = ref<PlaceOrderResult | null>(null)

  /**
   * The id of the card payment the provider already took for {@link pendingOrder}, kept from the
   * moment the card was confirmed until the backend has accepted it. When the backend call fails
   * after the provider charged the card, a retry only repeats that call with this id: opening a
   * new intent and confirming the card again would charge the payer twice.
   */
  const pendingPaymentIntentId = ref<string | null>(null)

  /** The order whose changed total the payer has agreed to pay; a retry of it is not asked again. */
  const acceptedTotalOrderId = ref<number | null>(null)

  watch(() => cart.items, () => {
    pendingOrder.value = null
    pendingPaymentIntentId.value = null
  }, { deep: true })

  /** Items priced in a currency other than the payer's current one: excluded from the order. */
  const staleItems = computed(() => cart.staleItems(currencyStore.payerCode))

  /** The order total in the payer's currency, formatted. */
  const totalLabel = computed(() => {
    const currency = currencyStore.moneyCurrencyFor(currencyStore.payerCode)
    return formatMoney(cart.items.length ? cart.total(currencyStore.payerCode) : 0, currency)
  })

  /** Whether the chosen method is a hosted gateway, which hands the browser off to the bank. */
  const isHostedGateway = computed(() => isHostedGatewayModule(selectedMethod.value))

  /** Short submit-button label while the order is being placed; the sentence lives in {@link submittingHint}. */
  const submittingLabel = computed(() =>
    isHostedGateway.value
      ? translate('checkout.redirecting')
      : translate('checkout.processing'))

  /**
   * Longer explanation shown under the submit button while a hosted gateway hands off to the bank.
   * Empty when nothing is in flight or the method does not leave the site, so the line only exists
   * when there is something to say.
   */
  const submittingHint = computed(() =>
    submitting.value && isHostedGateway.value ? translate('checkout.redirectingToBank') : '')

  /**
   * Formats one cart item's price, in the currency it was actually added in.
   *
   * @param item - The cart item.
   * @returns The formatted price.
   */
  const itemPrice = (item: CartItem): string =>
    formatMoney(item.amount, currencyStore.moneyCurrencyFor(item.currency))

  /**
   * Changes the guest's chosen checkout currency.
   *
   * Prices already in the cart were fetched in the previous currency and cannot be converted,
   * nothing on the frontend converts a stored price, so the cart is cleared once the visitor
   * confirms.
   *
   * @param code - The newly chosen currency code.
   * @param confirmClear - Asks the visitor to accept an emptied cart; only called when the cart has items.
   * @returns True when the currency changed.
   */
  const changeGuestCurrency = (code: string, confirmClear: () => boolean): boolean => {
    if (code === currencyStore.payerCode) {
      return false
    }
    if (cart.items.length && !confirmClear()) {
      return false
    }
    currencyStore.select(code)
    cart.clear()
    return true
  }

  /**
   * Re-prices the cart's lines from the catalogue, so a price that changed since the item was added
   * is what the payer sees before paying. A catalogue that cannot be read leaves the stored prices.
   *
   * @returns Nothing.
   */
  const refreshCartPrices = async (): Promise<void> => {
    const code = currencyStore.payerCode
    if (!code || cart.isEmpty) {
      return
    }
    cart.refreshPrices(await catalog.loadProducts(), code)
  }

  /**
   * Releases the submit button and drops the last refusal when the checkout page opens. The order
   * already placed for the cart is kept, so a payer who comes back pays it rather than a second one.
   *
   * @returns Nothing.
   */
  const resetFlow = (): void => {
    submitting.value = false
    error.value = ''
    fieldErrors.value = {}
  }

  /**
   * Reads the gateways an order may be paid through and preselects the first one.
   *
   * @returns Nothing; a failure leaves the list empty and {@link error} holding the sentence.
   */
  const loadPaymentMethods = async (): Promise<void> => {
    methodsLoading.value = true
    try {
      paymentMethods.value = await billing.fetchGatewayMethods()
      const first = paymentMethods.value[0]
      if (first && !selectedMethod.value) {
        selectedMethod.value = first.module
      }
    } catch (e: unknown) {
      paymentMethods.value = []
      error.value = apiErrorMessage(e)
    } finally {
      methodsLoading.value = false
    }
  }

  /**
   * Chooses the sentence for a failed step: the API's own, else the payment provider's, else a generic one.
   *
   * @param e - Whatever the step threw.
   * @returns The sentence to show.
   */
  const sentenceFor = (e: unknown): string => {
    const isApiRefusal = typeof e === 'object' && e !== null && 'data' in e
    if (isApiRefusal) {
      return apiErrorMessage(e)
    }
    // The card element throws `Error` carrying the payment provider's own sentence.
    return e instanceof Error && e.message ? e.message : translate('checkout.errors.generic')
  }

  /**
   * Builds the order-success path for a settled order.
   *
   * @param result - The placed order.
   * @param input - The page input, for the path localiser.
   * @returns Where the payer goes next.
   */
  const successDestination = (result: PlaceOrderResult, input: PlaceOrderInput): CheckoutDestination => {
    const domainItem = cart.items.find(i => i.itemType === CartItemType.Domain)
    return {
      kind: CheckoutDestinationKind.Route,
      target: input.localizePath(
        clientOrderSuccessPath({
          order: result.orderNumber,
          invoice: result.invoiceId,
          domain: domainItem?.domain,
          action: domainItem?.domainAction,
          digital: cart.onlyDigital(currencyStore.payerCode),
        })
      )
    }
  }

  /**
   * Places the order, or reuses the one already placed for this cart, and signs a new guest in.
   *
   * @param method - The chosen gateway module.
   * @param input - The guest's details.
   * @returns The order to settle.
   */
  const ensureOrder = async (method: string, input: PlaceOrderInput): Promise<PlaceOrderResult> => {
    if (pendingOrder.value) {
      return pendingOrder.value
    }
    const isGuest = !authStore.isLoggedIn
    const body: Record<string, unknown> = {
      // Stale-currency items are never sent: they are excluded from the total and flagged in
      // the summary for removal, and must not silently ride along into the order.
      items: cart.itemsInCurrency(currencyStore.payerCode).map(i => ({
        productId: i.pid,
        billingcycle: i.billingcycle,
        domain: i.domain || undefined,
        hostname: i.hostname || undefined,
        domainAction: i.domainAction || undefined,
        eppCode: i.eppCode || undefined,
        years: i.years || undefined,
      })),
      paymentmethod: method,
      // The guest's chosen currency; ignored by the backend for an existing signed-in client,
      // whose currency is already fixed on their client record.
      currency: currencyStore.payerCode,
    }
    if (isGuest) {
      Object.assign(body, { ...input.guest })
    }

    // The order and its invoice are created on the backend.
    const result = await billing.createOrder(body)
    pendingOrder.value = result

    // Auto-sign-in the guest after the account was created. A refusal here must not fail an
    // order that already exists; the client-area middleware asks them to sign in later.
    if (isGuest && input.guest.email && input.guest.password) {
      try {
        await authStore.login(input.guest.email, input.guest.password)
      } catch {
        // The order is placed either way.
      }
    }
    return result
  }

  /**
   * Whether the payer accepts what the backend billed. The backend prices the order itself, so its
   * total can differ from the one the cart showed (a price changed in between); a difference is put
   * to the payer, never charged silently.
   *
   * @param result - The placed order, with the total and currency the backend billed.
   * @param input - The page input, for the confirmation.
   * @returns True when the totals match or the payer agreed to the billed one.
   */
  const totalAccepted = (result: PlaceOrderResult, input: PlaceOrderInput): boolean => {
    const shownCode = currencyStore.payerCode
    const shown = cart.total(shownCode)
    const matches = result.currency === shownCode && Math.abs(result.total - shown) < TOTAL_TOLERANCE
    if (matches || acceptedTotalOrderId.value === result.orderId) {
      return true
    }
    const accepted = input.confirmTotalChange(
      formatMoney(shown, currencyStore.moneyCurrencyFor(shownCode)),
      formatMoney(result.total, currencyStore.moneyCurrencyFor(result.currency))
    )
    if (accepted) {
      acceptedTotalOrderId.value = result.orderId
    }
    return accepted
  }

  /**
   * Places the order and settles it by the chosen method.
   *
   * Refuses an empty cart, and a call while another is in flight. When it returns a destination,
   * {@link submitting} stays true: the page is leaving, and releasing the button for that moment
   * would invite a second order.
   *
   * @param input - The guest's details, the card confirmation, the total confirmation and the path localiser.
   * @returns Where the payer goes next, or null when nothing was placed or the order was refused
   * ({@link error} and {@link fieldErrors} then hold the reason).
   */
  const placeOrder = async (input: PlaceOrderInput): Promise<CheckoutDestination | null> => {
    // Claimed before the first await: a second call in the same tick (a double click, a second
    // submit handler) sees the flag and does nothing, so one cart is never placed twice. An
    // empty cart has nothing to order.
    if (!selectedMethod.value || submitting.value || cart.itemsInCurrency(currencyStore.payerCode).length === 0) {
      return null
    }
    error.value = ''
    fieldErrors.value = {}

    const method = selectedMethod.value

    submitting.value = true
    try {
      const result = await ensureOrder(method, input)

      if (result.total === 0) {
        // Nothing was due: the backend settles the invoice and fulfils a zero-total order while
        // placing it, so there is nothing to pay and no total to confirm; the payer goes straight
        // to the confirmation, whatever the method.
        const destination = successDestination(result, input)
        cart.clear()
        return destination
      }

      if (!totalAccepted(result, input)) {
        error.value = translate('checkout.errors.totalNotAccepted')
        submitting.value = false
        return null
      }

      if (method === PaymentModule.Stripe) {
        // The card is confirmed once. If the backend call below fails after the provider took the
        // payment, the id stays in pendingPaymentIntentId and a retry repeats only that call.
        if (pendingPaymentIntentId.value === null) {
          const { clientSecret } = await billing.createOrderPaymentIntent(result.orderId, result.paymentToken)
          const paymentIntent = await input.confirmCard(clientSecret)
          pendingPaymentIntentId.value = paymentIntent.id
        }

        // Tell the backend the payment succeeded: it accepts the order and creates services.
        await billing.confirmOrderPayment(
          result.orderId,
          { paymentIntentId: pendingPaymentIntentId.value },
          result.paymentToken
        )

        const destination = successDestination(result, input)
        cart.clear()
        return destination
      }

      if (isHostedGatewayModule(method)) {
        // Any plugin-backed payment method is a hosted gateway: register the payment and hand
        // the browser to the bank, so a second gateway plugin never falls through to the
        // bank-transfer branch below. The order and invoice already exist, and the payer
        // returns via /payment/result, which verifies with the bank.
        const returnUrl = new URL(
          input.localizePath(`/payment/result?order=${result.orderId}`),
          window.location.origin,
        ).toString()
        // Stored before we leave for the bank, not after: the payer returns as a fresh
        // navigation that knows only the order id, and that page has to prove the order is
        // theirs before the backend will say whether the payment went through.
        rememberPaymentToken(result.orderId, result.paymentToken, result.invoiceId)
        const { redirectUrl } = await billing.startOrderGatewayPayment(
          result.orderId,
          method,
          returnUrl,
          result.paymentToken
        )
        if (!isSafeRedirectUrl(redirectUrl)) {
          throw new Error(translate('checkout.errors.generic'))
        }
        // The cart is kept: the result page empties it once the bank confirms the payment, so a
        // declined or abandoned payment leaves the payer's choices where they were.
        return { kind: CheckoutDestinationKind.External, target: redirectUrl }
      }

      // Bank transfer: the order stays pending and the payer is sent to the invoice.
      cart.clear()
      return {
        kind: CheckoutDestinationKind.Route,
        target: input.localizePath(
          result.invoiceId ? `${CLIENT_INVOICES_PATH}/${result.invoiceId}/pay` : CLIENT_INVOICES_PATH
        )
      }
    } catch (e: unknown) {
      error.value = sentenceFor(e)
      fieldErrors.value = apiFieldErrors(e)
      submitting.value = false
      return null
    }
  }

  return {
    paymentMethods,
    methodsLoading,
    selectedMethod,
    submitting,
    error,
    fieldErrors,
    pendingOrder,
    pendingPaymentIntentId,
    staleItems,
    totalLabel,
    submittingLabel,
    submittingHint,
    itemPrice,
    changeGuestCurrency,
    refreshCartPrices,
    resetFlow,
    loadPaymentMethods,
    placeOrder
  }
})
