/**
 * GET /api/portal/knowledgebase/:id
 * Returns full detail for a single knowledgebase article from the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Article ID is required' })

  return await internalApiCall<Record<string, unknown>>(event, `/knowledgebase/articles/${id}`)
})
