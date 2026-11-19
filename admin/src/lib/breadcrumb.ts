/**
 * The breadcrumb of the page the operator is on, built from the one source the sidebar already uses
 * (`lib/navigation.ts`) plus the route record: no screen writes its own trail.
 *
 * The trail starts with the navigation entry the address belongs to (its section, then its page when the
 * section has one), then adds a step for what lies below that entry: a record's own page (`Details`), a form
 * (`New`, `Edit`) or a configuration screen. A page that is outside the navigation names itself with
 * `meta.crumb` on its route record.
 */
import type { NavChild, NavItem, NavSection } from '@/lib/navigation'
import type { UiBreadcrumbItem } from '@/utils/ui/breadcrumb'

/** Closed set of the literal path segments that name a step of the trail by themselves. */
export const BreadcrumbSegment = {
  /** A create form. */
  Create: 'create',
  /** A create form, in the routes that spell it so. */
  New: 'new',
  /** A create form, in the routes that spell it so. */
  Add: 'add',
  /** An edit form. */
  Edit: 'edit',
  /** A configuration screen of a record. */
  Configure: 'configure',
} as const

/** One of the segments that name a step. */
export type BreadcrumbSegment = (typeof BreadcrumbSegment)[keyof typeof BreadcrumbSegment]

/** Locale key of the step each named segment draws. */
const SEGMENT_LABEL_KEYS: Record<BreadcrumbSegment, string> = {
  [BreadcrumbSegment.Create]: 'layout.breadcrumb.new',
  [BreadcrumbSegment.New]: 'layout.breadcrumb.new',
  [BreadcrumbSegment.Add]: 'layout.breadcrumb.new',
  [BreadcrumbSegment.Edit]: 'layout.breadcrumb.edit',
  [BreadcrumbSegment.Configure]: 'layout.breadcrumb.configure',
}

/** Locale key of the step a record's own page draws (a path segment that is a route parameter). */
const DETAILS_LABEL_KEY = 'layout.breadcrumb.details'

/** Locale key of the lone step of a page the navigation does not know and that names no step itself. */
const FALLBACK_LABEL_KEY = 'common.admin'

/** Translates a locale key into the operator's language. */
export type BreadcrumbTranslate = (key: string) => string

/** A navigation entry that the address belongs to, and how specific the match is. */
interface NavMatch {
  /** The top-level entry. */
  item: NavItem
  /** The page of that entry the address belongs to, when it has such a page. */
  child?: NavChild
  /** The path of the match: the longer it is, the more specific the entry. */
  to: string
}

/**
 * Splits a path into its non-empty segments.
 *
 * @param path - A path or a route pattern.
 * @returns The segments, outermost first.
 */
const segmentsOf = (path: string): string[] => path.split('/').filter((segment) => segment.length > 0)

/**
 * Whether a path is the base or lies below it, on a segment boundary (`/clients/7` is below `/clients`, `/clientsX` is not).
 *
 * @param path - The path to test.
 * @param base - The path it may lie under.
 * @returns True when the path is the base or below it.
 */
const isUnder = (path: string, base: string): boolean => path === base || path.startsWith(`${base}/`)

/**
 * The caption of a navigation entry: its own text (a plugin's entry) or its translated key.
 *
 * @param entry - A top-level entry or one of its pages.
 * @param translate - Resolves a locale key.
 * @returns The caption.
 */
const labelOf = (entry: NavItem | NavChild, translate: BreadcrumbTranslate): string => {
  if (entry.labelKey !== undefined) {
    return translate(entry.labelKey)
  }
  return entry.label
}

/**
 * Lists every navigation entry and page whose path the address lies under, from the sections.
 *
 * A page counts for its section only when its path lies under the section's own (a page the menu files under a
 * section but that lives at another address is not a step of that section's trail).
 *
 * @param path - The address of the page.
 * @param sections - The navigation sections to look in.
 * @returns The entries the address belongs to, in menu order.
 */
const candidatesFor = (path: string, sections: readonly NavSection[]): NavMatch[] =>
  sections.flatMap((section) => section.items.flatMap((item): NavMatch[] => [
    { item, to: item.to },
    ...(item.children ?? []).filter((child) => isUnder(child.to, item.to)).map((child): NavMatch => ({ item, child, to: child.to })),
  ])).filter((candidate) => isUnder(path, candidate.to))

/**
 * Finds the navigation entry an address belongs to: the most specific one, a page of a section before the section.
 *
 * @param path - The address of the page.
 * @param sections - The navigation sections to look in.
 * @returns The best match, or undefined when the address belongs to no entry.
 */
const matchNav = (path: string, sections: readonly NavSection[]): NavMatch | undefined =>
  candidatesFor(path, sections).reduce<NavMatch | undefined>((best, candidate) => {
    if (best === undefined || candidate.to.length > best.to.length) {
      return candidate
    }
    return candidate.to.length === best.to.length && candidate.child !== undefined && best.child === undefined ? candidate : best
  }, undefined)

/**
 * Tells whether a route segment is one the breadcrumb names.
 *
 * @param segment - A literal segment of a route pattern.
 * @returns True for a {@link BreadcrumbSegment}, which narrows the type.
 */
const isBreadcrumbSegment = (segment: string): segment is BreadcrumbSegment => Object.values(BreadcrumbSegment).some((known) => known === segment)

/**
 * Names the steps below the navigation entry from the route's own pattern.
 *
 * A route parameter is the record's own page and a named segment is a form or a screen of it; any other literal
 * segment (a tab of a record) draws nothing, because the page itself shows where in the record the reader is. A
 * record's page followed by a named step is not repeated: `Edit` alone says it.
 *
 * @param pattern - The route pattern, such as `/settings/slides/:id/edit`.
 * @param depth - How many segments the matched navigation entry already covers.
 * @param translate - Resolves a locale key.
 * @returns The labels of the steps below the entry, outermost first.
 */
const stepsBelow = (pattern: string, depth: number, translate: BreadcrumbTranslate): string[] => {
  const details = translate(DETAILS_LABEL_KEY)
  const steps: string[] = []
  for (const segment of segmentsOf(pattern).slice(depth)) {
    if (segment.startsWith(':')) {
      steps.push(details)
    } else if (isBreadcrumbSegment(segment)) {
      steps.push(translate(SEGMENT_LABEL_KEYS[segment]))
    }
  }
  const named = steps.filter((step) => step !== details)
  return named.length > 0 ? named : steps.slice(-1)
}

/**
 * Builds the breadcrumb of a page.
 *
 * Every step but the last leads somewhere; the last is the current page and has no `to`.
 *
 * @param path - The address of the page.
 * @param pattern - The route pattern the address matched; the address itself when none did.
 * @param sections - The navigation sections, with the plugins' entries when they are known.
 * @param translate - Resolves a locale key.
 * @param crumbKey - The route record's `meta.crumb`, the name of a page outside the navigation.
 * @returns The trail, outermost first; never empty.
 */
export const breadcrumbOf = (
  path: string,
  pattern: string,
  sections: readonly NavSection[],
  translate: BreadcrumbTranslate,
  crumbKey?: string,
): UiBreadcrumbItem[] => {
  const match = matchNav(path, sections)
  if (match === undefined) {
    return [{ label: translate(crumbKey ?? FALLBACK_LABEL_KEY) }]
  }
  const trail: UiBreadcrumbItem[] = [{ label: labelOf(match.item, translate), to: match.item.to }]
  if (match.child !== undefined) {
    trail.push({ label: labelOf(match.child, translate), to: match.child.to })
  }
  for (const label of stepsBelow(pattern, segmentsOf(match.to).length, translate)) {
    trail.push({ label })
  }
  const current = trail.pop()
  return current === undefined ? trail : [...trail, { label: current.label }]
}
