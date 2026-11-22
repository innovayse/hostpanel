/**
 * The published-content reads (announcements, legal texts and the knowledgebase) by name, so a test that answers
 * them from a stub BFF route table does not repeat the paths.
 *
 * @module tests/helpers/contentPaths
 */

import type { LegalPage } from '~/constants/legalPage'

/** The BFF path of the published announcements, which a visitor reads without signing in. */
const PUBLIC_ANNOUNCEMENTS = '/api/portal/public/announcements'

/** The content reads that have one fixed path. */
export const ContentPath = {
  Announcements: '/api/portal/client/announcements',
  PublicAnnouncements: PUBLIC_ANNOUNCEMENTS,
  KbCategories: '/api/portal/knowledgebase/categories',
  KbArticles: '/api/portal/knowledgebase/articles',
} as const

/**
 * The path of one published announcement.
 *
 * @param id - Announcement id, as it appears in the route.
 * @returns The detail path.
 */
export const announcementPath = (id: string): string => `${PUBLIC_ANNOUNCEMENTS}/${id}`

/**
 * The path of one knowledgebase article.
 *
 * @param id - Article id, as it appears in the route.
 * @returns The detail path.
 */
export const kbArticlePath = (id: string): string => `/api/portal/knowledgebase/${id}`

/**
 * The path of the operator's own text of one legal page.
 *
 * @param page - The legal page, which is its segment in the path.
 * @returns The legal text path.
 */
export const legalTextPath = (page: LegalPage): string => `/api/portal/public/legal/${page}`
