import type { Announcement } from '~/types/announcement'

/** The envelope `GET /api/portal/client/announcements` answers with. */
export interface AnnouncementList {
  /** The announcements on this page of results. */
  items: Announcement[]
}
