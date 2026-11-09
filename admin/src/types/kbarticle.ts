/**
 * A knowledge-base article and the category it belongs to.
 */

import type { StorefrontTranslations } from '@/types/storefront'

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
  /** The category's address segment; fixed once the category exists. */
  slug: string
}

/** The title and body of an article in one language. */
export interface KbArticleTranslation {
  /** Article title in this language. */
  title: string
  /** Article body (Markdown or HTML) in this language. */
  content: string
}

/** DTO representing a knowledge base article. */
export interface KbArticle {
  /** Article primary key. */
  id: number
  /** The article's address segment; fixed once the article exists, so renaming it keeps its links. */
  slug: string
  /** Id of the category the article belongs to. */
  categoryId: number
  /** Name of that category. */
  categoryName: string
  /** Whether published and visible to clients. */
  isPublished: boolean
  /** Title and body per language; a language the operator has not written is absent. */
  translations: StorefrontTranslations<KbArticleTranslation>
}

/** Body that creates or replaces a knowledge base category. */
export interface KbCategoryPayload {
  /** Category display name. */
  name: string
  /** Category description. */
  description: string
  /** Whether hidden from clients. */
  isHidden: boolean
}

/** Body that replaces the category and the translations of a knowledge base article. */
export interface KbArticleUpdatePayload {
  /** Id of the category the article belongs to. */
  categoryId: number
  /** Title and body per language; a language left out is removed, and English is required. */
  translations: StorefrontTranslations<KbArticleTranslation>
}

/** Body that creates a knowledge base article. */
export interface KbArticlePayload extends KbArticleUpdatePayload {
  /** Whether the article is visible to clients at once; false saves a draft. */
  isPublished: boolean
}
