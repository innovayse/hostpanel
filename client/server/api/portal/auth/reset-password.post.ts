/**
 * POST /api/portal/auth/reset-password
 *
 * Forwards a new password and its reset token to the C# API. Not routed through
 * `internalApiCall`: the caller is by definition signed out, so there is no bearer token to
 * attach and no 401-refresh path to take.
 *
 * `LocalAuthController.ResetPasswordAsync` answers a bare `Ok()` on success and a 400 carrying
 * `{ error }` on a bad or expired token -- which `$fetch` raises rather than returns. So the
 * success body is empty, and an empty body parses to an empty string -- hence `$fetch<string>`.
 */
export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  const apiUrl = (config.apiUrl as string) || 'http://localhost:5000'
  const body = await readBody(event)

  // See the note in `forgot-password.post.ts`: a template literal with no generic sends Nitro
  // into its own route table and blows the type-instantiation depth.
  const url: string = `${apiUrl}/api/auth/reset-password`

  return await $fetch<string>(url, {
    method: 'POST',
    body,
  })
})
