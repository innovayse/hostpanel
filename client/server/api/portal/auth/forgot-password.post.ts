/**
 * POST /api/portal/auth/forgot-password
 *
 * Forwards a password-reset request to the C# API, which emails the link. Not routed through
 * `internalApiCall`: there is no session yet, so there is no bearer token and no refresh path.
 *
 * The backend answers `Ok()` with an empty body whether or not the address belongs to an
 * account -- deliberately, so this endpoint cannot be used to enumerate users. There is
 * therefore nothing to read back; the empty body parses to an empty string, which is what the
 * `$fetch<string>` below states.
 */
export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  const apiUrl = (config.apiUrl as string) || 'http://localhost:5000'
  const body = await readBody(event)

  // Bound to a plain `string` and given an explicit response type: handed a template literal
  // with no generic, Nitro tries to infer the reply by matching this URL against its own route
  // table and recurses until TypeScript gives up with TS2321 -- for an address that belongs to
  // the external C# API and matches no Nitro route at all.
  const url: string = `${apiUrl}/api/auth/forgot-password`

  return await $fetch<string>(url, {
    method: 'POST',
    body,
  })
})
