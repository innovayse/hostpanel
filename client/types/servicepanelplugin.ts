/**
 * The panel plugin that serves a service's server, as the backend's `ServicePanelPluginDto` sends it.
 *
 * @module types/servicepanelplugin
 */

/** The panel plugin that serves a service's server: the Open panel button shows its icon and names it. */
export interface ServicePanelPlugin {
  /** Plugin id as its manifest declares it. */
  id: string
  /** Display name of the plugin. */
  name: string
  /** True when the plugin ships an icon the portal's icon route serves. */
  hasIcon: boolean
}
