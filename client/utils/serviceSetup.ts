/**
 * Whether a service of the client area still waits for the client to set it up.
 *
 * A hosting service is ordered first and set up after: until the client chooses a control-panel
 * username it has none, and the lists send the client to the setup wizard. A digital service is
 * delivered by a fulfilment plugin and has nothing to set up, so it never carries a username and
 * must never be offered the wizard (the API refuses setup for it as well).
 *
 * @module utils/serviceSetup
 */

import type { ClientService } from '~/types/clientservice'
import { ServiceStatus } from '~/types/servicestatus'

/**
 * Whether the list entry of a service shows "needs setup" and links to the wizard.
 *
 * @param service - The service as the list reads it.
 * @returns True for a hosting service that has no control-panel account yet; false for a digital one.
 */
export const serviceAwaitsSetup = (service: Pick<ClientService, 'isDigital' | 'username'>): boolean =>
  !service.isDigital && !service.username

/**
 * Whether the detail page of a service shows the "finish setup" notice and action.
 *
 * Unlike {@link serviceAwaitsSetup}, which reads the username the lists have, the detail page goes
 * by the order's status: a hosting service whose order is still pending. A digital service is
 * delivered by its plugin and never offered the wizard.
 *
 * @param service - The service as the detail page reads it.
 * @returns True for a pending hosting service; false for a digital one or any other status.
 */
export const serviceSetupPending = (service: Pick<ClientService, 'isDigital' | 'status'>): boolean =>
  !service.isDigital && service.status === ServiceStatus.Pending
