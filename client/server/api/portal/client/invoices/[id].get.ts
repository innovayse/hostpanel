/**
 * GET /api/portal/client/invoices/:id
 * Returns full details of a single invoice from the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Invoice ID is required' })

  return await internalApiCall<Record<string, unknown>>(event, `/me/invoices/${id}`)
})
