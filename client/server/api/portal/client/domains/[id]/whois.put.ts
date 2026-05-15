/**
 * PUT /api/portal/client/domains/:id/whois
 * Updates WHOIS contact information for a domain via the C# backend.
 *
 * Body: { Registrant, Admin, Tech, Billing }
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Domain ID is required' })

  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, `/me/domains/${id}/whois`, {
    method: 'PUT',
    body,
  })
})
