<template>
  <div class="min-h-[60dvh] flex items-center justify-center px-4 py-16">
    <div class="w-full max-w-md p-8 rounded-3xl bg-white/5 border border-white/10 text-center">
      <!-- Verifying -->
      <template v-if="state === 'verifying'">
        <Loader2 :size="40" class="mx-auto text-cyan-400 animate-spin mb-4" />
        <p class="text-gray-300">{{ $t('paymentResult.verifying') }}</p>
      </template>

      <!-- Paid -->
      <template v-else-if="state === 'paid'">
        <CheckCircle2 :size="48" class="mx-auto text-green-400 mb-4" />
        <h1 class="text-xl font-bold text-white mb-2">{{ $t('paymentResult.successTitle') }}</h1>
        <p class="text-gray-400 mb-8">{{ $t('paymentResult.successBody') }}</p>
        <NuxtLink :to="continueTarget"
                  class="inline-block px-6 py-3 rounded-xl bg-gradient-to-r from-cyan-600 to-primary-600 text-white font-bold">
          {{ orderId ? $t('paymentResult.goToOrder') : $t('paymentResult.goToInvoice') }}
        </NuxtLink>
      </template>

      <!-- Pending -->
      <template v-else-if="state === 'pending'">
        <Clock :size="48" class="mx-auto text-yellow-400 mb-4" />
        <h1 class="text-xl font-bold text-white mb-2">{{ $t('paymentResult.pendingTitle') }}</h1>
        <p class="text-gray-400 mb-8">{{ $t('paymentResult.pendingBody') }}</p>
        <button class="px-6 py-3 rounded-xl bg-white/10 border border-white/10 text-white font-bold disabled:opacity-50"
                :disabled="checking" @click="check">
          {{ $t('paymentResult.retry') }}
        </button>
      </template>

      <!-- Declined -->
      <template v-else-if="state === 'declined'">
        <XCircle :size="48" class="mx-auto text-red-400 mb-4" />
        <h1 class="text-xl font-bold text-white mb-2">{{ $t('paymentResult.declinedTitle') }}</h1>
        <p class="text-gray-400 mb-8">{{ $t('paymentResult.declinedBody') }}</p>
        <NuxtLink :to="retryTarget"
                  class="inline-block px-6 py-3 rounded-xl bg-white/10 border border-white/10 text-white font-bold">
          {{ $t('paymentResult.tryAgain') }}
        </NuxtLink>
      </template>

      <!-- Missing: the URL carried neither ?order= nor ?invoice=, so there is nothing to check
           and nothing to retry — copy and layout must not imply a payment attempt exists. -->
      <template v-else-if="state === 'missing'">
        <AlertTriangle :size="48" class="mx-auto text-yellow-400 mb-4" />
        <h1 class="text-xl font-bold text-white mb-2">{{ $t('paymentResult.missingTitle') }}</h1>
        <p class="text-gray-400 mb-8">{{ $t('paymentResult.missingBody') }}</p>
        <NuxtLink :to="localePath('/client/invoices')"
                  class="inline-block px-6 py-3 rounded-xl bg-white/10 border border-white/10 text-white font-bold">
          {{ $t('paymentResult.viewInvoices') }}
        </NuxtLink>
      </template>

      <!-- Unknown: we could not reach/parse the completion check — do not assert a financial outcome -->
      <template v-else>
        <AlertTriangle :size="48" class="mx-auto text-yellow-400 mb-4" />
        <h1 class="text-xl font-bold text-white mb-2">{{ $t('paymentResult.unknownTitle') }}</h1>
        <p class="text-gray-400 mb-8">{{ $t('paymentResult.unknownBody') }}</p>
        <button class="px-6 py-3 rounded-xl bg-white/10 border border-white/10 text-white font-bold disabled:opacity-50"
                :disabled="checking" @click="check">
          {{ $t('paymentResult.retry') }}
        </button>
        <div class="mt-4">
          <NuxtLink :to="statusTarget" class="text-sm text-cyan-400 hover:underline">
            {{ orderId ? $t('paymentResult.goToOrder') : $t('paymentResult.goToInvoice') }}
          </NuxtLink>
        </div>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * Landing page for hosted-gateway returnUrl redirects (e.g. Inecobank).
 * Reads ?order=<id> (checkout flow, anonymous) or ?invoice=<id> (invoice pay flow,
 * authenticated) and verifies the payment against the backend, which pulls the
 * authoritative status from the bank.
 */
import { AlertTriangle, CheckCircle2, Clock, Loader2, XCircle } from 'lucide-vue-next'
import { useBillingApi } from '~/composables/apis/useBillingApi'

const route = useRoute()
const localePath = useLocalePath()
const { t } = useI18n()
const { recall: recallPaymentToken, forget: forgetPaymentToken } = useOrderPaymentToken()

const orderId = computed(() => route.query.order as string | undefined)
const invoiceId = computed(() => route.query.invoice as string | undefined)

// 'unknown' means we could not get an authoritative answer from the backend
// (network error, timeout, proxy failure, etc.) — distinct from 'declined',
// which is only ever set when the backend itself reported that state. 'missing'
// means the URL never carried an id to check in the first place (no ?order= or
// ?invoice=) — distinct from 'unknown' because there is no payment attempt to
// retry, so its copy and buttons must not imply one exists. Copy that asserts a
// financial outcome must only show for backend-confirmed states.
const state = ref<'verifying' | 'paid' | 'pending' | 'declined' | 'unknown' | 'missing'>('verifying')
const checking = ref(false)

/** Where the primary button leads after a successful payment. */
const continueTarget = computed(() =>
  orderId.value
    ? localePath(`/client/order-success?order=ORD-${String(orderId.value).padStart(4, '0')}`)
    : localePath(`/client/invoices/${invoiceId.value}`))

/** Where to send the payer to retry after a declined payment. */
const retryTarget = computed(() =>
  invoiceId.value ? localePath(`/client/invoices/${invoiceId.value}/pay`) : localePath('/cart'))

/**
 * Where to send the payer from the 'unknown' state to check on the order/invoice
 * themselves — a read-only view, not a "pay again" link, since we do not know
 * whether the payment already succeeded.
 */
const statusTarget = computed(() =>
  invoiceId.value ? localePath(`/client/invoices/${invoiceId.value}`) : localePath('/client/invoices'))

/** Calls the matching complete endpoint and updates the view state. */
const check = async (): Promise<void> => {
  // `onMounted` below already refuses to call this when neither id is present, but that
  // guarantee lives in another function and the compiler cannot carry it here — where the
  // invoice branch would otherwise post to `/invoices/undefined/gateway-payment/complete`. The
  // check is restated rather than asserted away so the two cannot drift apart.
  const target = orderId.value ? 'order' : 'invoice'
  const id = orderId.value ?? invoiceId.value
  if (!id) {
    state.value = 'missing'
    checking.value = false
    return
  }

  // An order is paid for without an account, so the backend authorises this call with the token
  // the order was placed with rather than a credential. An invoice is the caller's own and needs
  // none. A missing token is not special-cased here: the request is made either way and the
  // backend answers as it would for an order that does not exist, which is the same answer it
  // gives anyone guessing at ids.
  const paymentToken = target === 'order' ? recallPaymentToken(id) : undefined

  checking.value = true
  try {
    const { state: result } = await useBillingApi().completeGatewayPayment(target, id, paymentToken)
    state.value = result
    if (target === 'order' && result !== 'pending') {
      // Settled one way or the other: the token has done its job and should not sit in the
      // browser afterwards. 'pending' is left alone so a retry can still verify.
      forgetPaymentToken(id)
    }
  } catch {
    // We could not confirm the outcome — do not claim the payment was declined
    // (it may well have succeeded at the bank). The reconciler will settle the
    // invoice/order status regardless; this only affects what we tell the payer.
    state.value = 'unknown'
  } finally {
    checking.value = false
  }
}

onMounted(() => {
  if (!orderId.value && !invoiceId.value) {
    // Nothing to verify — do not call check(), which would fall through to the
    // invoice branch with an undefined id and hit /invoices/undefined/....
    state.value = 'missing'
    return
  }
  check()
})

// `t` is resolved here, at the top of setup, and not inside the computed below. A
// `useNuxtApp()` call in the getter runs when the head is resolved rather than during
// setup, by which point the Nuxt instance context is gone — server-side that threw
// "[nuxt] instance unavailable" and turned this whole page into a 500.
useHead({ title: computed(() => t('paymentResult.title')) })
</script>
