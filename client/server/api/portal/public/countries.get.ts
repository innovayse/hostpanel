/**
 * GET /api/portal/public/countries
 * Returns all supported countries from the C# backend.
 *
 * The path is `/reference/countries` — the API serves countries and currencies together on
 * `api/reference`, and the currencies route already spells it that way. This one asked for
 * `/countries`, which no controller answers, so every call 404'd and the catch below turned
 * that into the empty list. The fallback hid it: the country dropdown quietly ran on the
 * frontend's built-in list forever, and nothing looked broken.
 */
export default defineEventHandler(async (event) => {
  try {
    return await internalApiCall<unknown[]>(event, '/reference/countries')
  } catch {
    // Return empty so the frontend falls back to its built-in list
    return []
  }
})
