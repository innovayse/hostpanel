/**
 * Plugin slots on the admin's existing pages: the closed set of slot ids, the context a slot is
 * opened for and the widget the host lists for it.
 *
 * The shapes follow the slot index of the plugin extension points (`GET /api/admin/plugins/slots/{slotId}`):
 * the host has already filtered the widgets by enabled plugin, permission and the operator's hide
 * setting, localised the label and put them in order, so the admin neither filters nor translates.
 */

/** Closed set of slot ids an admin page offers; the strings are the stable ids the SDK publishes. */
export const PluginSlotId = {
  /** Panels on a client's summary page. */
  AdminClientPanels: 'admin.client.panels',
  /** Panels on a service's detail page. */
  AdminServicePanels: 'admin.service.panels',
} as const

/** One of the {@link PluginSlotId} values. */
export type PluginSlotId = (typeof PluginSlotId)[keyof typeof PluginSlotId]

/** The record a slot is opened for: the client of the summary page or the service of the detail page. */
export type PluginSlotContext = { clientId: number } | { serviceId: number }

/** One widget the host lists for a slot. */
export interface PluginSlotWidgetDto {
  /** Id of the plugin that declares the widget, the `id` field of plugin.json. */
  pluginId: string
  /** Id of the widget inside its plugin. */
  widgetId: string
  /** Title shown on the widget, already in the caller's language. */
  label: string
  /** Position inside the slot; the list arrives sorted by it. */
  order: number
}

/**
 * Setting row that holds the widgets the operator hid in each slot: a comma separated list of
 * {@link widgetRef} values. The row is seeded by the host; the panel cannot create settings.
 */
export const PluginSlotHiddenSetting: Record<PluginSlotId, string> = {
  [PluginSlotId.AdminClientPanels]: 'plugins.slots.admin.client.panels.hidden',
  [PluginSlotId.AdminServicePanels]: 'plugins.slots.admin.service.panels.hidden',
}

/**
 * Names a widget the way the hide setting lists it.
 *
 * @param widget - The widget to name.
 * @returns `pluginId:widgetId`.
 */
export const widgetRef = (widget: PluginSlotWidgetDto): string => `${widget.pluginId}:${widget.widgetId}`

/**
 * Names the record a slot is opened for, to key what is loaded for it.
 *
 * @param context - The slot's context.
 * @returns `client:<id>` or `service:<id>`.
 */
export const contextKey = (context: PluginSlotContext): string =>
  'clientId' in context ? `client:${context.clientId}` : `service:${context.serviceId}`
