/**
 * Types and helpers for `UiSidebar`.
 */
import type { RouteLocationRaw } from 'vue-router'

/** One entry of a `UiSidebar`. */
export interface UiSidebarItem {
  /** Stable key; matched against the sidebar's `activeKey` and used in the entry's test id. */
  key: string
  /** Caption shown when the sidebar is expanded and as the tooltip (already translated). */
  label: string
  /** Tooltip of the entry when it differs from its caption (the full name of an abbreviated caption); the caption by default. */
  title?: string
  /** Where the entry navigates. */
  to: RouteLocationRaw
  /** Inner SVG markup of the entry's icon (shape tags only; rendered without `v-html`). */
  icon: string
  /** Heading of the group the entry belongs to (already translated); consecutive entries with the same one share a heading. */
  group?: string
}

/** One entry of a `UiSidebar` with the heading it opens a group with, if any. */
export interface UiSidebarRow {
  /** The entry. */
  item: UiSidebarItem
  /** The group heading drawn above the entry: set on the first entry of each group, undefined on the others. */
  heading: string | undefined
}

/**
 * Marks the first entry of each group with its heading.
 *
 * @param items - The entries in order.
 * @returns One row per entry; an entry without a group opens no heading.
 */
export const sidebarRows = (items: readonly UiSidebarItem[]): UiSidebarRow[] =>
  items.map((item, index) => ({
    item,
    heading: item.group !== undefined && item.group !== items[index - 1]?.group ? item.group : undefined,
  }))

/**
 * Classes of the box of an inner sidebar, shared by the sidebar and its skeleton so the two are one shape. The surface is
 * not in it: the rail is frosted (`sidebarRailSurfaceClasses`) and the opened sidebar, which floats over the content, is denser.
 */
export const sidebarShellClasses = 'flex min-h-0 shrink-0 flex-col overflow-y-auto overflow-x-hidden transition-all duration-200 ease-in-out'

/** Surface of a sidebar folded to its rail: the frosted card look, beside the content and over nothing. */
export const sidebarRailSurfaceClasses = 'glass'

/** Surface of a sidebar opened over the content: denser, with the overlay shadow, so the page behind never shows through the captions. */
export const sidebarOpenSurfaceClasses = 'glass-strong shadow-overlay'

/** Width class of a sidebar folded to its icon rail. */
export const sidebarRailWidthClass = 'w-[56px]'

/** Width class of a sidebar opened to show its captions. */
export const sidebarOpenWidthClass = 'w-[230px]'

/**
 * Classes of the slot an inner sidebar keeps in the layout: always the width of the rail. The opened sidebar is laid over the
 * content from this slot (`sidebarFlyoutClasses`) instead of widening it, so hovering or focusing the rail never moves the page beside it.
 */
export const sidebarSlotClasses = `relative flex min-h-0 shrink-0 ${sidebarRailWidthClass}`

/** Classes that lay the sidebar over the content from its slot: pinned to the slot's left edge and full height, above the neighbouring pane. */
export const sidebarFlyoutClasses = 'absolute inset-y-0 left-0 z-20'

/** Classes of the list of entries inside the shell. */
export const sidebarListClasses = 'flex flex-col gap-1.5 px-1 pt-2 pb-4'

/** Classes of one entry's row before its state: the shared box of a link and of its placeholder. */
export const sidebarEntryClasses = 'relative flex items-center rounded-control border py-2.5'
