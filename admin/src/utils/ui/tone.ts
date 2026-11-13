/**
 * Semantic tones shared by the kit (badge, alert, stat tile, dialog confirm button).
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

/**
 * Solid fill classes per tone (confirm buttons). The status fills are bright pastels in the dark theme
 * and deep tones in the light one, so the label is the page's base colour: dark on the pastel, near
 * white on the deep fill. The accent fill steps to a deeper blue in the light theme for the same reason.
 */
export const toneSolidClasses: Record<UiTone, string> = {
  neutral: 'bg-surface-hover text-text-primary hover:bg-border',
  primary: 'bg-primary-400 light:bg-primary-600 text-surface-base hover:bg-primary-300 light:hover:bg-primary-700',
  info: 'bg-primary-400 light:bg-primary-600 text-surface-base hover:bg-primary-300 light:hover:bg-primary-700',
  success: 'bg-status-green text-surface-base hover:opacity-90',
  warning: 'bg-status-yellow text-surface-base hover:opacity-90',
  danger: 'bg-status-red text-surface-base hover:opacity-90',
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
