/**
 * Endpoints for published editorial content — announcements and the knowledgebase.
 *
 * Two naming shapes appear here and mean different things:
 *
 * - `load*` returns the `useApi()` handle (`data` / `pending` / `error` refs). These are the
 *   server-rendered reads: the pages below are public and have to be indexable, so the request
 *   must run during SSR, which only `useFetch` — and therefore only `useApi()` — can do.
 * - `fetch*` returns a plain `Promise`. Those are the one-shot reads a store or an event
 *   handler makes.
 *
 * `load*` returning refs is the transport layer's handle being passed through, not state this
 * file keeps: nothing here is cached, computed or re-read.
 *
 * @module composables/apis/useContentApi
 */

import { useApi } from '~/composables/useApi'
import type { AnnouncementList } from '~/types/announcementlist'
import type { KbArticle } from '~/types/kbarticle'
import type { KbArticleList } from '~/types/kbarticlelist'
import type { KbArticleQuery } from '~/types/kbarticlequery'
import type { KbCategory } from '~/types/kbcategory'

/**
 * The announcements and knowledgebase surface, one function per endpoint.
 *
 * @returns The content endpoint functions.
 */
export function useContentApi() {
  /**
   * Server-rendered read of the announcement list.
   *
   * @returns The `useApi()` handle for the announcement list.
   */
  const loadAnnouncements = () =>
    useApi<AnnouncementList>('/api/portal/client/announcements')

  /**
   * Server-rendered read of the knowledgebase categories.
   *
   * @returns The `useApi()` handle for the category list.
   */
  const loadKbCategories = () =>
    useApi<KbCategory[]>('/api/portal/knowledgebase/categories', { default: () => [] })

  /**
   * Server-rendered read of knowledgebase articles.
   *
   * @param query - Reactive filters; re-reads whenever they change, as `useApi()` watches them.
   * @param immediate - False to defer the first request until the caller executes it, which is
   * what the search box needs — it must not fire a blank search on page load.
   * @returns The `useApi()` handle for the article list.
   */
  const loadKbArticles = (query: () => KbArticleQuery, immediate = true) =>
    useApi<KbArticleList>('/api/portal/knowledgebase/articles', {
      query: () => query() as Record<string, unknown>,
      immediate
    })

  /**
   * Server-rendered read of one knowledgebase article, body included.
   *
   * @param id - Article id; a getter so the page re-reads when the route changes.
   * @returns The `useApi()` handle for the article.
   */
  const loadKbArticle = (id: () => string) =>
    useApi<KbArticle>(() => `/api/portal/knowledgebase/${id()}`)

  return { loadAnnouncements, loadKbCategories, loadKbArticles, loadKbArticle }
}
