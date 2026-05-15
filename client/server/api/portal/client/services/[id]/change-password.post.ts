/**
 * POST /api/portal/client/services/:id/change-password
 * Changes the hosting account password via the C# backend.
 *
 * Body: { password: string }
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Service ID is required' })

  const body = await readBody<{ password?: string }>(event)
  if (!body?.password || body.password.length < 8) {
    throw createError({ statusCode: 400, statusMessage: 'Password must be at least 8 characters' })
  }

  return await internalApiCall<Record<string, unknown>>(event, `/me/services/${id}/change-password`, {
    method: 'POST',
    body: { password: body.password },
  })
})
