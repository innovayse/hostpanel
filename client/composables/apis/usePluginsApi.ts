/**
 * Endpoints under `/api/portal/client/plugins`: the client area's plugin index, plugin pages, the
 * slots plugins fill on host pages, and the plugins' own routes, JSON and streamed.
 *
 * The list of what that area offers, and nothing else: state, loading flags and error sentences
 * live in `stores/plugins.ts`, the only intended caller.
 *
 * @module composables/apis/usePluginsApi
 */

import { apiFetch, apiStream } from '~/composables/useApi'
import type { HttpMethod } from '~/types/httpmethod'
import type { PluginPageView } from '~/types/pluginpageview'
import type { PluginSlotIndex } from '~/types/pluginslotindex'
import type { PluginSlotWidgetView } from '~/types/pluginslotwidgetview'
import type { PluginUi } from '~/types/pluginui'

/** The root of the plugin endpoints of the BFF. */
const PLUGINS_PATH = '/api/portal/client/plugins'

/**
 * The `/api/portal/client/plugins` surface, one function per endpoint.
 *
 * @returns The plugin endpoint functions.
 */
export const usePluginsApi = () => {
  /**
   * Reads the plugin menu items and pages the signed-in person may open.
   *
   * @returns The index; empty lists with no plugin.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchUi = (): Promise<PluginUi> =>
    apiFetch<PluginUi>(`${PLUGINS_PATH}/ui`)

  /**
   * Reads the plugin page an address names, with its view.
   *
   * @param pluginId - The plugin id.
   * @param path - The address below `/client/apps/<plugin>`, with its leading `/`, decoded: it travels as a query
   *   value, which the transport encodes once.
   * @returns The page.
   * @throws Whatever `apiFetch` throws, e.g. the `PLUGIN_PAGE_NOT_FOUND` refusal.
   */
  const fetchPageByPath = (pluginId: string, path: string): Promise<PluginPageView> =>
    apiFetch<PluginPageView>(`${PLUGINS_PATH}/${encodeURIComponent(pluginId)}/page`, { query: { path } })

  /**
   * Reads the widget index of one slot.
   *
   * @param slotId - The slot id, e.g. `client.service.tabs`.
   * @param context - The slot's context, e.g. `{ serviceId: '4' }`.
   * @returns The index; its widgets empty with no plugin.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchSlot = (slotId: string, context: Record<string, string>): Promise<PluginSlotIndex> =>
    apiFetch<PluginSlotIndex>(`${PLUGINS_PATH}/slots/${encodeURIComponent(slotId)}`, { query: context })

  /**
   * Reads one slot widget's view.
   *
   * @param slotId - The slot id.
   * @param pluginId - The plugin id.
   * @param widgetId - The widget id.
   * @param context - The slot's context.
   * @returns The widget with its view.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchSlotWidget = (
    slotId: string,
    pluginId: string,
    widgetId: string,
    context: Record<string, string>
  ): Promise<PluginSlotWidgetView> =>
    apiFetch<PluginSlotWidgetView>(
      `${PLUGINS_PATH}/slots/${encodeURIComponent(slotId)}/widgets/${encodeURIComponent(pluginId)}/${encodeURIComponent(widgetId)}`,
      { query: context })

  /**
   * Calls one of a plugin's JSON routes.
   *
   * @param pluginId - The plugin id.
   * @param method - The route's method.
   * @param path - The route's path, its parameters already filled and encoded.
   * @param body - The JSON body of a POST, PUT or PATCH.
   * @returns The route's reply; null for no content.
   * @throws Whatever `apiFetch` throws: the plugin's refusal carries its own sentence.
   */
  const callRoute = (pluginId: string, method: HttpMethod, path: string, body?: Record<string, unknown>): Promise<unknown> =>
    apiFetch<unknown>(`${PLUGINS_PATH}/${encodeURIComponent(pluginId)}/routes/${path}`, { method, body })

  /**
   * Opens one of a plugin's streaming routes.
   *
   * @param pluginId - The plugin id.
   * @param path - The route's path, its parameters already filled and encoded.
   * @param body - The JSON body.
   * @param signal - Aborting it closes the stream.
   * @returns The event stream.
   * @throws Whatever `apiStream` throws.
   */
  const openStream = (pluginId: string, path: string, body: Record<string, unknown>, signal: AbortSignal): Promise<ReadableStream<Uint8Array>> =>
    apiStream(`${PLUGINS_PATH}/${encodeURIComponent(pluginId)}/stream/${path}`, body, signal)

  return { fetchUi, fetchPageByPath, fetchSlot, fetchSlotWidget, callRoute, openStream }
}
