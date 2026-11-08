/**
 * The one rule that says whether a service of the client area still waits for the client to set it up.
 *
 * The backend decides the same way: the setup use case accepts only a service that is Pending and
 * refuses every other status (`SERVICE_NOT_PENDING`). A hosting service is ordered first and set up
 * after, so it stays Pending until the client chooses a control-panel username and the setup
 * provisions it. A digital service is delivered by a fulfilment plugin, is activated without any
 * setup and must never be offered the wizard. Whether a username exists is not the rule: an active
 * or terminated service without one is not set up through the wizard either.
 *
 * @module utils/serviceSetup
 */

import type { ClientService } from '~/types/clientservice'
import { ServiceStatus } from '~/types/servicestatus'

/**
 * Whether a service shows "needs setup" and is sent to the setup wizard. The lists, the dashboard,
 * the detail page, its notices and its sidebar all ask this one function.
 *
 * @param service - The service as the client area reads it.
 * @returns True for a Pending hosting service; false for a digital one or any other status.
 */
export const serviceAwaitsSetup = (service: Pick<ClientService, 'isDigital' | 'status'>): boolean =>
  !service.isDigital && service.status === ServiceStatus.Pending
