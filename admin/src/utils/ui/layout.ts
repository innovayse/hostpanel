/**
 * Layout primitives' vocabulary: gaps, column counts, alignment and split ratios, each mapped to
 * literal Tailwind classes (an interpolated class would emit no CSS).
 */

/** Closed set of gaps between children. */
export const UiGap = {
  None: 'none',
  Xs: 'xs',
  Sm: 'sm',
  Md: 'md',
  Lg: 'lg',
  Xl: 'xl',
} as const

/** One of the gaps. */
export type UiGap = (typeof UiGap)[keyof typeof UiGap]

/** Closed set of flex directions. */
export const UiDirection = {
  Column: 'column',
  Row: 'row',
} as const

/** One of the directions. */
export type UiDirection = (typeof UiDirection)[keyof typeof UiDirection]

/** Gap classes for a flex/grid container. */
export const gapClasses: Record<UiGap, string> = {
  none: 'gap-0',
  xs: 'gap-1',
  sm: 'gap-2',
  md: 'gap-4',
  lg: 'gap-6',
  xl: 'gap-8',
}

/** Closed set of cross-axis alignments of a stack. */
export const UiAlignItems = {
  Start: 'start',
  Center: 'center',
  End: 'end',
  Stretch: 'stretch',
} as const

/** One of the cross-axis alignments. */
export type UiAlignItems = (typeof UiAlignItems)[keyof typeof UiAlignItems]

/** `align-items` classes. */
export const alignItemsClasses: Record<UiAlignItems, string> = {
  start: 'items-start',
  center: 'items-center',
  end: 'items-end',
  stretch: 'items-stretch',
}

/** Closed set of main-axis distributions of a stack. */
export const UiJustify = {
  Start: 'start',
  Center: 'center',
  End: 'end',
  Between: 'between',
} as const

/** One of the main-axis distributions. */
export type UiJustify = (typeof UiJustify)[keyof typeof UiJustify]

/** `justify-content` classes. */
export const justifyClasses: Record<UiJustify, string> = {
  start: 'justify-start',
  center: 'justify-center',
  end: 'justify-end',
  between: 'justify-between',
}

/**
 * Classes that turn a stack the other way below the medium breakpoint (`UiStack`'s `reflowBelowMd`). They are
 * `max-md` variants, so they win over the stack's own direction and alignment there and are inert from `md` up.
 */
export const reflowClasses: Record<UiDirection, string> = {
  row: 'max-md:flex-col max-md:items-stretch',
  column: 'max-md:flex-row max-md:flex-wrap max-md:items-center',
}

/** Column counts a grid supports. */
export type UiGridColumns = 1 | 2 | 3 | 4 | 6

/** Responsive column classes: one column on narrow screens, the full count from `lg`. */
export const gridColumnClasses: Record<UiGridColumns, string> = {
  1: 'grid-cols-1',
  2: 'grid-cols-1 md:grid-cols-2',
  3: 'grid-cols-1 md:grid-cols-2 lg:grid-cols-3',
  4: 'grid-cols-1 md:grid-cols-2 lg:grid-cols-4',
  6: 'grid-cols-2 md:grid-cols-3 lg:grid-cols-6',
}

/** Closed set of split ratios (main : aside). */
export const UiSplitRatio = {
  Half: 'half',
  TwoThirds: 'two-thirds',
  ThreeQuarters: 'three-quarters',
} as const

/** One of the split ratios. */
export type UiSplitRatio = (typeof UiSplitRatio)[keyof typeof UiSplitRatio]

/** Column template per ratio, one column on narrow screens. */
export const splitRatioClasses: Record<UiSplitRatio, string> = {
  half: 'grid-cols-1 lg:grid-cols-2',
  'two-thirds': 'grid-cols-1 lg:grid-cols-[2fr_1fr]',
  'three-quarters': 'grid-cols-1 lg:grid-cols-[3fr_1fr]',
}
