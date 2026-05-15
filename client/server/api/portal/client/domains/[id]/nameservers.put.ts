/**
 * PUT /api/portal/client/domains/:id/nameservers
 * Updates the nameservers for a domain via the C# backend.
 *
 * Body: { ns1, ns2, ns3?, ns4?, ns5? }
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Domain ID is required' })

  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, `/me/domains/${id}/nameservers`, {
    method: 'PUT',
    body,
  })
})
