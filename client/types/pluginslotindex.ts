/**
 * The widgets plugins put into one slot, as `GET /api/client/plugins/slots/{slotId}` answers.
 *
 * @module types/pluginslotindex
 */

import type { PluginSlotWidget } from '~/types/pluginslotwidget'

/** The index of one slot. */
export interface PluginSlotIndex {
  /** The slot id, e.g. `client.service.tabs`. */
  slotId: string
  /** The widgets in display order; empty with no plugin. */
  widgets: PluginSlotWidget[]
}
