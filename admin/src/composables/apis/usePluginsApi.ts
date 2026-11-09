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
  PluginPackagePreviewDto,
  PluginRegistryItemDto,
  PluginRegistryPageDto,
  PluginRegistryQuery,
  PluginSlotWidgetBindingDto,
  RegistryInstanceDto,
  RegistryPluginDetailsDto,
  SaveLicenceKeyBody,
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
   * Reads what a plugin ZIP asks for before it is installed; nothing is installed.
   *
   * @param file - The ZIP file.
   * @returns The package's requested access, changelog and supported server versions.
   */
  const inspectPlugin = (file: File): Promise<PluginPackagePreviewDto> =>
    upload<PluginPackagePreviewDto>(`${ApiPath.AdminPlugins}/inspect`, file)

  /**
   * Removes an installed plugin.
   *
   * @param id - Plugin id as declared in plugin.json.
   * @returns Whether a restart is needed.
   */
  const removePlugin = (id: string): Promise<PluginActionResultDto> =>
    request<PluginActionResultDto>(`${ApiPath.AdminPlugins}/${encodeURIComponent(id)}`, { method: HttpMethod.Delete })

  /**
   * Deletes the data a removed plugin kept.
   *
   * @param id - Plugin id as declared in plugin.json.
   * @returns Whether a restart is needed.
   */
  const purgePluginData = (id: string): Promise<PluginActionResultDto> =>
    request<PluginActionResultDto>(`${ApiPath.AdminPlugins}/${encodeURIComponent(id)}/data`, { method: HttpMethod.Delete })

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
    request<PluginSlotWidgetBindingDto[]>(`${ApiPath.AdminPlugins}/${encodeURIComponent(pluginId)}/slot-widgets`)

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
    request<PluginSlotWidgetBindingDto>(`${ApiPath.AdminPlugins}/${encodeURIComponent(pluginId)}/slot-widgets/${encodeURIComponent(slotId)}/${encodeURIComponent(widgetId)}`, {
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
    request<PluginActionResultDto>(`${ApiPath.AdminPlugins}/registry/${encodeURIComponent(id)}/install`, {
      method: HttpMethod.Post,
      body: JSON.stringify(body),
    })

  /**
   * Reads where this server stands with the plugin registry.
   *
   * @returns The registration state; rejects with the registry-disabled code when the registry is switched off.
   */
  const getRegistryInstance = (): Promise<RegistryInstanceDto> =>
    request<RegistryInstanceDto>(`${ApiPath.AdminPlugins}/registry-instance`)

  /**
   * Registers this server with the plugin registry again at once, ignoring the pause between attempts.
   *
   * @returns The registration state after the run; a failed run is still an answer, with its code and sentence.
   */
  const retryRegistryInstance = (): Promise<RegistryInstanceDto> =>
    request<RegistryInstanceDto>(`${ApiPath.AdminPlugins}/registry-instance/retry`, { method: HttpMethod.Post })

  /**
   * Reads the details of one registry plugin.
   *
   * @param id - Plugin id as listed by the registry.
   * @returns The details with every version.
   */
  const getRegistryDetails = (id: string): Promise<RegistryPluginDetailsDto> =>
    request<RegistryPluginDetailsDto>(`${ApiPath.AdminPlugins}/registry/${encodeURIComponent(id)}`)

  /**
   * Stores the licence key of a registry plugin.
   *
   * @param id - Plugin id as listed by the registry.
   * @param body - The key.
   * @returns Resolves when stored.
   */
  const saveLicenceKey = (id: string, body: SaveLicenceKeyBody): Promise<void> =>
    request<void>(`${ApiPath.AdminPlugins}/registry/${encodeURIComponent(id)}/licence`, {
      method: HttpMethod.Put,
      body: JSON.stringify(body),
    })

  /**
   * Removes the stored licence key of a registry plugin.
   *
   * @param id - Plugin id as listed by the registry.
   * @returns Resolves when removed.
   */
  const removeLicenceKey = (id: string): Promise<void> =>
    request<void>(`${ApiPath.AdminPlugins}/registry/${encodeURIComponent(id)}/licence`, { method: HttpMethod.Delete })

  return {
    listPlugins, inspectPlugin, installPlugin, removePlugin, purgePluginData, restartServer, listSlotWidgets, saveSlotWidgetProducts,
    browseRegistry, listRegistryUpdates, installFromRegistry, getRegistryInstance, retryRegistryInstance, getRegistryDetails, saveLicenceKey, removeLicenceKey,
  }
}
