<script setup lang="ts">
/**
 * Plugin Manager page.
 *
 * Lists installed plugins as a grid of cards (one column on a phone) behind a search box and a status and category
 * filter that live in the address, provides ZIP upload for installation,
 * and shows a restart banner when pending changes need to take effect. A refused load offers a
 * retry; the outcome of an install or remove (success, or the API's sentence) shows above the list;
 * a removal is confirmed first in a danger prompt that says the plugin's data is kept. A newer upload
 * is staged until the restart; an upload older than the installed version is refused, and the
 * operator can force it after a confirmation. The data a removed plugin kept can be purged behind a
 * second danger prompt. The "Allow unsigned plugins" switch with its warning sits
 * under the uploader and saves through the settings store, like every other settings row. The slot widgets section
 * lists the widgets plugins add to pages, each with a product multi select the operator saves after install.
 * A second tab, Browse, lists the plugin registry (search, category filter and page live in the address through useFilterQuery) with the registry trust card
 * and installs or updates from it; the tab is not offered when the server answers that the registry is
 * switched off. A plugin with a newer registry version shows an update badge and an Update button in the
 * installed list, and a plugin the registry lists has a Details button. Registry cards open the details page,
 * and install, update and uninstall (the same confirmed removal as the installed list) per card. An update,
 * from the installed list or from a registry card, opens a prompt that lists the release notes of every
 * version it passes through and installs only after the admin confirms.
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useFilterQuery } from '@/composables/useFilterQuery'
import { enumValueOf } from '@/lib/enumValue'
import { filterInstalledPlugins, InstalledPluginState } from '@/lib/installedPlugins'
import { pluginPath } from '@/lib/routes'
import { usePluginsStore } from '@/stores/plugins'
import { useSettingsStore } from '@/stores/settings'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import PluginCard from '@/components/plugins/PluginCard.vue'
import PluginInstallDialog from '@/components/plugins/PluginInstallDialog.vue'
import PluginRegistryBrowser from '@/components/plugins/PluginRegistryBrowser.vue'
import PluginRegistryInstanceCard from '@/components/plugins/PluginRegistryInstanceCard.vue'
import PluginRegistryTrustCard from '@/components/plugins/PluginRegistryTrustCard.vue'
import PluginKeptData from '@/components/plugins/PluginKeptData.vue'
import PluginSigningCard from '@/components/plugins/PluginSigningCard.vue'
import PluginSlotWidgetsCard from '@/components/plugins/PluginSlotWidgetsCard.vue'
import PluginUploader from '@/components/plugins/PluginUploader.vue'
import PluginUpdateChanges from '@/components/plugins/details/PluginUpdateChanges.vue'
import RestartBanner from '@/components/plugins/RestartBanner.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import { UiDialogSize, UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'
import type { SettingSaveResult } from '@/types/setting'
import { PluginCategory, PluginCategoryAll } from '@/types/plugin'
import type { PluginCategoryFilter } from '@/types/plugin'
import type { UiFilterChip } from '@/utils/ui/filter'
import type { UiTabItem } from '@/utils/ui/tabs'

/** How many plugin cards the installed list draws as placeholders while it loads (two rows of the two-column grid). */
const PLUGIN_CARD_PLACEHOLDERS = 4

/** Translation function. */
const { t } = useI18n()

/** Router: opens a plugin's details page. */
const router = useRouter()

/** Plugins store: the installed list, the restart flag and the refusal sentence. */
const store = usePluginsStore()

/** Settings store: the rows the signing switch reads, and the save that writes one. */
const settingsStore = useSettingsStore()

/** Id of the plugin whose removal is in flight. */
const removingId = ref<string | null>(null)

/** Id of the plugin whose data purge is in flight. */
const purgingId = ref<string | null>(null)

/** Id of the plugin waiting for the purge confirmation, null when the prompt is closed. */
const purgeConfirmId = ref<string | null>(null)

/** True while the server restart request is in flight. */
const restarting = ref(false)

/** Id of the plugin waiting for the removal confirmation, null when the prompt is closed. */
const confirmingId = ref<string | null>(null)

/** The outcome line of the last install or remove. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/**
 * Shows the outcome of an install or remove.
 *
 * @param ok - Whether the API accepted the change.
 * @param successKey - Locale key of the success sentence.
 * @param failure - The API's sentence for this call's refusal, null when it gave none.
 */
const showOutcome = (ok: boolean, successKey: string, failure: string | null): void => {
  if (ok) {
    feedback.show(t(successKey), false)
  } else {
    feedback.show(failure ?? t('errors.noAnswer'), true)
  }
}

/**
 * Opens the removal confirmation for a plugin.
 *
 * @param id - Id of the plugin to remove.
 */
const askRemove = (id: string): void => {
  confirmingId.value = id
}

/** Closes the removal confirmation; ignored while the removal runs. */
const dismissRemove = (): void => {
  if (removingId.value === null) {
    confirmingId.value = null
  }
}

/** Removes the plugin the operator confirmed and shows the outcome. */
const handleRemove = async (): Promise<void> => {
  const id = confirmingId.value
  if (id === null) {
    return
  }
  feedback.clear()
  removingId.value = id
  const outcome = await store.remove(id)
  removingId.value = null
  confirmingId.value = null
  showOutcome(outcome.ok, 'plugins.plugins.pluginRemoved', outcome.error)
}

/**
 * Opens the purge confirmation for a removed plugin's data.
 *
 * @param id - Id of the plugin whose data to delete.
 */
const askPurge = (id: string): void => {
  purgeConfirmId.value = id
}

/** Closes the purge confirmation; ignored while the purge runs. */
const dismissPurge = (): void => {
  if (purgingId.value === null) {
    purgeConfirmId.value = null
  }
}

/** Purges the data the operator confirmed and shows the outcome. */
const handlePurge = async (): Promise<void> => {
  const id = purgeConfirmId.value
  if (id === null) {
    return
  }
  feedback.clear()
  purgingId.value = id
  const ok = await store.purge(id)
  purgingId.value = null
  purgeConfirmId.value = null
  showOutcome(ok, 'plugins.plugins.dataPurged', store.actionError)
}

/** Installs the older version the operator confirmed and shows the outcome. */
const handleForceDowngrade = async (): Promise<void> => {
  feedback.clear()
  const ok = await store.forceDowngrade()
  showOutcome(ok, 'plugins.plugins.pluginInstalled', store.actionError)
}

/** Restarts the server so installed or removed plugins take effect. */
const handleRestart = async (): Promise<void> => {
  restarting.value = true
  await store.restart()
  restarting.value = false
}

/**
 * Reads what a chosen plugin archive asks for and opens the confirmation; a refusal is shown at once.
 *
 * @param file - The chosen `.zip` archive.
 */
const handleUpload = async (file: File): Promise<void> => {
  feedback.clear()
  const ok = await store.inspect(file)
  if (!ok) {
    showOutcome(false, 'plugins.plugins.pluginInstalled', store.actionError)
  }
}

/** Installs the archive the admin confirmed after reading what it asks for, and shows the outcome. */
const handleConfirmInstall = async (): Promise<void> => {
  feedback.clear()
  const ok = await store.confirmInstall()
  if (ok && store.stagedVersion !== null) {
    feedback.show(t('plugins.plugins.pluginStaged', { version: store.stagedVersion }), false)
    return
  }
  showOutcome(ok, 'plugins.plugins.pluginInstalled', store.actionError)
}

/**
 * Persists the signing switch and reports how it went to the control that asked.
 *
 * @param id - Setting ID.
 * @param value - New value, `true` or `false`.
 * @returns Whether the value was stored, and the API's sentence when it was refused.
 */
const saveSetting = async (id: number, value: string): Promise<SettingSaveResult> => {
  const saved = await settingsStore.updateSetting(id, value)
  return { saved, error: saved ? null : settingsStore.actionError }
}

/** Closed set of the page's tabs; the strings are the tab values. */
const PluginsTab = {
  /** The installed plugins, the uploader and the settings. */
  Installed: 'installed',
  /** The registry browser. */
  Browse: 'browse',
} as const

/** One of the {@link PluginsTab} values. */
type PluginsTab = (typeof PluginsTab)[keyof typeof PluginsTab]

/** The tab on show. */
const tab = ref<PluginsTab>(PluginsTab.Installed)

/**
 * Switches tab from the strip's event; a value that is no tab is ignored.
 *
 * @param value - The value the strip reported.
 */
const selectTab = (value: string): void => {
  const next = Object.values(PluginsTab).find(known => known === value)
  if (next !== undefined) {
    tab.value = next
  }
}

/** The tab strip: Browse is offered only while the registry is switched on. */
const tabs = computed<UiTabItem[]>(() => [
  { value: PluginsTab.Installed, label: t('plugins.plugins.tabInstalled'), testid: 'plugins-tab-installed' },
  ...(store.registryDisabled
    ? []
    : [{ value: PluginsTab.Browse, label: t('plugins.plugins.tabBrowse'), testid: 'plugins-tab-browse' }]),
])

/** Ids of the plugins with a registry action in flight (the store tracks each plugin on its own); each card shows only its own busy state. */
const installingIds = computed<string[]>(() =>
  [...new Set([...store.registryItems.map(item => item.id), ...store.installed.map(plugin => plugin.id)])].filter(id => store.isBusy(id)),
)

/** Ids of the plugins the server holds a licence key for, as far as their details have been loaded. */
const licensedIds = computed<string[]>(() =>
  Object.entries(store.registryDetails)
    .filter(([, state]) => state.details?.hasLicenceKey === true)
    .map(([id]) => id),
)

/** Ids of the plugins the registry lists, on the current page or through an update; these have a details page. */
const registryListedIds = computed<string[]>(() =>
  store.registryDisabled
    ? []
    : [...new Set([...store.registryItems.map(item => item.id), ...Object.keys(store.updateVersions)])],
)

/** Staged version per installed plugin id, for the registry cards' staged state. */
const stagedVersions = computed<Record<string, string>>(() =>
  Object.fromEntries(
    store.installed.flatMap(plugin => (plugin.stagedVersion === null ? [] : [[plugin.id, plugin.stagedVersion]])),
  ),
)

/**
 * Opens the details page of a plugin.
 *
 * @param id - Plugin id.
 */
const openDetails = (id: string): void => {
  void router.push(pluginPath(id))
}

/** The update the admin asked for and has not confirmed yet: null when no prompt is open. */
const pendingUpdate = ref<{ id: string; version: string } | null>(null)

/** The registry details of the plugin being updated, for the list of what changes; null until they load or when they cannot. */
const pendingUpdateDetails = computed(() =>
  pendingUpdate.value === null ? null : (store.registryDetails[pendingUpdate.value.id]?.details ?? null),
)

/**
 * Opens the update prompt: it lists the release notes of every version the update passes through, and
 * installs nothing until the admin confirms. The details are read for the notes; a failed read leaves the
 * prompt without the list, and the update can still be confirmed.
 *
 * @param id - Plugin id.
 * @param version - The version to update to.
 */
const askUpdate = (id: string, version: string): void => {
  pendingUpdate.value = { id, version }
  void store.fetchRegistryDetails(id)
}

/**
 * Handles an install request from the registry list: an installed plugin is an update and is confirmed
 * first, anything else installs at once.
 *
 * @param id - Plugin id as listed by the registry.
 * @param version - Version to install.
 */
const handleRegistryInstallRequest = (id: string, version: string): void => {
  if (store.installed.some(plugin => plugin.id === id)) {
    askUpdate(id, version)
  } else {
    void handleRegistryInstall(id, version)
  }
}

/** Closes the update prompt; ignored while the update runs. */
const dismissUpdate = (): void => {
  if (pendingUpdate.value !== null && !store.isBusy(pendingUpdate.value.id)) {
    pendingUpdate.value = null
  }
}

/** Installs the update the admin confirmed. */
const confirmUpdate = async (): Promise<void> => {
  const pending = pendingUpdate.value
  if (pending === null) {
    return
  }
  await handleRegistryInstall(pending.id, pending.version)
  pendingUpdate.value = null
}

/**
 * Installs or updates a plugin from the registry and shows the outcome.
 *
 * @param id - Plugin id as listed by the registry.
 * @param version - Version to install.
 */
const handleRegistryInstall = async (id: string, version: string): Promise<void> => {
  feedback.clear()
  const outcome = await store.installFromRegistry(id, version)
  if (!outcome.ok) {
    feedback.show(outcome.error ?? t('errors.noAnswer'), true)
  } else if (outcome.stagedVersion !== null) {
    feedback.show(t('plugins.plugins.pluginStaged', { version: outcome.stagedVersion }), false)
  } else {
    feedback.show(t('plugins.plugins.registryInstalled'), false)
  }
}

/** The registry list's search text, category and page, kept in the address so a reload or a shared link restores the same list. */
const registry = reactive(useFilterQuery(['category'] as const))

/** The category in the address as the registry filter: a value that is no registry category means every category. */
const registryCategory = computed<PluginCategoryFilter>(
  () => enumValueOf(PluginCategory, registry.applied.category) ?? PluginCategoryAll,
)

/**
 * Reads the filter in the address into the store and loads the registry page it names.
 *
 * @returns Promise that resolves when the registry page is loaded.
 */
const loadRegistry = (): Promise<void> =>
  store.setRegistryFilter({ q: registry.appliedSearch, category: registryCategory.value, page: registry.page })

// A filter or page changed in the address (typing after its pause, the filter dialog, the pager, the back button) reloads the list.
watch([() => registry.appliedSearch, registryCategory, () => registry.page], () => {
  void loadRegistry()
})

/** Options of the registry category filter: every category, then one per registry category. */
const registryCategoryOptions = computed<{ value: string; label: string }[]>(() => [
  { value: PluginCategoryAll, label: t('plugins.pluginRegistry.allCategories') },
  ...Object.values(PluginCategory).map(value => ({ value, label: t(`plugins.pluginRegistry.category.${value}`) })),
])

/** The chip of the applied registry category, none while every category is shown. */
const registryChips = computed<UiFilterChip[]>(() => {
  const label = registryCategoryOptions.value.find(option => option.value === registry.applied.category)?.label
  return registry.applied.category === ''
    ? []
    : [{ key: 'category', label: t('plugins.pluginRegistry.categoryPlaceholder'), value: label ?? registry.applied.category }]
})

/**
 * Moves the registry list to a page through the address.
 *
 * @param next - The page, starting at 1.
 */
const handleRegistryPage = (next: number): void => {
  void registry.setPage(next)
}

/** Closed set of the Installed tab's filter fields; the strings are their keys in the address. */
const InstalledFilterKey = {
  /** The plugin's state on this server. */
  State: 'status',
  /** The plugin's category. */
  Category: 'kind',
} as const

/**
 * The Installed tab's search text and filters, kept in the address under their own keys so they do not
 * collide with the registry's.
 */
const installedFilters = reactive(useFilterQuery(Object.values(InstalledFilterKey), { searchKey: 'installedSearch', pageKey: 'installedPage' }))

/** Options of the state filter: any state, then each state a plugin can be in. */
const stateOptions = computed<{ value: string; label: string }[]>(() => [
  { value: '', label: t('shared.any') },
  { value: InstalledPluginState.Active, label: t('shared.active') },
  { value: InstalledPluginState.PendingRestart, label: t('plugins.pluginCard.pendingRestart') },
  { value: InstalledPluginState.Refused, label: t('plugins.pluginCard.refused') },
])

/** Options of the category filter: any category, then each category the installed plugins have. */
const installedCategoryOptions = computed<{ value: string; label: string }[]>(() => [
  { value: '', label: t('plugins.pluginRegistry.allCategories') },
  ...[...new Set(store.installed.map(plugin => plugin.category))].sort().map(category => ({ value: category, label: category })),
])

/** One chip per applied installed-list filter, with the value as the operator reads it. */
const installedChips = computed<UiFilterChip[]>(() => {
  const values = installedFilters.applied
  const state = stateOptions.value.find(option => option.value === values.status)
  const all: UiFilterChip[] = [
    { key: InstalledFilterKey.State, label: t('shared.status'), value: state?.label ?? values.status },
    { key: InstalledFilterKey.Category, label: t('plugins.pluginRegistry.categoryPlaceholder'), value: values.kind },
  ]
  return all.filter(chip => chip.value !== '')
})

/**
 * Clears one installed-list filter from its chip.
 *
 * @param key - The key the chip reported.
 */
const removeInstalledChip = (key: string): void => {
  const known = enumValueOf(InstalledFilterKey, key)
  if (known !== null) {
    void installedFilters.remove(known)
  }
}

/** The installed plugins that match the search text and filters in the address. */
const visibleInstalled = computed(() =>
  filterInstalledPlugins(store.installed, {
    search: installedFilters.appliedSearch,
    state: installedFilters.applied.status,
    category: installedFilters.applied.kind,
  }),
)

/** Plugins that declare at least one slot widget; the others have nothing to bind. */
const pluginsWithWidgets = computed(() => store.plugins.filter(plugin => (store.slotWidgets[plugin.id]?.length ?? 0) > 0))

/**
 * Loads the installed plugins, the settings the signing switch reads, the widgets with the products to bind,
 * and the registry (its first page decides whether the Browse tab exists; its updates feed the badges).
 */
onMounted(() => Promise.all([
  // The registration card exists only while the registry is on, which the first page decides.
  loadRegistry().then(() => (store.registryDisabled ? undefined : store.fetchRegistryInstance())),
  store.fetchRegistryUpdates(),
  // The widgets are listed per installed plugin, so they wait for the plugin list.
  store.fetchAll().then(() => store.fetchSlotWidgets()),
  settingsStore.fetchSettings(),
]))
</script>

<template>
  <UiStack gap="lg" class="w-full">
    <UiPageHeader
      :title="t('plugins.plugins.pluginManager')"
      :subtitle="store.installed.length > 0
        ? `${t('plugins.plugins.installAndManageProviderPlugins')} ${t('plugins.plugins.lengthInstalled', { length: store.installed.length })}`
        : t('plugins.plugins.installAndManageProviderPlugins')"
    />

    <!-- Restart banner -->
    <RestartBanner v-if="store.requiresRestart" :restarting="restarting" @restart="handleRestart" />

    <UiTabs
      v-if="!store.registryDisabled"
      :model-value="tab"
      :items="tabs"
      data-testid="plugins-tabs"
      @update:model-value="selectTab"
    />

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="plugins-feedback" />

    <!-- Browse -->
    <template v-if="tab === PluginsTab.Browse && !store.registryDisabled">
      <PluginRegistryTrustCard :settings="settingsStore.settings" :save="saveSetting" />
      <PluginRegistryInstanceCard
        :instance="store.registryInstance"
        :busy="store.registryInstanceBusy"
        :error="store.registryInstanceError"
        @retry="store.retryRegistryInstance()"
      />
      <UiListToolbar
        v-model:search="registry.search"
        :active-count="registry.activeCount"
        :chips="registryChips"
        search-testid="plugin-registry-search"
        :search-placeholder="t('plugins.pluginRegistry.search')"
        @apply="registry.apply()"
        @reset="registry.reset()"
        @cancel="registry.cancel()"
        @remove="registry.remove('category')"
        @clear="registry.clearAll()"
      >
        <UiField :label="t('plugins.pluginRegistry.categoryPlaceholder')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="registry.draft.category"
              :options="registryCategoryOptions"
              :placeholder="t('plugins.pluginRegistry.categoryPlaceholder')"
              data-testid="plugin-registry-category"
            />
          </template>
        </UiField>
      </UiListToolbar>
      <PluginRegistryBrowser
        :items="store.registryItems"
        :total="store.registryTotal"
        :page-size="store.registryPageSize"
        :page="registry.page"
        :loading="store.registryLoading"
        :error="store.registryError"
        :installing-ids="installingIds"
        :removing-id="removingId"
        :staged-versions="stagedVersions"
        :licensed-ids="licensedIds"
        @update:page="handleRegistryPage"
        @install="handleRegistryInstallRequest"
        @remove="askRemove"
        @details="openDetails"
        @retry="store.fetchRegistry()"
      />
    </template>

    <!-- Installed -->
    <template v-else>
      <!-- Upload -->
      <PluginUploader @upload="handleUpload" />

      <!-- Signature policy -->
      <UiErrorState
        v-if="settingsStore.error"
        :message="settingsStore.error"
        :loading="settingsStore.loading"
        testid="plugin-signing-error"
        @retry="settingsStore.fetchSettings()"
      />
      <PluginSigningCard v-else :settings="settingsStore.settings" :save="saveSetting" />

      <!-- Slot widgets and the products they are bound to -->
      <UiErrorState
        v-if="store.slotWidgetsError"
        :message="store.slotWidgetsError"
        testid="plugin-slot-widgets-error"
        @retry="store.fetchSlotWidgets()"
      />
      <template v-else>
        <PluginSlotWidgetsCard
          v-for="plugin in pluginsWithWidgets"
          :key="plugin.id"
          :plugin-id="plugin.id"
          :plugin-name="plugin.name"
          :plugin-icon-url="plugin.iconUrl"
          :plugin-color="plugin.color"
          :widgets="store.slotWidgets[plugin.id] ?? []"
          :products="store.productOptions"
          :save="(widget, productIds) => store.saveSlotWidgetProducts(plugin.id, widget, productIds)"
          :error="store.bindingError"
        />
      </template>

      <!-- Removed plugins whose data is kept -->
      <PluginKeptData
        v-if="store.keptDataIds.length > 0"
        :ids="store.keptDataIds"
        :purging-id="purgingId"
        @purge="askPurge"
      />

      <!-- Error -->
      <UiErrorState
        v-if="store.error"
        :message="store.error"
        :loading="store.loading"
        testid="plugins-error"
        @retry="store.fetchAll()"
      />

      <!-- Loading -->
      <UiStack v-else-if="store.loading && store.installed.length === 0" gap="sm" aria-busy="true" data-testid="plugins-loading">
        <UiHeading :level="2" variant="section">{{ t('plugins.plugins.installedPlugins') }}</UiHeading>
        <UiGrid :columns="2" gap="md">
          <PluginCard v-for="placeholder in PLUGIN_CARD_PLACEHOLDERS" :key="placeholder" :plugin="null" loading />
        </UiGrid>
      </UiStack>

      <!-- Empty state -->
      <UiEmptyState
        v-else-if="!store.loading && store.installed.length === 0"
        :title="t('plugins.plugins.noPluginsInstalledYet')"
        :description="`${t('plugins.plugins.uploadA')} ${t('plugins.plugins.zip')} ${t('plugins.plugins.fileAboveToGetStarted')}`"
        data-testid="plugins-empty"
      />

      <!-- Plugin list -->
      <UiStack v-else gap="sm" data-testid="plugins-list">
        <UiHeading :level="2" variant="section">{{ t('plugins.plugins.installedPlugins') }}</UiHeading>
        <UiListToolbar
          v-model:search="installedFilters.search"
          :active-count="installedFilters.activeCount"
          :chips="installedChips"
          search-testid="plugins-installed-search"
          @apply="installedFilters.apply()"
          @reset="installedFilters.reset()"
          @cancel="installedFilters.cancel()"
          @remove="removeInstalledChip"
          @clear="installedFilters.clearAll()"
        >
          <UiStack>
            <UiField :label="t('shared.status')">
              <template #default="{ id }">
                <UiSelect
                  :id="id"
                  v-model="installedFilters.draft.status"
                  :options="stateOptions"
                  :placeholder="t('shared.status')"
                  data-testid="plugins-installed-status"
                />
              </template>
            </UiField>
            <UiField :label="t('plugins.pluginRegistry.categoryPlaceholder')">
              <template #default="{ id }">
                <UiSelect
                  :id="id"
                  v-model="installedFilters.draft.kind"
                  :options="installedCategoryOptions"
                  :placeholder="t('plugins.pluginRegistry.categoryPlaceholder')"
                  data-testid="plugins-installed-category"
                />
              </template>
            </UiField>
          </UiStack>
        </UiListToolbar>
        <UiEmptyState
          v-if="visibleInstalled.length === 0"
          :title="t('plugins.plugins.noPluginsMatch')"
          data-testid="plugins-no-match"
        />
        <UiGrid v-else :columns="2" gap="md">
        <PluginCard
          v-for="plugin in visibleInstalled"
          :key="plugin.id"
          :plugin="plugin"
          :removing="removingId === plugin.id"
          :update-version="store.updateVersions[plugin.id] ?? null"
          :update-changelog="store.updateChangelogs[plugin.id] ?? null"
          :updating="installingIds.includes(plugin.id)"
          :has-details="registryListedIds.includes(plugin.id)"
          @remove="askRemove"
          @update="askUpdate"
          @details="openDetails"
        />
        </UiGrid>
      </UiStack>
    </template>

    <!-- An update is never one click: the prompt lists what changes between the installed and the new version first. -->
    <UiDialog
      :open="pendingUpdate !== null"
      :variant="UiDialogVariant.Confirm"
      :title="t('plugins.plugins.confirmUpdateTitle')"
      :message="t('plugins.plugins.confirmUpdateMessage', { id: pendingUpdate?.id ?? '', version: pendingUpdate?.version ?? '' })"
      :confirm-label="t('plugins.plugins.confirmUpdate')"
      :loading="pendingUpdate !== null && store.isBusy(pendingUpdate.id)"
      :tone="UiTone.Primary"
      :size="UiDialogSize.Lg"
      data-testid="plugins-update-dialog"
      @confirm="confirmUpdate"
      @close="dismissUpdate"
    >
      <PluginUpdateChanges
        v-if="pendingUpdate !== null && pendingUpdateDetails !== null"
        :versions="pendingUpdateDetails.versions"
        :installed-version="pendingUpdateDetails.installedVersion"
        :target-version="pendingUpdate.version"
      />
    </UiDialog>

    <UiDialog
      :open="confirmingId !== null"
      :variant="UiDialogVariant.Confirm"
      :title="t('plugins.plugins.confirmRemoveTitle')"
      :message="t('plugins.plugins.confirmRemoveMessage', { id: confirmingId ?? '' })"
      :confirm-label="t('plugins.pluginCard.remove')"
      :loading="removingId !== null"
      :tone="UiTone.Danger"
      @confirm="handleRemove"
      @close="dismissRemove"
    />

    <UiDialog
      :open="purgeConfirmId !== null"
      :variant="UiDialogVariant.Confirm"
      :title="t('plugins.plugins.confirmPurgeTitle')"
      :message="t('plugins.plugins.confirmPurgeMessage', { id: purgeConfirmId ?? '' })"
      :confirm-label="t('plugins.keptData.purge')"
      :loading="purgingId !== null"
      :tone="UiTone.Danger"
      data-testid="plugins-purge-dialog"
      @confirm="handlePurge"
      @close="dismissPurge"
    />

    <UiDialog
      :open="store.downgradeFile !== null"
      :variant="UiDialogVariant.Confirm"
      :title="t('plugins.plugins.confirmDowngradeTitle')"
      :message="store.actionError ?? t('plugins.plugins.confirmDowngradeMessage')"
      :confirm-label="t('plugins.plugins.forceDowngrade')"
      :tone="UiTone.Warning"
      data-testid="plugins-downgrade-dialog"
      @confirm="handleForceDowngrade"
      @close="store.dismissDowngrade()"
    />

    <PluginInstallDialog
      v-if="store.preview !== null"
      :preview="store.preview"
      :loading="store.loading"
      @confirm="handleConfirmInstall"
      @close="store.dismissPreview()"
    />
  </UiStack>
</template>
