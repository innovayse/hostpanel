<script setup lang="ts">
/**
 * The Settings tab: what applies to every plugin rather than to one. It holds the signature policy (the
 * "Allow unsigned plugins" switch with its warning, or the retry of a failed settings load) and, while
 * any exist, the removed plugins whose data the server kept. Purging that data is destructive, so the
 * component asks in a danger prompt first and only then reports the confirmed id; the screen runs the purge.
 */
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import PluginKeptData from '@/components/plugins/PluginKeptData.vue'
import PluginSigningCard from '@/components/plugins/PluginSigningCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'
import type { Setting, SettingSave } from '@/types/setting'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginGlobalSettings. */
defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
  /** Sentence for a failed settings load, null when none. */
  error: string | null
  /** True while the settings load. */
  loading: boolean
  /** Persists one settings row and answers how it went. */
  save: SettingSave
  /** Ids of the removed plugins whose data the server still keeps. */
  keptDataIds: string[]
  /** Id of the plugin whose data purge is in flight, null when none. */
  purgingId: string | null
}>()

/** Events emitted by PluginGlobalSettings. */
const emit = defineEmits<{
  /** Emitted when the admin asks to retry a failed settings load. */
  retry: []
  /** Emitted with the plugin id when the admin has confirmed the purge of its data. */
  purge: [id: string]
}>()

/** Id of the plugin waiting for the purge confirmation, null when the prompt is closed. */
const confirmId = ref<string | null>(null)

/**
 * Opens the purge confirmation for a removed plugin's data.
 *
 * @param id - Id of the plugin whose data to delete.
 */
const askPurge = (id: string): void => {
  confirmId.value = id
}

/** Closes the purge confirmation without purging. */
const dismissPurge = (): void => {
  confirmId.value = null
}

/** Reports the confirmed purge to the screen and closes the prompt. */
const confirmPurge = (): void => {
  if (confirmId.value === null) {
    return
  }
  emit('purge', confirmId.value)
  confirmId.value = null
}
</script>

<template>
  <UiStack gap="lg">
    <UiErrorState
      v-if="error"
      :message="error"
      :loading="loading"
      testid="plugin-signing-error"
      @retry="emit('retry')"
    />
    <PluginSigningCard v-else :settings="settings" :save="save" />

    <PluginKeptData
      v-if="keptDataIds.length > 0"
      :ids="keptDataIds"
      :purging-id="purgingId"
      @purge="askPurge"
    />

    <UiDialog
      :open="confirmId !== null"
      :variant="UiDialogVariant.Confirm"
      :title="t('plugins.plugins.confirmPurgeTitle')"
      :message="t('plugins.plugins.confirmPurgeMessage', { id: confirmId ?? '' })"
      :confirm-label="t('plugins.keptData.purge')"
      :tone="UiTone.Danger"
      data-testid="plugins-purge-dialog"
      @confirm="confirmPurge"
      @close="dismissPurge"
    />
  </UiStack>
</template>
