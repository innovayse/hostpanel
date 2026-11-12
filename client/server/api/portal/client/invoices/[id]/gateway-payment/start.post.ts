/**
 * POST /api/portal/client/invoices/:id/gateway-payment/start
 * Starts a hosted-gateway payment for an invoice via the C# backend.
 * Body: { module: string, returnUrl: string, savePaymentMethod?: boolean, consentTextVersion?: string }
 * The save fields are forwarded untouched; no screen offers them for an invoice today.
 * Requires an authenticated client session.
 *
 * @module server/api/portal/client/invoices/[id]/gateway-payment/start.post
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
  return await internalApiCall<GatewayStartResult>(event, `/me/invoices/${id}/gateway-payment/start`, {
    method: HttpMethod.Post,
    body,
  })
})
