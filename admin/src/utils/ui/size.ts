/**
 * Control sizes shared by the kit.
 *
 * A closed set declared once (const object + derived union, so templates may pass `"sm"`),
 * with the literal class strings each control reads for its size.
 */

/** Closed set of control sizes. */
export const UiSize = {
  Sm: 'sm',
  Md: 'md',
  Lg: 'lg',
} as const

/** One of the control sizes. */
export type UiSize = (typeof UiSize)[keyof typeof UiSize]

/**
 * Height scale of the kit, taken from the approved concept: 32px (sm, the segmented-toggle row), 42px (md, the search field and
 * the primary button) and 48px (lg). Fields, buttons and icon buttons share it, so a toolbar of mixed controls lines up.
 */

/** Height, side padding and type size of a single-line text field per size. */
export const fieldSizeClasses: Record<UiSize, string> = {
  sm: 'h-8 px-3 text-[0.8125rem]',
  md: 'h-10.5 px-3.5 text-sm',
  lg: 'h-12 px-4 text-[0.9375rem]',
}

/** Padding and type size of a multi-line field per size: the height follows the rows, so it takes vertical padding, not a height. */
export const textareaSizeClasses: Record<UiSize, string> = {
  sm: 'px-3 py-1.5 text-[0.8125rem]',
  md: 'px-3.5 py-2.5 text-sm',
  lg: 'px-4 py-3 text-[0.9375rem]',
}

/** Corner radius of a button-like control per size (12px, the concept's control radius; 9px for the small segment); the one place a size sets a radius (a look may replace it, see `buttonRadiusClasses`). */
export const controlRadiusClasses: Record<UiSize, string> = {
  sm: 'rounded-[9px]',
  md: 'rounded-control',
  lg: 'rounded-control',
}

/** Height, side padding and type size of a button per size. */
export const buttonSizeClasses: Record<UiSize, string> = {
  sm: 'h-8 px-3.5 text-[0.8125rem]',
  md: 'h-10.5 px-[18px] text-sm',
  lg: 'h-12 px-6 text-[0.9375rem]',
}

/**
 * Type size of a text-link look per size: a link sits inline with its text, so it keeps the
 * button's type scale but carries no padding (padding would push it away from its neighbours).
 */
export const buttonLinkSizeClasses: Record<UiSize, string> = {
  sm: 'text-[0.8125rem]',
  md: 'text-sm',
  lg: 'text-[0.9375rem]',
}

/** Square footprint of an icon-only button per size. */
export const iconButtonSizeClasses: Record<UiSize, string> = {
  sm: 'w-8 h-8',
  md: 'w-10.5 h-10.5',
  lg: 'w-12 h-12',
}

/** Icon edge length per size. */
export const iconSizeClasses: Record<UiSize, string> = {
  sm: 'w-3.5 h-3.5',
  md: 'w-4 h-4',
  lg: 'w-5 h-5',
}
