/**
 * The command palette's pure logic: the entries it offers (built from the sidebar's own navigation
 * definition plus a short list of create actions), the search over them, the split of a label around
 * the matched text, and the per-viewer list of recently opened entries kept in `localStorage` under a key that does not carry the
 * viewer's email.
 *
 * Nothing here touches Vue or the router, so it is unit-tested on plain values.
 */
import { Icon } from '@/lib/icons'
import { AppRoute } from '@/lib/routes'
import type { NavSection } from '@/lib/navigation'

/** Closed set of the groups the palette lists its entries under, in display order. */
export enum PaletteGroup {
  /** Entries the viewer opened lately, shown while the search is empty. */
  Recent = 'recent',
  /** Create actions such as a new client. */
  Actions = 'actions',
  /** Pages of the panel, taken from the sidebar. */
  Navigation = 'navigation',
}

/** One thing the palette can open. */
export interface PaletteEntry {
  /** Route path the entry opens; also its identity, so a page is never listed twice. */
  to: string
  /** Text shown and searched, already in the operator's language. */
  label: string
  /** Muted text after the label (the sidebar section or parent page), empty when there is none. */
  hint: string
  /** Inner SVG markup of the entry's icon. */
  icon: string
  /** Group the entry belongs to when the search is not empty. */
  group: PaletteGroup.Actions | PaletteGroup.Navigation
}

/** A piece of a label, either matched by the search or not. */
interface LabelPart {
  /** The text of the piece. */
  text: string
  /** Whether the search matched this piece. */
  match: boolean
  /** Offset of the piece in the label; unique within one label, so it is a stable list key. */
  start: number
}

/** A create action the palette offers: where it goes and the i18n key of its label. */
interface PaletteAction {
  /** Route the action opens. */
  to: AppRoute
  /** i18n key of the label. */
  labelKey: string
}

/** The create actions, in display order; each opens a route that exists in the router. */
const PALETTE_ACTIONS: readonly PaletteAction[] = [
  { to: AppRoute.ClientsAdd, labelKey: 'commandPalette.actions.newClient' },
  { to: AppRoute.SupportTicketsNew, labelKey: 'commandPalette.actions.newTicket' },
  { to: AppRoute.BillingQuotesAdd, labelKey: 'commandPalette.actions.newQuote' },
  { to: AppRoute.BillingAddTransaction, labelKey: 'commandPalette.actions.addTransaction' },
  { to: AppRoute.BillingBillableItemsAdd, labelKey: 'commandPalette.actions.newBillableItem' },
]

/** Most recently opened entries the palette remembers per viewer. */
export const MAX_RECENT = 5

/** Prefix of the `localStorage` key holding a viewer's recent paths; a hash of the viewer follows. */
const RECENT_KEY_PREFIX = 'hostpanel.admin.commandPalette.recent.'

/** Radix of the written hash: base 36 keeps the key short. */
const HASH_RADIX = 36

/**
 * The `localStorage` key a viewer's recent paths live under. The key holds a hash of the viewer
 * rather than the email itself, so the address is never written into browser storage as a key; the
 * hash is a cyrb53 digest (synchronous, unlike `crypto.subtle`), which separates accounts on one
 * browser and is not meant as a secret.
 *
 * @param viewer - The signed-in viewer's email.
 * @returns The storage key for that viewer.
 */
const recentKey = (viewer: string): string => {
  let first = 0xdeadbeef
  let second = 0x41c6ce57
  for (let index = 0; index < viewer.length; index += 1) {
    const code = viewer.charCodeAt(index)
    first = Math.imul(first ^ code, 2654435761)
    second = Math.imul(second ^ code, 1597334677)
  }
  first = Math.imul(first ^ (first >>> 16), 2246822507) ^ Math.imul(second ^ (second >>> 13), 3266489909)
  second = Math.imul(second ^ (second >>> 16), 2246822507) ^ Math.imul(first ^ (first >>> 13), 3266489909)
  const digest = 4294967296 * (2097151 & second) + (first >>> 0)
  return RECENT_KEY_PREFIX + digest.toString(HASH_RADIX)
}

/**
 * Builds every entry the palette can open: the create actions, then each top-level page of the
 * sidebar and its sub-pages. A path reached twice (a parent whose first child is itself) is listed
 * once, under the first label.
 *
 * @param sections - The sidebar sections, with the Add-ons section when plugins declare menu items.
 * @param translate - Resolves an i18n key to text.
 * @returns The entries, actions first, in sidebar order.
 */
export const buildPaletteEntries = (
  sections: readonly NavSection[],
  translate: (key: string) => string,
): PaletteEntry[] => {
  const entries: PaletteEntry[] = PALETTE_ACTIONS.map((action) => ({
    to: action.to,
    label: translate(action.labelKey),
    hint: '',
    icon: Icon.Add,
    group: PaletteGroup.Actions,
  }))
  const seen = new Set<string>(entries.map((entry) => entry.to))
  /**
   * Appends an entry unless its path is already listed.
   *
   * @param entry - The entry to add.
   */
  const add = (entry: PaletteEntry): void => {
    if (!seen.has(entry.to)) {
      seen.add(entry.to)
      entries.push(entry)
    }
  }
  for (const section of sections) {
    const sectionLabel = section.labelKey === undefined ? '' : translate(section.labelKey)
    for (const item of section.items) {
      const itemLabel = item.labelKey === undefined ? item.label : translate(item.labelKey)
      const icon = item.icon
      add({ to: item.to, label: itemLabel, hint: sectionLabel, icon, group: PaletteGroup.Navigation })
      for (const child of item.children ?? []) {
        add({ to: child.to, label: translate(child.labelKey), hint: itemLabel, icon, group: PaletteGroup.Navigation })
      }
    }
  }
  return entries
}

/**
 * The entries whose label or hint contains the search text, case-insensitively, keeping their order.
 *
 * @param entries - Every entry.
 * @param query - The text typed; blank returns all entries.
 * @returns The matching entries.
 */
export const filterPaletteEntries = (entries: readonly PaletteEntry[], query: string): PaletteEntry[] => {
  const needle = query.trim().toLowerCase()
  if (needle === '') {
    return [...entries]
  }
  return entries.filter((entry) => `${entry.label} ${entry.hint}`.toLowerCase().includes(needle))
}

/**
 * Splits a label around the first occurrence of the search text so the match can be highlighted.
 *
 * @param label - The text to split.
 * @param query - The text typed; blank or absent from the label returns the label whole.
 * @returns Two or three pieces, or one unmatched piece.
 */
export const splitLabel = (label: string, query: string): LabelPart[] => {
  const needle = query.trim().toLowerCase()
  const at = needle === '' ? -1 : label.toLowerCase().indexOf(needle)
  if (at === -1) {
    return [{ text: label, match: false, start: 0 }]
  }
  const end = at + needle.length
  const parts: LabelPart[] = []
  if (at > 0) {
    parts.push({ text: label.slice(0, at), match: false, start: 0 })
  }
  parts.push({ text: label.slice(at, end), match: true, start: at })
  if (end < label.length) {
    parts.push({ text: label.slice(end), match: false, start: end })
  }
  return parts
}

/**
 * Reads the paths a viewer opened lately, newest first. Storage that is unavailable or holds
 * something unexpected reads as an empty list.
 *
 * @param viewer - The signed-in viewer's email, hashed into the key; blank means nobody is remembered.
 * @returns The recent paths, at most {@link MAX_RECENT}.
 */
export const readRecent = (viewer: string): string[] => {
  if (viewer === '') {
    return []
  }
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(recentKey(viewer)) ?? '[]')
    if (!Array.isArray(parsed)) {
      return []
    }
    return parsed.filter((value): value is string => typeof value === 'string').slice(0, MAX_RECENT)
  } catch {
    // A blocked or corrupt store only means nothing is remembered; the palette still works.
    return []
  }
}

/**
 * Moves a path to the front of the viewer's recent list and stores the result.
 *
 * @param viewer - The signed-in viewer's email; blank stores nothing.
 * @param current - The recent paths held now, newest first.
 * @param path - The path just opened.
 * @returns The new list, at most {@link MAX_RECENT}, with the path first and no repeat.
 */
export const rememberRecent = (viewer: string, current: readonly string[], path: string): string[] => {
  const next = [path, ...current.filter((entry) => entry !== path)].slice(0, MAX_RECENT)
  if (viewer === '') {
    return next
  }
  try {
    localStorage.setItem(recentKey(viewer), JSON.stringify(next))
  } catch {
    // A full or blocked store only loses the memory of this visit.
  }
  return next
}

/**
 * Forgets the paths a viewer opened lately; called on sign-out so the history does not outlive the
 * session on a shared browser. A blank viewer, or a blocked store, has nothing to forget.
 *
 * @param viewer - The email of the viewer who is signing out; blank clears nothing.
 */
export const clearRecent = (viewer: string): void => {
  if (viewer === '') {
    return
  }
  try {
    localStorage.removeItem(recentKey(viewer))
  } catch {
    // A blocked store holds nothing this code could have written.
  }
}
