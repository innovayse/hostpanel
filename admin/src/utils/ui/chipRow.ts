/**
 * Arithmetic of `UiChipRow`: how many chips of a one-line row fit before the "+N" chip takes the rest.
 */

/**
 * Counts the chips that fit in one line next to the "+N" chip.
 *
 * Pure arithmetic over widths measured once, so a resize costs no layout read. When every chip fits there is no
 * "+N" chip and all of them stay; otherwise the largest count of leading chips is kept for which the chips, the gaps
 * and the "+N" chip still fit, which may be none.
 *
 * @param widths - Natural width in pixels of each chip, in order.
 * @param moreWidth - Width in pixels of the "+N" chip.
 * @param available - Width in pixels of the line.
 * @param gap - Space in pixels between two chips.
 * @returns How many leading chips to draw, from 0 to the number of chips.
 */
export const fitChipCount = (widths: readonly number[], moreWidth: number, available: number, gap: number): number => {
  const all = widths.reduce((sum, width) => sum + width, 0) + gap * Math.max(widths.length - 1, 0)
  if (all <= available) {
    return widths.length
  }
  for (let count = widths.length - 1; count > 0; count--) {
    const shown = widths.slice(0, count).reduce((sum, width) => sum + width, 0) + gap * (count - 1)
    if (shown + gap + moreWidth <= available) {
      return count
    }
  }
  return 0
}
