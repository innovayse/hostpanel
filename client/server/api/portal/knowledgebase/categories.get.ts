/**
 * GET /api/portal/knowledgebase/categories
 * Returns all knowledgebase categories from the C# backend.
 */
export default defineEventHandler(async (event) => {
  return await internalApiCall<unknown[]>(event, '/knowledgebase/categories')
})
