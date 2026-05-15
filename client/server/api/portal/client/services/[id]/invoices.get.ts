/**
 * GET /api/portal/client/services/:id/invoices
 * Returns invoices related to a specific service via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Service ID is required' })

  return await internalApiCall<unknown[]>(event, `/me/services/${id}/invoices`)
})
