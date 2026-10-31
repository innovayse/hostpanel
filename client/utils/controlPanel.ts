/**
 * Addresses of a hosting service's control panel and webmail on its own server, and the one rule
 * for offering a client the control panel sign-in.
 *
 * The service card, header, quick actions and sidebar all show the same sign-in entry, so they all
 * ask {@link canSignInToControlPanel} instead of each writing the condition inline.
 *
 * @module utils/controlPanel
 */

import { CONTROL_PANEL_PORT, WEBMAIL_PORT } from '~/constants/controlPanel'
import { HTTPS_PREFIX } from '~/constants/urlScheme'
import type { ClientService } from '~/types/clientservice'
import { ServiceStatus } from '~/types/servicestatus'

/**
 * Address of the control panel's plain sign-in page.
 *
 * @param host - The service's server host name.
 * @returns The secure address with the panel port.
 */
export const controlPanelUrl = (host: string): string => `${HTTPS_PREFIX}${host}:${CONTROL_PANEL_PORT}`

/**
 * Address of the service's webmail.
 *
 * @param host - The service's server host name.
 * @returns The secure address with the webmail port.
 */
export const webmailUrl = (host: string): string => `${HTTPS_PREFIX}${host}:${WEBMAIL_PORT}`

/**
 * Address of a service's website.
 *
 * @param domain - The service's domain name.
 * @returns The secure address of the site.
 */
export const websiteUrl = (domain: string): string => `${HTTPS_PREFIX}${domain}`

/**
 * Whether the control panel sign-in is offered for a service.
 *
 * It needs an active service (the panel is not reachable on a pending, suspended or terminated
 * one), a known server to sign in to, and a person allowed single sign-on. The API enforces the
 * permission on the request, so this only decides what to show.
 *
 * @param service - The service, only its status and assigned server are read.
 * @param mayUseSingleSignOn - Whether the signed-in person holds `Permission.AllowSingleSignOn`.
 * @returns True when the sign-in entry should be shown.
 */
export const canSignInToControlPanel = (
  service: Pick<ClientService, 'status' | 'server'>,
  mayUseSingleSignOn: boolean
): boolean => {
  return Boolean(service.server) && service.status === ServiceStatus.Active && mayUseSingleSignOn
}
