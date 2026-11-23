/**
 * Vocabulary of `UiSvgMap` (an interactive map drawn from outlines) and `UiLegendSwatch` (a key of its fills).
 */

/** One outline of a map. */
export interface UiMapShape {
  /** Stable id of the outline, which keys it and is what the map's events carry. */
  id: string
  /** The SVG path data of the outline. */
  path: string
  /** Tailwind fill class (a literal, so it emits CSS), e.g. `fill-border`. */
  fill: string
  /** Name read to assistive technology and shown as the tooltip text; an outline without one is decoration. */
  label?: string
  /** `data-testid` of the outline. */
  testid?: string
}

/**
 * Focus look of a labelled outline. The kit's `focusRingClasses` draw a box shadow, which an SVG path does not render,
 * so keyboard focus is drawn as the same accent colour on the outline's stroke, thickened to stay visible on a fill.
 */
export const mapShapeFocusClasses = 'outline-none focus-visible:stroke-text-accent focus-visible:[stroke-width:1.2]'

/** Closed set of the shapes a legend swatch takes. */
export const UiLegendSwatchShape = {
  /** A small rounded square, one entry of a key. */
  Square: 'square',
  /** A wide step of a scale; the first and the last of a row are rounded on the outer edge. */
  Step: 'step',
} as const

/** One of the swatch shapes. */
export type UiLegendSwatchShape = (typeof UiLegendSwatchShape)[keyof typeof UiLegendSwatchShape]

/** Classes of each swatch shape. */
export const legendSwatchClasses: Record<UiLegendSwatchShape, string> = {
  square: 'size-3 rounded-sm',
  step: 'h-3 w-5 first:rounded-l-sm last:rounded-r-sm',
}
