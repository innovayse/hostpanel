/**
 * POST /api/portal/auth/register
 *
 * Forwards a sign-up to the C# API. Not routed through `internalApiCall`: there is no account
 * yet, so there is no bearer token to attach and no 401-refresh path to take.
 *
 * `LocalAuthController.RegisterAsync` answers `Ok(new { userId })` on success and a 400 with
 * `{ error }` on a rejected registration, which `$fetch` raises rather than returns.
 */
export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  const apiUrl = (config.apiUrl as string) || 'http://localhost:5000'
  const body = await readBody(event)

  // See the note in `forgot-password.post.ts`: a template literal with no generic sends Nitro
  // into its own route table and blows the type-instantiation depth.
  const url: string = `${apiUrl}/api/auth/register`

  return await $fetch<{ userId: string }>(url, {
    method: 'POST',
    body,
  })
})
