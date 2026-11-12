<script setup lang="ts">
/**
 * The prompt that rejects an order held for fraud review: it asks for the reason the client is told in the e-mail and
 * refuses an empty one before the call. Mount it with `v-if` while the prompt is open, so every opening starts empty.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { UiButtonVariant } from '@/utils/ui/button'

/** Translates a key into the active locale. */
const { t } = useI18n()

/** Props for OrderRejectReviewDialog. */
const props = defineProps<{
  /** True while the reject request is in flight. */
  loading: boolean
}>()

/** Events of OrderRejectReviewDialog. */
const emit = defineEmits<{
  /** The reason was typed and confirmed; carries it trimmed. */
  submit: [reason: string]
  /** The prompt was cancelled or dismissed. */
  close: []
}>()

/** Longest reason the API accepts, mirrored here so the field stops typing at the limit. */
const REASON_MAX_LENGTH = 1000

/** The reason being typed. */
const reason = ref('')

/** True once a submit was tried, so a missing reason shows its sentence. */
const submitted = ref(false)

/** Sentence under the field after a submit without a reason. */
const reasonError = computed<string>(() =>
  submitted.value && reason.value.trim() === '' ? t('orders.fraud.rejectReasonRequired') : '',
)

/** Sends the reason when there is one; otherwise shows what is missing. */
const submit = (): void => {
  submitted.value = true
  if (reason.value.trim() === '' || props.loading) {
    return
  }
  emit('submit', reason.value.trim())
}

/** Closes the prompt, unless the reject is running. */
const close = (): void => {
  if (!props.loading) {
    emit('close')
  }
}
</script>

<template>
  <UiDialog
    :title="t('orders.fraud.rejectTitle')"
    data-testid="order-reject-dialog"
    @close="close"
  >
    <UiField
      :label="t('orders.fraud.rejectReasonLabel')"
      :hint="t('orders.fraud.rejectReasonHint')"
      :error="reasonError"
      required
      error-testid="order-reject-reason-error"
    >
      <template #default="{ id, invalid, describedby }">
        <UiTextarea
          :id="id"
          v-model="reason"
          rows="4"
          :maxlength="REASON_MAX_LENGTH"
          :invalid="invalid"
          :aria-describedby="describedby"
          :placeholder="t('orders.fraud.rejectReasonPlaceholder')"
          data-testid="order-reject-reason"
        />
      </template>
    </UiField>

    <template #footer>
      <UiButton :variant="UiButtonVariant.Secondary" data-testid="order-reject-cancel" @click="close">
        {{ t('shared.cancel') }}
      </UiButton>
      <UiButton
        :variant="UiButtonVariant.Danger"
        :loading="loading"
        :disabled="loading"
        data-testid="order-reject-submit"
        @click="submit"
      >
        {{ t('orders.fraud.rejectSubmit') }}
      </UiButton>
    </template>
  </UiDialog>
</template>
