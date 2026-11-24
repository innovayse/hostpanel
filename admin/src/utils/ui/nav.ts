/**
 * Navigation vocabulary: the type sizes of links and the classes of a navigation entry (`UiNavItem`).
 *
 * The rule: a navigation entry is 14px, a sub-menu entry 13px, a card action link ("View all") 13px
 * (`UiLink` `size="sm"`), and a link inside running text inherits the body size (14px) and sets none.
 */
import { focusRingClasses } from '@/utils/ui/input'

/** Type of a navigation entry (sidebar, section sidebar): 14px, medium weight. */
export const navItemTextClasses = 'text-sm font-medium'

/** Type of a sub-menu entry under a navigation entry: 13px, medium weight. */
export const navSubItemTextClasses = 'text-[0.8125rem] font-medium'

/** Closed set of levels of a navigation entry: a top-level entry, or a link inside the sub-menu of one. */
export const UiNavItemLevel = {
  Top: 'top',
  Sub: 'sub',
} as const

/** One of the levels of a navigation entry. */
export type UiNavItemLevel = (typeof UiNavItemLevel)[keyof typeof UiNavItemLevel]

/** Shape shared by every top-level entry: 42px tall, 12px corners, 14px type (the kit's navigation size). */
const navTopShapeClasses = `relative flex h-[2.625rem] w-full items-center rounded-control text-left no-underline ${navItemTextClasses} transition-all duration-150 ${focusRingClasses}`

/**
 * Shape of a sub-menu link: a row as tall as its label (a wrapped label grows it), 13px type (the kit's sub-menu size), and
 * its own piece of the tree guide (`nav-guide-row` in the stylesheet: the trunk through the row and the curve to the label),
 * which sits outside the link on its left, so neither the hover fill nor the active pill ever covers it.
 */
const navSubShapeClasses = `nav-guide-row flex min-h-8 w-full items-center gap-2 rounded-[7px] px-3 py-1 text-left ${navSubItemTextClasses} no-underline transition-all duration-150 ${focusRingClasses}`

/** Look of a top-level entry that is not the current page; the transparent border keeps both looks the same size. */
const navTopIdleClasses = 'border border-transparent text-text-secondary hover:bg-surface-hover hover:text-text-primary'

/** Look of the current page: the filled accent pill with its hairline. */
const navTopActiveClasses = 'border border-(color:--nav-active-border) bg-(--nav-active-bg) text-text-primary'

/** Look of a sub-menu link that is not the current page. */
const navSubIdleClasses = 'text-text-muted hover:bg-surface-hover hover:text-text-secondary'

/** Look of the sub-menu link of the current page. */
const navSubActiveClasses = 'bg-surface-hover text-text-accent'

/**
 * Classes of a navigation entry for its level and state.
 *
 * @param level - Top-level entry or sub-menu link.
 * @param active - Whether it is the current page.
 * @param rail - Whether the panel is the icon rail (a top-level entry is then its icon alone, centred).
 * @returns The class list.
 */
export const navItemClasses = (level: UiNavItemLevel, active: boolean, rail: boolean): string[] => {
  if (level === UiNavItemLevel.Sub) {
    return [navSubShapeClasses, active ? navSubActiveClasses : navSubIdleClasses]
  }
  return [navTopShapeClasses, rail ? 'justify-center px-0' : 'gap-3 px-3', active ? navTopActiveClasses : navTopIdleClasses]
}

/**
 * Colour class of the icon of a top-level entry.
 *
 * @param active - Whether it is the current page.
 * @returns Muted, or the accent on the current page.
 */
export const navIconClasses = (active: boolean): string => (active ? 'text-text-accent' : 'text-text-muted')

/** Shape of a sub-menu list: room for the guide on the left, and the guide's gap between links and above the first one. */
const navGuideListClasses = 'py-(--nav-guide-gap) pl-9 gap-(--nav-guide-gap)'

/** Place of a link on the lit path (`--i`), as literal classes since a class built at run time emits no CSS; as long as the longest sub-menu. */
const NAV_STOP_CLASSES = ['[--i:0]', '[--i:1]', '[--i:2]', '[--i:3]', '[--i:4]', '[--i:5]', '[--i:6]', '[--i:7]', '[--i:8]', '[--i:9]', '[--i:10]', '[--i:11]'] as const

/** Place of the active link on the lit path (`--n`), the same literals for the same reason. */
const NAV_ACTIVE_STOP_CLASSES = ['[--n:0]', '[--n:1]', '[--n:2]', '[--n:3]', '[--n:4]', '[--n:5]', '[--n:6]', '[--n:7]', '[--n:8]', '[--n:9]', '[--n:10]', '[--n:11]'] as const

/**
 * Picks the guide classes of a sub-menu link from its place relative to the active one: the segments on the path from the
 * parent to the active link are lit in the gradient's colours (the active one down to its curve), the rest stay muted.
 *
 * @param index - The index of the link in its sub-menu.
 * @param activeIndex - The index of the active link, or -1.
 * @returns The guide classes of the link; none for a muted one.
 */
export const navGuideClasses = (index: number, activeIndex: number): string => {
  if (activeIndex === -1 || index > activeIndex) {
    return ''
  }
  const lit = `nav-guide-lit ${NAV_STOP_CLASSES[index]}`
  return index === activeIndex ? `${lit} nav-guide-active` : lit
}

/**
 * Picks the classes of a sub-menu list: its shape and, when a link is active, the place of that link (`--n`) the lit path is measured by.
 *
 * @param activeIndex - The index of the active link, or -1.
 * @returns The list classes.
 */
export const navListClasses = (activeIndex: number): string =>
  activeIndex === -1 ? navGuideListClasses : `${navGuideListClasses} ${NAV_ACTIVE_STOP_CLASSES[activeIndex]}`
