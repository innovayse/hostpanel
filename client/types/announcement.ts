/**
 * The announcement shapes, from what the backend sends to what each screen renders.
 *
 * Kept together because the three describe one journey and only make sense against each
 * other: the backend's {@link PublishedAnnouncementPage} is what a portal proxy receives,
 * {@link Announcement} is what the announcements pages read, and {@link AnnouncementSummary}
 * is the narrower version the dashboard card takes. The differences between them are the
 * point — splitting them across files is what let two proxies flatten the same envelope in
 * two different ways, one of which forgot to rename `publishedAt` to `date` and 500ed the
 * announcements page for every signed-in customer.
 */

/** One published announcement exactly as the backend returns it. */
export interface PublishedAnnouncement {
  /** Announcement primary key. */
  id: number
  /** Announcement headline. */
  title: string
  /** Body content, rendered with `v-html` by the detail page. */
  content: string
  /** ISO-8601 UTC timestamp the announcement was published. */
  publishedAt: string
}

/** The backend's paging envelope around a page of announcements. */
export interface PublishedAnnouncementPage {
  /** The announcements on this page, newest first. */
  items: PublishedAnnouncement[]
  /** Total published announcements across all pages. */
  totalCount: number
  /** The 1-based page number this envelope describes. */
  page: number
  /** How many items a full page holds. */
  pageSize: number
}

/** One published announcement, as `GET /api/portal/client/announcements` lists it. */
export interface Announcement {
  /** Announcement id, used as the detail-page route parameter. */
  id: string | number
  /** Headline. */
  title: string
  /** Publication date, already formatted by the backend for display. */
  date: string
  /** Short plain-text lead-in shown in the list. */
  excerpt?: string
  /** Full body as HTML — rendered with `v-html`, so the backend owns its sanitisation. */
  body?: string
}

/**
 * The trimmed announcement the dashboard card renders.
 *
 * Narrower than {@link Announcement} on purpose: the card shows a headline and a date and has
 * no room for a body, so the public proxy does not carry one.
 */
export interface AnnouncementSummary {
  /** Announcement primary key, used as the list key. */
  id: number
  /** Announcement headline. */
  title: string
  /** Publication date, already formatted as `YYYY-MM-DD` for display. */
  date: string
}
