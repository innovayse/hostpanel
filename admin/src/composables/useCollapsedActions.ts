/**
 * Splits the actions of a page header into the buttons it draws and the rows of the one menu they collapse into.
 *
 * From `md` up every action is a button. Below `md` (a phone) no action is a button: they all become rows of one menu,
 * the primary action first, behind one compact trigger. The trigger is a plus when there is a primary action (the
 * create-like one) and the overflow icon otherwise. The floating bulk bar keeps its own rule, {@link useFoldedActions}.
 */
import { computed } from 'vue'
import type { ComputedRef } from 'vue'
import { useMediaQuery } from '@/composables/useMediaQuery'
import { primaryIndex } from '@/utils/ui/actionBar'
import type { UiBarAction } from '@/utils/ui/actionBar'
import { UI_BELOW_MD_QUERY } from '@/utils/ui/breakpoints'
import { UiButtonVariant } from '@/utils/ui/button'
import type { UiMenuAction } from '@/utils/ui/menu'

/** What {@link useCollapsedActions} returns. */
export interface CollapsedActions<T extends UiBarAction> {
  /** The actions drawn as buttons: all of them from `md` up, none below it. */
  buttons: ComputedRef<T[]>
  /** The menu rows below `md`: every action, the primary one first; none from `md` up. */
  rows: ComputedRef<UiMenuAction[]>
  /** Whether the menu trigger is a plus: there is an action in the primary look. */
  hasPrimary: ComputedRef<boolean>
}

/**
 * Splits a header's actions for the current screen width.
 *
 * @param actions - Reads the header's actions, in order.
 * @param toMenu - Builds the menu row of one action (its label, icon and what it does).
 * @returns The buttons to draw, the menu rows and whether the trigger is a plus.
 */
export const useCollapsedActions = <T extends UiBarAction>(actions: () => T[], toMenu: (action: T) => UiMenuAction): CollapsedActions<T> => {
  /** Whether the screen is below `md`, where every action collapses into the menu. */
  const isCollapsed = useMediaQuery(UI_BELOW_MD_QUERY)
  /** The buttons: every action while the screen is wide enough. */
  const buttons = computed<T[]>(() => (isCollapsed.value ? [] : actions()))
  /** The menu rows: the primary action first, the others in their order. */
  const rows = computed<UiMenuAction[]>(() => {
    if (!isCollapsed.value) {
      return []
    }
    const all = actions()
    const first = primaryIndex(all)
    return all.filter((_, index) => index === first).concat(all.filter((_, index) => index !== first)).map(toMenu)
  })
  /** Whether some action is in the primary look. */
  const hasPrimary = computed<boolean>(() => actions().some((action) => action.variant === UiButtonVariant.Primary))
  return { buttons, rows, hasPrimary }
}
