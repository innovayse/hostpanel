/**
 * Shared vocabulary of the bars that show their actions as buttons on a wide screen and move some or all of them into a menu
 * on a phone: the floating bulk bar (`UiBulkBar`) and the page header (`UiPageHeader`).
 */
import type { RouteLocationRaw } from 'vue-router'
import type { UiMenuAction } from '@/utils/ui/menu'
import { UiButtonVariant } from '@/utils/ui/button'

/** The part of an action that both bars draw the same way: a labelled icon button that can move into the menu. */
export interface UiBarAction {
  /** The label drawn on the button and in the menu (already translated). */
  label: string
  /** Longer sentence (already translated): the tooltip of the button and the name of the menu row; the label when left out. */
  title?: string
  /** Inner SVG markup of the icon (an `Icon` entry or `actionIcons[kind]`); every bar action has one. */
  icon: string
  /** Look of the button; the primary action of the bar is the one in the primary look. */
  variant: UiButtonVariant
  /** Blocks the action. */
  disabled?: boolean
  /** Shows progress on the button and blocks the action (the menu row of a folded action is disabled meanwhile) while something runs. */
  loading?: boolean
  /** `data-testid` of the button (and of the menu row on a phone). */
  testid: string
}

/**
 * Which action stays a button on a phone: the first one in the primary look, else the first one.
 *
 * @param actions - The actions of the bar, in order.
 * @returns Index of the action that stays; -1 when there are none.
 */
export const primaryIndex = (actions: readonly UiBarAction[]): number => {
  const primary = actions.findIndex((action) => action.variant === UiButtonVariant.Primary)
  return primary >= 0 ? primary : actions.length > 0 ? 0 : -1
}

/**
 * The menu row of a folded action, without what it does: the caller adds its `onSelect` or `to`.
 *
 * @param action - The action that folds into the menu.
 * @returns The menu row's label (the full sentence), icon, tone, disabled state (also while the action is loading) and test id.
 */
export const barActionToMenu = (action: UiBarAction): Pick<UiMenuAction, 'label' | 'icon' | 'danger' | 'disabled' | 'testid'> => ({
  label: action.title ?? action.label,
  icon: action.icon,
  danger: action.variant === UiButtonVariant.Danger,
  disabled: action.disabled === true || action.loading === true,
  testid: action.testid,
})

/** One action of a page header (`UiPageHeader`): a labelled icon button that runs a handler or opens a route. */
export interface UiHeaderAction extends UiBarAction {
  /** Route the action opens; the action is then a link. Without it the action is a button that calls `onSelect`. */
  to?: RouteLocationRaw
  /** Called when the button is pressed; a link action needs none. */
  onSelect?: () => void
}
