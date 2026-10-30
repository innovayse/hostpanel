/**
 * The script both checkout templates share: the stores they read, the signed-in line, the guest
 * form, the card element reference and the place-order flow.
 *
 * The order flow itself lives in `stores/checkout.ts`; what the two templates had each copied was
 * the glue around it. Only the markup differs between them, so a template calls this once and lays
 * the returned state out in its own design.
 *
 * `mayPlaceOrders` is the permission side of the page: a person who acts for a client account
 * without the "place new orders" flag is shown the payment step disabled with the reason, and the
 * order is not sent. The API refuses the same order with 403 `CLIENT_PERMISSION_DENIED`, which stays
 * the real check. A signed-in first-time buyer has no account and no flags, and may order.
 *
 * @module composables/useCheckoutPage
 */

import { storeToRefs } from 'pinia'
import { Permission } from '~/composables/usePermissions'
import { useAuthStore } from '~/stores/auth'
import { useCartStore } from '~/stores/cart'
import { useCheckoutStore } from '~/stores/checkout'
import { useClientStore } from '~/stores/client'
import { useCurrencyStore } from '~/stores/currency'
import { CheckoutDestinationKind } from '~/types/checkoutdestinationkind'
import { redirectToExternal } from '~/utils/externalRedirect'

/** The card form below the method list, as far as the page needs to drive it. */
export interface CardFormHandle {
  /** Confirms the card payment once the order exists. */
  confirmPayment: (clientSecret: string) => Promise<{ id: string }>
}

/**
 * Wires a checkout page. Call it once at the top of the page's `<script setup>` and `await` it:
 * it waits for the payment methods so the server renders them.
 *
 * @returns The state and actions a checkout template lays out.
 */
export const useCheckoutPage = async () => {
  const { t } = useI18n()

  /** Prefixes a path with the active locale. */
  const localePath = useLocalePath()

  /** The cart store. */
  const cart = useCartStore()

  /** Currency store: the payer's currency and the currencies on offer. */
  const currencyStore = useCurrencyStore()

  /** Checkout store: the one order flow. */
  const checkout = useCheckoutStore()

  /** Auth store: who is signed in. */
  const authStore = useAuthStore()

  /** Whether a client is signed in, and who. */
  const { isLoggedIn, user } = storeToRefs(authStore)

  /** Client store: the signed-in person and what they may do on their account. */
  const clientStore = useClientStore()

  /** Whether the signed-in person may place an order; true for a guest and for a first-time buyer. */
  const mayPlaceOrders = computed(() => clientStore.mayDo(Permission.PlaceNewOrders))

  /** Auth actions: reload the signed-in client, sign out. */
  const { fetchUser, logout } = authStore

  onMounted(async () => {
    // The store outlives a visit to the page: a hand-off that was started on an earlier visit
    // must not leave the button disabled.
    checkout.resetFlow()
    cart.init()
    currencyStore.init()
    currencyStore.load()
    // Prices in storage may be older than the catalogue's; the payer sees the current ones.
    await checkout.refreshCartPrices()
    if (isLoggedIn.value) {
      await fetchUser()
    }
  })

  /** Site name the operator set; the browser tab title ends with it. */
  const { name: siteName } = useSiteIdentity()

  useHead({ title: computed(() => `${t('checkout.title')} — ${siteName.value}`) })

  /** Checkout state the summary and the submit button read. */
  const {
    staleItems,
    totalLabel,
    submittingLabel,
    submittingHint,
    paymentMethods,
    selectedMethod,
    submitting,
    error: orderError
  } = storeToRefs(checkout)

  /** Formats a cart line price in the currency it was added in. */
  const { itemPrice } = checkout

  /** Currencies the guest may pick from, as select options. */
  const currencyOptions = computed(() => currencyStore.currencies.map(c => ({ value: c.code, label: c.code })))

  /**
   * Changes the guest's chosen checkout currency; the store clears the cart once the visitor confirms.
   *
   * @param code - The newly chosen currency code.
   */
  const changeGuestCurrency = (code: string): void => {
    checkout.changeGuestCurrency(code, () => confirm(t('cart.currency.changeConfirm')))
  }

  /** E-mail of the signed-in client, empty for a guest. */
  const userEmail = computed(() => user.value?.email ?? '')

  /** Signs the client out so the guest form shows. */
  const handleLogout = async (): Promise<void> => {
    await logout()
  }

  /** The guest's details; sent with the order when nobody is signed in. */
  const form = reactive({
    firstname: '',
    lastname: '',
    email: '',
    password: '',
    phonenumber: ''
  })

  /** The card form below the method list; asked to confirm the payment once the order exists. */
  const stripeCardFormRef = ref<CardFormHandle | null>(null)

  /**
   * Places the order through the checkout store and goes where it says.
   *
   * @returns Nothing; a refusal stays in the store's `error` and `fieldErrors`. Nothing is sent
   * while {@link mayPlaceOrders} is false.
   */
  const submitOrder = async (): Promise<void> => {
    // The buttons are disabled in this state; this keeps a stray call from sending an order the API would refuse.
    if (!mayPlaceOrders.value) {
      return
    }
    const destination = await checkout.placeOrder({
      guest: { ...form },
      confirmCard: (clientSecret) => {
        if (!stripeCardFormRef.value) {
          return Promise.reject(new Error(t('checkout.errors.generic')))
        }
        return stripeCardFormRef.value.confirmPayment(clientSecret)
      },
      confirmTotalChange: (shown, charged) => confirm(t('checkout.totalChangedConfirm', { shown, charged })),
      localizePath: localePath
    })
    if (!destination) {
      return
    }
    if (destination.kind === CheckoutDestinationKind.External) {
      // The store already refused an unsafe address; this is the last guard before the browser leaves.
      redirectToExternal(destination.target)
    } else {
      await navigateTo(destination.target)
    }
  }

  /** Whether the payment-method list is still loading. */
  const { pending: methodsPending } = await useAsyncData('checkout:payment-methods', () => checkout.loadPaymentMethods())

  return {
    localePath,
    cart,
    currencyStore,
    isLoggedIn,
    mayPlaceOrders,
    staleItems,
    totalLabel,
    submittingLabel,
    submittingHint,
    paymentMethods,
    selectedMethod,
    submitting,
    orderError,
    itemPrice,
    currencyOptions,
    changeGuestCurrency,
    methodsPending,
    userEmail,
    handleLogout,
    form,
    stripeCardFormRef,
    submitOrder
  }
}
