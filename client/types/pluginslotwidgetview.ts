/**
 * One slot widget with its view document, as `GET /api/client/plugins/slots/{slotId}/widgets/{pluginId}/{widgetId}`
 * answers. The view has the same shape as a plugin page's view and renders with the same components.
 *
 * @module types/pluginslotwidgetview
 */

import type { PluginView } from '~/types/pluginview'

/** One slot widget with its view. */
export interface PluginSlotWidgetView {
  /** The plugin id. */
  pluginId: string
  /** The widget id. */
  id: string
  /** The title, in the reader's language; null when the widget has none. */
  title: string | null
  /** The web origins the widget's links may open (the manifest's `externalOrigins`); empty allows host-relative links only. */
  externalOrigins: string[]
  /** The view document, its labels resolved by the host. */
  view: PluginView
}
