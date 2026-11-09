<script setup lang="ts">
/**
 * Plugin Manager page.
 *
 * Lists installed plugins, provides ZIP upload for installation,
 * and shows a restart banner when pending changes need to take effect. A refused load offers a
 * retry; the outcome of an install or remove (success, or the API's sentence) shows above the list;
 * a removal is confirmed first in a danger prompt that says the plugin's data is kept. A newer upload
 * is staged until the restart; an upload older than the installed version is refused, and the
 * operator can force it after a confirmation. The data a removed plugin kept can be purged behind a
 * second danger prompt. The "Allow unsigned plugins" switch with its warning sits
 * under the uploader and saves through the settings store, like every other settings row. The slot widgets section
 * lists the widgets plugins add to pages, each with a product multi select the operator saves after install.
 * A second tab, Browse, lists the plugin registry (search, category, pages) with the registry trust card
 * and installs or updates from it; the tab is not offered when the server answers that the registry is
 * switched off. A plugin with a newer registry version shows an update badge and an Update button in the
 * installed list, and a plugin the registry lists has a Details button. Registry cards open the details page,
 * and install, update and uninstall (the same confirmed removal as the installed list) per card.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { pluginPath } from '@/lib/routes'
import { usePluginsStore } from '@/stores/plugins'
import { useSettingsStore } from '@/stores/settings'
import PluginCard from '@/components/plugins/PluginCard.vue'
import PluginInstallDialog from '@/components/plugins/PluginInstallDialog.vue'
import PluginRegistryBrowser from '@/components/plugins/PluginRegistryBrowser.vue'
import PluginRegistryInstanceCard from '@/components/plugins/PluginRegistryInstanceCard.vue'
import PluginRegistryTrustCard from '@/components/plugins/PluginRegistryTrustCard.vue'
import PluginKeptData from '@/components/plugins/PluginKeptData.vue'
import PluginSigningCard from '@/components/plugins/PluginSigningCard.vue'
import PluginSlotWidgetsCard from '@/components/plugins/PluginSlotWidgetsCard.vue'
import PluginUploader from '@/components/plugins/PluginUploader.vue'
import RestartBanner from '@/components/plugins/RestartBanner.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'
import type { SettingSaveResult } from '@/types/setting'
import type { PluginCategoryFilter } from '@/types/plugin'
import type { UiTabItem } from '@/utils/ui/tabs'

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

/**
 * Applies a new search text to the registry list.
 *
 * @param q - The search text.
 */
const handleRegistryQuery = (q: string): void => {
  void store.setRegistryFilter({ q })
}

/**
 * Applies a new category to the registry list.
 *
 * @param category - The category, empty for every category.
 */
const handleRegistryCategory = (category: PluginCategoryFilter): void => {
  void store.setRegistryFilter({ category })
}

/**
 * Moves the registry list to a page.
 *
 * @param page - The page, starting at 1.
 */
const handleRegistryPage = (page: number): void => {
  void store.setRegistryFilter({ page })
}

/** Plugins that declare at least one slot widget; the others have nothing to bind. */
const pluginsWithWidgets = computed(() => store.plugins.filter(plugin => (store.slotWidgets[plugin.id]?.length ?? 0) > 0))

/**
 * Loads the installed plugins, the settings the signing switch reads, the widgets with the products to bind,
 * and the registry (its first page decides whether the Browse tab exists; its updates feed the badges).
 */
onMounted(() => Promise.all([
  // The registration card exists only while the registry is on, which the first page decides.
  store.fetchRegistry().then(() => (store.registryDisabled ? undefined : store.fetchRegistryInstance())),
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
      <PluginRegistryBrowser
        :items="store.registryItems"
        :total="store.registryTotal"
        :page-size="store.registryPageSize"
        :page="store.registryQuery.page"
        :query="store.registryQuery.q"
        :category="store.registryQuery.category"
        :loading="store.registryLoading"
        :error="store.registryError"
        :installing-ids="installingIds"
        :removing-id="removingId"
        :staged-versions="stagedVersions"
        :licensed-ids="licensedIds"
        @update:query="handleRegistryQuery"
        @update:category="handleRegistryCategory"
        @update:page="handleRegistryPage"
        @install="handleRegistryInstall"
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
      <UiSkeleton v-else-if="store.loading && store.installed.length === 0" :lines="3" data-testid="plugins-loading" />

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
        <PluginCard
          v-for="plugin in store.installed"
          :key="plugin.id"
          :plugin="plugin"
          :removing="removingId === plugin.id"
          :update-version="store.updateVersions[plugin.id] ?? null"
          :updating="installingIds.includes(plugin.id)"
          :has-details="registryListedIds.includes(plugin.id)"
          @remove="askRemove"
          @update="handleRegistryInstall"
          @details="openDetails"
        />
      </UiStack>
    </template>

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
