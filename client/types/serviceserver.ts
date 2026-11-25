/**
 * The server a hosting service runs on, as the backend's `ServiceServerDto` sends it.
 *
 * @module types/serviceserver
 */

import type { ServicePanelPlugin } from '~/types/servicepanelplugin'

/** The server a hosting service runs on. */
export interface ServiceServer {
  /** Display name of the server. */
  name: string
  /** Hostname of the server. */
  hostname: string
  /** IP address of the server; null when it has none. */
  ipAddress: string | null
  /** The panel plugin that serves the server's module; null when none does (a module the host provisions itself). */
  panelPlugin: ServicePanelPlugin | null
}
