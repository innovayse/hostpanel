/**
 * GET /api/portal/public/slides
 *
 * Returns active slides for the current locale.
 * Forwards the x-locale header to the backend for translation resolution.
 * Optionally accepts `audience` query param (guest/authenticated).
 */
export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const audienceParam = query.audience ? `?audience=${query.audience}` : ''

  return await internalApiCall(event, `/slides${audienceParam}`)
})
