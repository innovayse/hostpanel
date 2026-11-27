/**
 * Finding the link an event came from: the one place that reads "did this click land on an anchor", shared by the
 * delegated link prefetch and by the rows that open their record on a click.
 */

/**
 * The link element an event landed on, or the link around the element it landed on.
 *
 * @param target - The event's target.
 * @returns The closest `<a href>` at or above the target, null when the target is not inside a link.
 */
export const anchorOf = (target: EventTarget | null): HTMLAnchorElement | null => {
  if (!(target instanceof Element)) {
    return null
  }
  const anchor = target.closest('a[href]')
  return anchor instanceof HTMLAnchorElement ? anchor : null
}
