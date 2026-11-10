/**
 * Pinia store for managing installed plugins.
 *
 * Handles list, the preview of a ZIP before its install (what it asks for), install or upgrade (multipart ZIP), forced downgrade, remove, purge of the data a
 * removed plugin kept, restart operations and the registry (browse with search, category and page,
 * the update list, install or update from it). The only caller of
 * `usePluginsApi`; a refused load is kept as the API's own sentence in `error`, a refused install or
 * remove in `actionError`, never thrown.
 */

import { defineStore } from 'pinia'
import { computed, ref, shallowRef } from 'vue'
import type { Ref } from 'vue'
import { usePluginsApi } from '@/composables/apis/usePluginsApi'
import { useSettingsApi } from '@/composables/apis/useSettingsApi'
import { errorCode, errorStatus } from '@/lib/errorHandler'
import { runAction } from '@/lib/runAction'
import { PluginErrorCode, PluginStatus } from '@/types/plugin'
import type {
  PluginActionOutcome,
  PluginListItemDto,
  PluginPackagePreviewDto,
  PluginRegistryItemDto,
  PluginRegistryQuery,
  RegistryInstanceDto,
  PluginSlotWidgetBindingDto,
  RegistryDetailsState,
} from '@/types/plugin'
import type { Product } from '@/types/product'

/** HTTP status of an answer that names a missing resource. */
const NOT_FOUND_STATUS = 404

/**
 * Whether a refusal says the registry has no such plugin: the 404 status or the registry's not-found code.
 *
 * @param cause - The value caught from a rejected request.
 * @returns True for a not-found refusal.
 */
const isNotFound = (cause: unknown): boolean =>
  errorStatus(cause) === NOT_FOUND_STATUS || errorCode(cause) === PluginErrorCode.RegistryPluginNotFound

/**
 * Runs one action against its own error sink, so the outcome belongs to this call alone; the shared
 * sentence is then set to the same value for callers that only show the latest one.
 *
 * @param shared - The store's shared sentence ref, set to this call's sentence when it ends.
 * @param task - The request to run.
 * @returns The runner's result plus the sentence of this call.
 */
const runOwn = async <T>(
  shared: Ref<string | null>,
  task: () => Promise<T>,
): Promise<{ ok: true; value: T; error: null } | { ok: false; error: string | null }> => {
  /** This call's own sentence. */
  const own = ref<string | null>(null)
  const result = await runAction(own, task)
  shared.value = own.value
  return result.ok ? { ok: true, value: result.value, error: null } : { ok: false, error: own.value }
}

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

  /**
   * The archive the admin picked and the server described, waiting for the admin to confirm the install after reading
   * what it asks for; null when no upload waits for that decision.
   */
  const pendingFile = shallowRef<File | null>(null)

  /** What {@link pendingFile} asks for, as the server read it; null when nothing waits. */
  const preview = ref<PluginPackagePreviewDto | null>(null)

  /** Listings per registry page, as the server answered; 0 until the first page has loaded. */
  const registryPageSize = ref(0)

  /** The registry listings of the current page, populated by {@link fetchRegistry}. */
  const registryItems = ref<PluginRegistryItemDto[]>([])

  /** Listings matching the registry filter across all pages. */
  const registryTotal = ref(0)

  /** The registry filter: search text, category and page. */
  const registryQuery = ref<PluginRegistryQuery>({ q: '', category: '', page: 1 })

  /** True while a registry page is loading. */
  const registryLoading = ref(false)

  /** Sentence for the last failed registry load, null when none. */
  const registryError = ref<string | null>(null)

  /** True when the server answered that the registry is switched off; the Browse tab is then hidden. */
  const registryDisabled = ref(false)

  /** Where this server stands with the registry, populated by {@link fetchRegistryInstance}; null until loaded. */
  const registryInstance = ref<RegistryInstanceDto | null>(null)

  /** True while the registration state is loading or a retry runs. */
  const registryInstanceBusy = ref(false)

  /** Sentence for the last failed load or retry of the registration state, null when none. */
  const registryInstanceError = ref<string | null>(null)

  /** Installed plugins the registry has a newer version of, populated by {@link fetchRegistryUpdates}. */
  const registryUpdates = ref<PluginRegistryItemDto[]>([])

  /** Sentence for the last failed load of the update list, null when none; kept off screen, the badges are only a hint. */
  const registryUpdatesError = ref<string | null>(null)

  /** Sentence for the last refused registry install, null when none. */
  const registryInstallError = ref<string | null>(null)

  /** Plugin ids with a registry action (install or licence key change) in flight; each plugin is busy on its own. */
  const busyIds = ref<Record<string, boolean>>({})

  /** Sentence for the last refused licence key save or removal, null when none. */
  const licenceError = ref<string | null>(null)

  /** Registry details with their load state, by plugin id; filled by {@link fetchRegistryDetails}. */
  const registryDetails = ref<Record<string, RegistryDetailsState>>({})

  /** Id of the first plugin with an action in flight, null when none; kept for callers that show one busy card. */
  const registryInstallingId = computed<string | null>(() => Object.keys(busyIds.value).find(id => busyIds.value[id]) ?? null)

  /** The newer version the registry offers, by installed plugin id; drives the update badge. */
  const updateVersions = computed<Record<string, string>>(() =>
    Object.fromEntries(registryUpdates.value.map(item => [item.id, item.latestVersion])),
  )

  /** The release notes of the newer version the registry offers, by installed plugin id; null when it sent none. */
  const updateChangelogs = computed<Record<string, string | null>>(() =>
    Object.fromEntries(registryUpdates.value.map(item => [item.id, item.latestChangelog ?? null])),
  )

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
   * Asks the server what an archive asks for (host access, capabilities, changelog, supported server versions) and
   * keeps the answer for the confirmation dialog. Nothing is installed.
   *
   * @param file - The ZIP file selected by the admin.
   * @returns True when the archive was described; false when refused (see `actionError`).
   */
  const inspect = async (file: File): Promise<boolean> => {
    loading.value = true
    pendingFile.value = null
    preview.value = null
    const result = await runAction(actionError, () => api.inspectPlugin(file))
    loading.value = false
    if (result.ok) {
      pendingFile.value = file
      preview.value = result.value
    }
    return result.ok
  }

  /**
   * Installs the archive waiting for confirmation, now that the admin confirmed it.
   *
   * @returns True when installed; false when nothing waits or the server refused it.
   */
  const confirmInstall = async (): Promise<boolean> => {
    const file = pendingFile.value
    pendingFile.value = null
    preview.value = null
    if (file === null) {
      return false
    }
    return install(file)
  }

  /** Drops the archive waiting for confirmation without installing it. */
  const dismissPreview = (): void => {
    pendingFile.value = null
    preview.value = null
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
   * Removes an installed plugin by its identifier; the plugin counts as busy (see {@link isBusy}) until the list is reloaded.
   *
   * @param id - Plugin identifier as declared in plugin.json.
   * @returns This call's outcome: `ok`, or the API's sentence in `error` (also left in `actionError`).
   */
  const remove = async (id: string): Promise<PluginActionOutcome> => {
    setBusy(id, true)
    const result = await runOwn(actionError, () => api.removePlugin(id))
    if (result.ok) {
      // Only ever raised here: a removal that needs no restart (the files were already gone) must not hide the banner
      // an earlier install or removal still needs.
      if (result.value.requiresRestart) {
        requiresRestart.value = true
      }
      // Removal keeps the plugin's data, so the reloaded list answers with a Removed row the operator can purge.
      await Promise.all([
        fetchAll(),
        // The Browse tab and the update badges must not keep offering Uninstall for what is gone; only a registry
        // that was already browsed needs the reload.
        ...(registryPageSize.value > 0 ? [fetchRegistry(), fetchRegistryUpdates()] : []),
      ])
    }
    setBusy(id, false)
    return { ok: result.ok, stagedVersion: null, error: result.error }
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

  /**
   * Runs a registry request; the server's "registry is switched off" answer is recorded in
   * `registryDisabled` and read as an empty result instead of a failure.
   *
   * @param task - The registry request.
   * @param whenDisabled - The value to use when the registry is off.
   * @returns The response, or `whenDisabled`.
   * @throws {ApiError} Every other refusal, rethrown so the caller's action runner keeps its sentence.
   */
  const guardRegistry = async <T>(task: () => Promise<T>, whenDisabled: T): Promise<T> => {
    try {
      return await task()
    } catch (cause) {
      if (errorCode(cause) === PluginErrorCode.RegistryDisabled) {
        registryDisabled.value = true
        return whenDisabled
      }
      throw cause
    }
  }

  /**
   * Loads the registry page the current filter names.
   *
   * @returns Resolves when the request is over; a refusal is left in `registryError`.
   */
  const fetchRegistry = async (): Promise<void> => {
    registryLoading.value = true
    const result = await runAction(registryError, () =>
      guardRegistry(() => api.browseRegistry(registryQuery.value), { items: [], total: 0, pageSize: registryPageSize.value }),
    )
    registryLoading.value = false
    // A failed load must not leave the previous page on show beside the error.
    registryItems.value = result.ok ? result.value.items : []
    registryTotal.value = result.ok ? result.value.total : 0
    if (result.ok) {
      registryPageSize.value = result.value.pageSize
    }
  }

  /**
   * Changes the registry filter and reloads. A new search text or category starts again at page 1;
   * a page change keeps the filter.
   *
   * @param change - The members of the filter to change.
   * @returns Resolves when the reload is over.
   */
  const setRegistryFilter = async (change: Partial<PluginRegistryQuery>): Promise<void> => {
    const resetsPage = change.q !== undefined || change.category !== undefined
    registryQuery.value = { ...registryQuery.value, ...(resetsPage ? { page: 1 } : {}), ...change }
    await fetchRegistry()
  }

  /**
   * Loads the installed plugins the registry has an update for. A failure leaves the list empty:
   * the badge is a hint, not a reason to show an error.
   *
   * @returns Resolves when the request is over.
   */
  const fetchRegistryUpdates = async (): Promise<void> => {
    const result = await runAction(registryUpdatesError, () => guardRegistry(api.listRegistryUpdates, []))
    registryUpdates.value = result.ok ? result.value : []
  }

  /**
   * Loads where this server stands with the plugin registry. A registry that is switched off leaves it empty.
   *
   * @returns Resolves when the request is over; a refusal is left in `registryInstanceError`.
   */
  const fetchRegistryInstance = async (): Promise<void> => {
    registryInstanceBusy.value = true
    const result = await runAction(registryInstanceError, () => guardRegistry<RegistryInstanceDto | null>(api.getRegistryInstance, null))
    registryInstanceBusy.value = false
    if (result.ok) {
      registryInstance.value = result.value
    }
  }

  /**
   * Registers this server with the registry again at once. A run that fails is still an answer: the state it
   * returns carries the code and the sentence, and only a refusal of the call itself lands in `registryInstanceError`.
   *
   * @returns Resolves when the run is over.
   */
  const retryRegistryInstance = async (): Promise<void> => {
    registryInstanceBusy.value = true
    const result = await runAction(registryInstanceError, () => guardRegistry<RegistryInstanceDto | null>(api.retryRegistryInstance, null))
    registryInstanceBusy.value = false
    if (result.ok) {
      registryInstance.value = result.value
    }
  }

  /**
   * Whether a registry action is in flight for one plugin; other plugins stay usable meanwhile.
   *
   * @param id - Plugin id as listed by the registry.
   * @returns True while that plugin is busy.
   */
  const isBusy = (id: string): boolean => busyIds.value[id] === true

  /**
   * Marks one plugin busy or idle.
   *
   * @param id - Plugin id as listed by the registry.
   * @param busy - True to mark busy; false to clear.
   */
  const setBusy = (id: string, busy: boolean): void => {
    if (busy) {
      busyIds.value = { ...busyIds.value, [id]: true }
      return
    }
    const rest = { ...busyIds.value }
    delete rest[id]
    busyIds.value = rest
  }

  /**
   * Loads the details of one registry plugin into {@link registryDetails}. The previous details stay
   * on show while a reload runs; a refusal is kept as the sentence of that plugin only, except a not-found answer, which sets `notFound`.
   *
   * @param id - Plugin id as listed by the registry.
   * @returns Resolves when the request is over.
   */
  const fetchRegistryDetails = async (id: string): Promise<void> => {
    const previous = registryDetails.value[id]?.details ?? null
    registryDetails.value = { ...registryDetails.value, [id]: { details: previous, loading: true, error: null, errorCode: null, notFound: false } }
    /** Local sink so one plugin's refusal does not touch another's state. */
    const failure = ref<string | null>(null)
    let notFound = false
    let failureCode: string | null = null
    const result = await runAction(failure, async () => {
      try {
        return await api.getRegistryDetails(id)
      } catch (cause) {
        notFound = isNotFound(cause)
        failureCode = errorCode(cause)
        throw cause
      }
    })
    registryDetails.value = {
      ...registryDetails.value,
      // A plugin the registry does not know is not a failure to retry: no sentence, no stale details.
      [id]: notFound
        ? { details: null, loading: false, error: null, errorCode: null, notFound: true }
        : {
          details: result.ok ? result.value : previous,
          loading: false,
          error: failure.value,
          errorCode: result.ok ? null : failureCode,
          notFound: false,
        },
    }
  }

  /**
   * Stores the licence key of a registry plugin, then reloads its details so `hasLicenceKey` is current.
   *
   * @param id - Plugin id as listed by the registry.
   * @param key - The licence key.
   * @returns This call's outcome: `ok`, or the API's sentence in `error` (also left in `licenceError`).
   */
  const saveLicenceKey = async (id: string, key: string): Promise<PluginActionOutcome> => {
    setBusy(id, true)
    const result = await runOwn(licenceError, () => api.saveLicenceKey(id, { key }))
    if (result.ok) {
      await fetchRegistryDetails(id)
    }
    setBusy(id, false)
    return { ok: result.ok, stagedVersion: null, error: result.error }
  }

  /**
   * Removes the stored licence key of a registry plugin, then reloads its details.
   *
   * @param id - Plugin id as listed by the registry.
   * @returns This call's outcome: `ok`, or the API's sentence in `error` (also left in `licenceError`).
   */
  const removeLicenceKey = async (id: string): Promise<PluginActionOutcome> => {
    setBusy(id, true)
    const result = await runOwn(licenceError, () => api.removeLicenceKey(id))
    if (result.ok) {
      await fetchRegistryDetails(id)
    }
    setBusy(id, false)
    return { ok: result.ok, stagedVersion: null, error: result.error }
  }

  /**
   * Installs a plugin from the registry, or stages its upgrade, then refreshes the lists.
   *
   * @param id - Plugin id as listed by the registry.
   * @param version - Version to install.
   * @param allowDowngrade - True to allow a version older than the installed one.
   * @returns This call's outcome: `ok`, the version it staged, or the API's sentence in `error`
   *   (the shared `stagedVersion` and `registryInstallError` follow the latest call).
   */
  const installFromRegistry = async (id: string, version: string, allowDowngrade = false): Promise<PluginActionOutcome> => {
    setBusy(id, true)
    stagedVersion.value = null
    const result = await runOwn(registryInstallError, () => api.installFromRegistry(id, { version, allowDowngrade }))
    let staged: string | null = null
    if (result.ok) {
      if (result.value.requiresRestart) {
        requiresRestart.value = true
      }
      staged = result.value.stagedVersion ?? null
      stagedVersion.value = staged
      await Promise.all([
        fetchAll(),
        fetchRegistry(),
        fetchRegistryUpdates(),
        // Only a plugin whose details are on show needs them reloaded.
        ...(registryDetails.value[id] === undefined ? [] : [fetchRegistryDetails(id)]),
      ])
    }
    setBusy(id, false)
    return { ok: result.ok, stagedVersion: staged, error: result.error }
  }

  return {
    plugins, loading, error, actionError, requiresRestart, downgradeFile, stagedVersion, pendingFile, preview, installed, keptDataIds, slotWidgets, productOptions, slotWidgetsError, bindingError,
    registryPageSize, registryItems, registryTotal, registryQuery, registryLoading, registryError, registryDisabled, registryUpdates, registryUpdatesError,
    registryInstallError, registryInstallingId, updateVersions, updateChangelogs, licenceError, registryDetails,
    registryInstance, registryInstanceBusy, registryInstanceError,
    isBusy, fetchRegistryDetails, saveLicenceKey, removeLicenceKey,
    fetchRegistry, setRegistryFilter, fetchRegistryUpdates, fetchRegistryInstance, retryRegistryInstance, installFromRegistry,
    fetchAll, inspect, confirmInstall, dismissPreview, install, forceDowngrade, dismissDowngrade, remove, purge, restart, fetchSlotWidgets, saveSlotWidgetProducts,
  }
})
