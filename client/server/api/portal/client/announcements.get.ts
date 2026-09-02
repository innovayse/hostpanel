/**
 * GET /api/portal/client/announcements
 *
 * The signed-in announcements list, shaped for the page that renders it.
 *
 * This used to hand the backend's envelope straight through, and the page reads `date` while
 * the backend sends `publishedAt` — so `item.date` was `undefined` on every row and
 * `pages/announcements/index.vue` threw `Cannot read properties of undefined (reading 'match')`
 * during SSR. **Anonymous visitors never saw it**: the upstream call needs a session, so the
 * list came back empty and the loop that reads `date` never ran. Every signed-in customer got
 * a 500. The sibling public proxy has always mapped the shape; this one now does the same.
 *
 * The old query also spelled its paging the WHMCS way, `limitstart`/`limitnum`, which no
 * controller here has ever read — the backend pages with `page`/`pageSize`, so the parameters
 * were silently ignored and the caller always got page one at the default size.
 */
import type { Announcement, PublishedAnnouncementPage } from '~/types/announcement'
import type { AnnouncementList } from '~/types/announcementlist'

export default defineEventHandler(async (event): Promise<AnnouncementList> => {
  const query = getQuery(event)
  const page = Number(query.page ?? 1)
  const pageSize = Number(query.pageSize ?? 50)

  const result = await internalApiCall<PublishedAnnouncementPage>(
    event,
    `/announcements/published?page=${page}&pageSize=${pageSize}`
  )

  const items: Announcement[] = (result?.items ?? []).map(a => ({
    id: a.id,
    title: a.title,
    // The raw timestamp, not a formatted string. The page renders it through `formatDate` and
    // derives its month tabs from it, both of which need the real date and the reader's
    // locale — a server-side format would pick one language for everybody.
    date: a.publishedAt ?? '',
    body: a.content ?? '',
  }))

  return { items }
})
