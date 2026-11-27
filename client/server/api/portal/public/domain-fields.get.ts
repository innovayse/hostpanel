/**
 * GET /api/portal/public/domain-fields
 * Returns the extra fields the registrar plugin asks for a domain and an operation, from the C# backend.
 *
 * Forwards `domain`, `operation` and `holder` as they came; the backend's validator refuses a malformed name or an
 * operation that is none of register, transfer and contactChange (400 `VALIDATION_ERROR`), so nothing is checked here.
 * Open to a visitor who has not signed in, like the availability check the storefront runs first.
 */

/** The query members the backend reads. */
const FORWARDED = ['domain', 'operation', 'holder'] as const

export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const params = new URLSearchParams()
  for (const key of FORWARDED) {
    const value = query[key]
    if (typeof value === 'string') {
      params.set(key, value)
    }
  }
  return await internalApiCall<Record<string, unknown>[]>(event, `/domains/fields?${params.toString()}`)
})
