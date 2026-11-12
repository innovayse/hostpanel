/**
 * POST /api/portal/client/invoices/:id/gateway-payment/complete
 * Verifies a hosted-gateway payment for an invoice against the C# backend,
 * which pulls the authoritative status from the bank.
 * Body: { returnParameters: { name, value }[] }, forwarded untouched.
 * Requires an authenticated client session.
 *
 * @module server/api/portal/client/invoices/[id]/gateway-payment/complete.post
 */

import type { GatewayCompletion } from '~/types/gatewaycompletion'
import { HttpMethod } from '~/types/httpmethod'

export default defineEventHandler(async (event) => {
  const id = requireRouteParam(event, 'id')

  // The names and values the gateway returned the payer with, and for an order its payment token.
  const body = await readBody(event)

  return await internalApiCall<GatewayCompletion>(
    event, `/me/invoices/${id}/gateway-payment/complete`, { method: HttpMethod.Post, body })
})
