/** DTO representing an announcement. */
export interface Announcement {
  /** Announcement primary key. */
  id: number
  /** Announcement title. */
  title: string
  /** HTML content body. */
  content: string
  /** Whether the announcement is published. */
  isPublished: boolean
  /** ISO 8601 creation timestamp. */
  createdAt: string
}

/** Body that creates or replaces an announcement. */
export interface AnnouncementPayload {
  /** Announcement title. */
  title: string
  /** HTML content body. */
  content: string
  /** Whether the announcement is published. */
  isPublished: boolean
}

/** What narrows the announcement list: the search text and the published state; each member left out means no narrowing. */
export interface AnnouncementListFilters {
  /** Search term over the title and the content; empty for none. */
  search?: string
  /** Keep only the published announcements (true) or only the drafts (false). */
  isPublished?: boolean
}

/** Filter and paging of the announcement list. */
export interface AnnouncementFilter extends AnnouncementListFilters {
  /** Page, 1-based. */
  page: number
  /** Items per page. */
  pageSize: number
}
