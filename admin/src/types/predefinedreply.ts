/**
 * A canned support reply and the category it files under.
 */

/** DTO representing a predefined reply category. */
export interface PredefinedReplyCategory {
  /** Category primary key. */
  id: number
  /** Category display name. */
  name: string
  /** Parent category ID, or null if top-level. */
  parentCategoryId: number | null
  /** Number of replies in this category. */
  replyCount: number
}

/** DTO representing a predefined reply. */
export interface PredefinedReply {
  /** Reply primary key. */
  id: number
  /** Reply display name. */
  name: string
  /** Reply content body. */
  content: string
  /** FK to the category. */
  categoryId: number
  /** Category name, if resolved. */
  categoryName: string | null
}
