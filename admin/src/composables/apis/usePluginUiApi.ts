/**
 * Endpoints behind the plugin ui store: the index of the pages and menu items plugins add to the admin
 * panel, one page by its address, and the calls and streams a page's blocks make to the plugin's own
 * admin routes (`/admin/plugins/{id}/ext/{path}`).
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import { ApiPath } from '@/lib/apiPaths'
import type { HttpMethod, ServerSentEvent } from '@/types/http'
import type { PluginPageViewDto, PluginUiDto } from '@/types/pluginui'

/**
 * The address of a plugin's admin route.
 *
 * @param pluginId - Id of the plugin.
 * @param path - The filled path below the plugin's prefix, with its query, without a leading `/`.
 * @returns `/admin/plugins/<id>/ext/<path>`.
 */
const extPath = (pluginId: string, path: string): string => `${ApiPath.AdminPlugins}/${encodeURIComponent(pluginId)}/ext/${path}`

/**
 * Endpoint functions for `/admin/plugins/ui` and the plugin routes.
 *
 * @returns One typed function per endpoint.
 */
export const usePluginUiApi = () => {
  const { request, stream } = useApi()

  /**
   * Reads the plugin ui index: the menu items and pages of every available plugin, labels in the operator's language.
   *
   * @returns The index; both lists empty with no available plugin.
   */
  const fetchUi = (): Promise<PluginUiDto> => request<PluginUiDto>(ApiPath.AdminPluginUi)

  /**
   * Reads the page a plugin declares for an address.
   *
   * @param pluginId - Id of the plugin.
   * @param path - The address below the plugin's prefix, decoded, with its leading `/`.
   * @returns The page with its view document.
   */
  const fetchPageByPath = (pluginId: string, path: string): Promise<PluginPageViewDto> =>
    request<PluginPageViewDto>(`${ApiPath.AdminPluginUi}/${encodeURIComponent(pluginId)}/pages/by-path${buildQuery({ path })}`)

  /**
   * Calls one of a plugin's JSON admin routes.
   *
   * @param pluginId - Id of the plugin.
   * @param method - The method.
   * @param path - The filled path with its query, without a leading `/`.
   * @param body - The JSON body, for a method that sends one.
   * @returns The plugin's reply; undefined for an empty one.
   */
  const callRoute = (pluginId: string, method: HttpMethod, path: string, body?: unknown): Promise<unknown> =>
    request<unknown>(extPath(pluginId, path), { method, ...(body === undefined ? {} : { body: JSON.stringify(body) }) })

  /**
   * Opens one of a plugin's streaming admin routes with a POST and hands each event over.
   *
   * @param pluginId - Id of the plugin.
   * @param path - The filled path, without a leading `/`.
   * @param body - The JSON body.
   * @param onEvent - Called with each event, in order.
   * @param signal - Aborting it closes the stream.
   * @returns Resolves when the stream ended or was aborted.
   */
  const openStream = (
    pluginId: string,
    path: string,
    body: unknown,
    onEvent: (event: ServerSentEvent) => void,
    signal: AbortSignal,
  ): Promise<void> => stream(extPath(pluginId, path), body, onEvent, signal)

  return { fetchUi, fetchPageByPath, callRoute, openStream }
}
