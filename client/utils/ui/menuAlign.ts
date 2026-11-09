/**
 * Horizontal anchoring of the kit's floating menus (UiPopover, UiLanguageSwitcher,
 * UiCurrencySwitcher).
 *
 * A menu hangs from its trigger, never from the viewport: the trigger's wrapper is `relative`
 * and the menu is `absolute` against it, so the menu follows the trigger wherever the header
 * places it. `End` lines the menu's right edge up with the trigger's right edge, which keeps a
 * menu opened from the right end of a header inside the header. Every menu is also capped to the
 * viewport width less a 1rem gutter on each side, so a wide menu never runs off a phone screen.
 *
 * @module utils/ui/menuAlign
 */

import { UiMenuAlign } from '~/utils/ui/enums'

/** Caps a menu to the viewport width less a 1rem gutter on each side. */
export const MENU_VIEWPORT_CAP = 'max-w-[calc(100vw-2rem)]'

/** Edge utility per alignment (literal classes, so Tailwind emits them). */
const EDGE_CLASSES: Record<UiMenuAlign, string> = {
  [UiMenuAlign.Start]: 'left-0',
  [UiMenuAlign.End]: 'right-0',
}

/**
 * Classes that anchor a menu to one edge of its `relative` trigger wrapper and cap its width.
 *
 * @param align Edge of the trigger the menu lines up with.
 * @returns The `absolute`, edge and width-cap utilities.
 */
export const menuAlignClasses = (align: `${UiMenuAlign}`): string =>
  `absolute ${EDGE_CLASSES[align as UiMenuAlign]} ${MENU_VIEWPORT_CAP}`

/** Gutter, in pixels, a menu keeps from either viewport edge (matches `MENU_VIEWPORT_CAP`). */
export const MENU_VIEWPORT_GUTTER = 16

/**
 * Picks the edge a menu actually opens from: the preferred one when the menu fits on screen that
 * way, otherwise the other one when that fits, otherwise the preferred one (the width cap then
 * keeps the overflow small). A trigger at the left of a wrapped header row can so open a wide
 * menu rightwards instead of pushing it past the left edge of a phone screen.
 *
 * @param preferred Edge the screen asked for.
 * @param triggerLeft Left edge of the trigger wrapper, in viewport pixels.
 * @param triggerRight Right edge of the trigger wrapper, in viewport pixels.
 * @param menuWidth Rendered width of the menu, in pixels.
 * @param viewportWidth Width of the viewport, in pixels.
 * @returns The edge to anchor the menu to.
 */
export const resolveMenuAlign = (
  preferred: `${UiMenuAlign}`,
  triggerLeft: number,
  triggerRight: number,
  menuWidth: number,
  viewportWidth: number,
): UiMenuAlign => {
  const fitsEnd = triggerRight - menuWidth >= MENU_VIEWPORT_GUTTER
  const fitsStart = triggerLeft + menuWidth <= viewportWidth - MENU_VIEWPORT_GUTTER
  if (preferred === UiMenuAlign.End) {
    return fitsEnd || !fitsStart ? UiMenuAlign.End : UiMenuAlign.Start
  }
  return fitsStart || !fitsEnd ? UiMenuAlign.Start : UiMenuAlign.End
}
