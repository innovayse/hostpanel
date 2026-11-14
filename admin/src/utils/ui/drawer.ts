/**
 * Drawer vocabulary for `UiDrawer`.
 */
import { UI_BELOW_LG_QUERY, UI_BELOW_SM_QUERY } from '@/utils/ui/breakpoints'

/** Closed set of edges the drawer attaches to. */
export const UiDrawerSide = {
  /** The `left` option. */
  Left: 'left',
  /** The `right` option. */
  Right: 'right',
} as const

/** One of the UiDrawerSide values. */
export type UiDrawerSide = (typeof UiDrawerSide)[keyof typeof UiDrawerSide]

/** Closed set of breakpoints under which a sheet-capable drawer is a bottom sheet. */
export const UiDrawerSheetBelow = {
  /** A phone: below `sm` (the filters and the ticket details). */
  Sm: 'sm',
  /** Anything below `lg` (the navigation menu, which the sidebar replaces from `lg` up). */
  Lg: 'lg',
} as const

/** One of the UiDrawerSheetBelow values. */
export type UiDrawerSheetBelow = (typeof UiDrawerSheetBelow)[keyof typeof UiDrawerSheetBelow]

/** The media query of each sheet breakpoint (the width of the matching Tailwind `max-` variant, so classes and script agree). */
export const sheetQueries: Record<UiDrawerSheetBelow, string> = {
  [UiDrawerSheetBelow.Sm]: UI_BELOW_SM_QUERY,
  [UiDrawerSheetBelow.Lg]: UI_BELOW_LG_QUERY,
}
