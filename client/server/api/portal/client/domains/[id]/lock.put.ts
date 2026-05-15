/**
 * PUT /api/portal/client/domains/:id/lock
 * Toggles registrar lock status for a domain via the C# backend.
 *
 * Body: { lockstatus: 'on' | 'off' }
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Domain ID is required' })

  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, `/me/domains/${id}/lock`, {
    method: 'PUT',
    body,
  })
})
