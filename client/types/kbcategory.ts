/** One knowledgebase category, as `GET /api/portal/knowledgebase/categories` lists it. */
export interface KbCategory {
  /** Category id, used as the category-page route parameter. */
  id: string | number
  /** Category display name. */
  name: string
  /** How many articles the category holds — shown beside the name. */
  articlecount?: number
}
