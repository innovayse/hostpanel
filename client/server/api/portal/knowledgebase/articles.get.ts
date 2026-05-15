/**
 * GET /api/portal/knowledgebase/articles
 * Returns paginated knowledgebase articles from the C# backend.
 */
export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const params = new URLSearchParams()

  if (query.categoryid)  params.set('categoryid',  String(query.categoryid))
  if (query.search)      params.set('search',       String(query.search))
  if (query.limitnum)    params.set('limitnum',     String(query.limitnum))
  if (query.limitstart)  params.set('limitstart',   String(query.limitstart))

  const qs = params.toString()
  return await internalApiCall<Record<string, unknown>>(event, `/knowledgebase/articles${qs ? `?${qs}` : ''}`)
})
