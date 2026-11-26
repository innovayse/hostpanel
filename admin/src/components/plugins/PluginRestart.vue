<script setup lang="ts">
/**
 * Everything a plugins screen needs for the server restart, in one place: the warning banner while a restart is
 * pending, the confirmation the "Restart now" button opens, and the blocking dialog that waits for the server to
 * come back. The screens render it once and hold no restart state of their own.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import PluginRestartDialog from '@/components/plugins/PluginRestartDialog.vue'
import RestartBanner from '@/components/plugins/RestartBanner.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import { usePluginsStore } from '@/stores/plugins'
import { PluginRestartState } from '@/types/plugin'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Plugins store: the pending flag, the restart state and the action. */
const store = usePluginsStore()

/** Whether the confirmation is open. */
const confirming = ref(false)

/** True while the panel waits for the server; the banner button shows its spinner. */
const restarting = computed<boolean>(() => store.restartState === PluginRestartState.Restarting)

/** Opens the confirmation. */
const ask = (): void => {
  confirming.value = true
}

/** Closes the confirmation without restarting. */
const dismiss = (): void => {
  confirming.value = false
}

/** Closes the confirmation and restarts the server; the dialog opens at once and waits for it. */
const confirm = async (): Promise<void> => {
  confirming.value = false
  await store.restart()
}
</script>

<template>
  <RestartBanner v-if="store.requiresRestart" :restarting="restarting" @restart="ask" />
  <UiStatusMessage v-if="store.restartError !== null" :message="store.restartError" :tone="UiTone.Danger" data-testid="plugins-restart-error" />
  <UiDialog
    :open="confirming"
    :variant="UiDialogVariant.Confirm"
    :title="t('plugins.restart.confirmTitle')"
    :message="t('plugins.restart.confirmMessage')"
    :confirm-label="t('plugins.restartBanner.restart')"
    :tone="UiTone.Warning"
    data-testid="plugins-restart-confirm"
    @confirm="confirm"
    @close="dismiss"
  />
  <PluginRestartDialog />
</template>
