/**
 * POST /api/portal/auth/register
 * Creates a new client account via the C# backend.
 * Does not log the user in — redirect to login after.
 */
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  const { firstname, lastname, email, password } = body ?? {}

  if (!firstname || !lastname || !email || !password) {
    throw createError({
      statusCode: 400,
      statusMessage: serverT(event, 'error.registerFields')
    })
  }

  try {
    return await internalApiCall<Record<string, unknown>>(event, '/auth/register', {
      method: 'POST',
      body: { firstName: firstname, lastName: lastname, email, password }
    })
  } catch (err: unknown) {
    const e = err as { statusCode?: number; statusMessage?: string }
    const msg = e?.statusMessage ?? ''

    if (e?.statusCode === 400 && msg.toLowerCase().includes('duplicate')) {
      throw createError({ statusCode: 409, statusMessage: serverT(event, 'error.emailInUse') })
    }

    throw createError({
      statusCode: e?.statusCode ?? 500,
      statusMessage: msg.toLowerCase().includes('is already taken')
        ? serverT(event, 'error.emailInUse')
        : serverT(event, 'error.registerFailed')
    })
  }
})
