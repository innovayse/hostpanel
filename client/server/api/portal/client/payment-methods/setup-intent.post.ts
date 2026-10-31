/**
 * POST /api/portal/client/payment-methods/setup-intent
 * Starts saving a card without a payment, recording the payer's agreement to automatic renewal charges.
 *
 * @module server/api/portal/client/payment-methods/setup-intent.post
 */

import { HttpMethod } from '~/types/httpmethod'

/** What the backend answers a setup with. */
interface SetupIntentResult {
  /** The setup intent to confirm afterwards. */
  setupIntentId: string
  /** Client secret the browser hands to Stripe to confirm the card. */
  clientSecret: string
}

export default defineEventHandler(async (event) => {
  const body = await readBody<{ consentAccepted?: boolean } | undefined>(event)
  return await internalApiCall<SetupIntentResult>(event, '/me/payment-methods/setup-intent', {
    method: HttpMethod.Post,
    body: { consentAccepted: body?.consentAccepted === true },
  })
})
