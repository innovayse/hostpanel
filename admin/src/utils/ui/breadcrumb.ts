/**
 * Vocabulary of the breadcrumb (`UiBreadcrumb`): the shape of one step, the two layouts of a trail and the rule that folds a long trail.
 */
import type { RouteLocationRaw } from 'vue-router'

/** One step of a breadcrumb trail. */
export interface UiBreadcrumbItem {
  /** Visible caption (already translated). */
  label: string
  /** Where the step leads; omitted for the current page, which is the last step and is not a link. */
  to?: RouteLocationRaw
  /** Runs when the step is chosen, for a trail a screen keeps in its own state (a drill into nested categories) and not in the address; used when there is no `to`. */
  onSelect?: () => void
  /** `data-testid` of the step when it is a button; the current page takes the trail's `currentTestid`. */
  testid?: string
  /** Draws a bar in place of the label, for the current page whose name is still being read. */
  pending?: boolean
}

/** How a trail is laid out. */
export const UiBreadcrumbVariant = {
  /** One line in the top bar: the parents hide below `md`, where only the current page is drawn. */
  Bar: 'bar',
  /** Above a page title: every step stays at every width, a long trail wraps and a long name is cut with an ellipsis. */
  Page: 'page',
} as const

/** One of the layouts of a trail. */
export type UiBreadcrumbVariant = (typeof UiBreadcrumbVariant)[keyof typeof UiBreadcrumbVariant]

/** A trail split for drawing: the steps shown before the fold, the ones the fold hides and the steps shown after it. */
export interface UiBreadcrumbFold {
  /** The leading steps, always shown. */
  head: UiBreadcrumbItem[]
  /** The middle steps collapsed behind the "..." button; empty when the trail fits. */
  hidden: UiBreadcrumbItem[]
  /** The trailing steps, always shown; the last of them is the current page. Empty when the trail is not folded. */
  tail: UiBreadcrumbItem[]
}

/** How many steps of a trail are shown before the middle ones fold into the "..." button. */
export const BREADCRUMB_MAX_VISIBLE = 4

/**
 * Folds a long trail: the first step stays, the middle ones are hidden and the steps nearest the current page stay.
 *
 * A trail of `maxVisible` steps or fewer is not folded. A longer one keeps its first step and its last
 * `maxVisible - 2` steps (the current page and its parents) and hides what lies between them, so the "..."
 * button always stands for at least two steps and never for a single one that would have fitted.
 *
 * @param items - The whole trail, outermost first; the last is the current page.
 * @param maxVisible - How many steps may be drawn besides the "..." button.
 * @returns The trail split into what is drawn before the fold, what the fold hides and what is drawn after it.
 */
export const foldBreadcrumb = (items: readonly UiBreadcrumbItem[], maxVisible: number = BREADCRUMB_MAX_VISIBLE): UiBreadcrumbFold => {
  if (items.length <= maxVisible) {
    return { head: [...items], hidden: [], tail: [] }
  }
  const tailLength = maxVisible - 2
  return {
    head: items.slice(0, 1),
    hidden: items.slice(1, items.length - tailLength),
    tail: items.slice(items.length - tailLength),
  }
}
