/**
 * Composable for Stripe.js integration.
 *
 * Lazily loads and caches the Stripe instance using the publishable key
 * from runtime config. The same promise is reused across all callers so
 * the Stripe.js script is loaded at most once.
 *
 * @returns Object with {@link getStripe} function that resolves to a Stripe instance.
 */
import { loadStripe, type Stripe } from '@stripe/stripe-js'

let stripePromise: Promise<Stripe | null> | null = null

/**
 * Provides access to the Stripe.js instance.
 *
 * @returns Object containing the getStripe method.
 */
export function useStripe() {
  const config = useRuntimeConfig()

  /**
   * Returns a cached Stripe.js instance, creating one on first call.
   *
   * @returns Promise resolving to the Stripe instance, or null if the key is missing.
   */
  function getStripe(): Promise<Stripe | null> {
    if (!stripePromise) {
      const key = config.public.stripePublishableKey as string
      if (!key) {
        console.error('Stripe publishable key is not configured')
        return Promise.resolve(null)
      }
      stripePromise = loadStripe(key)
    }
    return stripePromise
  }

  return { getStripe }
}
