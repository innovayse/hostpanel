/**
 * The client area slots the host offers plugins, spelled once: the backend's `PluginSlots` client ids.
 *
 * @module types/pluginslotid
 */

/** A client area slot id. */
export enum PluginSlotId {
  /** Cards on the client dashboard. */
  DashboardCards = 'client.dashboard.cards',
  /** Tabs on the service detail page; the slot's context is the service. */
  ServiceTabs = 'client.service.tabs'
}
