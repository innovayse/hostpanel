/**
 * Typography vocabulary for `UiText`, `UiHeading`, `UiLabel` and `UiMetric`.
 *
 * Each set is closed. `plain` adds no classes, so a screen that passes its own typography classes
 * keeps them.
 */
import type { UiSize } from '@/utils/ui/size'

/** Closed set of elements `UiText` can be drawn as. */
export const UiTextTag = {
  P: 'p',
  Span: 'span',
} as const

/** One of the text elements. */
export type UiTextTag = (typeof UiTextTag)[keyof typeof UiTextTag]

/** Closed set of text colours. */
export const UiTextTone = {
  Plain: 'plain',
  Primary: 'primary',
  Secondary: 'secondary',
  Muted: 'muted',
  Accent: 'accent',
  Success: 'success',
  Warning: 'warning',
  Danger: 'danger',
} as const

/** One of the text colours. */
export type UiTextTone = (typeof UiTextTone)[keyof typeof UiTextTone]

/** Text colour classes. */
export const textToneClasses: Record<UiTextTone, string> = {
  plain: '',
  primary: 'text-text-primary',
  secondary: 'text-text-secondary',
  muted: 'text-text-muted',
  accent: 'text-text-accent',
  success: 'text-status-green',
  warning: 'text-status-yellow',
  danger: 'text-status-red',
}

/** Closed set of text sizes. */
export const UiTextSize = {
  Plain: 'plain',
  Xs: 'xs',
  Sm: 'sm',
  Md: 'md',
  Lg: 'lg',
} as const

/** One of the text sizes. */
export type UiTextSize = (typeof UiTextSize)[keyof typeof UiTextSize]

/** Text size classes. */
export const textSizeClasses: Record<UiTextSize, string> = {
  plain: '',
  xs: 'text-xs',
  sm: 'text-[0.8125rem]',
  md: 'text-sm',
  lg: 'text-base',
}

/** Closed set of heading looks. */
export const UiHeadingVariant = {
  Plain: 'plain',
  Page: 'page',
  Section: 'section',
  Card: 'card',
} as const

/** One of the heading looks. */
export type UiHeadingVariant = (typeof UiHeadingVariant)[keyof typeof UiHeadingVariant]

/** Heading classes per look. */
export const headingVariantClasses: Record<UiHeadingVariant, string> = {
  plain: '',
  page: 'font-display text-[1.875rem] font-semibold text-text-primary leading-tight break-words',
  section: 'font-sans text-[1.0625rem] font-semibold text-text-primary break-words',
  card: 'font-sans text-base font-semibold text-text-primary break-words',
}

/** Closed set of label elements: the native `label` (ties to a control) or a `span` (a caption tied to none). */
export const UiLabelTag = {
  Label: 'label',
  Span: 'span',
} as const

/** One of the label elements. */
export type UiLabelTag = (typeof UiLabelTag)[keyof typeof UiLabelTag]

/** Closed set of label looks. */
export const UiLabelVariant = {
  Plain: 'plain',
  Field: 'field',
  Inline: 'inline',
} as const

/** One of the label looks. */
export type UiLabelVariant = (typeof UiLabelVariant)[keyof typeof UiLabelVariant]

/** Label classes per look: `field` is the caption above a control, `inline` wraps a checkbox or radio. */
export const labelVariantClasses: Record<UiLabelVariant, string> = {
  plain: '',
  field: 'block text-[0.8125rem] font-medium text-text-muted',
  inline: 'flex items-center gap-2 cursor-pointer text-sm text-text-secondary',
}

/**
 * Value and skeleton-bar classes of `UiMetric` per size. The value is the display font in tabular figures;
 * `md` is the 30px concept tile value. The bar matches the value's line so a loading metric keeps its height.
 */
export const metricSizeClasses: Record<UiSize, { value: string, bar: string }> = {
  sm: { value: 'font-display text-xl font-semibold leading-none tabular-nums', bar: 'h-5 w-16 rounded-md' },
  md: { value: 'font-display text-[1.875rem] font-semibold leading-none tabular-nums', bar: 'h-[30px] w-36 rounded-md' },
  lg: { value: 'font-display text-4xl font-semibold leading-none tabular-nums', bar: 'h-9 w-44 rounded-md' },
}
