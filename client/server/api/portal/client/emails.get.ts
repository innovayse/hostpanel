/**
 * GET /api/portal/client/emails
 * Returns email history for the authenticated client from the C# backend.
 *
 * The API answers with a page object, the same shape the admin route uses. This unwraps it to
 * the list the account screen renders, and asks for a page big enough to cover what that screen
 * paginates client-side. The mapping lives here rather than in the API because it exists for
 * one screen — the API keeps the shape every other caller already reads.
 */

/** One email as the C# backend describes it. */
interface EmailLogEntry {
  id: number
  to: string
  subject: string
  sentAt: string
  success: boolean
  error: string | null
}

/** A page of them. */
interface EmailLogPage {
  items: EmailLogEntry[]
  totalCount: number
  page: number
  pageSize: number
}

export default defineEventHandler(async (event) => {
  // 100 is the API's per-page ceiling; asking for more silently returns 100, so the number is
  // spelled out here rather than left to look like an unlimited request.
  const result = await internalApiCall<EmailLogPage>(event, '/me/emails?page=1&pageSize=100')

  return (result?.items ?? []).map((entry) => ({
    id: String(entry.id),
    date: entry.sentAt,
    subject: entry.subject,
  }))
})
