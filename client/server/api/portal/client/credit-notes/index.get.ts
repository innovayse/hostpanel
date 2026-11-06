/**
 * GET /api/portal/client/credit-notes
 * Returns the credit notes issued to the authenticated client from the C# backend. The client comes from the session, never from the
 * request, so only the caller's own notes are listed.
 *
 * A credit note is not invoice credit: it is a numbered document that reverses part of an invoice that was already paid.
 *
 * @module server/api/portal/client/credit-notes/index.get
 */
export default defineEventHandler(async (event) => {
  return await internalApiCall<unknown[]>(event, '/me/credit-notes')
})
