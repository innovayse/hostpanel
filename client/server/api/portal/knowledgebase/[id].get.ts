/**
 * GET /api/portal/knowledgebase/:id
 *
 * Returns one published knowledgebase article, body included.
 *
 * Calls `GET /api/knowledgebase/{id}` -- the route the backend actually declares. This used to
 * call `/knowledgebase/articles/{id}`, a path that was never declared, so every article page
 * 404'd.
 *
 * A missing id and an unpublished draft both come back as **400** with code `INVALID_OPERATION`,
 * not 404: `GetPublishedKbArticleHandler` throws a bare `InvalidOperationException` for either,
 * deliberately answering the two identically so a visitor cannot map the unpublished backlog by
 * guessing ids. That is passed straight through -- the article page renders the failure rather
 * than an empty article.
 */

import { toKbArticle } from '../../../utils/kb'
import type { BackendKbArticle } from '../../../utils/kb'
import type { KbArticle } from '~/types/kbarticle'

export default defineEventHandler(async (event): Promise<KbArticle> => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Article ID is required' })

  const article = await internalApiCall<BackendKbArticle>(event, `/knowledgebase/${id}`)
  return toKbArticle(article)
})
