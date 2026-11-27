/**
 * Semantic tones shared by the kit (badge, alert, stat tile).
 *
 * The tone vocabulary is a closed set, declared once as a const object plus a derived union:
 * `UiTone.Danger` is the named member, and a template may still pass the literal `"danger"`
 * (a TypeScript `enum` would reject the literal in a template). Every class below is a full
 * literal so Tailwind sees it; nothing is interpolated.
 */

/** Closed set of semantic tones. */
export const UiTone = {
  Neutral: 'neutral',
  Primary: 'primary',
  Info: 'info',
  Success: 'success',
  Warning: 'warning',
  Danger: 'danger',
} as const

/** One of the semantic tones. */
export type UiTone = (typeof UiTone)[keyof typeof UiTone]

/**
 * Pill/badge classes per tone: the 14 percent status tint as the fill and the status colour as the text,
 * with no border. The status and accent tokens swap per theme (`:root.light` in main.css), so no `light:`
 * step is needed and the text stays AA-readable on the tint in both themes.
 */
export const toneBadgeClasses: Record<UiTone, string> = {
  neutral: 'bg-surface-hover text-text-secondary',
  primary: 'bg-tint-blue text-text-accent',
  info: 'bg-tint-blue text-text-accent',
  success: 'bg-tint-green text-status-green',
  warning: 'bg-tint-yellow text-status-yellow',
  danger: 'bg-tint-red text-status-red',
}

/** Banner classes per tone: the badge tint and text on a block surface, with a faint border of the tone. */
export const toneAlertClasses: Record<UiTone, string> = {
  neutral: `${toneBadgeClasses.neutral} border-border`,
  primary: `${toneBadgeClasses.primary} border-text-accent/25`,
  info: `${toneBadgeClasses.info} border-text-accent/25`,
  success: `${toneBadgeClasses.success} border-status-green/25`,
  warning: `${toneBadgeClasses.warning} border-status-yellow/25`,
  danger: `${toneBadgeClasses.danger} border-status-red/25`,
}

/** Foreground-only classes per tone (icons, trend text, stat values). */
export const toneTextClasses: Record<UiTone, string> = {
  neutral: 'text-text-primary',
  primary: 'text-text-accent',
  info: 'text-text-accent',
  success: 'text-status-green',
  warning: 'text-status-yellow',
  danger: 'text-status-red',
}

/** Dot classes per tone (status dots inside a badge). */
export const toneDotClasses: Record<UiTone, string> = {
  neutral: 'bg-text-muted',
  primary: 'bg-primary-500',
  info: 'bg-primary-500',
  success: 'bg-status-green',
  warning: 'bg-status-yellow',
  danger: 'bg-status-red',
}

/** Fill classes of a progress bar per tone, for both engines' pseudo-elements (a bar whose colour states an outcome). */
export const toneProgressClasses: Record<UiTone, string> = {
  neutral: '[&::-moz-progress-bar]:bg-text-muted [&::-webkit-progress-value]:bg-text-muted',
  primary: '[&::-moz-progress-bar]:bg-primary-500 [&::-webkit-progress-value]:bg-primary-500',
  info: '[&::-moz-progress-bar]:bg-primary-500 [&::-webkit-progress-value]:bg-primary-500',
  success: '[&::-moz-progress-bar]:bg-status-green [&::-webkit-progress-value]:bg-status-green',
  warning: '[&::-moz-progress-bar]:bg-status-yellow [&::-webkit-progress-value]:bg-status-yellow',
  danger: '[&::-moz-progress-bar]:bg-status-red [&::-webkit-progress-value]:bg-status-red',
}
