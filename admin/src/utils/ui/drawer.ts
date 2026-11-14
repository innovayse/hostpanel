/**
 * Drawer vocabulary for `UiDrawer`.
 */

/** Closed set of edges the drawer attaches to. */
export const UiDrawerSide = {
  /** The `left` option. */
  Left: 'left',
  /** The `right` option. */
  Right: 'right',
} as const

/** One of the UiDrawerSide values. */
export type UiDrawerSide = (typeof UiDrawerSide)[keyof typeof UiDrawerSide]

/**
 * Media query under which a sheet-capable drawer is a bottom sheet instead of a side panel. It is the
 * width of Tailwind's `max-sm:` variant (the `sm` breakpoint is 640 px), so the classes and the surface a
 * script picks (`UiFilterTrigger`) change at the same width.
 */
export const UI_SHEET_QUERY = '(max-width: 639.98px)'

/**
 * Media query under which a drawer that asks for the `lg` breakpoint is a bottom sheet: the navigation menu,
 * whose button is shown below `lg`. It is the width of Tailwind's `max-lg:` variant (1024 px).
 */
export const UI_SHEET_LG_QUERY = '(max-width: 1023.98px)'

/** Closed set of breakpoints under which a sheet-capable drawer is a bottom sheet. */
export const UiDrawerSheetBelow = {
  /** A phone: below `sm` (the filters and the ticket details). */
  Sm: 'sm',
  /** Anything below `lg` (the navigation menu, which the sidebar replaces from `lg` up). */
  Lg: 'lg',
} as const

/** One of the UiDrawerSheetBelow values. */
export type UiDrawerSheetBelow = (typeof UiDrawerSheetBelow)[keyof typeof UiDrawerSheetBelow]

/** The media query of each sheet breakpoint. */
export const sheetQueries: Record<UiDrawerSheetBelow, string> = {
  [UiDrawerSheetBelow.Sm]: UI_SHEET_QUERY,
  [UiDrawerSheetBelow.Lg]: UI_SHEET_LG_QUERY,
}
