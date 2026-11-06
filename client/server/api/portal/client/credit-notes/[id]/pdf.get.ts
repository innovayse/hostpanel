/**
 * GET /api/portal/client/credit-notes/:id/pdf
 * Downloads one of the signed-in client's credit notes as a PDF from the C# backend.
 *
 * The backend's `Cache-Control` is passed on, and `private, no-store` stands in when it sends none: a credit note carries a client's
 * billing details, so a missing header must not leave it cacheable.
 *
 * @module server/api/portal/client/credit-notes/[id]/pdf.get
 */

/** What the download answers with when the backend names no caching rule of its own. */
const NO_STORE = 'private, no-store'

export default defineEventHandler(async (event) => {
  const id = requireRouteParam(event, 'id')

  const file = await internalApiBinaryCall(event, `/me/credit-notes/${id}/pdf`)
  setResponseHeaders(event, {
    'content-type': file.contentType,
    'cache-control': file.cacheControl ?? NO_STORE,
    ...(file.contentDisposition ? { 'content-disposition': file.contentDisposition } : {})
  })
  return file.body
})
