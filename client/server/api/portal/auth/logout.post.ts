/**
 * POST /api/portal/auth/logout
 * Calls the backend logout endpoint and clears the JWT auth cookies.
 */
export default defineEventHandler(async (event) => {
  try {
    await internalApiCall(event, '/auth/logout', { method: 'POST' })
  } catch {
    // Ignore backend errors on logout — clear cookies regardless
  }

  clearAuthCookie(event)
  return { ok: true }
})
