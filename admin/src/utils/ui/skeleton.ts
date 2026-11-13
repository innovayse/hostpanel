/**
 * Placeholder vocabulary for `UiSkeleton`.
 */

/** Closed set of placeholder shapes: three single shapes and two presets that mimic a whole area (a form is drawn with its real fields, see `UiField`). */
export const UiSkeletonShape = {
  /** The `text` option. */
  Text: 'text',
  /** The `block` option. */
  Block: 'block',
  /** The `circle` option. */
  Circle: 'circle',
  /** Preset: a header row and body rows of cells, like a data table. */
  Table: 'table',
  /** Preset: a responsive grid of cards, each with an avatar and a few lines. */
  Cards: 'cards',
} as const

/** One of the UiSkeletonShape values. */
export type UiSkeletonShape = (typeof UiSkeletonShape)[keyof typeof UiSkeletonShape]

/** The presets, which draw a layout of several placeholders instead of one. */
export const skeletonPresets: readonly UiSkeletonShape[] = [UiSkeletonShape.Table, UiSkeletonShape.Cards]