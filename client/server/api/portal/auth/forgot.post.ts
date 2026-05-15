/**
 * POST /api/portal/auth/forgot
 * Sends a password reset email via the C# backend.
 * Always returns success to prevent email enumeration.
 */
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  const { email } = body ?? {}

  if (!email) {
    throw createError({ statusCode: 400, statusMessage: serverT(event, 'error.emailRequired') })
  }

  try {
    await internalApiCall(event, '/auth/forgot-password', { method: 'POST', body: { email } })
  } catch {
    // Swallow backend errors (e.g. email not found) to prevent enumeration
  }

  // Always return success so the UI can show a generic confirmation message
  return { ok: true }
})
