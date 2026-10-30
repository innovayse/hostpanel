/**
 * Endpoint behind the plugin slots store: the widgets the host lists for one slot of a page.
 *
 * No state lives here; the function rejects with an `ApiError` carrying the problem+json body.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import { ApiPath } from '@/lib/apiPaths'
import type { PluginSlotContext, PluginSlotId, PluginSlotWidgetDto } from '@/types/pluginslot'

/**
 * Endpoint functions for `/admin/plugins/slots`.
 *
 * @returns One typed function per endpoint.
 */
export const usePluginSlotsApi = () => {
  const { request } = useApi()

  /**
   * Lists the widgets of one slot for one client or service. The host checks that the record
   * exists and filters the widgets before any plugin is called.
   *
   * @param slotId - Id of the slot.
   * @param context - The client or service the page shows.
   * @returns The widgets in display order, empty when no plugin offers one.
   */
  const getSlot = (slotId: PluginSlotId, context: PluginSlotContext): Promise<PluginSlotWidgetDto[]> =>
    request<PluginSlotWidgetDto[]>(`${ApiPath.AdminPluginSlots}/${slotId}${buildQuery({ ...context })}`)

  return { getSlot }
}
