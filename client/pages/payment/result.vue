<template>
  <div class="min-h-[60vh] flex items-center justify-center px-4 py-16">
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

      <!-- Declined / error -->
      <template v-else>
        <XCircle :size="48" class="mx-auto text-red-400 mb-4" />
        <h1 class="text-xl font-bold text-white mb-2">{{ $t('paymentResult.declinedTitle') }}</h1>
        <p class="text-gray-400 mb-8">{{ $t('paymentResult.declinedBody') }}</p>
        <NuxtLink :to="retryTarget"
                  class="inline-block px-6 py-3 rounded-xl bg-white/10 border border-white/10 text-white font-bold">
          {{ $t('paymentResult.tryAgain') }}
        </NuxtLink>
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
import { CheckCircle2, Clock, Loader2, XCircle } from 'lucide-vue-next'

const route = useRoute()
const localePath = useLocalePath()

const orderId = computed(() => route.query.order as string | undefined)
const invoiceId = computed(() => route.query.invoice as string | undefined)

const state = ref<'verifying' | 'paid' | 'pending' | 'declined'>('verifying')
const checking = ref(false)

/** Where the primary button leads after a successful payment. */
const continueTarget = computed(() =>
  orderId.value
    ? localePath(`/client/order-success?order=ORD-${String(orderId.value).padStart(4, '0')}`)
    : localePath(`/client/invoices/${invoiceId.value}`))

/** Where to send the payer to retry after a declined payment. */
const retryTarget = computed(() =>
  invoiceId.value ? localePath(`/client/invoices/${invoiceId.value}/pay`) : localePath('/cart'))

/** Calls the matching complete endpoint and updates the view state. */
async function check() {
  checking.value = true
  try {
    const url = orderId.value
      ? `/api/portal/order/${orderId.value}/gateway-payment/complete`
      : `/api/portal/client/invoices/${invoiceId.value}/gateway-payment/complete`
    const { state: result } = await apiFetch<{ state: 'paid' | 'pending' | 'declined' }>(
      url, { method: 'POST' })
    state.value = result
  } catch {
    state.value = 'declined'
  } finally {
    checking.value = false
  }
}

onMounted(() => {
  if (!orderId.value && !invoiceId.value) {
    state.value = 'declined'
    return
  }
  check()
})

useHead({ title: computed(() => useNuxtApp().$i18n.t('paymentResult.title')) })
</script>
