<template>
  <UiStack align="center" justify="center" gap="none" class="min-h-[60dvh] px-4 py-16" data-testid="payment-result">
    <!-- Result card: an intrinsically small card, not page layout. -->
    <UiCard padding="xl" class="w-full max-w-md text-center rounded-3xl [.tpl-classic_&]:rounded-lg [.tpl-classic_&]:shadow-sm">
      <!-- Verifying -->
      <template v-if="state === GatewayPaymentState.Verifying">
        <Loader2 :size="40" class="mx-auto text-cyan-700 dark:text-cyan-400 [.tpl-classic_&]:text-classic-brand animate-spin mb-4" />
        <UiText tone="muted" data-testid="payment-verifying">{{ $t('paymentResult.verifying') }}</UiText>
      </template>

      <!-- Paid -->
      <template v-else-if="state === GatewayPaymentState.Paid">
        <CheckCircle2 :size="48" class="mx-auto text-green-600 dark:text-green-400 mb-4" />
        <UiHeading :level="1" size="subsection" class="text-xl mb-2" data-testid="payment-title">{{ $t('paymentResult.successTitle') }}</UiHeading>
        <UiText tone="muted" class="mb-8">{{ $t('paymentResult.successBody') }}</UiText>
        <UiButton :to="continueTarget" size="lg" class="font-bold" data-testid="payment-continue">
          {{ orderId ? $t('paymentResult.goToOrder') : $t('paymentResult.goToInvoice') }}
        </UiButton>
      </template>

      <!-- Pending -->
      <template v-else-if="state === GatewayPaymentState.Pending">
        <Clock :size="48" class="mx-auto text-yellow-600 dark:text-yellow-400 mb-4" />
        <UiHeading :level="1" size="subsection" class="text-xl mb-2" data-testid="payment-title">{{ $t('paymentResult.pendingTitle') }}</UiHeading>
        <UiText tone="muted" class="mb-8">{{ $t('paymentResult.pendingBody') }}</UiText>
        <UiButton variant="subtle" size="lg" class="font-bold" :disabled="checking" data-testid="payment-retry" @click="check">
          {{ $t('paymentResult.retry') }}
        </UiButton>
      </template>

      <!-- Declined -->
      <template v-else-if="state === GatewayPaymentState.Declined">
        <XCircle :size="48" class="mx-auto text-red-600 dark:text-red-400 mb-4" />
        <UiHeading :level="1" size="subsection" class="text-xl mb-2" data-testid="payment-title">{{ $t('paymentResult.declinedTitle') }}</UiHeading>
        <UiText tone="muted" class="mb-8">{{ $t('paymentResult.declinedBody') }}</UiText>
        <UiButton variant="subtle" size="lg" :to="retryTarget" class="font-bold" data-testid="payment-try-again">
          {{ $t('paymentResult.tryAgain') }}
        </UiButton>
      </template>

      <!-- Missing: the URL carried neither ?order= nor ?invoice=, so there is nothing to check
           and nothing to retry; copy and layout must not imply a payment attempt exists. -->
      <template v-else-if="state === GatewayPaymentState.Missing">
        <AlertTriangle :size="48" class="mx-auto text-yellow-600 dark:text-yellow-400 mb-4" />
        <UiHeading :level="1" size="subsection" class="text-xl mb-2" data-testid="payment-title">{{ $t('paymentResult.missingTitle') }}</UiHeading>
        <UiText tone="muted" class="mb-8">{{ $t('paymentResult.missingBody') }}</UiText>
        <UiButton variant="subtle" size="lg" :to="localePath(CLIENT_INVOICES_PATH)" class="font-bold" data-testid="payment-invoices">
          {{ $t('paymentResult.viewInvoices') }}
        </UiButton>
      </template>

      <!-- Unknown: the completion check could not be reached or parsed; no financial outcome is asserted. -->
      <template v-else>
        <AlertTriangle :size="48" class="mx-auto text-yellow-600 dark:text-yellow-400 mb-4" />
        <UiHeading :level="1" size="subsection" class="text-xl mb-2" data-testid="payment-title">{{ $t('paymentResult.unknownTitle') }}</UiHeading>
        <UiText tone="muted" class="mb-8">{{ $t('paymentResult.unknownBody') }}</UiText>
        <UiButton variant="subtle" size="lg" class="font-bold" :disabled="checking" data-testid="payment-retry" @click="check">
          {{ $t('paymentResult.retry') }}
        </UiButton>
        <UiLink :to="statusTarget" :underline="false" class="block mt-4 text-sm hover:underline" testid="payment-status">
          {{ orderId ? $t('paymentResult.goToOrder') : $t('paymentResult.goToInvoice') }}
        </UiLink>
      </template>
    </UiCard>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Landing page for hosted-gateway returnUrl redirects.
 * Reads ?order=<id> (checkout flow, anonymous) or ?invoice=<id> (invoice pay flow,
 * authenticated) and verifies the payment against the backend, which pulls the
 * authoritative status from the bank.
 */
import { CLIENT_INVOICES_PATH, clientInvoicePayPath, clientOrderSuccessPath } from '~/constants/clientRoutes'
import { AlertTriangle, CheckCircle2, Clock, Loader2, XCircle } from 'lucide-vue-next'
import { useCartStore } from '~/stores/cart'
import { useCurrencyStore } from '~/stores/currency'
import { useInvoicePaymentStore } from '~/stores/invoicepayment'
import { GatewayPaymentState } from '~/types/gatewaypaymentstate'
import { PaymentTarget } from '~/types/paymenttarget'
import { queryId } from '~/utils/queryId'

/** The current route. */
const route = useRoute()
/** Prefixes a path with the active language. */
const localePath = useLocalePath()
/** Translation helpers and the active language. */
const { t } = useI18n()
/** The invoice payment store, which verifies a hosted-gateway payment. */
const paymentStore = useInvoicePaymentStore()
/** The currency store: the payer's currency decides which cart lines the paid order held. */
const currencyStore = useCurrencyStore()
/** The cart, emptied once a checkout payment is confirmed. */
const cart = useCartStore()
/** Reads and forgets the token an order was placed with, and the invoice it was billed on. */
const { recall: recallPaymentToken, recallInvoiceId, forget: forgetPaymentToken } = useOrderPaymentToken()

/** The order id from `?order=`, when the payer came from checkout and the value is one whole number. */
const orderId = computed(() => queryId(route.query.order))

/** The invoice id from `?invoice=`, when the payer came from the invoice pay flow and the value is one whole number. */
const invoiceId = computed(() => queryId(route.query.invoice))

/**
 * Whether the address carries nothing to check: no id at all, or an id that is repeated or not a
 * whole number. Such an address is treated as missing rather than partly trusted.
 */
const addressUnusable = computed(() =>
  (!orderId.value && !invoiceId.value) ||
  (route.query.order !== undefined && !orderId.value) ||
  (route.query.invoice !== undefined && !invoiceId.value))

/**
 * The invoice a declined payment is retried against: the one in the address, or for an order the
 * one it was billed on, read from the stored entry before it is dropped.
 */
const retryInvoiceId = ref<string | undefined>(undefined)

/**
 * Where the check stands. `Unknown` means no authoritative answer could be had from the backend
 * (network error, timeout, proxy failure) and is distinct from `Declined`, which is only ever set
 * when the backend itself reported that state. `Missing` means the URL never carried an id to
 * check in the first place, so there is no payment attempt to retry and the copy and buttons must
 * not imply one exists. Copy that asserts a financial outcome only shows for confirmed states.
 */
const state = ref<GatewayPaymentState>(GatewayPaymentState.Verifying)

/** Whether a check request is in flight; the retry buttons are disabled meanwhile. */
const checking = ref(false)

/**
 * Whether the order that was just paid held only digital goods, read from the cart before it is
 * emptied: the confirmation then offers no hosting setup.
 */
const orderWasDigital = ref(false)

/**
 * Where the primary button leads after a successful payment. An order's number is the one the
 * backend answered with, never built here from the id: the prefix and padding are the operator's.
 */
const continueTarget = computed(() =>
  orderId.value
    ? localePath(clientOrderSuccessPath({ order: paymentStore.verifiedOrderNumber, digital: orderWasDigital.value }))
    : localePath(`${CLIENT_INVOICES_PATH}/${invoiceId.value}`))

/**
 * Where to send the payer to retry after a declined payment: paying the same invoice again, never
 * a fresh cart, since the order already exists. Without a known invoice the invoice list is the
 * place the unpaid one can be found.
 */
const retryTarget = computed(() => {
  const invoice = invoiceId.value ?? retryInvoiceId.value
  return invoice ? localePath(clientInvoicePayPath(invoice)) : localePath(CLIENT_INVOICES_PATH)
})

/**
 * Where to send the payer from the unknown state to check on the order or invoice themselves: a
 * read-only view, not a "pay again" link, since we do not know whether the payment succeeded.
 */
const statusTarget = computed(() =>
  invoiceId.value ? localePath(`${CLIENT_INVOICES_PATH}/${invoiceId.value}`) : localePath(CLIENT_INVOICES_PATH))

/** Calls the matching complete endpoint and updates the view state. */
const check = async (): Promise<void> => {
  // `onMounted` below already refuses to call this when neither id is present, but that
  // guarantee lives in another function and the compiler cannot carry it here, where the invoice
  // branch would otherwise post to `/invoices/undefined/gateway-payment/complete`. The check is
  // restated rather than asserted away so the two cannot drift apart.
  const target = orderId.value ? PaymentTarget.Order : PaymentTarget.Invoice
  const id = orderId.value ?? invoiceId.value
  if (!id || addressUnusable.value) {
    state.value = GatewayPaymentState.Missing
    checking.value = false
    return
  }

  // An order is paid for without an account, so the backend authorises this call with the token
  // the order was placed with rather than a credential. An invoice is the caller's own and needs
  // none. A missing token is not special-cased: the request is made either way and the backend
  // answers as it would for an order that does not exist, the same answer it gives anyone
  // guessing at ids.
  const paymentToken = target === PaymentTarget.Order ? recallPaymentToken(id) : undefined
  if (target === PaymentTarget.Order) {
    const billedInvoice = recallInvoiceId(id)
    retryInvoiceId.value = billedInvoice === undefined ? undefined : String(billedInvoice)
  }

  checking.value = true
  const result = await paymentStore.verifyGatewayPayment(target, id, paymentToken)
  state.value = result
  if (target === PaymentTarget.Order && result === GatewayPaymentState.Paid) {
    // The cart was kept while the payer was at the bank so a declined payment loses nothing; it is
    // emptied only now that the payment is confirmed, after noting whether it held only digital goods.
    orderWasDigital.value = cart.onlyDigital(currencyStore.payerCode)
    cart.clear()
  }
  if (target === PaymentTarget.Order && result !== GatewayPaymentState.Pending && result !== GatewayPaymentState.Unknown) {
    // Settled one way or the other: the token has done its job and should not sit in the browser
    // afterwards. A pending payment keeps it so a retry can still verify, and so does an unknown
    // outcome: the store reports a failure to get an answer as `Unknown`, and the payer may need
    // the token to check again.
    forgetPaymentToken(id)
  }
  checking.value = false
}

onMounted(() => {
  if (addressUnusable.value) {
    // Nothing to verify: do not call check(), which would fall through to the invoice branch
    // with an undefined id and request /invoices/undefined.
    state.value = GatewayPaymentState.Missing
    return
  }
  check()
})

// `t` is resolved here, at the top of setup, and not inside the computed below. A `useNuxtApp()`
// call in the getter runs when the head is resolved rather than during setup, by which point the
// Nuxt instance context is gone: server-side that threw "[nuxt] instance unavailable" and turned
// this whole page into a 500.
useHead({ title: computed(() => t('paymentResult.title')) })
</script>
