/** The filters `GET /api/portal/knowledgebase/articles` accepts. */
export interface KbArticleQuery {
  /** Restrict to one category. Omit for every category. */
  categoryid?: string | number
  /** Free-text search term. Omit to list rather than search. */
  search?: string
  /** How many articles to return. */
  limitnum?: number
  /** How many articles to skip, for paging. */
  limitstart?: number
}
