/**
 * GET /api/portal/knowledgebase/categories
 *
 * Returns the knowledgebase categories, derived from the published articles.
 *
 * There is no category endpoint on the public C# API -- this route used to call
 * `/knowledgebase/categories`, which was never declared, so the whole category grid 404'd.
 * An article carries its category as a name, and that name is the category: `toKbCategories`
 * groups the one published list the API does serve.
 */

import { toKbCategories } from '../../../utils/kb'
import type { BackendKbArticle } from '../../../utils/kb'
import type { KbCategory } from '~/types/kbcategory'

export default defineEventHandler(async (event): Promise<KbCategory[]> => {
  const published = await internalApiCall<BackendKbArticle[]>(event, '/knowledgebase')
  return toKbCategories(published)
})
