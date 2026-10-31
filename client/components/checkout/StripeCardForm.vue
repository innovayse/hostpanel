<template>
  <UiField :label="t('checkout.cardDetails')" :error="cardError" data-testid="stripe-card-field">
    <UiWidgetFrame
      ref="frameRef"
      :ready="cardReady"
      :invalid="Boolean(cardError)"
      data-testid="stripe-card-frame"
    />
  </UiField>
</template>

<script setup lang="ts">
/**
 * Card input form component.
 *
 * Mounts the payment provider's card element and exposes {@link confirmPayment} and
 * {@link confirmSetup} for parent components to confirm a payment, or only the saving of the card,
 * with a client secret.
 */
import type { Stripe, StripeCardElement } from '@stripe/stripe-js'
import { buildStripeCardStyle } from '~/utils/stripeCardStyle'

/** Translates a key in the active language. */
const { t } = useI18n()

/** Whether the Stripe card element is fully mounted and ready for input. */
const cardReady = ref(false)

/** Validation error message from the card element, or empty string when valid. */
const cardError = ref('')

/** What the widget frame exposes. */
interface WidgetFrameHandle {
  /** The DOM node Stripe Elements mounts the card input into. */
  el: HTMLDivElement | null
}

/** The frame component, whose exposed `el` is the mount point of the card input. */
const frameRef = ref<WidgetFrameHandle | null>(null)

/** Whether the page is in dark mode; the card element's inner text colours follow it. */
const { isDark } = useAppColorMode()

/** Cached Stripe.js instance used for payment confirmation. */
let stripeInstance: Stripe | null = null

/** Mounted Stripe card element instance. */
let cardElement: StripeCardElement | null = null

/**
 * Initializes the Stripe card element on mount.
 * Creates the Elements instance, applies the colour-mode styling, and
 * attaches event listeners for readiness and validation errors.
 */
onMounted(async () => {
  const { getStripe } = useStripe()
  stripeInstance = await getStripe()

  if (!stripeInstance) {
    // No publishable key: the card field cannot appear, so say so instead of showing an empty box.
    cardError.value = t('checkout.card.unavailable')
    return
  }

  const mountPoint = frameRef.value?.el
  if (!mountPoint) {
    return
  }

  const elements = stripeInstance.elements()
  cardElement = elements.create('card', { style: buildStripeCardStyle(isDark.value) })

  cardElement.mount(mountPoint)

  cardElement.on('ready', () => {
    cardReady.value = true
  })

  cardElement.on('change', (event) => {
    cardError.value = event.error?.message ?? ''
  })
})

/** Restyles the mounted card element when the visitor toggles the colour mode. */
watch(isDark, (dark) => {
  cardElement?.update({ style: buildStripeCardStyle(dark) })
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
 * @throws Error if the payment provider is not initialized or if the payment fails.
 */
const confirmPayment = async (clientSecret: string): Promise<{ id: string }> => {
  if (!stripeInstance || !cardElement) {
    throw new Error(t('checkout.card.notInitialized'))
  }

  const { error, paymentIntent } = await stripeInstance.confirmCardPayment(clientSecret, {
    payment_method: { card: cardElement },
  })

  if (error) {
    throw new Error(error.message ?? t('checkout.card.failed'))
  }

  if (!paymentIntent) {
    throw new Error(t('checkout.card.noIntent'))
  }

  return { id: paymentIntent.id }
}

/**
 * Confirms saving the card with the provided setup intent secret, without a payment.
 *
 * @param clientSecret - The Stripe SetupIntent client secret from the backend.
 * @returns The confirmed SetupIntent object with at least its id.
 * @throws Error if the payment provider is not initialized or if the card is refused.
 */
const confirmSetup = async (clientSecret: string): Promise<{ id: string }> => {
  if (!stripeInstance || !cardElement) {
    throw new Error(t('checkout.card.notInitialized'))
  }

  const { error, setupIntent } = await stripeInstance.confirmCardSetup(clientSecret, {
    payment_method: { card: cardElement },
  })

  if (error) {
    throw new Error(error.message ?? t('checkout.card.setupFailed'))
  }

  if (!setupIntent) {
    throw new Error(t('checkout.card.noIntent'))
  }

  return { id: setupIntent.id }
}

defineExpose({ confirmPayment, confirmSetup, ready: cardReady })
</script>
