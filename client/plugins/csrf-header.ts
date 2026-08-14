/**
 * Attaches the CSRF header to every request this app makes to its own API.
 *
 * The header is one half of the check in `server/middleware/csrf.ts`; this is the
 * other. A cross-site page can make the browser send a request with the session
 * cookie attached, but it cannot set a custom header on it without a CORS preflight
 * this API never approves — so a request carrying the header came from this origin.
 *
 * Done here rather than at each call site because there is no list of call sites:
 * pages, composables and components all reach the API through the global `$fetch`,
 * and one missed call would be a broken button rather than a missing protection.
 *
 * Only relative URLs. An absolute one is a different origin — another product's API
 * or a third party — and sending a header it did not ask for turns a simple request
 * into a preflighted one, which fails wherever the other side has not allowed it.
 */
export default defineNuxtPlugin(() => {
  const original = globalThis.$fetch

  globalThis.$fetch = original.create({
    onRequest({ request, options }) {
      const url = typeof request === 'string' ? request : request.url
      if (/^[a-z][a-z0-9+.-]*:\/\//i.test(url)) return

      // ofetch gives a Headers instance on current versions and a plain object on
      // older ones, and these two apps do not pin the same Nuxt minor.
      if (options.headers instanceof Headers) {
        options.headers.set('X-Requested-With', 'XMLHttpRequest')
      } else {
        options.headers = { ...(options.headers as Record<string, string>), 'X-Requested-With': 'XMLHttpRequest' }
      }
    },
  })
})
