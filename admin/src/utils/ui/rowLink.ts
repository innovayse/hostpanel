/**
 * Look vocabulary for `UiRowLink`, the link whose whole surface is the target.
 */

/** Closed set of looks of a whole-surface link. */
export const UiRowLinkLook = {
  /** A rounded tile on the row surface, its content in a line (a short list of destinations). */
  Tile: 'tile',
  /** A padded block that fills a card and lights up on hover (a card that is one destination). */
  Card: 'card',
  /** A row of a list, divided from the next by a hairline, its content in a line. */
  Divided: 'divided',
  /** A bare block that only makes what it wraps a link (a card that carries its own look). */
  Block: 'block',
} as const

/** One of the UiRowLinkLook values. */
export type UiRowLinkLook = (typeof UiRowLinkLook)[keyof typeof UiRowLinkLook]

/** Layout and surface classes of each look, written out whole so Tailwind emits them. */
export const rowLinkLookClasses: Record<UiRowLinkLook, string> = {
  [UiRowLinkLook.Tile]: 'flex items-center gap-3 rounded-[14px] bg-surface-row p-3 text-text-primary transition-colors',
  [UiRowLinkLook.Card]: 'block h-full rounded-card p-5 transition-colors',
  [UiRowLinkLook.Divided]: 'flex items-start gap-3 border-b border-border px-4 py-3.5 last:border-0 transition-colors',
  [UiRowLinkLook.Block]: 'block',
}

/** The looks that light up on hover when the link is live. */
export const rowLinkLooksWithHover: ReadonlySet<UiRowLinkLook> = new Set<UiRowLinkLook>([
  UiRowLinkLook.Tile,
  UiRowLinkLook.Card,
  UiRowLinkLook.Divided,
])
