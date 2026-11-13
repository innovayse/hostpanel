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

/**
 * Gap classes for a flex/grid container. Each also publishes its gap as `--stack-gap`, which `UiPageHeader` reads
 * to subtract the gap its parent already puts under it, so the header-to-content distance is the same on every screen.
 */
export const gapClasses: Record<UiGap, string> = {
  none: 'gap-0 [--stack-gap:0rem]',
  xs: 'gap-1 [--stack-gap:0.25rem]',
  sm: 'gap-2 [--stack-gap:0.5rem]',
  md: 'gap-4 [--stack-gap:1rem]',
  lg: 'gap-6 [--stack-gap:1.5rem]',
  xl: 'gap-8 [--stack-gap:2rem]',
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

/** Closed set of breakpoints a stack can be shown or hidden at. */
export const UiBreakpoint = {
  Sm: 'sm',
  Md: 'md',
  Lg: 'lg',
} as const

/** One of the breakpoints. */
export type UiBreakpoint = (typeof UiBreakpoint)[keyof typeof UiBreakpoint]

/**
 * Display classes of a stack shown only from a breakpoint up (`UiStack`'s `visibleFrom`). They replace the
 * stack's own `flex`, because two display utilities on one element are decided by stylesheet order, not by
 * which one was written last.
 */
export const visibleFromClasses: Record<UiBreakpoint, string> = {
  sm: 'hidden sm:flex',
  md: 'hidden md:flex',
  lg: 'hidden lg:flex',
}

/** Display classes of a stack shown only below a breakpoint (`UiStack`'s `hiddenFrom`). */
export const hiddenFromClasses: Record<UiBreakpoint, string> = {
  sm: 'flex sm:hidden',
  md: 'flex md:hidden',
  lg: 'flex lg:hidden',
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

/** Closed set of minimum card widths for a grid that fills its row with as many columns as fit. */
export const UiGridMinWidth = {
  Sm: 'sm',
  Md: 'md',
  Lg: 'lg',
} as const

/** One of the minimum card widths (14rem, 18rem, 24rem). */
export type UiGridMinWidth = (typeof UiGridMinWidth)[keyof typeof UiGridMinWidth]

/** Auto-fill column classes per minimum width: one column on phones, then as many columns as fit. */
export const gridMinWidthClasses: Record<UiGridMinWidth, string> = {
  sm: 'grid-cols-1 sm:grid-cols-[repeat(auto-fill,minmax(14rem,1fr))]',
  md: 'grid-cols-1 sm:grid-cols-[repeat(auto-fill,minmax(18rem,1fr))]',
  lg: 'grid-cols-1 sm:grid-cols-[repeat(auto-fill,minmax(24rem,1fr))]',
}

/** Closed set of split ratios (main : aside). */
export const UiSplitRatio = {
  Half: 'half',
  TwoThirds: 'two-thirds',
  ThreeQuarters: 'three-quarters',
  /** Main 1.7 : aside 1, as the dashboard concept draws the chart beside the list. */
  WideMain: 'wide-main',
  /** Main 1 : aside 1.7, the mirror of `WideMain`. */
  WideAside: 'wide-aside',
} as const

/** One of the split ratios. */
export type UiSplitRatio = (typeof UiSplitRatio)[keyof typeof UiSplitRatio]

/**
 * Media query under which a split drops its columns: the width of Tailwind's `lg` breakpoint, where the split
 * classes switch to two columns, so the classes and the aside-to-sheet mode of `UiSplit` change at the same width.
 */
export const UI_SPLIT_COMPACT_QUERY = '(max-width: 1023.98px)'

/** Column template per ratio, one column on narrow screens. */
export const splitRatioClasses: Record<UiSplitRatio, string> = {
  half: 'grid-cols-1 lg:grid-cols-2',
  'two-thirds': 'grid-cols-1 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]',
  'three-quarters': 'grid-cols-1 lg:grid-cols-[minmax(0,3fr)_minmax(0,1fr)]',
  'wide-main': 'grid-cols-1 lg:grid-cols-[minmax(0,1.7fr)_minmax(0,1fr)]',
  'wide-aside': 'grid-cols-1 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.7fr)]',
}
