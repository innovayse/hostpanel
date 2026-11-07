/**
 * POST /api/portal/client/credit/top-ups
 * Raises the top-up invoice a signed-in client pays to add funds to their credit balance, via the C# backend, and answers with the
 * new invoice's id (the backend replies 201). Body: `{ amount: number }`. The client comes from the session, never from the body.
 * Requires an authenticated client session.
 *
 * @module server/api/portal/client/credit/top-ups.post
 */

import { HttpMethod } from '~/types/httpmethod'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  return await internalApiCall<number>(event, '/me/credit/top-ups', {
    method: HttpMethod.Post,
    body,
  })
})
