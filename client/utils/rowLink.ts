/**
 * Finding the control a row click came from: the one place that reads "did this click land on something that has its own
 * job", so a table row that opens its record on a click leaves links, buttons, menus and form fields alone.
 *
 * @module utils/rowLink
 */

/** What inside a row answers a click itself: links, buttons, form fields, labels and the menus and boxes drawn from roles. */
const OWN_CONTROL_SELECTOR = 'a[href], button, input, select, textarea, label, summary, [role="button"], [role="checkbox"], [role="menu"], [role="menuitem"], [role="link"]'

/**
 * Whether an event landed on a control inside the row, rather than on the row itself or its plain text.
 *
 * The row can carry `role="link"` itself, so the closest match is compared with the row: only a control between the target
 * and the row counts.
 *
 * @param target - The event's target.
 * @param row - The row element the listener sits on.
 * @returns True when the click belongs to a control inside the row.
 */
export const isOwnControlClick = (target: EventTarget | null, row: Element): boolean => {
  if (!(target instanceof Element)) {
    return false
  }
  const control = target.closest(OWN_CONTROL_SELECTOR)
  return control !== null && control !== row && row.contains(control)
}
