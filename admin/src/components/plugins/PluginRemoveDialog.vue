<script setup lang="ts">
/**
 * The confirmation before a plugin is removed from the server.
 *
 * Says that the plugin's settings and stored data are kept (a later install restores them and the data
 * can be purged afterwards). The dialog owns no state: the screen opens it, runs the removal on `confirm`
 * and closes it, so the plugins list and the plugin details page ask the same question the same way.
 */
import { useI18n } from 'vue-i18n'
import UiDialog from '@/components/ui/UiDialog.vue'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginRemoveDialog. */
defineProps<{
  /** Whether the prompt is open. */
  open: boolean
  /** Id of the plugin to remove, named in the message. */
  pluginId: string
  /** True while the removal runs; the prompt cannot be closed meanwhile. */
  loading: boolean
}>()

/** Events emitted by PluginRemoveDialog. */
const emit = defineEmits<{
  /** Emitted when the operator confirms the removal. */
  confirm: []
  /** Emitted when the operator closes the prompt without removing. */
  close: []
}>()
</script>

<template>
  <UiDialog
    :open="open"
    :variant="UiDialogVariant.Confirm"
    :title="t('plugins.plugins.confirmRemoveTitle')"
    :message="t('plugins.plugins.confirmRemoveMessage', { id: pluginId })"
    :confirm-label="t('plugins.pluginCard.remove')"
    :loading="loading"
    :tone="UiTone.Danger"
    data-testid="plugin-remove-dialog"
    @confirm="emit('confirm')"
    @close="emit('close')"
  />
</template>
