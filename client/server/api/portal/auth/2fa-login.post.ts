export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  const apiUrl = (config.apiUrl as string) || 'http://localhost:5000'
  const body = await readBody(event)

  const response = await $fetch<{ accessToken: string; expiresIn: number }>(
    `${apiUrl}/api/auth/2fa-login`,
    { method: 'POST', body }
  )

  setCookie(event, 'auth_token', response.accessToken, {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    maxAge: response.expiresIn ?? 900,
    path: '/',
  })
  setCookie(event, 'authed', '1', {
    httpOnly: false,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    maxAge: 60 * 60 * 24 * 7,
    path: '/',
  })

  return { success: true }
})
