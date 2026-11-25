/**
 * The trail a screen's header draws above its title, as pure steps.
 *
 * A screen's trail is its parents followed by the page itself. The parents of a page are the steps the navigation already
 * names for it (the section and its page, from `lib/breadcrumb.ts`), and for a page that sits inside a client they are the
 * client list, the client and the client's section. The page itself is always the last step, has no target, and shows a bar
 * while its name is still being read. `composables/usePageCrumbs.ts` binds these to the language and the open client.
 */
import { CLIENT_NAV } from '@/lib/clientSections'
import { AppRoute, clientPath, clientSectionPath } from '@/lib/routes'
import type { ClientSection, RouteId } from '@/lib/routes'
import type { UiBreadcrumbItem } from '@/utils/ui/breadcrumb'

/** Translates a locale key into the operator's language. */
export type PageCrumbsTranslate = (key: string) => string

/** Locale key of the client list's name, the first step of every trail inside a client. */
const CLIENTS_LABEL_KEY = 'nav.clients'

/**
 * The parents of the page the navigation trail ends in: every step but the last, which is the generic name of the page.
 *
 * @param trail - The navigation's trail of the current page, outermost first.
 * @returns The trail without its last step.
 */
export const parentsOf = (trail: readonly UiBreadcrumbItem[]): UiBreadcrumbItem[] => trail.slice(0, -1)

/**
 * Completes a trail with the page itself.
 *
 * @param parents - The steps above the page, each leading somewhere.
 * @param current - The page's own name; absent or empty while it is still being read, which draws a bar in its place.
 * @returns The whole trail, whose last step has no target.
 */
export const withCurrent = (parents: readonly UiBreadcrumbItem[], current: string | undefined): UiBreadcrumbItem[] => [
  ...parents,
  current === undefined || current === '' ? { label: '', pending: true } : { label: current },
]

/** What the steps above a page inside a client are made of. */
export interface ClientTrailOrigin {
  /** The client's id, in the paths of its steps. */
  clientId: RouteId
  /** The client's name, as the trail shows it. */
  clientName: string
  /** The section of the client the page belongs to. */
  section: ClientSection
  /** Resolves a locale key. */
  translate: PageCrumbsTranslate
}

/**
 * The trail of a section page of a client (its summary, its domains list): the client list, the client, then the section itself.
 *
 * @param origin - The client, its section and the translation.
 * @returns The whole trail, whose last step is the section and has no target.
 */
export const clientSectionTrail = (origin: ClientTrailOrigin): UiBreadcrumbItem[] => {
  const parents = clientParents(origin)
  const section = parents.pop()
  return section === undefined ? parents : [...parents, { label: section.label }]
}

/**
 * The steps above a page that sits inside a client: the client list, the client and the client's section.
 *
 * @param origin - The client, its section and the translation.
 * @returns The parents, outermost first.
 */
export const clientParents = (origin: ClientTrailOrigin): UiBreadcrumbItem[] => {
  const section = CLIENT_NAV.find((entry) => entry.section === origin.section)
  return [
    { label: origin.translate(CLIENTS_LABEL_KEY), to: AppRoute.Clients },
    { label: origin.clientName, to: clientPath(origin.clientId) },
    ...(section === undefined ? [] : [{ label: origin.translate(section.label), to: clientSectionPath(origin.clientId, origin.section) }]),
  ]
}
