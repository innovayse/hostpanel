/**
 * Pinia store for managing installed plugins.
 *
 * Handles list, install or upgrade (multipart ZIP), forced downgrade, remove, purge of the data a
 * removed plugin kept, and restart operations. The only caller of
 * `usePluginsApi`; a refused load is kept as the API's own sentence in `error`, a refused install or
 * remove in `actionError`, never thrown.
 */

import { defineStore } from 'pinia'
import { computed, ref, shallowRef } from 'vue'
import { usePluginsApi } from '@/composables/apis/usePluginsApi'
import { useSettingsApi } from '@/composables/apis/useSettingsApi'
import { errorCode } from '@/lib/errorHandler'
import { runAction } from '@/lib/runAction'
import { PluginErrorCode, PluginStatus } from '@/types/plugin'
import type { PluginListItemDto, PluginSlotWidgetBindingDto } from '@/types/plugin'
import type { Product } from '@/types/product'

/** Holds the installed plugin list and whether a restart is pending. */
export const usePluginsStore = defineStore('plugins', () => {
  const api = usePluginsApi()

  /** Products API: the only source of the products a widget can be bound to. */
  const productsApi = useSettingsApi()

  /** Installed plugin list, populated by {@link fetchAll}. */
  const plugins = ref<PluginListItemDto[]>([])

  /** True while any request is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed load, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last refused install or remove, null when none. */
  const actionError = ref<string | null>(null)

  /** Slot widgets with their product binding, by plugin id; filled by {@link fetchSlotWidgets}. */
  const slotWidgets = ref<Record<string, PluginSlotWidgetBindingDto[]>>({})

  /** Every product a widget can be bound to, populated by {@link fetchSlotWidgets}. */
  const productOptions = ref<Product[]>([])

  /** Sentence for the last failed load of the slot widgets or the products, null when none. */
  const slotWidgetsError = ref<string | null>(null)

  /** Sentence for the last refused binding save, null when none. */
  const bindingError = ref<string | null>(null)

  /**
   * Whether a server restart is required for pending plugin changes to take effect.
   * Set to true after a successful install or remove, cleared after restart.
   */
  const requiresRestart = ref(false)

  /**
   * The archive the server refused as a downgrade, kept so the operator can force it after a
   * confirmation; null when no upload waits for that decision.
   */
  const downgradeFile = shallowRef<File | null>(null)

  /** Version the last install staged for the next restart, null when it staged nothing. */
  const stagedVersion = ref<string | null>(null)

  /** The plugins that are on disk (loaded or refused): every row except a removed plugin that only keeps data. */
  const installed = computed<PluginListItemDto[]>(() => plugins.value.filter(p => p.status !== PluginStatus.Removed))

  /** Ids of removed plugins whose stored data the server still keeps; each can be purged. */
  const keptDataIds = computed<string[]>(() => plugins.value.filter(p => p.status === PluginStatus.Removed).map(p => p.id))

  /**
   * Uploads one archive and records whether the server refused it as a downgrade.
   *
   * @param file - The ZIP file.
   * @param force - True to allow an older version than the installed one.
   * @returns The restart flag the server answered with.
   * @throws {ApiError} The refusal, rethrown so the caller's action runner keeps its sentence.
   */
  const uploadArchive = async (file: File, force: boolean) => {
    try {
      return await api.installPlugin(file, force)
    } catch (cause) {
      if (errorCode(cause) === PluginErrorCode.DowngradeRefused) {
        downgradeFile.value = file
      }
      throw cause
    }
  }

  /**
   * Fetches the list of all installed plugins.
   *
   * @returns Resolves when the request is over; a refusal is left in `error`.
   */
  const fetchAll = async (): Promise<void> => {
    loading.value = true
    const result = await runAction(error, api.listPlugins)
    loading.value = false
    if (result.ok) {
      plugins.value = result.value
    }
  }

  /**
   * Uploads and installs a plugin from a ZIP file, then refreshes the list. A newer version of an
   * installed plugin is staged by the server (see `stagedVersion` on the list item) and applied at
   * the restart. An older version is refused; the archive is then kept in `downgradeFile`.
   *
   * @param file - The ZIP file selected by the admin.
   * @param force - True to install a version older than the installed one.
   * @returns True when installed; false when refused (see `actionError`).
   */
  const install = async (file: File, force = false): Promise<boolean> => {
    loading.value = true
    downgradeFile.value = null
    stagedVersion.value = null
    const result = await runAction(actionError, () => uploadArchive(file, force))
    loading.value = false
    if (result.ok) {
      requiresRestart.value = result.value.requiresRestart
      stagedVersion.value = result.value.stagedVersion ?? null
      await fetchAll()
    }
    return result.ok
  }

  /**
   * Installs the archive the server refused as a downgrade, now that the operator confirmed it.
   *
   * @returns True when installed; false when nothing waits or the server refused again.
   */
  const forceDowngrade = async (): Promise<boolean> => {
    const file = downgradeFile.value
    if (file === null) {
      return false
    }
    return install(file, true)
  }

  /** Drops the refused downgrade without installing it. */
  const dismissDowngrade = (): void => {
    downgradeFile.value = null
  }

  /**
   * Removes an installed plugin by its identifier.
   *
   * @param id - Plugin identifier as declared in plugin.json.
   * @returns True when removed; false when refused (see `actionError`).
   */
  const remove = async (id: string): Promise<boolean> => {
    loading.value = true
    const result = await runAction(actionError, () => api.removePlugin(id))
    loading.value = false
    if (result.ok) {
      // Only ever raised here: a removal that needs no restart (the files were already gone) must not hide the banner
      // an earlier install or removal still needs.
      if (result.value.requiresRestart) {
        requiresRestart.value = true
      }
      // Removal keeps the plugin's data, so the reloaded list answers with a Removed row the operator can purge.
      await fetchAll()
    }
    return result.ok
  }

  /**
   * Deletes the data a removed plugin kept.
   *
   * @param id - Plugin identifier as declared in plugin.json.
   * @returns True when purged; false when refused (see `actionError`).
   */
  const purge = async (id: string): Promise<boolean> => {
    loading.value = true
    const result = await runAction(actionError, () => api.purgePluginData(id))
    loading.value = false
    if (result.ok) {
      await fetchAll()
    }
    return result.ok
  }

  /**
   * Triggers a graceful server restart so installed/removed plugins take effect.
   * After calling this, poll GET /health until the server responds.
   *
   * @returns Resolves when the restart request is over.
   */
  const restart = async (): Promise<void> => {
    loading.value = true
    /** Local sink: a server that drops the connection while restarting is expected, not an error. */
    const ignored = ref<string | null>(null)
    await runAction(ignored, api.restartServer)
    requiresRestart.value = false
    loading.value = false
  }

  /**
   * Fetches the slot widgets of every loaded plugin with their product binding, then the products
   * they can be bound to. A plugin that is not loaded declares nothing the server can list.
   *
   * @returns Resolves when the requests are over; a refusal is left in `slotWidgetsError`.
   */
  const fetchSlotWidgets = async (): Promise<void> => {
    const byPlugin: Record<string, PluginSlotWidgetBindingDto[]> = {}
    for (const plugin of plugins.value.filter(p => p.isLoaded)) {
      const widgets = await runAction(slotWidgetsError, () => api.listSlotWidgets(plugin.id))
      if (!widgets.ok) {
        return
      }
      byPlugin[plugin.id] = widgets.value
    }
    slotWidgets.value = byPlugin
    const products = await runAction(slotWidgetsError, productsApi.listProducts)
    if (products.ok) {
      productOptions.value = products.value
    }
  }

  /**
   * Saves the products one slot widget is bound to, then reloads the widgets so the list shows
   * what the server kept (unknown or deleted products are dropped on save).
   *
   * @param pluginId - Plugin that declares the widget.
   * @param widget - The widget being bound.
   * @param productIds - Product ids to bind it to; empty clears the operator binding.
   * @returns True when saved; false when refused (see `bindingError`).
   */
  const saveSlotWidgetProducts = async (
    pluginId: string,
    widget: PluginSlotWidgetBindingDto,
    productIds: number[],
  ): Promise<boolean> => {
    const result = await runAction(
      bindingError,
      () => api.saveSlotWidgetProducts(pluginId, widget.slotId, widget.widgetId, { productIds }),
    )
    if (result.ok) {
      await fetchSlotWidgets()
    }
    return result.ok
  }

  return {
    plugins, loading, error, actionError, requiresRestart, downgradeFile, stagedVersion, installed, keptDataIds, slotWidgets, productOptions, slotWidgetsError, bindingError,
    fetchAll, install, forceDowngrade, dismissDowngrade, remove, purge, restart, fetchSlotWidgets, saveSlotWidgetProducts,
  }
})
