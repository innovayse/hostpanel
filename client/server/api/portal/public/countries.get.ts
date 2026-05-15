/**
 * GET /api/portal/public/countries
 * Returns all supported countries from the C# backend.
 */
export default defineEventHandler(async (event) => {
  try {
    return await internalApiCall<unknown[]>(event, '/countries')
  } catch {
    // Return empty so the frontend falls back to its built-in list
    return []
  }
})
