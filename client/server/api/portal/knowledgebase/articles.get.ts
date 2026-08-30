/**
 * GET /api/portal/knowledgebase/articles
 *
 * Returns published knowledgebase articles from the C# backend.
 *
 * The backend exposes one public list route -- `GET /api/knowledgebase`, which answers the
 * whole published set as `KbArticleDto[]`. It has no `articles` sub-path, no filters and no
 * paging, which is why this route used to call `/knowledgebase/articles` and 404 on every
 * request. The WHMCS-shaped filters the pages send (`categoryid`, `search`, `limitstart`,
 * `limitnum`) are therefore applied here rather than upstream: the published knowledgebase is
 * a small, public, cacheable set, so narrowing it in the BFF costs one call and keeps the API
 * surface as it is.
 */

import { toKbArticle } from '../../../utils/kb'
import type { BackendKbArticle } from '../../../utils/kb'
import type { KbArticle } from '~/types/kbarticle'
import type { KbArticleList } from '~/types/kbarticlelist'

export default defineEventHandler(async (event): Promise<KbArticleList> => {
  const query = getQuery(event)

  const categoryId = query.categoryid ? String(query.categoryid) : ''
  const search = query.search ? String(query.search).trim().toLowerCase() : ''
  const limitStart = Number(query.limitstart ?? 0) || 0
  const limitNum = Number(query.limitnum ?? 0) || 0

  const published = await internalApiCall<BackendKbArticle[]>(event, '/knowledgebase')

  let matched = published

  // Categories have no ids of their own in this backend -- an article carries its category as
  // a name -- so `categoryid` is matched against that name. `kb.ts` derives the category list
  // from the same field, so the two always agree on what a category is.
  if (categoryId) {
    matched = matched.filter(a => (a.category ?? '').toLowerCase() === categoryId.toLowerCase())
  }

  if (search) {
    matched = matched.filter(
      a => (a.title ?? '').toLowerCase().includes(search)
        || (a.content ?? '').toLowerCase().includes(search)
    )
  }

  const page = limitNum > 0 ? matched.slice(limitStart, limitStart + limitNum) : matched.slice(limitStart)

  // List rows show the excerpt only, so the full body is dropped here: it is HTML, it is the
  // largest field on the record, and sending it for fifty rows nobody expands is the whole
  // payload for none of the rendering.
  const items: KbArticle[] = page.map((a) => {
    const article = toKbArticle(a)
    return { id: article.id, title: article.title, excerpt: article.excerpt }
  })

  return { items }
})
