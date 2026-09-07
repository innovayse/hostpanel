/** Generic paginated response wrapper. */
export interface PagedResult<T> {
  /** Data items for the current page. */
  items: T[]
  /** Total number of items across all pages. */
  totalCount: number
  /** Current page number (1-based). */
  page: number
  /** Number of items per page. */
  pageSize: number
}
