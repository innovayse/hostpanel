<script setup lang="ts">
/**
 * Plugin Manager page, in three tabs that live in the address (`tab`), so a reload or a shared link opens the same one.
 *
 * Installed lists the installed plugins as a three-column grid of cards behind a search box and a status and
 * category filter; a card opens the plugin's own page, and an Update button appears on a plugin the registry
 * has a newer version of. The Upload plugin button in the header opens a dialog with the ZIP uploader: a newer
 * upload is staged until the restart, an upload older than the installed version is refused and the operator
 * can force it after a confirmation. Browse lists the plugin registry (search, category filter and page live
 * in the address through useFilterQuery) with the registry trust card, and installs or updates from it; the tab
 * is not offered when the server answers that the registry is switched off. Settings holds what applies to
 * every plugin: the "Allow unsigned plugins" switch and the data removed plugins kept, which can be purged
 * behind a danger prompt. An update, from the installed list or from a registry card, opens a prompt that lists
 * the release notes of every version it passes through and installs only after the admin confirms. A restart
 * banner shows while pending changes need a restart; a purge's outcome shows above the tabs, and every install, update and upload runs behind the processing dialog.
 * Removing a plugin is done on its own page.
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useFilterQuery } from '@/composables/useFilterQuery'
import { enumValueOf } from '@/lib/enumValue'
import { pluginPath } from '@/lib/routes'
import { usePluginsStore } from '@/stores/plugins'
import { useSettingsStore } from '@/stores/settings'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import PluginGlobalSettings from '@/components/plugins/PluginGlobalSettings.vue'
import PluginInstalledList from '@/components/plugins/PluginInstalledList.vue'
import PluginInstallDialog from '@/components/plugins/PluginInstallDialog.vue'
import PluginProcessingDialog from '@/components/plugins/PluginProcessingDialog.vue'
import PluginRegistryBrowser from '@/components/plugins/PluginRegistryBrowser.vue'
import PluginRegistryInstanceCard from '@/components/plugins/PluginRegistryInstanceCard.vue'
import PluginRegistryTrustCard from '@/components/plugins/PluginRegistryTrustCard.vue'
import PluginUploader from '@/components/plugins/PluginUploader.vue'
import PluginUpdateChanges from '@/components/plugins/details/PluginUpdateChanges.vue'
import RestartBanner from '@/components/plugins/RestartBanner.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import { usePluginProcessing } from '@/composables/usePluginProcessing'
import { useResultBanner } from '@/composables/useResultBanner'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDialogSize, UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'
import type { SettingSaveResult } from '@/types/setting'
import { PluginCategory, PluginCategoryAll, PluginProcessingStep } from '@/types/plugin'
import type { PluginActionOutcome, PluginCategoryFilter } from '@/types/plugin'
import type { UiFilterChip } from '@/utils/ui/filter'
import type { UiTabItem } from '@/utils/ui/tabs'

/** Translation function. */
const { t } = useI18n()

/** Router: opens a plugin's details page. */
const router = useRouter()

/** Plugins store: the installed list, the restart flag and the refusal sentence. */
const store = usePluginsStore()

/** Settings store: the rows the signing switch reads, and the save that writes one. */
const settingsStore = useSettingsStore()

/** Id of the plugin whose data purge is in flight. */
const purgingId = ref<string | null>(null)

/** True while the server restart request is in flight. */
const restarting = ref(false)

/** Whether the upload dialog is open. */
const uploadOpen = ref(false)

/** The outcome line of the last purge. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/** The processing dialog's state: every install, update and upload runs behind it. */
const processing = usePluginProcessing()

/**
 * Purges the data of a removed plugin the admin confirmed and shows the outcome.
 *
 * @param id - Id of the plugin whose data to delete.
 */
const handlePurge = async (id: string): Promise<void> => {
  feedback.clear()
  purgingId.value = id
  const ok = await store.purge(id)
  purgingId.value = null
  if (ok) {
    feedback.show(t('plugins.plugins.dataPurged'), false)
  } else {
    feedback.show(store.actionError ?? t('errors.noAnswer'), true)
  }
}

/**
 * Turns the store's yes or no for an archive job into the outcome the processing dialog reads.
 *
 * @param ok - Whether the store accepted the archive.
 * @returns The outcome: the staged version of this install when there is one, the store's sentence for a refusal.
 */
const archiveOutcome = (ok: boolean): PluginActionOutcome => ({
  ok,
  stagedVersion: ok ? store.stagedVersion : null,
  error: ok ? null : store.actionError,
})

/** Installs the older version the operator confirmed, behind the processing dialog. */
const handleForceDowngrade = async (): Promise<void> => {
  await processing.run({
    name: store.downgradeFile?.name ?? '',
    step: PluginProcessingStep.Installing,
    /**
     * The store call the dialog waits for.
     *
     * @returns The call's outcome.
     */
    work: async () => archiveOutcome(await store.forceDowngrade()),
  })
}

/** Restarts the server so installed or removed plugins take effect. */
const handleRestart = async (): Promise<void> => {
  restarting.value = true
  await store.restart()
  restarting.value = false
}

/** Opens the upload dialog. */
const openUpload = (): void => {
  uploadOpen.value = true
}

/** Closes the upload dialog. */
const closeUpload = (): void => {
  uploadOpen.value = false
}

/**
 * Reads what a chosen plugin archive asks for behind the processing dialog: a refusal stays on it, a success
 * closes it and opens the confirmation of what the archive asks for.
 *
 * @param file - The chosen `.zip` archive.
 */
const handleUpload = async (file: File): Promise<void> => {
  closeUpload()
  await processing.run({
    name: file.name,
    step: PluginProcessingStep.Uploading,
    /**
     * The store call the dialog waits for.
     *
     * @returns The call's outcome.
     */
    work: async () => archiveOutcome(await store.inspect(file)),
    closeOnSuccess: true,
  })
}

/** Installs the archive the admin confirmed after reading what it asks for, behind the processing dialog; a refused downgrade moves on to its own prompt. */
const handleConfirmInstall = async (): Promise<void> => {
  const name = store.preview?.name ?? ''
  await processing.run({
    name,
    step: PluginProcessingStep.Installing,
    /**
     * The store call the dialog waits for.
     *
     * @returns The call's outcome.
     */
    work: async () => archiveOutcome(await store.confirmInstall()),
  })
  // An archive older than the installed version is refused with its own prompt (force the downgrade), which carries the sentence.
  if (store.downgradeFile !== null) {
    processing.close()
  }
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

/** Closed set of the page's tabs; the strings are the tab values kept in the address. */
const PluginsTab = {
  /** The installed plugins. */
  Installed: 'installed',
  /** The registry browser. */
  Browse: 'browse',
  /** What applies to every plugin: the signature policy and the data removed plugins kept. */
  Settings: 'settings',
} as const

/** One of the {@link PluginsTab} values. */
type PluginsTab = (typeof PluginsTab)[keyof typeof PluginsTab]

/** The tab kept in the address, under its own search and page keys so it does not collide with the lists'. */
const tabQuery = reactive(useFilterQuery(['tab'] as const, { searchKey: 'tabSearch', pageKey: 'tabPage' }))

/** The tab on show: the one in the address, Installed when it is missing, unknown or Browse while the registry is off. */
const tab = computed<PluginsTab>(() => {
  const named = enumValueOf(PluginsTab, tabQuery.applied.tab)
  return named === null || (named === PluginsTab.Browse && store.registryDisabled) ? PluginsTab.Installed : named
})

/**
 * Switches tab from the strip's event by writing it to the address; a value that is no tab is ignored.
 *
 * @param value - The value the strip reported.
 */
const selectTab = (value: string): void => {
  const next = enumValueOf(PluginsTab, value)
  if (next !== null) {
    tabQuery.draft.tab = next
    void tabQuery.apply()
  }
}

/** The tab strip: Browse is offered only while the registry is switched on. */
const tabs = computed<UiTabItem[]>(() => [
  { value: PluginsTab.Installed, label: t('plugins.plugins.tabInstalled'), testid: 'plugins-tab-installed' },
  ...(store.registryDisabled
    ? []
    : [{ value: PluginsTab.Browse, label: t('plugins.plugins.tabBrowse'), testid: 'plugins-tab-browse' }]),
  { value: PluginsTab.Settings, label: t('plugins.plugins.tabSettings'), testid: 'plugins-tab-settings' },
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

/** Closes the update prompt without updating. */
const dismissUpdate = (): void => {
  pendingUpdate.value = null
}

/** Closes the prompt and installs the update the admin confirmed. */
const confirmUpdate = async (): Promise<void> => {
  const pending = pendingUpdate.value
  if (pending === null) {
    return
  }
  pendingUpdate.value = null
  await handleRegistryInstall(pending.id, pending.version)
}

/**
 * Installs or updates a plugin from the registry behind the processing dialog.
 *
 * @param id - Plugin id as listed by the registry.
 * @param version - Version to install.
 */
const handleRegistryInstall = async (id: string, version: string): Promise<void> => {
  const installed = store.installed.some(plugin => plugin.id === id)
  await processing.run({
    name: store.registryItems.find(item => item.id === id)?.name ?? store.installed.find(plugin => plugin.id === id)?.name ?? id,
    step: installed ? PluginProcessingStep.Updating : PluginProcessingStep.Installing,
    /**
     * The store call the dialog waits for.
     *
     * @returns The call's outcome.
     */
    work: () => store.installFromRegistry(id, version),
  })
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

/** Loads the installed plugins, the settings the signing switch reads, and the registry (its first page decides whether the Browse tab exists; its updates feed the badges). */
onMounted(() => Promise.all([
  // The registration card exists only while the registry is on, which the first page decides.
  loadRegistry().then(() => (store.registryDisabled ? undefined : store.fetchRegistryInstance())),
  store.fetchRegistryUpdates(),
  store.fetchAll(),
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
    >
      <template #actions>
        <UiButton type="button" :variant="UiButtonVariant.Primary" data-testid="plugins-upload-open" @click="openUpload">
          {{ t('plugins.plugins.uploadPlugin') }}
        </UiButton>
      </template>
    </UiPageHeader>

    <!-- Restart banner -->
    <RestartBanner v-if="store.requiresRestart" :restarting="restarting" @restart="handleRestart" />

    <UiTabs
      :model-value="tab"
      :items="tabs"
      :aria-label="t('plugins.plugins.tabsLabel')"
      data-testid="plugins-tabs"
      @update:model-value="selectTab"
    />

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="plugins-feedback" />

    <!-- Browse -->
    <template v-if="tab === PluginsTab.Browse">
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
        :staged-versions="stagedVersions"
        :licensed-ids="licensedIds"
        @update:page="handleRegistryPage"
        @install="handleRegistryInstallRequest"
        @details="openDetails"
        @retry="store.fetchRegistry()"
      />
    </template>

    <!-- Settings -->
    <PluginGlobalSettings
      v-else-if="tab === PluginsTab.Settings"
      :settings="settingsStore.settings"
      :error="settingsStore.error"
      :loading="settingsStore.loading"
      :save="saveSetting"
      :kept-data-ids="store.keptDataIds"
      :purging-id="purgingId"
      @retry="settingsStore.fetchSettings()"
      @purge="handlePurge"
    />

    <!-- Installed -->
    <PluginInstalledList
      v-else
      :plugins="store.installed"
      :update-versions="store.updateVersions"
      :loading="store.loading"
      :error="store.error"
      :updating-ids="installingIds"
      @update="askUpdate"
      @retry="store.fetchAll()"
    />

    <UiDialog
      :open="uploadOpen"
      :title="t('plugins.plugins.uploadTitle')"
      :size="UiDialogSize.Md"
      data-testid="plugins-upload-dialog"
      @close="closeUpload"
    >
      <PluginUploader @upload="handleUpload" />
    </UiDialog>

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

    <PluginProcessingDialog
      :open="processing.open.value"
      :name="processing.name.value"
      :step="processing.step.value"
      :running="processing.running.value"
      :failed="processing.failed.value"
      :message="processing.message.value"
      :restart-required="store.requiresRestart"
      @close="processing.close()"
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
