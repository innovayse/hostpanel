/**
 * POST /api/portal/auth/confirm-email
 *
 * Forwards an email-confirmation token to the C# API. Deliberately not routed through
 * `internalApiCall`: confirmation happens before there is a session, so there is no bearer
 * token to attach and no 401-refresh path to take.
 */
export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  const apiUrl = (config.apiUrl as string) || 'http://localhost:5000'
  const body = await readBody(event)

  // Bound to a plain `string`, and given an explicit response type. Handed a template literal
  // with no generic, Nitro tries to infer the response by matching the URL against its own
  // route table, and recurses far enough to raise TS2321 "excessive stack depth" six times over
  // -- for an address that is the external C# API and matches no Nitro route.
  //
  // `string`, because the success body is empty and ofetch parses that to `''`:
  // `LocalAuthController.ConfirmEmailAsync` answers a bare `Ok()` and throws on failure. `void`
  // would read better but is not a valid generic argument here, and the one caller
  // (`useAuthApi.confirmEmail`) reads nothing from the body either way.
  const url: string = `${apiUrl}/api/auth/confirm-email`

  return await $fetch<string>(url, {
    method: 'POST',
    body,
  })
})
