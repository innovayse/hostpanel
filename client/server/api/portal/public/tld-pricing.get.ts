/**
 * GET /api/portal/public/tld-pricing
 * Returns TLD pricing from the C# backend.
 * Forwards the optional `currency` query parameter for locale-based conversion.
 */
export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null) {
      params.append(key, String(value))
    }
  }
  const qs = params.toString()
  const endpoint = qs ? `/domains/tld-pricing?${qs}` : '/domains/tld-pricing'
  return await internalApiCall<Record<string, unknown>>(event, endpoint)
})
