<script setup lang="ts">
/**
 * Dialog of the "Sync with panel" command of a service. It first asks for the confirmation, saying what is
 * pushed to the panel (the platform's status and package, and the password only when its box is ticked) and
 * what is not (the domain and the username are never changed); once the sync has run it lists what was done
 * about each field. It holds no request: the page runs the sync and hands the result back.
 */
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { panelSyncOutcomeTone } from '@/lib/tones'
import { PanelSyncField, PanelSyncOutcome } from '@/types/service'
import type { ServiceSyncResult } from '@/types/service'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiLabelVariant, UiTextTone } from '@/utils/ui/text'

/** Locale key of the name of each field. */
const FIELD_LABEL_KEYS: Record<PanelSyncField, string> = {
  [PanelSyncField.Status]: 'clients.clientServiceDetail.syncField.status',
  [PanelSyncField.Package]: 'clients.clientServiceDetail.syncField.package',
  [PanelSyncField.Domain]: 'clients.clientServiceDetail.syncField.domain',
  [PanelSyncField.Password]: 'clients.clientServiceDetail.syncField.password',
  [PanelSyncField.IpAddress]: 'clients.clientServiceDetail.syncField.ipAddress',
}

/** Locale key of the sentence for each outcome. */
const OUTCOME_LABEL_KEYS: Record<PanelSyncOutcome, string> = {
  [PanelSyncOutcome.InSync]: 'clients.clientServiceDetail.syncOutcome.inSync',
  [PanelSyncOutcome.Applied]: 'clients.clientServiceDetail.syncOutcome.applied',
  [PanelSyncOutcome.Failed]: 'clients.clientServiceDetail.syncOutcome.failed',
  [PanelSyncOutcome.Unsupported]: 'clients.clientServiceDetail.syncOutcome.unsupported',
}

const { t } = useI18n()

/** Props for ServicePanelSyncDialog. */
defineProps<{
  /** Whether the sync is running (the confirm button shows a spinner and is disabled). */
  loading?: boolean
  /** What the sync did about each field; null until it has run, which shows the confirmation. */
  result: ServiceSyncResult | null
}>()

const emit = defineEmits<{
  /** Emitted with whether the stored password is also set on the panel when the admin confirms. */
  confirm: [pushPassword: boolean]
  /** Emitted when the dialog is closed. */
  close: []
}>()

/** Whether the stored password is also set on the panel. */
const pushPassword = ref(false)
</script>

<template>
  <UiDialog
    :title="t('clients.clientServiceDetail.syncWithPanel')"
    data-testid="client-service-sync-dialog"
    @close="emit('close')"
  >
    <UiStack v-if="result === null">
      <UiText :tone="UiTextTone.Secondary">{{ t('clients.clientServiceDetail.syncIntro') }}</UiText>
      <UiText :tone="UiTextTone.Muted">{{ t('clients.clientServiceDetail.syncNotChanged') }}</UiText>
      <UiLabel :variant="UiLabelVariant.Inline">
        <UiCheckbox
          v-model="pushPassword"
          :aria-label="t('clients.clientServiceDetail.syncPushPassword')"
          data-testid="client-service-sync-push-password"
        />
        {{ t('clients.clientServiceDetail.syncPushPassword') }}
      </UiLabel>
      <UiText :tone="UiTextTone.Muted">{{ t('clients.clientServiceDetail.syncPushPasswordHint') }}</UiText>
    </UiStack>

    <UiStack v-else data-testid="client-service-sync-result">
      <UiStack
        v-for="entry in result.fields"
        :key="entry.field"
        direction="row"
        wrap
        gap="sm"
        :data-testid="`client-service-sync-field-${entry.field}`"
      >
        <UiText :tone="UiTextTone.Secondary">{{ t(FIELD_LABEL_KEYS[entry.field]) }}</UiText>
        <UiBadge :tone="panelSyncOutcomeTone(entry.outcome)" :data-testid="`client-service-sync-outcome-${entry.field}`">
          {{ t(OUTCOME_LABEL_KEYS[entry.outcome]) }}
        </UiBadge>
        <UiText v-if="entry.panelValue" :tone="UiTextTone.Muted">
          {{ t('clients.clientServiceDetail.syncPanelValue', { value: entry.panelValue }) }}
        </UiText>
      </UiStack>
    </UiStack>

    <template #footer>
      <UiButton
        :action="result === null ? UiActionKind.Cancel : UiActionKind.Close"
        :variant="UiButtonVariant.Secondary"
        data-testid="client-service-sync-close"
        @click="emit('close')"
      >
        {{ result === null ? t('shared.cancel') : t('shared.close') }}
      </UiButton>
      <UiButton
        v-if="result === null"
        :action="UiActionKind.Confirm"
        :variant="UiButtonVariant.Primary"
        :loading="loading"
        data-testid="client-service-sync-confirm"
        @click="emit('confirm', pushPassword)"
      >
        {{ t('clients.clientServiceDetail.syncWithPanel') }}
      </UiButton>
    </template>
  </UiDialog>
</template>
