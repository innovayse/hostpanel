/**
 * The trail a screen hands to its `UiPageHeader` (or the client layout draws for a section page): one source for every
 * detail page, so no screen spells out its own steps.
 */
import { useI18n } from 'vue-i18n'
import { useBreadcrumb } from '@/composables/useBreadcrumb'
import { clientParents, clientSectionTrail, parentsOf, withCurrent } from '@/lib/pageCrumbs'
import type { ClientTrailOrigin } from '@/lib/pageCrumbs'
import type { ClientSection, RouteId } from '@/lib/routes'
import { useClientsStore } from '@/stores/clients'
import type { UiBreadcrumbItem } from '@/utils/ui/breadcrumb'

/** What {@link usePageCrumbs} hands back. */
export interface PageCrumbs {
  /**
   * The trail of a page that lives in the navigation: its parents as the top bar names them, then the page itself.
   *
   * @param current - The page's own name; absent while it is still being read.
   * @returns The whole trail, outermost first.
   */
  crumbs: (current?: string) => UiBreadcrumbItem[]
  /**
   * The trail of a page inside a client: the client list, the open client, the client's section, then the page itself.
   *
   * @param clientId - The client's id.
   * @param section - The section of the client the page belongs to.
   * @param current - The page's own name; absent while it is still being read.
   * @returns The whole trail, outermost first.
   */
  clientCrumbs: (clientId: RouteId, section: ClientSection, current?: string) => UiBreadcrumbItem[]
  /**
   * The trail of a section page of the open client: the client list, the client, then the section itself.
   *
   * @param clientId - The client's id.
   * @param section - The section the page is.
   * @returns The whole trail, outermost first.
   */
  sectionCrumbs: (clientId: RouteId, section: ClientSection) => UiBreadcrumbItem[]
}

/**
 * Builds the trail functions bound to the language, the navigation and the client the clients store holds.
 *
 * Call them from a template (or a computed): they read the live trail, the language and the client, so a screen's trail
 * follows a language switch and a client that finishes loading.
 *
 * @returns The trail builders.
 */
export const usePageCrumbs = (): PageCrumbs => {
  /** Translation function, which also makes the trail follow a language switch. */
  const { t } = useI18n()

  /** The navigation's trail of the page on display, whose parents the page's own trail starts with. */
  const navigation = useBreadcrumb()

  /** The clients store, owner of the client a client screen sits in. */
  const clients = useClientsStore()

  /**
   * What the steps above a page of the open client are made of; the client is named by its id until it has loaded.
   *
   * @param clientId - The client's id.
   * @param section - The section of the client.
   * @returns The origin of the client trail.
   */
  const originOf = (clientId: RouteId, section: ClientSection): ClientTrailOrigin => {
    const client = clients.current
    const clientName = client === null ? `#${clientId}` : `${client.firstName} ${client.lastName}`
    /** Translates a key in the active language. */
    const translate = (key: string): string => t(key)
    return { clientId, clientName, section, translate }
  }

  /**
   * The trail of a page that lives in the navigation.
   *
   * @param current - The page's own name; absent while it is still being read.
   * @returns The whole trail, outermost first.
   */
  const crumbs = (current?: string): UiBreadcrumbItem[] => withCurrent(parentsOf(navigation.value), current)

  /**
   * The trail of a page inside the open client.
   *
   * @param clientId - The client's id.
   * @param section - The section of the client the page belongs to.
   * @param current - The page's own name; absent while it is still being read.
   * @returns The whole trail, outermost first.
   */
  const clientCrumbs = (clientId: RouteId, section: ClientSection, current?: string): UiBreadcrumbItem[] =>
    withCurrent(clientParents(originOf(clientId, section)), current)

  /**
   * The trail of a section page of the open client.
   *
   * @param clientId - The client's id.
   * @param section - The section the page is.
   * @returns The whole trail, outermost first.
   */
  const sectionCrumbs = (clientId: RouteId, section: ClientSection): UiBreadcrumbItem[] => clientSectionTrail(originOf(clientId, section))

  return { crumbs, clientCrumbs, sectionCrumbs }
}
