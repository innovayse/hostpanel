/**
 * DELETE /api/portal/client/payment-methods/auto-charge
 * Turns off automatic card charges for the signed-in account.
 *
 * @module server/api/portal/client/payment-methods/auto-charge.delete
 */

import { HttpMethod } from '~/types/httpmethod'

export default defineEventHandler(async (event) => {
  return await internalApiCall<unknown>(event, '/me/payment-methods/auto-charge', { method: HttpMethod.Delete })
})
