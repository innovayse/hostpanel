/**
 * Class strings the classic header paints its controls with.
 *
 * Every control in the bar (language, currency, theme, cart, account, menu button) shares one
 * height, radius and border, so they read as one family. The strings are complete class names
 * (Tailwind reads them from this file) and use classic tokens only.
 *
 * @module utils/headerControl
 */

/** Ring drawn by the keyboard on every header control, in the classic brand colour. */
export const HEADER_FOCUS = 'focus-visible:ring-classic-brand dark:focus-visible:ring-classic-brand focus-visible:ring-offset-classic-surface dark:focus-visible:ring-offset-classic-surface'

/**
 * Frame of a header control: 40px tall, modest radius, hairline border, a brand border and tint on
 * hover, in light and dark. Add `w-10` for an icon-only control or `gap-2 px-3` for one with text.
 */
export const HEADER_CONTROL = `h-10 rounded-lg border border-classic-border-strong bg-classic-surface text-classic-ink transition-colors hover:border-classic-brand hover:bg-classic-brand-soft hover:text-classic-brand active:bg-classic-brand-soft ${HEADER_FOCUS}`

/** Frame of a navigation entry in the bar: same height and radius as the controls, no border. */
export const HEADER_NAV_ITEM = `inline-flex h-10 items-center gap-1.5 rounded-lg px-3 text-sm font-medium transition-colors ${HEADER_FOCUS}`

/** A navigation entry that is not the current page. */
export const HEADER_NAV_IDLE = 'text-classic-muted hover:bg-classic-surface-2 hover:text-classic-ink'

/** The navigation entry of the current page: the solid brand fill with white text. */
export const HEADER_NAV_ACTIVE = 'bg-classic-fill text-classic-on-brand'

/** Row of a dropdown or drawer link. */
export const HEADER_MENU_ROW = `flex items-start gap-3 rounded-md px-3 py-2.5 text-classic-ink transition-colors hover:bg-classic-surface-2 focus-visible:bg-classic-surface-2 ${HEADER_FOCUS}`

/**
 * Row of a dropdown link that carries an icon tile: a fixed 60px height so every row is the same
 * whether or not it has a description, the tile centred on the left, and the `group` the tile
 * reads to fill in on hover and keyboard focus.
 */
export const HEADER_ICON_ROW = `group flex min-h-[60px] items-center gap-3 rounded-md px-3 py-2.5 text-classic-ink transition-colors hover:bg-classic-surface-2 focus-visible:bg-classic-surface-2 ${HEADER_FOCUS}`
