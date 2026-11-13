/**
 * Button looks shared by `UiButton`, `UiIconButton` and `UiLink`.
 *
 * `plain` adds no colours of its own, so the screen's classes decide everything. Every other look
 * is fully owned by the kit and reads the theme tokens only.
 */
import { focusRingClasses } from '@/utils/ui/input'
import { controlRadiusClasses } from '@/utils/ui/size'
import type { UiSize } from '@/utils/ui/size'

/** Closed set of button looks. */
export const UiButtonVariant = {
  Plain: 'plain',
  Primary: 'primary',
  Secondary: 'secondary',
  Ghost: 'ghost',
  Field: 'field',
  Danger: 'danger',
  Link: 'link',
} as const

/** One of the button looks. */
export type UiButtonVariant = (typeof UiButtonVariant)[keyof typeof UiButtonVariant]

/** Closed set of native button types. */
export const UiButtonType = {
  Button: 'button',
  Submit: 'submit',
  Reset: 'reset',
} as const

/** One of the native button types. */
export type UiButtonType = (typeof UiButtonType)[keyof typeof UiButtonType]

/**
 * Colour classes per look. `field` is a pill on the input-field surface (the search trigger of the top bar). `plain` is empty on purpose.
 *
 * Hover and the gradient apply only while the control is not disabled (`not-disabled:`, which also
 * matches a link), and each look has a disabled colour set of its own: a muted label on a flat
 * surface, so a disabled control reads as unavailable by contrast and shape, never by opacity alone.
 * The primary label is white (`text-on-primary`) as in the concept; the gradient tokens of both themes keep it at AA contrast, and hover and pressed swap in a deeper gradient token.
 * A disabled control draws its border or label in the muted text colour (3:1 or more on every surface).
 */
export const buttonVariantClasses: Record<UiButtonVariant, string> = {
  plain: '',
  primary:
    'not-disabled:gradient-brand not-disabled:shadow-glow-primary not-disabled:hover:bg-(image:--gradient-primary-hover) not-disabled:hover:shadow-glow-primary-hover not-disabled:active:bg-(image:--gradient-primary-active) not-disabled:active:shadow-glow-primary text-text-on-primary font-semibold disabled:bg-surface-hover disabled:shadow-[inset_0_0_0_1px_var(--text-muted)] disabled:text-text-muted',
  secondary:
    'bg-surface-field border border-border-control text-text-secondary font-medium not-disabled:hover:bg-surface-active not-disabled:hover:border-border-control-hover not-disabled:hover:text-text-primary not-disabled:active:bg-surface-active not-disabled:active:border-border-control-hover disabled:bg-transparent disabled:border-text-muted disabled:text-text-muted',
  ghost:
    'text-text-secondary not-disabled:hover:text-text-primary not-disabled:hover:bg-surface-hover not-disabled:active:bg-surface-active disabled:text-text-muted',
  field:
    'bg-surface-field text-text-secondary font-medium not-disabled:hover:bg-surface-hover not-disabled:hover:text-text-primary not-disabled:active:bg-surface-active disabled:text-text-muted',
  danger:
    'bg-tint-red border border-status-red/30 text-status-red font-medium not-disabled:hover:bg-status-red/20 not-disabled:hover:border-status-red/50 not-disabled:active:bg-status-red/30 disabled:bg-surface-hover disabled:border-border disabled:text-text-muted',
  link: 'text-text-accent underline-offset-2 not-disabled:hover:underline disabled:text-text-muted',
}

/** The ring a loading button shows beside its label: it takes the label's colour, and stands still under reduced motion. */
export const buttonSpinnerClasses = 'inline-block h-4 w-4 shrink-0 animate-spin rounded-full border-2 border-current border-t-transparent motion-reduce:animate-none'

/** Classes every non-plain look shares (focus ring, not-allowed cursor, motion). */
export const buttonBaseClasses = `inline-flex items-center justify-center gap-1.5 transition-all duration-200 cursor-pointer disabled:cursor-not-allowed ${focusRingClasses}`

/**
 * Look of the content of a menu trigger that is an icon (the top bar's theme and language pickers): the 42px footprint and the
 * ghost colours of `UiIconButton`, drawn on the span inside `UiMenu`'s trigger, whose `group` class carries the hover. It is
 * the one icon-trigger look of the top bar, so the search, theme, language and notification triggers read as one family.
 */
export const iconTriggerClasses =
  'inline-flex h-10.5 min-w-10.5 items-center justify-center rounded-control text-text-secondary transition-colors group-hover:bg-surface-hover group-hover:text-text-primary'

/**
 * Corner radius of a button-like control: the `field` look is always a pill, every other look takes the radius of its size.
 * Radius comes from this one function, so no two classes of the same property compete.
 *
 * @param variant - The look.
 * @param size - The size.
 * @returns The radius class.
 */
export const buttonRadiusClasses = (variant: UiButtonVariant, size: UiSize): string =>
  variant === UiButtonVariant.Field ? 'rounded-pill' : controlRadiusClasses[size]
