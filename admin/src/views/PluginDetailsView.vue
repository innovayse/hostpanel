<script setup lang="ts">
/**
 * Plugin details page (`/plugins/:id`): one page for every installed plugin.
 *
 * This server's own list is the source of truth: the head (picture, name, author, state on this server, uninstall) and the
 * Overview and Settings tabs exist for any installed plugin. The registry's details are optional enrichment: when the registry
 * is off or does not know the plugin, only the Verified and price badges, the update action and the Versions, Licence and
 * Changes tabs are left out. Settings holds the plugin's configuration and the product bindings of its slot widgets. A chosen
 * version is installed after a confirmation; an older one than the installed version needs its own explicit downgrade
 * confirmation. Uninstalling is confirmed first (the plugin's data is kept), raises the restart banner, shows its outcome in the processing dialog and returns to the plugins page once the operator closes it. The
 * page shows a skeleton while the list loads, the API's sentence with a retry when it fails, and a not-found state for an id that is not installed.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useIntegrationsStore } from '@/stores/integrations'
import { usePluginsStore } from '@/stores/plugins'
import PluginDetailsHeader from '@/components/plugins/details/PluginDetailsHeader.vue'
import PluginDetailsLinks from '@/components/plugins/details/PluginDetailsLinks.vue'
import PluginInstalledOverview from '@/components/plugins/details/PluginInstalledOverview.vue'
import PluginLicenceCard from '@/components/plugins/details/PluginLicenceCard.vue'
import PluginSettingsTab from '@/components/plugins/details/PluginSettingsTab.vue'
import PluginUpdateChanges from '@/components/plugins/details/PluginUpdateChanges.vue'
import PluginVersionList from '@/components/plugins/details/PluginVersionList.vue'
import { relationTo } from '@/components/plugins/details/versionOrder'
import PluginProcessingDialog from '@/components/plugins/PluginProcessingDialog.vue'
import PluginRemoveDialog from '@/components/plugins/PluginRemoveDialog.vue'
import RestartBanner from '@/components/plugins/RestartBanner.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import { usePluginProcessing } from '@/composables/usePluginProcessing'
import { useResultBanner } from '@/composables/useResultBanner'
import { Icon } from '@/lib/icons'
import { AppRoute } from '@/lib/routes'
import { PluginErrorCode, PluginProcessingStep, VersionRelation } from '@/types/plugin'
import type { PluginListItemDto, PluginSlotWidgetBindingDto, RegistryDetailsState, RegistryPluginDetailsDto } from '@/types/plugin'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDialogSize, UiDialogVariant } from '@/utils/ui/dialog'
import type { UiTabItem } from '@/utils/ui/tabs'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** The current route: its `id` parameter names the plugin. */
const route = useRoute()

/** Router, to return to the plugins page once the plugin is removed. */
const router = useRouter()

/** Plugins store: the installed list, the registry details, the installs, the removal, the licence key and the slot widgets. */
const store = usePluginsStore()

/** Integrations store: its list tells whether the plugin has a config form. */
const integrations = useIntegrationsStore()

/** The outcome line of the last action. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/** One install the operator asked for and has not confirmed yet. */
interface PendingInstall {
  /** The version to install. */
  version: string
  /** Whether it is older than the installed version, so the explicit downgrade prompt applies. */
  downgrade: boolean
}

/** Plugin id from the address; empty when the route carries none. */
const pluginId = computed<string>(() => {
  const raw = route.params.id
  return typeof raw === 'string' ? raw : ''
})

/** The installed plugin the address names; undefined while the list loads or when no such plugin is installed. */
const plugin = computed<PluginListItemDto | undefined>(() => store.installed.find(item => item.id === pluginId.value))

/** True once the installed list has answered (successfully or not), so "not installed" is a fact and not a wait. */
const listSettled = ref(false)

/** True while the list loads and nothing is on screen yet. */
const showLoading = computed<boolean>(() => plugin.value === undefined && !listSettled.value)

/** The sentence of a failed list load when the plugin cannot be shown, null otherwise. */
const loadError = computed<string | null>(() => (plugin.value === undefined ? store.error : null))

/** Load state of this plugin's registry details; undefined until the first load starts. */
const state = computed<RegistryDetailsState | undefined>(() => store.registryDetails[pluginId.value])

/** The registry details, null while loading, after a failure, when the registry is off or when it does not know the plugin. */
const details = computed<RegistryPluginDetailsDto | null>(() => state.value?.details ?? null)

/** True while the registry details load and none are on screen yet. */
const registryLoading = computed<boolean>(() => details.value === null && (state.value === undefined || state.value.loading))

/**
 * The sentence of a failed registry load worth showing, null otherwise. A registry that is switched off or does not
 * know the plugin is not a failure: those only leave the registry's tabs out.
 */
const registryError = computed<string | null>(() =>
  details.value === null && state.value?.errorCode !== PluginErrorCode.RegistryDisabled ? (state.value?.error ?? null) : null,
)

/** True when the registry load was refused because the plugin's page is gated by a licence key the server lacks or the registry rejected. */
const licenceGated = computed<boolean>(
  () => details.value === null
    && (state.value?.errorCode === PluginErrorCode.RegistryLicenceRequired
      || state.value?.errorCode === PluginErrorCode.RegistryLicenceInvalid),
)

/** True when the plugin has a config form: the integrations list holds an entry whose slug is the plugin's id. */
const hasConfig = computed<boolean>(() => integrations.integrations.some(item => item.slug === pluginId.value))

/** The plugin's slot widgets with their binding; empty until loaded. */
const widgets = computed<PluginSlotWidgetBindingDto[]>(() => store.slotWidgets[pluginId.value] ?? [])

/** True while the plugin's slot widgets load, so the Settings tab shows a skeleton instead of an empty note. */
const widgetsLoading = computed<boolean>(() => store.slotWidgetsLoading[pluginId.value] === true)

/** The sentence of the failed load of the plugin's slot widgets, null when none. */
const widgetsError = computed<string | null>(() => store.slotWidgetsError[pluginId.value] ?? null)

/** True while an install, update, uninstall or licence change of this plugin is in flight. */
const busy = computed<boolean>(() => store.isBusy(pluginId.value))

/** True while the server restart request is in flight. */
const restarting = ref(false)

/** Bumped after a licence key is saved or removed; keys the licence card so its field starts empty again. */
const licenceFieldEpoch = ref(0)

/** The install waiting for the operator's confirmation, null when no prompt is open. */
const pendingInstall = ref<PendingInstall | null>(null)

/** Whether the removal prompt is open. */
const confirmingUninstall = ref(false)

/** Whether the open install prompt is an ordinary one (not a downgrade). */
const installPromptOpen = computed<boolean>(() => pendingInstall.value !== null && !pendingInstall.value.downgrade)

/** Whether the open install prompt is the explicit downgrade one. */
const downgradePromptOpen = computed<boolean>(() => pendingInstall.value !== null && pendingInstall.value.downgrade)

/** Whether an update is on offer and not yet staged, so the page lists what it changes. */
const updatePending = computed<boolean>(
  () =>
    details.value !== null &&
    details.value.installed &&
    details.value.updateAvailable &&
    details.value.stagedVersion !== details.value.latestVersion,
)

/** Whether the paid plugin still needs a key before it can be installed. */
const licenceNeeded = computed<boolean>(() => details.value !== null && details.value.requiresLicence && !details.value.hasLicenceKey)

/** Closed set of the page's tabs; the strings are the tab values. */
const DetailsTab = {
  /** The outside links. */
  Overview: 'overview',
  /** The plugin's configuration and slot widget bindings. */
  Settings: 'settings',
  /** Every version. */
  Versions: 'versions',
  /** The licence key of a paid plugin. */
  Licence: 'licence',
  /** What a pending update changes. */
  Changes: 'changes',
} as const

/** One of the {@link DetailsTab} values. */
type DetailsTab = (typeof DetailsTab)[keyof typeof DetailsTab]

/** The tab the operator picked; {@link activeTab} falls back when it no longer applies. */
const pickedTab = ref<DetailsTab>(DetailsTab.Overview)

/** The tab strip: Settings for a loaded plugin; Versions, Licence and Changes only with registry details (Licence for a plugin that needs a key, Changes while an update is pending). */
const tabs = computed<UiTabItem[]>(() => [
  { value: DetailsTab.Overview, label: t('pluginDetails.tabs.overview'), testid: 'plugin-details-tab-overview' },
  ...(plugin.value?.isLoaded === true
    ? [{ value: DetailsTab.Settings, label: t('pluginDetails.tabs.settings'), testid: 'plugin-details-tab-settings' }]
    : []),
  ...(details.value !== null
    ? [{ value: DetailsTab.Versions, label: t('pluginDetails.tabs.versions'), testid: 'plugin-details-tab-versions' }]
    : []),
  ...(details.value?.requiresLicence === true || licenceGated.value
    ? [{ value: DetailsTab.Licence, label: t('pluginDetails.tabs.licence'), testid: 'plugin-details-tab-licence' }]
    : []),
  ...(updatePending.value
    ? [{ value: DetailsTab.Changes, label: t('pluginDetails.tabs.changes'), testid: 'plugin-details-tab-changes' }]
    : []),
])

/** The tab on show: the picked one while it is offered, the overview otherwise (an update was just installed, say). */
const activeTab = computed<DetailsTab>(
  () => Object.values(DetailsTab).find(known => known === pickedTab.value && tabs.value.some(tab => tab.value === known)) ?? DetailsTab.Overview,
)

/**
 * Switches tab from the strip's event; a value that is no tab is ignored.
 *
 * @param value - The value the strip reported.
 */
const selectTab = (value: string): void => {
  const next = Object.values(DetailsTab).find(known => known === value)
  if (next !== undefined) {
    pickedTab.value = next
  }
}

/**
 * Loads the installed list, which every part of the page reads from.
 *
 * @returns Resolves when the request is over; a refusal is kept by the store as its sentence.
 */
const loadList = async (): Promise<void> => {
  await store.fetchAll()
  listSettled.value = true
}

/**
 * Loads the registry details of the plugin the address names.
 *
 * @returns Resolves when the request is over; a refusal is kept by the store as that plugin's sentence.
 */
const loadRegistry = (): Promise<void> => store.fetchRegistryDetails(pluginId.value)

/**
 * Loads the plugin's slot widgets; a plugin that is not loaded declares none the server can list.
 *
 * @returns Resolves when the request is over.
 */
const loadWidgets = async (): Promise<void> => {
  if (plugin.value?.isLoaded === true) {
    await store.fetchSlotWidgetsFor(pluginId.value)
  }
}

/**
 * Loads everything the page shows: the list first (the plugin must be known), the registry details, the integrations
 * list and the slot widgets alongside.
 *
 * @returns Resolves when every request is over.
 */
const load = async (): Promise<void> => {
  await Promise.all([loadList(), loadRegistry(), integrations.fetchAll()])
  await loadWidgets()
}

/**
 * Shows the outcome of an action: the success sentence, or the API's own sentence on a refusal.
 *
 * @param ok - Whether the API accepted the change.
 * @param successText - The success sentence.
 * @param failure - The API's sentence for the refusal, null when it gave none.
 */
const showOutcome = (ok: boolean, successText: string, failure: string | null): void => {
  if (ok) {
    feedback.show(successText, false)
  } else {
    feedback.show(failure ?? t('errors.noAnswer'), true)
  }
}

/**
 * Asks to install a version: ordinary installs and updates get the plain prompt, an older version than
 * the installed one gets the downgrade prompt.
 *
 * @param version - The version to install.
 */
const askInstall = (version: string): void => {
  const downgrade = relationTo(version, plugin.value?.version ?? null) === VersionRelation.Older
  pendingInstall.value = { version, downgrade }
}

/** Asks to update to the newest version the registry offers. */
const askUpdate = (): void => {
  if (details.value !== null) {
    askInstall(details.value.latestVersion)
  }
}

/** Closes the install prompts without installing. */
const dismissInstall = (): void => {
  pendingInstall.value = null
}

/** The processing dialog's state: the install, update and removal of this plugin run behind it. */
const processing = usePluginProcessing()

/** Name the processing dialog shows: the plugin's, its id while the list does not know it. */
const processingName = computed<string>(() => plugin.value?.name ?? details.value?.name ?? pluginId.value)

/** Closes the prompt and installs the version the operator confirmed, behind the processing dialog. */
const handleInstall = async (): Promise<void> => {
  const pending = pendingInstall.value
  if (pending === null) {
    return
  }
  pendingInstall.value = null
  await processing.run({
    name: processingName.value,
    step: plugin.value === undefined ? PluginProcessingStep.Installing : PluginProcessingStep.Updating,
    /**
     * The store call the dialog waits for.
     *
     * @returns The call's outcome.
     */
    work: () => store.installFromRegistry(pluginId.value, pending.version, pending.downgrade),
  })
}

/** True once the plugin is removed: the page goes back to the plugins page when the operator closes the outcome dialog. */
const leavingAfterClose = ref(false)

/** Opens the removal prompt. */
const askUninstall = (): void => {
  confirmingUninstall.value = true
}

/** Closes the removal prompt without removing. */
const dismissUninstall = (): void => {
  confirmingUninstall.value = false
}

/** Closes the prompt and uninstalls the plugin behind the processing dialog; on success returns to the plugins page, on a refusal the dialog keeps the API's sentence. */
const handleUninstall = async (): Promise<void> => {
  confirmingUninstall.value = false
  const outcome = await processing.run({
    name: processingName.value,
    step: PluginProcessingStep.Removing,
    /**
     * The store call the dialog waits for.
     *
     * @returns The call's outcome.
     */
    work: () => store.remove(pluginId.value),
  })
  leavingAfterClose.value = outcome.ok
}

/**
 * Empties the licence key field by giving the card a new identity: `hasKey` does not change when a key
 * replaces one already saved, so the card cannot tell a successful save from the field alone.
 */
const clearLicenceField = (): void => {
  licenceFieldEpoch.value += 1
}

/**
 * Stores the licence key the operator typed and shows the outcome.
 *
 * @param key - The licence key.
 */
const handleSaveKey = async (key: string): Promise<void> => {
  feedback.clear()
  // The store reloads the details itself, so `hasLicenceKey` is current when this returns.
  const outcome = await store.saveLicenceKey(pluginId.value, key)
  if (outcome.ok) {
    clearLicenceField()
  }
  showOutcome(outcome.ok, t('pluginDetails.feedback.licenceSaved'), outcome.error)
}

/** Removes the stored licence key and shows the outcome. */
const handleRemoveKey = async (): Promise<void> => {
  feedback.clear()
  const outcome = await store.removeLicenceKey(pluginId.value)
  if (outcome.ok) {
    clearLicenceField()
  }
  showOutcome(outcome.ok, t('pluginDetails.feedback.licenceRemoved'), outcome.error)
}

/** Restarts the server so installed or removed plugins take effect. */
const handleRestart = async (): Promise<void> => {
  restarting.value = true
  await store.restart()
  restarting.value = false
}

// The same screen serves every plugin: a new id in the address loads that plugin.
watch(pluginId, () => {
  feedback.clear()
  void loadRegistry()
  void loadWidgets()
})

// The removal's outcome (the restart note) stays on screen until the operator closes it, then the page is left.
watch(processing.open, (open) => {
  if (!open && leavingAfterClose.value) {
    void router.push(AppRoute.Plugins)
  }
})

// Loads the details when the screen opens.
onMounted(load)
</script>

<template>
  <UiStack gap="lg" class="w-full" data-testid="plugin-details">
    <UiLink :to="AppRoute.Plugins" :variant="UiButtonVariant.Link" class="self-start" data-testid="plugin-details-back">
      <UiIcon :markup="Icon.Back" size="sm" />
      {{ t('pluginDetails.back') }}
    </UiLink>

    <RestartBanner v-if="store.requiresRestart" :restarting="restarting" @restart="handleRestart" />

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="plugin-details-feedback" />

    <template v-if="showLoading">
      <PluginDetailsHeader :plugin="null" :registry="null" :busy="false" />
      <UiTabs :model-value="DetailsTab.Overview" :items="tabs" :aria-label="t('pluginDetails.tabs.label')" />
      <PluginDetailsLinks :details="null" />
    </template>

    <UiErrorState
      v-else-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="plugin-details-error"
      @retry="load"
    />

    <UiEmptyState
      v-else-if="plugin === undefined"
      :title="t('pluginDetails.notFoundTitle')"
      :description="t('pluginDetails.notFoundDescription')"
      data-testid="plugin-details-not-found"
    />

    <template v-else>
      <PluginDetailsHeader :plugin="plugin" :registry="details" :busy="busy" @update="askUpdate" @uninstall="askUninstall" />
      <!-- A registry that is off or does not know the plugin is silent; any other refusal is told, without hiding the installed plugin. -->
      <UiErrorState
        v-if="registryError !== null"
        :message="registryError"
        :loading="state?.loading ?? false"
        testid="plugin-details-registry-error"
        @retry="loadRegistry"
      />
      <UiTabs
        :model-value="activeTab"
        :items="tabs"
        :aria-label="t('pluginDetails.tabs.label')"
        data-testid="plugin-details-tabs"
        @update:model-value="selectTab"
      />
      <template v-if="activeTab === DetailsTab.Overview">
        <PluginInstalledOverview :plugin="plugin" />
        <PluginDetailsLinks v-if="details !== null || registryLoading" :details="details" />
      </template>
      <PluginSettingsTab
        v-else-if="activeTab === DetailsTab.Settings"
        :plugin="plugin"
        :has-config="hasConfig"
        :widgets="widgets"
        :products="store.productOptions"
        :widgets-error="widgetsError"
        :widgets-loading="widgetsLoading"
        :save="(widget, productIds) => store.saveSlotWidgetProducts(pluginId, widget, productIds)"
        :binding-error="store.bindingError"
        @retry="loadWidgets"
      />
      <PluginVersionList
        v-else-if="activeTab === DetailsTab.Versions && details !== null"
        :versions="details.versions"
        :installed-version="plugin.version"
        :staged-version="plugin.stagedVersion"
        :latest-version="details.latestVersion"
        :busy="busy"
        @install="askInstall"
      />
      <template v-else-if="activeTab === DetailsTab.Licence">
        <UiAlert v-if="licenceNeeded" :variant="UiTone.Warning" data-testid="plugin-details-licence-needed">
          {{ t('pluginDetails.licence.needed') }}
        </UiAlert>
        <PluginLicenceCard
          :key="licenceFieldEpoch"
          :has-key="details?.hasLicenceKey ?? false"
          :busy="busy"
          @save="handleSaveKey"
          @remove="handleRemoveKey"
        />
      </template>
      <UiCard v-else-if="activeTab === DetailsTab.Changes && details !== null" padding="md" data-testid="plugin-details-changes">
        <PluginUpdateChanges
          :versions="details.versions"
          :installed-version="plugin.version"
          :target-version="details.latestVersion"
        />
      </UiCard>
    </template>

    <UiDialog
      :open="installPromptOpen"
      :variant="UiDialogVariant.Confirm"
      :title="t('pluginDetails.confirm.installTitle')"
      :message="t('pluginDetails.confirm.installMessage', { name: plugin?.name ?? '', version: pendingInstall?.version ?? '' })"
      :confirm-label="t('pluginDetails.confirm.install')"
      :loading="busy"
      :tone="UiTone.Primary"
      :size="UiDialogSize.Lg"
      data-testid="plugin-details-install-dialog"
      @confirm="handleInstall"
      @close="dismissInstall"
    >
      <PluginUpdateChanges
        v-if="details !== null && plugin !== undefined && pendingInstall !== null"
        :versions="details.versions"
        :installed-version="plugin.version"
        :target-version="pendingInstall.version"
      />
    </UiDialog>

    <UiDialog
      :open="downgradePromptOpen"
      :variant="UiDialogVariant.Confirm"
      :title="t('pluginDetails.confirm.downgradeTitle')"
      :message="t('pluginDetails.confirm.downgradeMessage', {
        name: plugin?.name ?? '',
        version: pendingInstall?.version ?? '',
        installed: plugin?.version ?? '',
      })"
      :confirm-label="t('pluginDetails.confirm.downgrade')"
      :loading="busy"
      :tone="UiTone.Warning"
      data-testid="plugin-details-downgrade-dialog"
      @confirm="handleInstall"
      @close="dismissInstall"
    />

    <PluginProcessingDialog :processing="processing" />

    <PluginRemoveDialog
      :open="confirmingUninstall"
      :plugin-id="pluginId"
      :loading="busy"
      @confirm="handleUninstall"
      @close="dismissUninstall"
    />
  </UiStack>
</template>
