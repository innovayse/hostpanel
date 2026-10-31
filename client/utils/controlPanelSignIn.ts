/**
 * The one rule for offering a client the control panel sign-in on a service.
 *
 * The service card, header, quick actions and sidebar all show the same entry, so they all ask
 * {@link canSignInToControlPanel} instead of each writing the condition inline.
 *
 * @module utils/controlPanelSignIn
 */

import type { ClientService } from '~/types/clientservice'
import { ServiceStatus } from '~/types/servicestatus'

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
