/**
 * GET /api/portal/public/currencies
 * Returns all configured currencies from the C# backend.
 */
export default defineEventHandler(async (event) => {
  return await internalApiCall<unknown[]>(event, '/reference/currencies')
})
