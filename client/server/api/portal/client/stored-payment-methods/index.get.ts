/**
 * GET /api/portal/client/stored-payment-methods
 * Lists the client's stored payment methods via the C# backend.
 * Requires an authenticated client session.
 *
 * @module server/api/portal/client/stored-payment-methods/index.get
 */

import type { StoredMethod } from '~/types/storedmethod'

export default defineEventHandler(async (event) => {
  return await internalApiCall<StoredMethod[]>(event, '/me/stored-payment-methods')
})
