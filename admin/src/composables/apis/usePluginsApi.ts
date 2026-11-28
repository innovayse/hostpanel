/**
 * Endpoints behind the plugins store: the installed list, ZIP install or upgrade, removal, data purge, the restart and the registry (browse, updates, install).
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { ApiError, BROKEN_STREAM_STATUS, buildQuery, useApi } from '@/composables/useApi'
import { PluginChangeEvent } from '@/types/plugin'
import type {
  InstallRegistryPluginBody,
  PluginActionResultDto,
  PluginChangeListener,
  PluginChangeProgressDto,
  PluginListItemDto,
  PluginPackagePreviewDto,
  PluginRegistryItemDto,
  PluginRegistryPageDto,
  PluginRegistryQuery,
  PluginServersDto,
  PluginSlotWidgetBindingDto,
  RegistryInstanceDto,
  RegistryPluginDetailsDto,
  SaveLicenceKeyBody,
  SavePluginSlotWidgetProductsBody,
} from '@/types/plugin'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'
import type { ServerSentEvent } from '@/types/http'

/**
 * Endpoint functions for `/admin/plugins`.
 *
 * @returns One typed function per endpoint.
 */
export const usePluginsApi = () => {
  const { request, upload, stream } = useApi()

  /**
   * Runs one plugin change as a stream: hands each `progress` event to the listener and resolves with the `done` event's
   * data, or rejects with the `failed` event's error body as an {@link ApiError}. A stream that ends with neither (the
   * server went away mid-change) rejects with a plain error that carries no status.
   *
   * @param endpoint - API path of the change.
   * @param body - The JSON body, a `FormData`, or undefined for none.
   * @param method - The HTTP method.
   * @param onProgress - Told each step as it arrives.
   * @returns The change's answer.
   * @throws {ApiError} The refusal before or during the change.
   */
  const streamChange = async <T>(
    endpoint: string,
    body: unknown,
    method: HttpMethod,
    onProgress: PluginChangeListener,
  ): Promise<T> => {
    /** The last event, once the change has ended. */
    let end: ServerSentEvent | null = null
    await stream(
      endpoint,
      body,
      (event) => {
        if (event.type === PluginChangeEvent.Progress) {
          onProgress(event.data as PluginChangeProgressDto)
        } else if (event.type === PluginChangeEvent.Done || event.type === PluginChangeEvent.Failed) {
          end = event
        }
      },
      new AbortController().signal,
      method,
    )
    const last = end as ServerSentEvent | null
    if (last === null) {
      throw new Error('The plugin change stream ended without an outcome.')
    }
    if (last.type === PluginChangeEvent.Failed) {
      throw new ApiError((last.data as { status?: number } | null)?.status ?? BROKEN_STREAM_STATUS, last.data)
    }
    return last.data as T
  }

  /**
   * Builds the multipart body of an uploaded archive.
   *
   * @param file - The ZIP file.
   * @returns The form with the file under `file`.
   */
  const archiveForm = (file: File): FormData => {
    const form = new FormData()
    form.append('file', file)
    return form
  }

  /**
   * Lists the installed plugins.
   *
   * @returns Every installed plugin.
   */
  const listPlugins = (): Promise<PluginListItemDto[]> =>
    request<PluginListItemDto[]>(ApiPath.AdminPlugins)

  /**
   * Uploads a plugin ZIP for installation, streaming each step of it.
   *
   * @param file - The ZIP file.
   * @param force - True to install a version older than the installed one (a downgrade).
   * @param onProgress - Told each step as it arrives.
   * @returns Whether a restart is needed.
   */
  const installPlugin = (file: File, force: boolean, onProgress: PluginChangeListener): Promise<PluginActionResultDto> =>
    streamChange<PluginActionResultDto>(
      `${ApiPath.AdminPlugins}/install${force ? '?force=true' : ''}`, archiveForm(file), HttpMethod.Post, onProgress)

  /**
   * Reads what a plugin ZIP asks for before it is installed; nothing is installed.
   *
   * @param file - The ZIP file.
   * @returns The package's requested access, changelog and supported server versions.
   */
  const inspectPlugin = (file: File): Promise<PluginPackagePreviewDto> =>
    upload<PluginPackagePreviewDto>(`${ApiPath.AdminPlugins}/inspect`, file)

  /**
   * Removes an installed plugin, streaming each step of it.
   *
   * @param id - Plugin id as declared in plugin.json.
   * @param onProgress - Told each step as it arrives.
   * @returns Whether a restart is needed.
   */
  const removePlugin = (id: string, onProgress: PluginChangeListener): Promise<PluginActionResultDto> =>
    streamChange<PluginActionResultDto>(`${ApiPath.AdminPlugins}/${encodeURIComponent(id)}`, undefined, HttpMethod.Delete, onProgress)

  /**
   * Deletes the data a removed plugin kept.
   *
   * @param id - Plugin id as declared in plugin.json.
   * @returns Whether a restart is needed.
   */
  const purgePluginData = (id: string): Promise<PluginActionResultDto> =>
    request<PluginActionResultDto>(`${ApiPath.AdminPlugins}/${encodeURIComponent(id)}/data`, { method: HttpMethod.Delete })

  /**
   * Asks the server for a graceful restart, streaming the step that says it has begun.
   *
   * @param onProgress - Told each step as it arrives.
   * @returns Nothing; the server may drop the connection before it answers.
   */
  const restartServer = (onProgress: PluginChangeListener): Promise<void> =>
    streamChange<void>(`${ApiPath.AdminPlugins}/restart`, undefined, HttpMethod.Post, onProgress)

  /**
   * Lists the slot widgets one loaded plugin declares, with the products the operator bound each to.
   *
   * @param pluginId - Plugin id as declared in plugin.json.
   * @returns One entry per widget.
   */
  const listSlotWidgets = (pluginId: string): Promise<PluginSlotWidgetBindingDto[]> =>
    request<PluginSlotWidgetBindingDto[]>(`${ApiPath.AdminPlugins}/${encodeURIComponent(pluginId)}/slot-widgets`)

  /**
   * Lists the servers one loaded panel plugin serves: the ones stored on a server module its manifest declares.
   *
   * @param pluginId - Plugin id as declared in plugin.json.
   * @returns The plugin's modules and its servers; both empty for a plugin that serves no server module.
   */
  const listPluginServers = (pluginId: string): Promise<PluginServersDto> =>
    request<PluginServersDto>(`${ApiPath.AdminPlugins}/${encodeURIComponent(pluginId)}/servers`)

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
   * @param query - Search text, category, page and page size.
   * @returns The page; rejects with the registry-disabled code when the registry is switched off.
   */
  const browseRegistry = (query: PluginRegistryQuery): Promise<PluginRegistryPageDto> =>
    request<PluginRegistryPageDto>(
      `${ApiPath.AdminPlugins}/registry${buildQuery({ search: query.search, category: query.category, page: query.page, pageSize: query.pageSize })}`,
    )

  /**
   * Lists the installed plugins the registry has a newer version of.
   *
   * @returns One entry per plugin with an update.
   */
  const listRegistryUpdates = (): Promise<PluginRegistryItemDto[]> =>
    request<PluginRegistryItemDto[]>(`${ApiPath.AdminPlugins}/registry/updates`)

  /**
   * Installs (or stages an upgrade of) a plugin from the registry, streaming each step of it, the download's bytes included.
   *
   * @param id - Plugin id as listed by the registry.
   * @param body - The version to install.
   * @param onProgress - Told each step as it arrives.
   * @returns Whether a restart is needed, and the staged version for an upgrade.
   */
  const installFromRegistry = (
    id: string,
    body: InstallRegistryPluginBody,
    onProgress: PluginChangeListener,
  ): Promise<PluginActionResultDto> =>
    streamChange<PluginActionResultDto>(
      `${ApiPath.AdminPlugins}/registry/${encodeURIComponent(id)}/install`, body, HttpMethod.Post, onProgress)

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
    listPlugins, inspectPlugin, installPlugin, removePlugin, purgePluginData, restartServer, listSlotWidgets, listPluginServers, saveSlotWidgetProducts,
    browseRegistry, listRegistryUpdates, installFromRegistry, getRegistryInstance, retryRegistryInstance, getRegistryDetails, saveLicenceKey, removeLicenceKey,
  }
}
