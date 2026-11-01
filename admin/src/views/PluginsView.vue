<script setup lang="ts">
/**
 * Plugin Manager page.
 *
 * Lists installed plugins, provides ZIP upload for installation,
 * and shows a restart banner when pending changes need to take effect. A refused load offers a
 * retry; the outcome of an install or remove (success, or the API's sentence) shows above the list;
 * a removal is confirmed first in a danger prompt. The "Allow unsigned plugins" switch with its warning sits
 * under the uploader and saves through the settings store, like every other settings row.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { usePluginsStore } from '@/stores/plugins'
import { useSettingsStore } from '@/stores/settings'
import PluginCard from '@/components/plugins/PluginCard.vue'
import PluginSigningCard from '@/components/plugins/PluginSigningCard.vue'
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
import { useResultBanner } from '@/composables/useResultBanner'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'
import type { SettingSaveResult } from '@/types/setting'

/** Translation function. */
const { t } = useI18n()

/** Plugins store: the installed list, the restart flag and the refusal sentence. */
const store = usePluginsStore()

/** Settings store: the rows the signing switch reads, and the save that writes one. */
const settingsStore = useSettingsStore()

/** Id of the plugin whose removal is in flight. */
const removingId = ref<string | null>(null)

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
 */
const showOutcome = (ok: boolean, successKey: string): void => {
  if (ok) {
    feedback.show(t(successKey), false)
  } else {
    feedback.show(store.actionError ?? t('errors.noAnswer'), true)
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
  const ok = await store.remove(id)
  removingId.value = null
  confirmingId.value = null
  showOutcome(ok, 'plugins.plugins.pluginRemoved')
}

/** Restarts the server so installed or removed plugins take effect. */
const handleRestart = async (): Promise<void> => {
  restarting.value = true
  await store.restart()
  restarting.value = false
}

/**
 * Installs an uploaded plugin archive.
 *
 * @param file - The chosen `.zip` archive.
 */
const handleUpload = async (file: File): Promise<void> => {
  feedback.clear()
  showOutcome(await store.install(file), 'plugins.plugins.pluginInstalled')
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

/** Loads the installed plugins and the settings the signing switch reads. */
onMounted(() => Promise.all([store.fetchAll(), settingsStore.fetchSettings()]))
</script>

<template>
  <UiStack gap="lg" class="w-full">
    <UiPageHeader
      :title="t('plugins.plugins.pluginManager')"
      :subtitle="store.plugins.length > 0
        ? `${t('plugins.plugins.installAndManageProviderPlugins')} ${t('plugins.plugins.lengthInstalled', { length: store.plugins.length })}`
        : t('plugins.plugins.installAndManageProviderPlugins')"
    />

    <!-- Restart banner -->
    <RestartBanner v-if="store.requiresRestart" :restarting="restarting" @restart="handleRestart" />

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

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="plugins-feedback" />

    <!-- Error -->
    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="plugins-error"
      @retry="store.fetchAll()"
    />

    <!-- Loading -->
    <UiSkeleton v-else-if="store.loading && store.plugins.length === 0" :lines="3" data-testid="plugins-loading" />

    <!-- Empty state -->
    <UiEmptyState
      v-else-if="!store.loading && store.plugins.length === 0"
      :title="t('plugins.plugins.noPluginsInstalledYet')"
      :description="`${t('plugins.plugins.uploadA')} ${t('plugins.plugins.zip')} ${t('plugins.plugins.fileAboveToGetStarted')}`"
      data-testid="plugins-empty"
    />

    <!-- Plugin list -->
    <UiStack v-else gap="sm" data-testid="plugins-list">
      <UiHeading :level="2" variant="section">{{ t('plugins.plugins.installedPlugins') }}</UiHeading>
      <PluginCard
        v-for="plugin in store.plugins"
        :key="plugin.id"
        :plugin="plugin"
        :removing="removingId === plugin.id"
        @remove="askRemove"
      />
    </UiStack>

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
  </UiStack>
</template>
