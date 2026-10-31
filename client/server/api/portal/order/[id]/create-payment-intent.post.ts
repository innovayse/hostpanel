/**
 * POST /api/portal/order/:id/create-payment-intent
 * Creates a Stripe PaymentIntent via the C# backend.
 *
 * Only the order's payment token and the payer's choice to keep the card are forwarded: the backend
 * reads where the request came from itself, so nothing the browser says about that is passed on.
 *
 * @module server/api/portal/order/[id]/create-payment-intent.post
 */

import { HttpMethod } from '~/types/httpmethod'

/** What the browser sends to open a PaymentIntent. */
interface PaymentIntentRequest {
  /** The order's payment token, returned when it was placed. */
  paymentToken?: string
  /** Whether the payer ticked "save the card for automatic renewals". */
  saveCard?: boolean
}

/** What the backend answers a PaymentIntent creation with. */
interface PaymentIntentResult {
  /** Client secret the browser hands to Stripe to confirm the payment. */
  clientSecret: string
}

export default defineEventHandler(async (event) => {
  const id = requireRouteParam(event, 'id')

  const { paymentToken, saveCard } = (await readBody<PaymentIntentRequest | undefined>(event)) ?? {}
  return await internalApiCall<PaymentIntentResult>(event, `/orders/${id}/create-payment-intent`, {
    method: HttpMethod.Post,
    body: { paymentToken, saveCard: saveCard === true },
  })
})
