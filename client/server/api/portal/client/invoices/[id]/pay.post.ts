/**
 * POST /api/portal/client/invoices/:id/pay
 * Charges the saved payment method for an invoice via the backend.
 *
 * Body: { gatewayModuleId: string, paymentMethodId: string, attemptId: string }, passed through unchanged: the attempt id
 * is what the backend deduplicates the charge by.
 */

import { HttpMethod } from '~/types/httpmethod'

export default defineEventHandler(async (event) => {
  const id = requireRouteParam(event, 'id')

  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, `/me/invoices/${id}/pay`, {
    method: HttpMethod.Post,
    body,
  })
})
