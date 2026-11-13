/**
 * Text-like field vocabulary shared by `UiInput`, `UiTextarea` and `UiSelect`.
 *
 * Fields draw from one class set so a form of mixed controls lines up, and every colour comes
 * from the theme tokens (surface-field, border, text-*), which is what makes them follow the
 * light/dark switch.
 */

/** Input types `UiInput` renders. File inputs have their own `UiFileInput`, colours `UiColorField`. */
export const UiInputType = {
  /** The `text` input type. */
  Text: 'text',
  /** The `email` input type. */
  Email: 'email',
  /** The `password` input type. */
  Password: 'password',
  /** The `number` input type. */
  Number: 'number',
  /** The `url` input type. */
  Url: 'url',
  /** The `tel` input type. */
  Tel: 'tel',
  /** The `search` input type. */
  Search: 'search',
  /** The `date` input type. */
  Date: 'date',
  /** The `time` input type. */
  Time: 'time',
  /** The `datetime-local` input type. */
  DatetimeLocal: 'datetime-local',
  /** The `month` input type. */
  Month: 'month',
} as const

/** One of the input types. */
export type UiInputType = (typeof UiInputType)[keyof typeof UiInputType]

/**
 * Width is applied through `:where()` so it has zero specificity: a screen's own `w-24` or
 * `flex-1` always wins over the kit default instead of racing it.
 */
export const fieldWidthClasses = '[:where(&)]:w-full'

/**
 * Keyboard focus ring of every kit control that is not a text field (buttons, switches, boxes, drop
 * zones). The ring takes the accent text token, which is a light blue on the dark ground and a deep
 * blue on the light one, so it keeps at least 3:1 against the surface in both themes.
 */
export const focusRingClasses = 'outline-none focus-visible:ring-2 focus-visible:ring-text-accent'

/**
 * Border, background, text and focus classes of a text-like field, without a corner radius (the
 * concept's field: a 5% white fill, a 9% hairline, the muted placeholder). A joined field, such as the
 * phone number's country picker, sets its own corners. A disabled field turns to a muted, filled box
 * with a not-allowed cursor, so it reads as unavailable by more than its opacity.
 */
export const fieldSurfaceClasses =
  'bg-surface-field border border-border-control light:not-disabled:hover:border-border-control-hover text-text-primary placeholder:text-text-muted outline-none transition-colors duration-200 focus-visible:border-text-accent focus-visible:ring-2 focus-visible:ring-text-accent/40 disabled:bg-surface-hover disabled:text-text-muted disabled:cursor-not-allowed read-only:cursor-default'

/** A standalone text-like field: the surface plus the control radius (12px). */
export const fieldBaseClasses = `${fieldSurfaceClasses} rounded-control`

/** Extra classes of a field flagged invalid (the server refused its value). */
export const fieldInvalidClasses = 'border-status-red/60 focus-visible:border-status-red focus-visible:ring-status-red/30'

/** Browser autofill hint per input type; a screen overrides it with its own `autocomplete`. */
export const inputAutocomplete: Partial<Record<UiInputType, string>> = {
  email: 'email',
  password: 'current-password',
  tel: 'tel',
  url: 'url',
}
