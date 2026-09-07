/**
 * A knowledge-base article and the category it belongs to.
 */

/** DTO representing a knowledge base category. */
export interface KbCategory {
  /** Category primary key. */
  id: number
  /** Category display name. */
  name: string
  /** Category description. */
  description: string
  /** Whether hidden from clients. */
  isHidden: boolean
  /** Parent category ID, or null if top-level. */
  parentCategoryId: number | null
  /** Number of articles in this category. */
  articleCount: number
}

/** DTO representing a knowledge base article. */
export interface KbArticle {
  /** Article primary key. */
  id: number
  /** Article title. */
  title: string
  /** Article body content. */
  content: string
  /** Category name. */
  category: string
  /** Whether published and visible to clients. */
  isPublished: boolean
}
