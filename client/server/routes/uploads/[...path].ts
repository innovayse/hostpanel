/**
 * GET and HEAD /uploads/**
 *
 * Proxies uploaded file requests to the backend API server.
 * Slide images are stored on the API server's wwwroot/uploads/ directory
 * and need to be accessible from the client portal's domain.
 *
 * Only the uploads folder is reachable: the path is validated and re-encoded by `requireUploadPath`
 * and appended to a fixed `/uploads/` prefix. Nothing of the visitor's request is forwarded but a
 * short allow list of cache and range headers, so a session cookie or an `Authorization` header
 * never reaches the file server, and only a short allow list of the answer's headers comes back,
 * so a `Set-Cookie` of the file server never reaches the visitor. The file server's own
 * `Content-Length` comes back with it, for a `GET` and a `HEAD` alike, so a probe that sends a `HEAD`
 * learns the size the `GET` would deliver.
 *
 * @module server/routes/uploads/[...path]
 */

import { answeredContentLength, defineReadOnlyEventHandler, isHeadRequest } from '~/server/utils/readOnlyEventHandler'
import { requireUploadPath } from '~/server/utils/uploadPath'
import { HttpMethod } from '~/types/httpmethod'

/** Request headers worth forwarding: conditional and range reads of a static file. */
const FORWARDED_REQUEST_HEADERS = ['accept', 'if-none-match', 'if-modified-since', 'range']

/** Response headers worth returning: what a browser needs to render and cache a static file. */
const RETURNED_RESPONSE_HEADERS = ['content-type', 'cache-control', 'etag', 'last-modified', 'content-range', 'accept-ranges']

/** The response header that gives the size of the file; a `HEAD` answer is the only place it tells a probe how big the file is. */
const CONTENT_LENGTH = 'content-length'

/** How long the file server may take to start answering, in milliseconds. */
const UPSTREAM_TIMEOUT_MS = 15000

export default defineReadOnlyEventHandler(async (event) => {
  const path = requireUploadPath(event)

  const headers: Record<string, string> = {}
  for (const name of FORWARDED_REQUEST_HEADERS) {
    const value = getHeader(event, name)
    if (value) {
      headers[name] = value
    }
  }

  // A refusal or a missing file is the file server's own answer: its status is passed on and the
  // body streamed through, as the browser would have seen it from the file server directly.
  //
  // A HEAD is asked of the file server as a HEAD, so answering it never pulls the file itself.
  const upstream = await $fetch.raw<ReadableStream>(`/uploads/${path}`, {
    baseURL: getApiUrl(),
    method: isHeadRequest(event) ? HttpMethod.Head : HttpMethod.Get,
    headers,
    responseType: 'stream',
    redirect: 'manual',
    ignoreResponseError: true,
    timeout: UPSTREAM_TIMEOUT_MS
  })

  const returned: Record<string, string> = { 'X-Content-Type-Options': 'nosniff' }
  for (const name of RETURNED_RESPONSE_HEADERS) {
    const value = upstream.headers.get(name)
    if (value) {
      returned[name] = value
    }
  }
  const length = answeredContentLength(upstream.headers)
  if (length) {
    returned[CONTENT_LENGTH] = length
  }
  setResponseStatus(event, upstream.status)
  setResponseHeaders(event, returned)

  // The status was set above, so `sendNoContent` answers it as it is; a bare `null` return would
  // turn a 200 into a 204.
  return isHeadRequest(event) ? sendNoContent(event, upstream.status) : upstream._data
})
