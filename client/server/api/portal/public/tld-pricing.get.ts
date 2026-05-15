/**
 * GET /api/portal/public/tld-pricing
 * Returns TLD pricing from the C# backend.
 */
export default defineEventHandler(async (event) => {
  return await internalApiCall<Record<string, unknown>>(event, '/domains/tld-pricing')
})
