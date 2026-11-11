<script setup lang="ts">
/**
 * Dialog that asks when a campaign should go out. The operator picks a date and time with the
 * kit's date-time picker (nothing earlier than now) and confirms; the dialog only reports the
 * moment, the form does the scheduling. Emits `confirm` with the moment and `close` on cancel,
 * Escape or a backdrop click.
 */
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiDateTimePicker from '@/components/ui/UiDateTimePicker.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import { UiButtonVariant } from '@/utils/ui/button'

const { t } = useI18n()

/** Props for EmailCampaignScheduleDialog. */
defineProps<{
  /** True while the schedule request is in flight. */
  loading: boolean
}>()

const emit = defineEmits<{
  /** Emitted with the chosen send time when the operator confirms. */
  confirm: [at: Date]
  /** Emitted when the operator closes the dialog. */
  close: []
}>()

/** The moment the operator picked, null while none is. */
const at = ref<Date | null>(null)

/** The earliest moment that can be picked: the moment the dialog opened. */
const earliest = new Date()

/** Reports the chosen moment, if there is one. */
const confirm = (): void => {
  if (at.value !== null) {
    emit('confirm', at.value)
  }
}
</script>

<template>
  <UiDialog :title="t('emailCampaigns.scheduleTitle')" data-testid="email-campaign-schedule-dialog" @close="emit('close')">
    <UiField :label="t('emailCampaigns.sendAt')" :hint="t('emailCampaigns.sendAtHint')">
      <UiDateTimePicker
        v-model="at"
        :min-date="earliest"
        :placeholder="t('emailCampaigns.sendAtPlaceholder')"
        data-testid="email-campaign-schedule-at"
      />
    </UiField>
    <template #footer>
      <UiButton :variant="UiButtonVariant.Secondary" data-testid="email-campaign-schedule-cancel" @click="emit('close')">
        {{ t('shared.cancel') }}
      </UiButton>
      <UiButton
        :variant="UiButtonVariant.Primary"
        :loading="loading"
        :disabled="at === null"
        data-testid="email-campaign-schedule-confirm"
        @click="confirm"
      >
        {{ t('emailCampaigns.schedule') }}
      </UiButton>
    </template>
  </UiDialog>
</template>
