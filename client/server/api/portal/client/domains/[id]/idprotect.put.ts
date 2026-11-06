/**
 * PUT /api/portal/client/domains/:id/idprotect
 * Toggles WHOIS privacy protection for a domain via the C# backend.
 *
 * Body: { value: boolean }
 */

import { HttpMethod } from '~/types/httpmethod'

export default defineEventHandler(async (event) => {
  const id = requireRouteParam(event, 'id')

  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, `/me/domains/${id}/whois-privacy`, {
    method: HttpMethod.Put,
    body,
  })
})
