<script setup lang="ts">
/**
 * Confirmation prompt for giving a client a status that restricts the account (Suspended or
 * Closed), with the effects of that status spelled out. Shared by the screens that set a client's
 * status; the caller mounts it with `v-if` while a change is waiting for the operator.
 */
import { useI18n } from 'vue-i18n'
import UiDialog from '@/components/ui/UiDialog.vue'
import { ClientStatus } from '@/types/client'
import type { RestrictiveClientStatus } from '@/lib/clientStatus'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** Props for ClientStatusConfirmDialog. */
defineProps<{
  /** The restrictive status awaiting confirmation. */
  status: RestrictiveClientStatus
  /** Whether the confirmed action is running. */
  loading?: boolean
  /** Prefix of the `data-testid` of the dialog. */
  testid: string
}>()

const emit = defineEmits<{
  /** Emitted when the operator confirms the status. */
  confirm: []
  /** Emitted when the operator backs out. */
  close: []
}>()
</script>

<template>
  <UiDialog
    variant="confirm"
    :title="t(`clients.clientProfile.statusConfirm.${status}.title`)"
    :message="t(`clients.clientProfile.statusEffects.${status}`)"
    :confirm-label="t(`clients.clientProfile.statusConfirm.${status}.action`)"
    :loading="loading"
    :tone="status === ClientStatus.Closed ? UiTone.Danger : UiTone.Warning"
    :data-testid="testid"
    @confirm="emit('confirm')"
    @close="emit('close')"
  />
</template>
