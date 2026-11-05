/**
 * One plugin widget in a host slot, as the slot index `GET /api/client/plugins/slots/{slotId}` lists it. The index
 * carries no view document: each widget's view is read on its own (`PluginSlotWidgetView`), so one slow or broken
 * widget never holds up the others.
 *
 * @module types/pluginslotwidget
 */

/** One slot widget, as the index lists it. */
export interface PluginSlotWidget {
  /** The plugin that declares the widget. */
  pluginId: string
  /** The widget id, unique within the plugin and slot. */
  id: string
  /** The path of the widget's view document inside the plugin package; the host reads it, the page never does. */
  view: string
  /** The title, in the reader's language; null when the widget has none. */
  title: string | null
  /** The position among the slot's widgets, lowest first; the index is already in this order. */
  order: number
  /** The context keys the widget receives, e.g. `serviceId`; the host verified them. */
  contextKeys: string[]
}
