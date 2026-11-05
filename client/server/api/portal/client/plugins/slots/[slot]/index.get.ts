/**
 * GET /api/portal/client/plugins/slots/:slot?serviceId=
 * The plugin widgets of one client area slot for the signed-in person, titles in their language
 * (`GET /api/client/plugins/slots/{slot}`). The backend verifies the service is theirs before it
 * answers; this route only checks the shapes it forwards.
 *
 * @module server/api/portal/client/plugins/slots/[slot]/index.get
 */

import { readSlotQuery } from '~/server/utils/slotQuery'
import type { PluginSlotIndex } from '~/types/pluginslotindex'
import { RouteParamKind } from '~/types/routeparamkind'

export default defineEventHandler((event): Promise<PluginSlotIndex> => {
  const slot = requireRouteParam(event, 'slot', RouteParamKind.DottedName)
  return internalApiCall<PluginSlotIndex>(event, PluginBackendPaths.slot(slot, readSlotQuery(event)))
})
