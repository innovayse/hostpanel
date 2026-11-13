/**
 * Shared vocabulary of the actions menu, used by `UiMenu` and `UiRowActions`: the shape of one action and the
 * helpers that decide how an action is drawn (link or button, tone).
 */
import type { RouteLocationRaw } from 'vue-router'

/** One item in the actions menu. */
export interface UiMenuAction {
  /** Visible label and accessible name of the action. */
  label: string
  /** Called when the user activates the action; optional for a link action, whose navigation is the `to` route. */
  onSelect?: () => void
  /** Route the action opens. When set (and the action is enabled) the item renders as a RouterLink anchor. */
  to?: RouteLocationRaw
  /** Inner SVG markup of the action's icon (an `Icon` entry); shown beside the label in the menu and alone for a single action. */
  icon?: string
  /** Styles the item as a destructive (red) action. */
  danger?: boolean
  /** Prevents the item from being selected; it is shown faded. */
  disabled?: boolean
  /** `data-testid` forwarded to the menu item button. */
  testid?: string
  /**
   * Makes the item one choice of a group: `true` for the current choice (a check mark is drawn), `false` for the
   * others. Left unset for a plain action.
   */
  checked?: boolean
}

/**
 * Whether an action renders as a link: it has a route and is not disabled (a disabled link cannot be inert).
 *
 * @param action - The action to check.
 * @returns True when the action is an enabled link.
 */
export const isLink = (action: UiMenuAction): action is UiMenuAction & { to: RouteLocationRaw } => {
  return action.to !== undefined && !action.disabled
}

/**
 * Text colour of an action: the danger tone for a destructive one.
 *
 * @param action - The action to colour.
 * @returns The colour class.
 */
export const toneClasses = (action: UiMenuAction): string => (action.danger ? 'text-status-red' : 'text-text-primary')

/**
 * Attributes that differ between the two elements an action can be: a link takes its route, a button its type and
 * disabled state (a disabled link cannot be inert, so a disabled action is always a button).
 *
 * @param action - The action to draw.
 * @returns The attributes for the element chosen by {@link isLink}.
 */
export const elementAttrs = (action: UiMenuAction): Record<string, unknown> =>
  isLink(action) ? { to: action.to } : { type: 'button', disabled: action.disabled }

/**
 * ARIA role of an action's row: a choice in a group (`checked` is set) is a radio item, any other action a plain item.
 *
 * @param action - The action to draw.
 * @returns `menuitemradio` or `menuitem`.
 */
export const itemRole = (action: UiMenuAction): 'menuitem' | 'menuitemradio' =>
  action.checked === undefined ? 'menuitem' : 'menuitemradio'
