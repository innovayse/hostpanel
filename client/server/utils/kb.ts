/**
 * Shapes and mapping for the knowledgebase, shared by the three BFF routes under
 * `server/api/portal/knowledgebase/`.
 *
 * The C# backend answers `KbArticleDto` -- `{ id, title, content, category, isPublished }` --
 * while the public pages read the WHMCS-shaped `KbArticle` (`body`, `excerpt`). One mapping
 * lives here rather than three copies in the routes, so a field added on either side is
 * changed once.
 *
 * @module server/utils/kb
 */

import type { KbArticle } from '~/types/kbarticle'
import type { KbCategory } from '~/types/kbcategory'

/** How many characters of plain text a list row's excerpt carries. */
const EXCERPT_LENGTH = 140

/** One published article exactly as `GET /api/knowledgebase` returns it. */
export interface BackendKbArticle {
  /** Article primary key. */
  id: number
  /** Article title. */
  title: string
  /** Full article body as HTML. */
  content: string
  /** Category name -- categories have no id of their own in this backend. */
  category: string
  /** Whether the article is visible to clients. The public list returns only `true`. */
  isPublished: boolean
}

/**
 * Reduces an article body to the plain-text lead-in a list row shows.
 *
 * The body is HTML, and a row renders its excerpt as text, so tags are stripped rather than
 * escaped -- an excerpt that kept its markup would print the tags at the reader.
 *
 * @param html - The article body.
 * @returns Up to {@link EXCERPT_LENGTH} characters of collapsed plain text.
 */
const toExcerpt = (html: string): string =>
  (html ?? '')
    .replace(/<[^>]*>/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .slice(0, EXCERPT_LENGTH)

/**
 * Maps one backend article onto the shape the knowledgebase pages render.
 *
 * `views` and `dateupdated` are deliberately absent: this backend records neither, and the
 * pages hide both when they are missing. Filling them with zeroes or today's date would put
 * a number on screen that means nothing.
 *
 * @param article - The article as the backend returned it.
 * @returns The article in the page's shape, body included.
 */
export const toKbArticle = (article: BackendKbArticle): KbArticle => ({
  id: article.id,
  title: article.title,
  excerpt: toExcerpt(article.content),
  body: article.content
})

/**
 * Derives the category list from the published articles.
 *
 * There is no category endpoint on the public API and no category id anywhere in the payload:
 * an article names its category as a string. So a category *is* its name here, and the name
 * doubles as the id the category page routes on. A category holding no published article does
 * not appear, which is the intended answer for a public listing -- it would link to an empty
 * page.
 *
 * @param articles - Every published article.
 * @returns One entry per distinct category, alphabetical, with its article count.
 */
export const toKbCategories = (articles: BackendKbArticle[]): KbCategory[] => {
  const counts = new Map<string, number>()

  for (const article of articles) {
    const name = (article.category ?? '').trim()
    if (!name) continue
    counts.set(name, (counts.get(name) ?? 0) + 1)
  }

  return [...counts.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([name, count]) => ({ id: name, name, articlecount: count }))
}
