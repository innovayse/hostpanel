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

/** Closed set of shapes a drawer takes: where it sits and how it moves. */
export const UiDrawerPresentation = {
  /** A panel attached to a page edge (`UiDrawerSide`), for a screen wide enough to leave the page visible beside it. */
  Side: 'side',
  /** A bottom sheet: full width along the bottom edge, with a grip, the safe-area inset and the touch gestures of {@link useSheetDrag}. */
  Sheet: 'sheet',
} as const

/** One of the UiDrawerPresentation values. */
export type UiDrawerPresentation = (typeof UiDrawerPresentation)[keyof typeof UiDrawerPresentation]
