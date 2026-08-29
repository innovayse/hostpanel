/**
 * GET /api/portal/public/announcements
 *
 * The dashboard's "Recent News" card. Returns the ten most recent *published* announcements.
 *
 * This used to call `/announcements?limitnum=10`, which is the admin controller
 * (`[Authorize(Roles = Roles.Admin)]`), so every signed-in customer got 403 and an empty card
 * while admins saw content — which is why it went unnoticed. It now calls the client-facing
 * `/announcements/published`, whose projection has no `isPublished` flag and never contains a
 * draft. The `limitnum` parameter was WHMCS's spelling and no controller here ever read it; the
 * backend pages with `page`/`pageSize`.
 *
 * The response is flattened to the `{ id, title, date }` shape the card renders, so the page
 * does not have to know about the backend's paging envelope.
 */

/** One published announcement as the backend returns it. */
interface PublishedAnnouncement {
  /** Announcement primary key. */
  id: number
  /** Announcement headline. */
  title: string
  /** Body content. Not rendered by the dashboard card, kept for callers that show detail. */
  content: string
  /** ISO-8601 UTC timestamp the announcement was published. */
  publishedAt: string
}

/** The backend's paging envelope around a page of announcements. */
interface PublishedAnnouncementPage {
  /** The announcements on this page, newest first. */
  items: PublishedAnnouncement[]
  /** Total published announcements across all pages. */
  totalCount: number
  /** The 1-based page number this envelope describes. */
  page: number
  /** How many items a full page holds. */
  pageSize: number
}

/** The shape the dashboard card consumes. */
interface AnnouncementSummary {
  /** Announcement primary key, used as the list key. */
  id: number
  /** Announcement headline. */
  title: string
  /** Publication date, already formatted as `YYYY-MM-DD` for display. */
  date: string
}

export default defineEventHandler(async (event): Promise<AnnouncementSummary[]> => {
  const result = await internalApiCall<PublishedAnnouncementPage>(
    event,
    '/announcements/published?page=1&pageSize=10'
  )

  return (result?.items ?? []).map(a => ({
    id: a.id,
    title: a.title,
    // Date only: the card has one narrow line for it, and a full timestamp wraps.
    date: a.publishedAt?.slice(0, 10) ?? '',
  }))
})
