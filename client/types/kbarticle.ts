/**
 * One knowledgebase article.
 *
 * The list endpoints fill in the summary fields only; {@link KbArticle.body} and
 * {@link KbArticle.dateupdated} arrive from the single-article endpoint.
 */
export interface KbArticle {
  /** Article id, used as the article-page route parameter. */
  id: string | number
  /** Article title. */
  title: string
  /** Short plain-text lead-in shown in list rows. */
  excerpt?: string
  /** View counter, used to order the "popular" list. */
  views?: number
  /** Full body as HTML — rendered with `v-html`, so the backend owns its sanitisation. */
  body?: string
  /** When the article was last edited, already formatted by the backend for display. */
  dateupdated?: string
}
