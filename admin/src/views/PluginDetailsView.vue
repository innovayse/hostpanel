<script setup lang="ts">
/**
 * Plugin details page (`/plugins/:id`): the registry's page for one plugin.
 *
 * Shows the head (picture, name, publisher, badges, state on this server, update and uninstall) over four tabs:
 * Overview (the outside links), Versions (every version with its release notes, compatibility with this host,
 * permissions and status), Licence (the key card of a paid plugin) and Changes (what a pending update changes;
 * the last two are offered only when they apply). A chosen version is installed after
 * a confirmation; an older one than the installed version needs its own explicit downgrade
 * confirmation. Uninstalling is confirmed first and raises the restart banner. The page loads the
 * details through the plugins store, shows a skeleton while they load, the API's sentence with a
 * retry when the load fails, and a not-found state when the registry has no such plugin.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { usePluginsStore } from '@/stores/plugins'
import PluginDetailsHeader from '@/components/plugins/details/PluginDetailsHeader.vue'
import PluginDetailsLinks from '@/components/plugins/details/PluginDetailsLinks.vue'
import PluginLicenceCard from '@/components/plugins/details/PluginLicenceCard.vue'
import PluginUpdateChanges from '@/components/plugins/details/PluginUpdateChanges.vue'
import PluginVersionList from '@/components/plugins/details/PluginVersionList.vue'
import { relationTo } from '@/components/plugins/details/versionOrder'
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
import { useResultBanner } from '@/composables/useResultBanner'
import { Icon } from '@/lib/icons'
import { AppRoute } from '@/lib/routes'
import { PluginErrorCode, VersionRelation } from '@/types/plugin'
import type { RegistryDetailsState, RegistryPluginDetailsDto } from '@/types/plugin'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDialogSize, UiDialogVariant } from '@/utils/ui/dialog'
import type { UiTabItem } from '@/utils/ui/tabs'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** The current route: its `id` parameter names the plugin. */
const route = useRoute()

/** Plugins store: the registry details, the installs, the removal and the licence key. */
const store = usePluginsStore()

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

/** Load state of this plugin's details; undefined until the first load starts. */
const state = computed<RegistryDetailsState | undefined>(() => store.registryDetails[pluginId.value])

/** The loaded details, null while loading, after a failure or when the plugin is unknown. */
const details = computed<RegistryPluginDetailsDto | null>(() => state.value?.details ?? null)

/** True while the first load runs and nothing is on screen yet. */
const showLoading = computed<boolean>(() => details.value === null && (state.value === undefined || state.value.loading))

/** The sentence of a failed load when there is nothing to show, null otherwise. */
const loadError = computed<string | null>(() => (details.value === null ? (state.value?.error ?? null) : null))

/** True when the load was refused because the plugin's page is gated by a licence key the server lacks or the registry rejected. */
const licenceGated = computed<boolean>(
  () => details.value === null && (state.value?.errorCode === PluginErrorCode.RegistryLicenceRequired
    || state.value?.errorCode === PluginErrorCode.RegistryLicenceInvalid),
)

/** True when the registry answered that it has no such plugin. */
const notFound = computed<boolean>(() => state.value?.notFound === true)

/** True while an install, update, uninstall or licence change of this plugin is in flight. */
const busy = computed<boolean>(() => store.isBusy(pluginId.value))

/** True while the server restart request is in flight. */
const restarting = ref(false)

/** Bumped after a licence key is saved or removed; keys the licence card so its field starts empty again. */
const licenceFieldEpoch = ref(0)

/** The install waiting for the operator's confirmation, null when no prompt is open. */
const pendingInstall = ref<PendingInstall | null>(null)

/** Whether the uninstall prompt is open. */
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

/** The tab strip: Licence only for a plugin that needs a key, Changes only while an update is pending. */
const tabs = computed<UiTabItem[]>(() => [
  { value: DetailsTab.Overview, label: t('pluginDetails.tabs.overview'), testid: 'plugin-details-tab-overview' },
  { value: DetailsTab.Versions, label: t('pluginDetails.tabs.versions'), testid: 'plugin-details-tab-versions' },
  ...(details.value?.requiresLicence === true
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
 * Loads the details of the plugin the address names.
 *
 * @returns Resolves when the request is over; a refusal is kept by the store as that plugin's sentence.
 */
const load = (): Promise<void> => store.fetchRegistryDetails(pluginId.value)

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
  const downgrade = relationTo(version, details.value?.installedVersion ?? null) === VersionRelation.Older
  pendingInstall.value = { version, downgrade }
}

/** Asks to update to the newest version the registry offers. */
const askUpdate = (): void => {
  if (details.value !== null) {
    askInstall(details.value.latestVersion)
  }
}

/** Closes the install prompts; ignored while the install runs. */
const dismissInstall = (): void => {
  if (!busy.value) {
    pendingInstall.value = null
  }
}

/** Installs the version the operator confirmed and shows the outcome. */
const handleInstall = async (): Promise<void> => {
  const pending = pendingInstall.value
  if (pending === null) {
    return
  }
  feedback.clear()
  const outcome = await store.installFromRegistry(pluginId.value, pending.version, pending.downgrade)
  pendingInstall.value = null
  if (outcome.ok && outcome.stagedVersion !== null) {
    showOutcome(true, t('pluginDetails.feedback.staged', { version: outcome.stagedVersion }), null)
  } else {
    showOutcome(outcome.ok, t('pluginDetails.feedback.installed', { version: pending.version }), outcome.error)
  }
}

/** Opens the uninstall prompt. */
const askUninstall = (): void => {
  confirmingUninstall.value = true
}

/** Closes the uninstall prompt; ignored while the removal runs. */
const dismissUninstall = (): void => {
  if (!busy.value) {
    confirmingUninstall.value = false
  }
}

/** Uninstalls the plugin the operator confirmed and shows the outcome. */
const handleUninstall = async (): Promise<void> => {
  feedback.clear()
  const outcome = await store.remove(pluginId.value)
  confirmingUninstall.value = false
  showOutcome(outcome.ok, t('pluginDetails.feedback.uninstalled'), outcome.error)
  if (outcome.ok) {
    await load()
  }
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
  void load()
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
      <PluginDetailsHeader :details="null" :busy="false" />
      <UiTabs :model-value="DetailsTab.Overview" :items="tabs" :aria-label="t('pluginDetails.tabs.label')" />
      <PluginDetailsLinks :details="null" />
    </template>

    <template v-else-if="loadError !== null">
      <UiErrorState
        :message="loadError"
        :loading="state?.loading ?? false"
        testid="plugin-details-error"
        @retry="load"
      />
      <!-- A licence-gated page has no details to read the key state from, so the card is offered as soon as the gate is named. -->
      <PluginLicenceCard
        v-if="licenceGated"
        :key="licenceFieldEpoch"
        :has-key="false"
        :busy="busy"
        @save="handleSaveKey"
        @remove="handleRemoveKey"
      />
    </template>

    <UiEmptyState
      v-else-if="notFound"
      :title="t('pluginDetails.notFoundTitle')"
      :description="t('pluginDetails.notFoundDescription')"
      data-testid="plugin-details-not-found"
    />

    <template v-else-if="details !== null">
      <PluginDetailsHeader :details="details" :busy="busy" @update="askUpdate" @uninstall="askUninstall" />
      <UiTabs
        :model-value="activeTab"
        :items="tabs"
        :aria-label="t('pluginDetails.tabs.label')"
        data-testid="plugin-details-tabs"
        @update:model-value="selectTab"
      />
      <PluginDetailsLinks v-if="activeTab === DetailsTab.Overview" :details="details" />
      <PluginVersionList
        v-else-if="activeTab === DetailsTab.Versions"
        :versions="details.versions"
        :installed-version="details.installedVersion"
        :staged-version="details.stagedVersion"
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
          :has-key="details.hasLicenceKey"
          :busy="busy"
          @save="handleSaveKey"
          @remove="handleRemoveKey"
        />
      </template>
      <UiCard v-else-if="activeTab === DetailsTab.Changes" padding="md" data-testid="plugin-details-changes">
        <PluginUpdateChanges
          :versions="details.versions"
          :installed-version="details.installedVersion"
          :target-version="details.latestVersion"
        />
      </UiCard>
    </template>

    <UiDialog
      :open="installPromptOpen"
      :variant="UiDialogVariant.Confirm"
      :title="t('pluginDetails.confirm.installTitle')"
      :message="t('pluginDetails.confirm.installMessage', { name: details?.name ?? '', version: pendingInstall?.version ?? '' })"
      :confirm-label="t('pluginDetails.confirm.install')"
      :loading="busy"
      :tone="UiTone.Primary"
      :size="UiDialogSize.Lg"
      data-testid="plugin-details-install-dialog"
      @confirm="handleInstall"
      @close="dismissInstall"
    >
      <PluginUpdateChanges
        v-if="details !== null && pendingInstall !== null"
        :versions="details.versions"
        :installed-version="details.installedVersion"
        :target-version="pendingInstall.version"
      />
    </UiDialog>

    <UiDialog
      :open="downgradePromptOpen"
      :variant="UiDialogVariant.Confirm"
      :title="t('pluginDetails.confirm.downgradeTitle')"
      :message="t('pluginDetails.confirm.downgradeMessage', {
        name: details?.name ?? '',
        version: pendingInstall?.version ?? '',
        installed: details?.installedVersion ?? '',
      })"
      :confirm-label="t('pluginDetails.confirm.downgrade')"
      :loading="busy"
      :tone="UiTone.Warning"
      data-testid="plugin-details-downgrade-dialog"
      @confirm="handleInstall"
      @close="dismissInstall"
    />

    <UiDialog
      :open="confirmingUninstall"
      :variant="UiDialogVariant.Confirm"
      :title="t('pluginDetails.confirm.uninstallTitle')"
      :message="t('pluginDetails.confirm.uninstallMessage', { name: details?.name ?? '' })"
      :confirm-label="t('pluginDetails.uninstall')"
      :loading="busy"
      :tone="UiTone.Danger"
      data-testid="plugin-details-uninstall-dialog"
      @confirm="handleUninstall"
      @close="dismissUninstall"
    />
  </UiStack>
</template>
