/**
 * Media queries that mirror Tailwind's `max-` variants, so a surface a script picks and the classes of that surface
 * change at the same width.
 */

/** Media query under the Tailwind `sm` breakpoint (640 px): the width of the `max-sm:` variant, a phone. */
export const UI_BELOW_SM_QUERY = '(max-width: 639.98px)'

/** Media query under the Tailwind `lg` breakpoint (1024 px): the width of the `max-lg:` variant. */
export const UI_BELOW_LG_QUERY = '(max-width: 1023.98px)'
