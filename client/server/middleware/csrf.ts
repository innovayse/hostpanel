/**
 * Server middleware — CSRF
 *
 * Refuses a state-changing request that does not carry
 * `X-Requested-With: XMLHttpRequest`.
 *
 * The session lives in a cookie, and a cookie is attached by the browser to
 * requests this app did not make. SameSite=Lax stops the obvious version of that
 * for POST, but it is one flag on one cookie, decided by the browser: it does not
 * apply to every client, it has been relaxed before, and it leaves nothing to check
 * on this side. A custom header cannot be set cross-site without a CORS preflight
 * this app never approves, so its presence is evidence the request came from here.
 *
 * This is the same check the platform's .NET products run in
 * `RequireCustomHeaderMiddleware`, and the same refusal text. Without it the two
 * Nuxt portals were the only places in the suite where `POST
 * /api/portal/auth/logout` was accepted from any page on the internet — signing the
 * visitor out with nothing on this side able to tell that it had happened.
 *
 * Bearer callers are exempt: a bearer token has to be attached deliberately, which
 * a cross-site page cannot do, and the server-to-server callers that use one are
 * not browsers.
 */
const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS'])

export default defineEventHandler((event) => {
  if (SAFE_METHODS.has(event.method)) return

  // Only this app's own API. Everything else Nitro serves is a page render.
  if (!(event.path ?? '').startsWith('/api/')) return

  if (getHeader(event, 'authorization')?.startsWith('Bearer ')) return

  if (getHeader(event, 'x-requested-with') === 'XMLHttpRequest') return

  throw createError({
    statusCode: 403,
    statusMessage: 'Forbidden',
    message:
      'This endpoint changes state and requires the header '
      + 'X-Requested-With: XMLHttpRequest.',
  })
})
