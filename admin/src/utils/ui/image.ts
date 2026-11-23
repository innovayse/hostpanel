/**
 * Image vocabulary for `UiImage` and `UiImagePreview`.
 */

/** Closed set of fits. */
export const UiImageFit = {
  /** The `cover` option. */
  Cover: 'cover',
  /** The `contain` option. */
  Contain: 'contain',
  /** The `none` option. */
  None: 'none',
} as const

/** One of the UiImageFit values. */
export type UiImageFit = (typeof UiImageFit)[keyof typeof UiImageFit]

/** Closed set of corner radii. */
export const UiImageRounded = {
  /** The `none` option. */
  None: 'none',
  /** The `md` option. */
  Md: 'md',
  /** The `lg` option. */
  Lg: 'lg',
  /** The `full` option. */
  Full: 'full',
} as const

/** One of the UiImageRounded values. */
export type UiImageRounded = (typeof UiImageRounded)[keyof typeof UiImageRounded]

/** Closed set of box sizes of a picture; `Auto` leaves the box to the caller's classes. */
export const UiImageSize = {
  /** The box is the caller's. */
  Auto: 'auto',
  /** A framed square thumbnail (40 by 40) for a list row. */
  Thumb: 'thumb',
  /** A framed wide thumbnail (40 by 64), for a logo or a cover in a list row. */
  ThumbWide: 'thumbWide',
} as const

/** One of the UiImageSize values. */
export type UiImageSize = (typeof UiImageSize)[keyof typeof UiImageSize]

/** Closed set of the stages a picture is previewed on (`UiImagePreview`). */
export const UiImageBackdrop = {
  /** A light checkerboard, where a dark picture reads. */
  Light: 'light',
  /** A dark checkerboard, where a light picture reads. */
  Dark: 'dark',
} as const

/** One of the UiImageBackdrop values. */
export type UiImageBackdrop = (typeof UiImageBackdrop)[keyof typeof UiImageBackdrop]
