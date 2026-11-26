/**
 * Endpoint behind the plugin slots store: the widgets the host lists for one slot of a page.
 *
 * No state lives here; the function rejects with an `ApiError` carrying the problem+json body.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import { ApiPath } from '@/lib/apiPaths'
import type { PluginSlotContext, PluginSlotId, PluginSlotIndexDto, PluginSlotWidgetViewDto } from '@/types/pluginslot'

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
   * @returns The slot's index: its id and its widgets in display order, empty when no plugin offers one.
   */
  const getSlot = (slotId: PluginSlotId, context: PluginSlotContext): Promise<PluginSlotIndexDto> =>
    request<PluginSlotIndexDto>(`${ApiPath.AdminPluginSlots}/${slotId}${buildQuery({ ...context })}`)

  /**
   * Reads one widget of a slot with its view document. The host checks the record, the widget's plugin and the
   * operator's right before the plugin is asked for the view.
   *
   * @param slotId - Id of the slot.
   * @param pluginId - Id of the plugin that declares the widget.
   * @param widgetId - Id of the widget inside its plugin.
   * @param context - The client or service the page shows.
   * @returns The widget with its view, its labels in the operator's language.
   */
  const getSlotWidget = (
    slotId: PluginSlotId,
    pluginId: string,
    widgetId: string,
    context: PluginSlotContext,
  ): Promise<PluginSlotWidgetViewDto> =>
    request<PluginSlotWidgetViewDto>(
      `${ApiPath.AdminPluginSlots}/${slotId}/widgets/${encodeURIComponent(pluginId)}/${encodeURIComponent(widgetId)}${buildQuery({ ...context })}`,
    )

  return { getSlot, getSlotWidget }
}
