/**
 * The one way a storefront route that serves a read-only file declares which methods it answers.
 *
 * Nitro registers a route file named `*.get.ts` for `GET` alone, and h3 does not fall back to the
 * `GET` handler for `HEAD`. A `HEAD` of such a route therefore never reached the handler: it fell
 * through to the page renderer and answered a status and headers that described no file, so a link
 * checker, an uptime monitor or a proxy revalidating a cached logo saw a missing file where there was
 * one. A file route is instead declared without a method suffix and wrapped here, which makes `GET`
 * and `HEAD` run the same handler (the server drops the body of a `HEAD` answer) and refuses every
 * other method with 405 before the handler runs. Dropping the suffix alone would have opened the
 * route to `POST` and the rest; the wrapper is what keeps it read-only.
 *
 * The module also holds `answeredContentLength`, the one place that decides whether a file server's
 * `Content-Length` may be passed on, so the routes that proxy a file cannot disagree about it.
 *
 * @module server/utils/readOnlyEventHandler
 */

import type { H3Event } from 'h3'
import { PortalErrorCode } from '~/constants/portalErrorCode'
import { HttpMethod } from '~/types/httpmethod'

/** The status a method that a read-only route does not answer is refused with. */
const METHOD_NOT_ALLOWED_STATUS = 405

/** The methods a read-only route answers: reading the file and reading only its headers. */
const READ_METHODS: ReadonlySet<string> = new Set([HttpMethod.Get, HttpMethod.Head])

/** The `Allow` header value a refused method is told, built from the same set the check uses. */
const ALLOWED_METHODS = [...READ_METHODS].join(', ')

/** The response header that gives the size of the file the file server holds. */
const CONTENT_LENGTH = 'content-length'

/** The response header that names the coding the file server applied to the bytes it sent. */
const CONTENT_ENCODING = 'content-encoding'

/**
 * Whether the request asks only for the headers of the file.
 *
 * @param event - The request.
 * @returns True for `HEAD`, whose answer carries no body.
 */
export const isHeadRequest = (event: H3Event): boolean => {
  return event.method === HttpMethod.Head
}

/**
 * Declares a route that answers `GET` and `HEAD` with one handler and refuses every other method.
 *
 * @param handler - The route's handler, written once for `GET`; it also runs for `HEAD`.
 * @returns The event handler to export as the route's default.
 * @throws H3Error 405 carrying `data.code = PortalErrorCode.MethodNotAllowed` and an `Allow` header, for a method
 *   other than `GET` and `HEAD`; the handler is not called.
 */
export const defineReadOnlyEventHandler = <Reply>(handler: (event: H3Event) => Reply) => {
  return defineEventHandler((event) => {
    if (!READ_METHODS.has(event.method)) {
      setResponseHeader(event, 'Allow', ALLOWED_METHODS)
      throw createError({ statusCode: METHOD_NOT_ALLOWED_STATUS, data: { code: PortalErrorCode.MethodNotAllowed } })
    }
    return handler(event)
  })
}

/**
 * The size a file route may promise for the file it answers with.
 *
 * The fetch layer undoes a content coding on its own, so for a coded answer the file server's length is
 * the size of bytes this route never sends; passing it on would promise a size the body does not have,
 * and a probe that sends a `HEAD` would learn a size the `GET` does not deliver.
 *
 * @param headers - The headers of the file server's answer.
 * @returns The file server's length, or null when it gave none or the answer was content-coded.
 */
export const answeredContentLength = (headers: Headers): string | null => {
  if (headers.get(CONTENT_ENCODING)) {
    return null
  }
  return headers.get(CONTENT_LENGTH)
}
