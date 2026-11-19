/**
 * The one rule that turns every popup of the kit into a bottom sheet on a phone: below the `sm` breakpoint a
 * floating surface (menu, select, picker, notifications, a form dialog) is a `UiDrawer` sheet, from there up it keeps
 * its popover, dialog or side panel. Components ask for the shape here instead of reading the breakpoint themselves.
 */
import { computed } from 'vue'
import type { ComputedRef } from 'vue'
import { useMediaQuery } from '@/composables/useMediaQuery'
import { UI_BELOW_SM_QUERY } from '@/utils/ui/breakpoints'
import { UiDrawerPresentation } from '@/utils/ui/drawer'

/**
 * Follows the screen and answers which shape a drawer-capable surface takes right now.
 *
 * @returns A computed that is `Sheet` on a phone (below `sm`) and `Side` otherwise.
 */
export const useDrawerPresentation = (): ComputedRef<UiDrawerPresentation> => {
  /** Whether the screen is a phone. */
  const phone = useMediaQuery(UI_BELOW_SM_QUERY)

  return computed<UiDrawerPresentation>(() => (phone.value ? UiDrawerPresentation.Sheet : UiDrawerPresentation.Side))
}
