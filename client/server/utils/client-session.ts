/**
 * Server-side session utility — used in all protected /api/portal/client/* routes
 *
 * Reads the `whmcs_client` httpOnly cookie set by `POST /api/portal/auth/login`,
 * parses it, and returns the typed session object.
 *
 * Throws a 401 if the cookie is missing or malformed, so callers don't need
 * to add their own auth checks — just call `requireClientSession(event)` at the top.
 *
 * Usage:
 * ```ts
 * export default defineEventHandler(async (event) => {
 *   const session = requireClientSession(event)
 *   // session.id   → WHMCS client ID
 *   // session.email
 *   // session.name
 * })
 * ```
 */

/** Shape of the data stored in the whmcs_client httpOnly cookie */
export interface ClientSession {
  /** WHMCS numeric client ID */
  id: number
  email: string
  name: string
}

/**
 * Read and validate the `whmcs_client` session cookie from the incoming request.
 *
 * @param event - H3 event from `defineEventHandler`
 * @returns Parsed `ClientSession`
 * @throws H3Error 401 if the cookie is absent, unparseable, or missing `id`
 */
export function requireClientSession(event: Parameters<typeof getCookie>[0]): ClientSession {
  const raw = getCookie(event, 'whmcs_client')

  if (!raw) {
    throw createError({ statusCode: 401, statusMessage: 'Unauthorized' })
  }

  try {
    const session = JSON.parse(raw) as ClientSession
    if (!session?.id) throw new Error('Invalid session payload')
    return session
  } catch {
    throw createError({ statusCode: 401, statusMessage: 'Unauthorized' })
  }
}
