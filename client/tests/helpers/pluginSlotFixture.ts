/**
 * Slot fixtures for the slot tests, in the backend's wire shapes: index entries, the widget views a
 * stub backend answers with (one that works, one that throws while it renders, one whose first data
 * the test holds back), and the BFF paths they are read from.
 *
 * @module tests/helpers/pluginSlotFixture
 */

import { PluginBlockKind } from '~/types/pluginblockkind'
import type { PluginBlock } from '~/types/pluginblock'
import type { PluginSlotId } from '~/types/pluginslotid'
import type { PluginSlotIndex } from '~/types/pluginslotindex'
import type { PluginSlotWidget } from '~/types/pluginslotwidget'
import type { PluginSlotWidgetView } from '~/types/pluginslotwidgetview'

/** The fake plugin every fixture belongs to. */
export const SLOT_PLUGIN = 'acme-sites'

/**
 * The BFF path of a slot's index.
 *
 * @param slotId - The slot id.
 * @returns The path, without query.
 */
export const slotPath = (slotId: PluginSlotId): string => `/api/portal/client/plugins/slots/${slotId}`

/**
 * The BFF path of one widget's view.
 *
 * @param slotId - The slot id.
 * @param widgetId - The widget id.
 * @returns The path, without query.
 */
export const slotWidgetPath = (slotId: PluginSlotId, widgetId: string): string =>
  `${slotPath(slotId)}/widgets/${SLOT_PLUGIN}/${widgetId}`

/** The BFF path the reading widget's status route is called on. */
export const STATUS_ROUTE_PREFIX = `/api/portal/client/plugins/${SLOT_PLUGIN}/routes/`

/**
 * A widget as the slot index lists it.
 *
 * @param id - The widget id.
 * @param title - The title; `Widget <id>` when omitted, null for a widget with none.
 * @returns The index entry.
 */
export const slotEntry = (id: string, title: string | null = `Widget ${id}`): PluginSlotWidget => ({
  pluginId: SLOT_PLUGIN,
  id,
  view: `views/${id}.json`,
  title,
  order: 0,
  contextKeys: ['serviceId'],
})

/**
 * A slot index listing the given widgets.
 *
 * @param slotId - The slot id.
 * @param ids - The widget ids, in order.
 * @returns The index.
 */
export const slotIndex = (slotId: PluginSlotId, ids: string[]): PluginSlotIndex => ({
  slotId,
  widgets: ids.map((id, order) => ({ ...slotEntry(id), order })),
})

/**
 * A widget view with the given blocks.
 *
 * @param id - The widget id.
 * @param blocks - The view's blocks.
 * @returns The widget view.
 */
export const slotView = (id: string, blocks: PluginBlock[]): PluginSlotWidgetView => ({
  pluginId: SLOT_PLUGIN,
  id,
  title: `Widget ${id}`,
  externalOrigins: [],
  view: { schema: 1, blocks },
})

/**
 * A widget that shows text.
 *
 * @returns The view.
 */
export const workingView = (): PluginSlotWidgetView => slotView('ok', [{ kind: PluginBlockKind.Text, text: 'Site is live' }])

/**
 * A widget whose tabs block has no tab list, so it throws while it is set up.
 *
 * @returns The view.
 */
export const throwingView = (): PluginSlotWidgetView =>
  slotView('broken', [{ kind: PluginBlockKind.Tabs, tabs: null } as never])

/**
 * A widget whose stat reads its status from a plugin route.
 *
 * @returns The view.
 */
export const readingView = (): PluginSlotWidgetView =>
  slotView('slow', [{ kind: PluginBlockKind.Stat, label: 'Status', source: { route: { method: 'get', path: '/sites/status' }, field: 'status' } }])

/** The backend's refusal of a widget it would not list for this caller and context. */
export const WIDGET_NOT_FOUND = {
  status: 404,
  body: { type: 'PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND', title: 'Not Found', status: 404, detail: 'This widget is not available.' },
}

/** The backend's refusal of a service that is not the reader's, as the BFF passes it on. */
export const SERVICE_NOT_FOUND = {
  status: 404,
  body: { type: 'SERVICE_NOT_FOUND', title: 'Not Found', status: 404, detail: 'The service was not found.' },
}

