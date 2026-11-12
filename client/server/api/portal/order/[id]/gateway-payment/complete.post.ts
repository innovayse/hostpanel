/**
 * POST /api/portal/order/:id/gateway-payment/complete
 * Verifies a hosted-gateway payment for an order against the C# backend,
 * which pulls the authoritative status from the bank.
 * Body: { paymentToken: string, returnParameters: { name, value }[] }, forwarded untouched.
 * Anonymous: matches the start endpoint's auth model.
 *
 * @module server/api/portal/order/[id]/gateway-payment/complete.post
 */

import type { GatewayCompletion } from '~/types/gatewaycompletion'
import { HttpMethod } from '~/types/httpmethod'

export default defineEventHandler(async (event) => {
  const id = requireRouteParam(event, 'id')

  // The names and values the gateway returned the payer with, and for an order its payment token.
  const body = await readBody(event)

  return await internalApiCall<GatewayCompletion>(
    event, `/orders/${id}/gateway-payment/complete`, { method: HttpMethod.Post, body })
})
