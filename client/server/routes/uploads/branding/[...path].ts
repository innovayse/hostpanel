/**
 * GET and HEAD /uploads/branding/**
 *
 * Serves operator-uploaded branding files (the logo, the favicon and the icon set
 * generated from them) from the storefront's own origin.
 *
 * **There is also a generic `/uploads/**` proxy** beside this one, for slide images.
 * This route is deliberately narrower and therefore wins for branding paths. The generic one
 * calls `proxyRequest`, which hands the upstream response back verbatim: its status, and its
 * `Content-Type`. That is acceptable for slides and not for these: branding files are what a
 * page links as its icon, so they are the ones worth pinning to a known type, refusing by
 * extension, and serving with `nosniff` and a long immutable cache.
 *
 * The files are written by the C# API into its web root, which is a different origin to
 * this one. The setting rows store root-relative URLs (`/uploads/branding/<file>`) so that a
 * domain change never strands them, which means the storefront has to answer for that
 * path itself. This route is that answer: it streams the bytes through rather than
 * redirecting, so the browser never learns the API's address and no CORS negotiation is
 * involved for what is, to the page, a same-origin image.
 *
 * Two things it deliberately does:
 *
 * - **Serves only images, and says so.** The upstream content type is ignored in favour of
 *   one derived from the extension, and `X-Content-Type-Options: nosniff` accompanies it.
 *   The API only ever writes PNGs here, so anything else is a sign something is wrong and
 *   is refused rather than passed to a browser to interpret.
 * - **Refuses to walk.** The captured path goes through `requireUploadPath`, the same reader the
 *   generic uploads proxy uses: it is decoded once, refused when it holds a `.`, `..` or empty
 *   segment, a backslash or a second encoding, and re-encoded before it is appended to the
 *   upstream URL.
 */

import type { FetchResponse } from 'ofetch'
import { defineReadOnlyEventHandler, isHeadRequest } from '~/server/utils/readOnlyEventHandler'
import { requireUploadPath } from '~/server/utils/uploadPath'
import { HttpMethod } from '~/types/httpmethod'

/** Extensions this route will serve, and the type each is served as. */
const ALLOWED_TYPES: Record<string, string> = {
  '.png': 'image/png',
  '.ico': 'image/x-icon',
}

/** The upstream response header that gives the size of the file. */
const CONTENT_LENGTH = 'content-length'

export default defineReadOnlyEventHandler(async (event) => {
  // The upstream is a file server, so a `..` that survived this far would be resolved by it, not
  // by us; the shared reader refuses it, and every other way out of the folder, first.
  const path = requireUploadPath(event)

  const extension = path.slice(path.lastIndexOf('.')).toLowerCase()
  const contentType = ALLOWED_TYPES[extension]
  if (!contentType) {
    throw createError({ statusCode: 404 })
  }

  const isHead = isHeadRequest(event)
  let upstream: FetchResponse<ArrayBuffer>
  try {
    // A HEAD is asked of the file server as a HEAD, so answering it never pulls the file itself.
    upstream = await $fetch.raw<ArrayBuffer>(`/uploads/branding/${path}`, {
      baseURL: getApiUrl(),
      method: isHead ? HttpMethod.Head : HttpMethod.Get,
      responseType: 'arrayBuffer',
    })
  } catch {
    // A missing file is a 404 here too. Nothing upstream is worth quoting: the caller is a
    // browser fetching an icon, and the only useful answer is "there is no such icon".
    throw createError({ statusCode: 404 })
  }

  const headers: Record<string, string> = {
    'Content-Type': contentType,
    'X-Content-Type-Options': 'nosniff',
    // Immutable is safe and worth a year: each upload is written into its own directory
    // named by a fresh identifier, so a replaced logo arrives at a URL no browser has
    // seen rather than under the old one.
    'Cache-Control': 'public, max-age=31536000, immutable',
  }

  if (isHead) {
    // The size is the file server's own answer to the HEAD; a bodyless answer has nothing to measure.
    const length = upstream.headers.get(CONTENT_LENGTH)
    if (length) {
      headers['Content-Length'] = length
    }
    setResponseHeaders(event, headers)
    // The status is explicit: a bare `null` return would turn the 200 into a 204.
    return sendNoContent(event, upstream.status)
  }

  setResponseHeaders(event, headers)

  // Wrapped, not returned bare. h3 recognises a Buffer (it has `.buffer`) and sends the
  // bytes; a plain ArrayBuffer matches none of its response branches and falls through to
  // JSON.stringify, which yields the string "{}" served under Content-Type: image/png.
  // With the immutable cache above, that broken body would then stick in every browser
  // that fetched it once.
  return Buffer.from(upstream._data as ArrayBuffer)
})
