/**
 * POST /api/portal/client/payment-methods/confirm-setup
 * Finishes saving a card once the browser has confirmed the setup intent with Stripe.
 *
 * @module server/api/portal/client/payment-methods/confirm-setup.post
 */

import { HttpMethod } from '~/types/httpmethod'

export default defineEventHandler(async (event) => {
  const body = await readBody<{ setupIntentId?: string } | undefined>(event)
  return await internalApiCall<unknown>(event, '/me/payment-methods/confirm-setup', {
    method: HttpMethod.Post,
    body: { setupIntentId: body?.setupIntentId },
  })
})
