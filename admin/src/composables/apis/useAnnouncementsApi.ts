/**
 * Endpoints behind the announcements store.
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import type { Announcement, AnnouncementFilter, AnnouncementPayload } from '@/types/announcement'
import type { PaginatedList } from '@/types/paginatedlist'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'

/**
 * Endpoint functions for `/announcements`.
 *
 * @returns One typed function per endpoint.
 */
export const useAnnouncementsApi = () => {
  const { request } = useApi()

  /**
   * Lists announcements, one page.
   *
   * @param filter - Page, page size, search and published state.
   * @returns The page and the total count.
   */
  const listAnnouncements = (filter: AnnouncementFilter): Promise<PaginatedList<Announcement>> =>
    request<PaginatedList<Announcement>>(
      `${ApiPath.Announcements}${buildQuery({
        page: filter.page,
        pageSize: filter.pageSize,
        search: filter.search,
        isPublished: filter.isPublished,
      })}`,
    )

  /**
   * Reads one announcement.
   *
   * @param id - Announcement id.
   * @returns The announcement.
   */
  const getAnnouncement = (id: number | string): Promise<Announcement> =>
    request<Announcement>(`${ApiPath.Announcements}/${id}`)

  /**
   * Creates an announcement.
   *
   * @param payload - Title, content and published flag.
   * @returns The new announcement's id.
   */
  const createAnnouncement = (payload: AnnouncementPayload): Promise<number> =>
    request<number>(ApiPath.Announcements, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Replaces an announcement.
   *
   * @param id - Announcement id.
   * @param payload - Title, content and published flag.
   * @returns Nothing.
   */
  const updateAnnouncement = (id: number | string, payload: AnnouncementPayload): Promise<void> =>
    request<void>(`${ApiPath.Announcements}/${id}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Deletes an announcement.
   *
   * @param id - Announcement id.
   * @returns Nothing.
   */
  const deleteAnnouncement = (id: number | string): Promise<void> =>
    request<void>(`${ApiPath.Announcements}/${id}`, { method: HttpMethod.Delete })

  return { listAnnouncements, getAnnouncement, createAnnouncement, updateAnnouncement, deleteAnnouncement }
}
