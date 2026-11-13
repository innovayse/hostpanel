/**
 * Card padding vocabulary for `UiCard`.
 */

/** Closed set of inner paddings. */
export const UiCardPadding = {
  /** The `none` option. */
  None: 'none',
  /** The `sm` option. */
  Sm: 'sm',
  /** The `md` option. */
  Md: 'md',
  /** The `lg` option. */
  Lg: 'lg',
} as const

/** One of the UiCardPadding values. */
export type UiCardPadding = (typeof UiCardPadding)[keyof typeof UiCardPadding]

/** Closed set of corner radii. */
export const UiCardRadius = {
  /** The card radius, for cards inside a screen. */
  Card: 'card',
  /** The larger panel radius, for a card that is the screen's main surface. */
  Panel: 'panel',
} as const

/** One of the UiCardRadius values. */
export type UiCardRadius = (typeof UiCardRadius)[keyof typeof UiCardRadius]
