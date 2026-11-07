/**
 * Endpoints behind the plugins store: the installed list, ZIP install or upgrade, removal, data purge, the restart and the registry (browse, updates, install).
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import type {
  InstallRegistryPluginBody,
  PluginActionResultDto,
  PluginListItemDto,
  PluginRegistryItemDto,
  PluginRegistryPageDto,
  PluginRegistryQuery,
  PluginSlotWidgetBindingDto,
  SavePluginSlotWidgetProductsBody,
} from '@/types/plugin'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'

/**
 * Endpoint functions for `/admin/plugins`.
 *
 * @returns One typed function per endpoint.
 */
export const usePluginsApi = () => {
  const { request, upload } = useApi()

  /**
   * Lists the installed plugins.
   *
   * @returns Every installed plugin.
   */
  const listPlugins = (): Promise<PluginListItemDto[]> =>
    request<PluginListItemDto[]>(ApiPath.AdminPlugins)

  /**
   * Uploads a plugin ZIP for installation.
   *
   * @param file - The ZIP file.
   * @param force - True to install a version older than the installed one (a downgrade).
   * @returns Whether a restart is needed.
   */
  const installPlugin = (file: File, force = false): Promise<PluginActionResultDto> =>
    upload<PluginActionResultDto>(`${ApiPath.AdminPlugins}/install${force ? '?force=true' : ''}`, file)

  /**
   * Removes an installed plugin.
   *
   * @param id - Plugin id as declared in plugin.json.
   * @returns Whether a restart is needed.
   */
  const removePlugin = (id: string): Promise<PluginActionResultDto> =>
    request<PluginActionResultDto>(`${ApiPath.AdminPlugins}/${id}`, { method: HttpMethod.Delete })

  /**
   * Deletes the data a removed plugin kept.
   *
   * @param id - Plugin id as declared in plugin.json.
   * @returns Whether a restart is needed.
   */
  const purgePluginData = (id: string): Promise<PluginActionResultDto> =>
    request<PluginActionResultDto>(`${ApiPath.AdminPlugins}/${id}/data`, { method: HttpMethod.Delete })

  /**
   * Asks the server for a graceful restart.
   *
   * @returns Nothing; the server may drop the connection before it answers.
   */
  const restartServer = (): Promise<void> =>
    request<void>(`${ApiPath.AdminPlugins}/restart`, { method: HttpMethod.Post })

  /**
   * Lists the slot widgets one loaded plugin declares, with the products the operator bound each to.
   *
   * @param pluginId - Plugin id as declared in plugin.json.
   * @returns One entry per widget.
   */
  const listSlotWidgets = (pluginId: string): Promise<PluginSlotWidgetBindingDto[]> =>
    request<PluginSlotWidgetBindingDto[]>(`${ApiPath.AdminPlugins}/${pluginId}/slot-widgets`)

  /**
   * Saves the products one slot widget is bound to.
   *
   * @param pluginId - Plugin id as declared in plugin.json.
   * @param slotId - Id of the slot the widget sits in.
   * @param widgetId - Widget id within the plugin.
   * @param body - The product ids; empty clears the binding.
   * @returns The widget with the products the server stored.
   */
  const saveSlotWidgetProducts = (
    pluginId: string,
    slotId: string,
    widgetId: string,
    body: SavePluginSlotWidgetProductsBody,
  ): Promise<PluginSlotWidgetBindingDto> =>
    request<PluginSlotWidgetBindingDto>(`${ApiPath.AdminPlugins}/${pluginId}/slot-widgets/${slotId}/${widgetId}`, {
      method: HttpMethod.Put,
      body: JSON.stringify(body),
    })

  /**
   * Lists one page of the plugins the registry offers.
   *
   * @param query - Search text, category and page.
   * @returns The page; rejects with the registry-disabled code when the registry is switched off.
   */
  const browseRegistry = (query: PluginRegistryQuery): Promise<PluginRegistryPageDto> =>
    request<PluginRegistryPageDto>(
      `${ApiPath.AdminPlugins}/registry${buildQuery({ q: query.q, category: query.category, page: query.page })}`,
    )

  /**
   * Lists the installed plugins the registry has a newer version of.
   *
   * @returns One entry per plugin with an update.
   */
  const listRegistryUpdates = (): Promise<PluginRegistryItemDto[]> =>
    request<PluginRegistryItemDto[]>(`${ApiPath.AdminPlugins}/registry/updates`)

  /**
   * Installs (or stages an upgrade of) a plugin from the registry.
   *
   * @param id - Plugin id as listed by the registry.
   * @param body - The version to install.
   * @returns Whether a restart is needed, and the staged version for an upgrade.
   */
  const installFromRegistry = (id: string, body: InstallRegistryPluginBody): Promise<PluginActionResultDto> =>
    request<PluginActionResultDto>(`${ApiPath.AdminPlugins}/registry/${id}/install`, {
      method: HttpMethod.Post,
      body: JSON.stringify(body),
    })

  return {
    listPlugins, installPlugin, removePlugin, purgePluginData, restartServer, listSlotWidgets, saveSlotWidgetProducts,
    browseRegistry, listRegistryUpdates, installFromRegistry,
  }
}
