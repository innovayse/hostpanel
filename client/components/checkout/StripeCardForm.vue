<template>
  <div class="space-y-4">
    <label class="block text-sm font-bold text-white/70 uppercase tracking-wider mb-2">Card Details</label>
    <div ref="cardElementRef"
         class="p-4 rounded-2xl bg-white/5 border border-white/10 min-h-[44px] transition-colors"
         :class="{ 'border-red-500/50': cardError, 'border-cyan-500/30': cardReady && !cardError }" />
    <p v-if="cardError" class="text-sm text-red-400">{{ cardError }}</p>
  </div>
</template>

<script setup lang="ts">
/**
 * Stripe card input form component.
 *
 * Mounts a Stripe Elements card element and exposes a {@link confirmPayment}
 * method for parent components to trigger payment confirmation with a client secret.
 */
import type { Stripe, StripeCardElement } from '@stripe/stripe-js'

/** Whether the Stripe card element is fully mounted and ready for input. */
const cardReady = ref(false)

/** Validation error message from the card element, or empty string when valid. */
const cardError = ref('')

/** Template ref for the DOM element where Stripe Elements mounts the card input. */
const cardElementRef = ref<HTMLDivElement | null>(null)

/** Cached Stripe.js instance used for payment confirmation. */
let stripeInstance: Stripe | null = null

/** Mounted Stripe card element instance. */
let cardElement: StripeCardElement | null = null

/**
 * Initializes the Stripe card element on mount.
 * Creates the Elements instance, applies dark theme styling, and
 * attaches event listeners for readiness and validation errors.
 */
onMounted(async () => {
  const { getStripe } = useStripe()
  stripeInstance = await getStripe()

  if (!stripeInstance || !cardElementRef.value) return

  const elements = stripeInstance.elements()
  cardElement = elements.create('card', {
    style: {
      base: {
        color: '#ffffff',
        fontFamily: '"Inter", system-ui, sans-serif',
        fontSize: '16px',
        '::placeholder': { color: '#6b7280' },
      },
      invalid: {
        color: '#f87171',
        iconColor: '#f87171',
      },
    },
  })

  cardElement.mount(cardElementRef.value)

  cardElement.on('ready', () => {
    cardReady.value = true
  })

  cardElement.on('change', (event) => {
    cardError.value = event.error?.message ?? ''
  })
})

/**
 * Destroys the Stripe card element on unmount to prevent memory leaks.
 */
onBeforeUnmount(() => {
  if (cardElement) {
    cardElement.destroy()
    cardElement = null
  }
})

/**
 * Confirms a card payment using the provided client secret.
 *
 * @param clientSecret - The Stripe PaymentIntent client secret from the backend.
 * @returns The confirmed PaymentIntent object with at least its id.
 * @throws Error if Stripe is not initialized or if the payment fails.
 */
async function confirmPayment(clientSecret: string): Promise<{ id: string }> {
  if (!stripeInstance || !cardElement) {
    throw new Error('Stripe is not initialized')
  }

  const { error, paymentIntent } = await stripeInstance.confirmCardPayment(clientSecret, {
    payment_method: { card: cardElement },
  })

  if (error) {
    throw new Error(error.message ?? 'Payment failed')
  }

  if (!paymentIntent) {
    throw new Error('No payment intent returned')
  }

  return { id: paymentIntent.id }
}

defineExpose({ confirmPayment })
</script>
