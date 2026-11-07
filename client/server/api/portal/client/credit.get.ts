/**
 * GET /api/portal/client/credit
 * Returns the signed-in client's own credit from the C# backend: the balance, the limits of one top-up and a page of the history.
 * Query: `page` and `pageSize`, both optional and forwarded untouched. The client comes from the session, never from the request.
 * Requires an authenticated client session.
 *
 * @module server/api/portal/client/credit.get
 */

import { readQueryString } from '~/server/utils/readQueryString'

export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const forwarded = new URLSearchParams()
  for (const key of ['page', 'pageSize'] as const) {
    const value = readQueryString(query[key])
    if (value !== undefined) {
      forwarded.set(key, value)
    }
  }
  const suffix = forwarded.size > 0 ? `?${forwarded.toString()}` : ''

  return await internalApiCall<Record<string, unknown>>(event, `/me/credit${suffix}`)
})
