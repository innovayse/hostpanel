/**
 * GET /api/portal/auth/sso/profile
 * Proxies to SSO GET /api/account/profile and returns user data.
 */
export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  const accessToken = getCookie(event, 'auth_token')
  if (!accessToken) throw createError({ statusCode: 401 })

  return $fetch<Record<string, unknown>>(`${config.ssoUrl}/api/account/profile`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  })
})
