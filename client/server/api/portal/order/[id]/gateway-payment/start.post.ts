/**
 * POST /api/portal/order/:id/gateway-payment/start
 * Starts a hosted-gateway payment for an order via the C# backend.
 * Body: { module: string, returnUrl: string, paymentToken?: string, savePaymentMethod?: boolean, consentTextVersion?: string }
 * The save fields are forwarded untouched: the backend records the consent and the gateway saves the method within the payment.
 * Anonymous: the order/invoice already exist from checkout.
 *
 * @module server/api/portal/order/[id]/gateway-payment/start.post
 */

import { HttpMethod } from '~/types/httpmethod'

/** What the backend answers a gateway payment start with. */
interface GatewayStartResult {
  /** The bank page the browser is sent to. */
  redirectUrl: string
}

export default defineEventHandler(async (event) => {
  const id = requireRouteParam(event, 'id')

  const body = await readBody(event)
  return await internalApiCall<GatewayStartResult>(event, `/orders/${id}/gateway-payment/start`, {
    method: HttpMethod.Post,
    body,
  })
})
