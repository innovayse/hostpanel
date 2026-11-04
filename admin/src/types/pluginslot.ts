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

/** One widget the host lists for a slot (`PluginSlotWidgetDto` on the host). */
export interface PluginSlotWidgetDto {
  /** Id of the plugin that declares the widget, the `id` field of plugin.json. */
  pluginId: string
  /** Id of the widget inside its plugin. */
  id: string
  /** Package-relative path of the widget's view document, which the plugin view renderer (#647) reads. */
  view: string
  /** Title shown on the widget, already in the caller's language; null when the widget declares none. */
  title: string | null
  /** Position inside the slot; the list arrives sorted by it, then by plugin id and widget id. */
  order: number
  /** Names of the context values the widget asks its slot for, such as `clientId` or `serviceId`. */
  contextKeys: string[]
}

/** The host's answer for one slot (`PluginSlotIndexDto`): the slot id and its widgets in display order. */
export interface PluginSlotIndexDto {
  /** Id of the slot the widgets belong to. */
  slotId: string
  /** The widgets, already filtered and sorted by the host; empty when no plugin offers one. */
  widgets: PluginSlotWidgetDto[]
}

/**
 * Setting row that holds every widget the operator hid, across all slots: comma separated
 * {@link widgetRef} entries (`PluginSettingKeys.HiddenSlotWidgets` on the host, seeded empty). The panel
 * cannot create settings, so a host that has not seeded it is told so.
 */
export const HIDDEN_SLOT_WIDGETS_SETTING = 'plugins.hidden_slot_widgets'

/**
 * Names a widget the way the hide setting lists it, the host's `PluginSettingKeys.HiddenWidgetEntry`.
 *
 * @param slotId - The slot the widget is hidden from; the same widget may stay in another slot.
 * @param widget - The widget to name.
 * @returns `slot:plugin:widget`, such as `admin.client.panels:acme:usage`.
 */
export const widgetRef = (slotId: PluginSlotId, widget: PluginSlotWidgetDto): string =>
  `${slotId}:${widget.pluginId}:${widget.id}`

/**
 * Reads the hide setting's entries the way the host does: split at commas, trimmed, empty entries dropped.
 *
 * @param value - The stored value; null when the row holds nothing.
 * @returns The entries in stored order.
 */
export const hiddenWidgetRefs = (value: string | null): string[] =>
  (value ?? '').split(',').map((entry) => entry.trim()).filter((entry) => entry !== '')

/**
 * Names the record a slot is opened for, to key what is loaded for it.
 *
 * @param context - The slot's context.
 * @returns `client:<id>` or `service:<id>`.
 */
export const contextKey = (context: PluginSlotContext): string =>
  'clientId' in context ? `client:${context.clientId}` : `service:${context.serviceId}`
