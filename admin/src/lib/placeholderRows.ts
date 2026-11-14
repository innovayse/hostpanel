/**
 * The rows of a list that keeps its shape while it loads: the real rows, or a fixed number of `null` placeholders
 * the template draws as bars of the same row.
 */

/**
 * Picks what a list draws.
 *
 * @param rows - The rows once they have arrived.
 * @param loading - True until the first read has answered; the rows are not drawn before.
 * @param count - How many placeholders stand in for the rows while loading.
 * @returns `count` nulls while loading, else the rows themselves.
 */
export const placeholderRows = <T>(rows: readonly T[], loading: boolean, count: number): ReadonlyArray<T | null> =>
  (loading ? Array.from({ length: count }, (): null => null) : rows)
