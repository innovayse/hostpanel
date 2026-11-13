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
