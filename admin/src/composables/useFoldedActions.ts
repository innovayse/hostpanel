/**
 * Splits the actions of a bar into the buttons it draws and the rows of its "…" menu.
 *
 * On a wide screen every action is a button. On a phone (below `sm`) only the primary action stays a button and the
 * others fold into one menu, so the bar never outgrows the screen. The floating bulk bar and the toolbars use this
 * implementation; the page header collapses all of its actions instead ({@link useCollapsedActions}).
 */
import { computed } from 'vue'
import type { ComputedRef } from 'vue'
import { useMediaQuery } from '@/composables/useMediaQuery'
import { primaryIndex } from '@/utils/ui/actionBar'
import type { UiBarAction } from '@/utils/ui/actionBar'
import { UI_BELOW_SM_QUERY } from '@/utils/ui/breakpoints'
import type { UiMenuAction } from '@/utils/ui/menu'

/** What {@link useFoldedActions} returns. */
export interface FoldedActions<T extends UiBarAction> {
  /** The actions drawn as buttons: all of them, or only the primary one on a phone. */
  buttons: ComputedRef<T[]>
  /** The menu rows of the folded actions on a phone, in order; none on a wider screen. */
  folded: ComputedRef<UiMenuAction[]>
}

/**
 * Splits a bar's actions for the current screen width.
 *
 * @param actions - Reads the bar's actions, in order.
 * @param toMenu - Builds the menu row of one folded action (its label, icon and what it does).
 * @returns The buttons to draw and the menu rows.
 */
export const useFoldedActions = <T extends UiBarAction>(actions: () => T[], toMenu: (action: T) => UiMenuAction): FoldedActions<T> => {
  /** Whether the screen is a phone, where all but the primary action fold into the menu. */
  const isPhone = useMediaQuery(UI_BELOW_SM_QUERY)
  /** Index of the action that stays a button on a phone. */
  const keep = computed<number>(() => primaryIndex(actions()))
  /** The actions drawn as buttons. */
  const buttons = computed<T[]>(() => (isPhone.value ? actions().filter((_, index) => index === keep.value) : actions()))
  /** The menu rows of the folded actions. */
  const folded = computed<UiMenuAction[]>(() => (isPhone.value ? actions().filter((_, index) => index !== keep.value).map(toMenu) : []))
  return { buttons, folded }
}
