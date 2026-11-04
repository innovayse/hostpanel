/**
 * POST /api/portal/order/:id/create-payment-intent
 * Creates a Stripe PaymentIntent via the C# backend.
 *
 * @module server/api/portal/order/[id]/create-payment-intent.post
 */

import { HttpMethod } from '~/types/httpmethod'

/** What the backend answers a PaymentIntent creation with. */
interface PaymentIntentResult {
  /** Client secret the browser hands to Stripe to confirm the payment. */
  clientSecret: string
}

export default defineEventHandler(async (event) => {
  const id = requireRouteParam(event, 'id')

  return await internalApiCall<PaymentIntentResult>(event, `/orders/${id}/create-payment-intent`, {
    method: HttpMethod.Post,
  })
})
