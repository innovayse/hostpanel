/**
 * GET /api/portal/client/plugins/slots/:slot/widgets/:plugin/:widget?serviceId=
 * One slot widget with its view document, labels in the reader's language
 * (`GET /api/client/plugins/slots/{slot}/widgets/{plugin}/{widget}`). The backend serves it only
 * when the slot index would list it for the same person and context; this route only checks shapes.
 *
 * @module server/api/portal/client/plugins/slots/[slot]/widgets/[plugin]/[widget].get
 */

import { readSlotQuery } from '~/server/utils/slotQuery'
import type { PluginSlotWidgetView } from '~/types/pluginslotwidgetview'
import { RouteParamKind } from '~/types/routeparamkind'

export default defineEventHandler((event): Promise<PluginSlotWidgetView> => {
  const slot = requireRouteParam(event, 'slot', RouteParamKind.DottedName)
  const plugin = requireRouteParam(event, 'plugin', RouteParamKind.PluginId)
  const widget = requireRouteParam(event, 'widget', RouteParamKind.PluginWidgetId)
  return internalApiCall<PluginSlotWidgetView>(event, PluginBackendPaths.slotWidget(slot, plugin, widget, readSlotQuery(event)))
})
