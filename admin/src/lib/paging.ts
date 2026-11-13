/**
 * Page arithmetic shared by every paged list: how many pages a total makes, which slice of an
 * in-memory list a page shows, which page numbers a pager shows, and the first and last row number of a page for the
 * "showing x to y" line.
 */

/**
 * Counts the pages a list of `total` rows makes; an empty list still has one (empty) page.
 *
 * @param total - Number of rows across all pages.
 * @param pageSize - Rows per page.
 * @returns The page count, at least 1.
 */
export const pageCount = (total: number, pageSize: number): number => Math.max(1, Math.ceil(total / pageSize))

/**
 * Cuts one page out of an in-memory list.
 *
 * @param items - The whole list.
 * @param page - Page number, starting at 1.
 * @param pageSize - Rows per page.
 * @returns The rows of that page (fewer on the last page, none past it).
 */
export const pageSlice = <T>(items: readonly T[], page: number, pageSize: number): T[] =>
  items.slice((page - 1) * pageSize, page * pageSize)

/**
 * Number of the first row on a page, for the record count line.
 *
 * @param page - Page number, starting at 1.
 * @param pageSize - Rows per page.
 * @returns The 1-based row number.
 */
export const pageFirstRow = (page: number, pageSize: number): number => (page - 1) * pageSize + 1

/**
 * Number of the last row on a page, for the record count line.
 *
 * @param page - Page number, starting at 1.
 * @param pageSize - Rows per page.
 * @param total - Number of rows across all pages.
 * @returns The 1-based row number, never past `total`.
 */
export const pageLastRow = (page: number, pageSize: number, total: number): number => Math.min(page * pageSize, total)

/**
 * Chooses which page numbers a pager shows: the first and last page and the current page with one
 * neighbour on each side. A hidden run of two or more pages becomes one gap (`null`); a run of a
 * single page is shown as that page, because a gap marker would take the same room.
 *
 * @param current - The current page, starting at 1.
 * @param count - Number of pages, at least 1.
 * @returns Page numbers in ascending order with `null` marking each gap.
 */
export const pageWindow = (current: number, count: number): (number | null)[] => {
  const wanted = new Set<number>([1, count, current - 1, current, current + 1])
  const pages = [...wanted].filter(n => n >= 1 && n <= count).sort((a, b) => a - b)
  const result: (number | null)[] = []
  pages.forEach((n, index) => {
    const previous = pages[index - 1]
    if (previous !== undefined && n - previous === 2) {
      result.push(previous + 1)
    } else if (previous !== undefined && n - previous > 2) {
      result.push(null)
    }
    result.push(n)
  })
  return result
}

/**
 * A stable list key for one entry of a pager window: the page number, or `gap-start` / `gap-end` for
 * a gap before / after the current page (a window has at most one gap on each side).
 *
 * @param entries - The window from {@link pageWindow}.
 * @param index - Position of the entry in the window.
 * @param current - The current page.
 * @returns The page number as text, or the name of the gap.
 */
export const pageEntryKey = (entries: readonly (number | null)[], index: number, current: number): string => {
  const entry = entries[index]
  if (entry !== null && entry !== undefined) {
    return String(entry)
  }
  return index < entries.indexOf(current) ? 'gap-start' : 'gap-end'
}
