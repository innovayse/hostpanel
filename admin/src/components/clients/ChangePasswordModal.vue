<script setup lang="ts">
/**
 * Small modal for admin-initiated password change.
 * Single password field + Save/Cancel.
 */
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiInput from '@/components/ui/UiInput.vue'
import { UiButtonVariant } from '@/utils/ui/button'

const { t } = useI18n()

/** Props for ChangePasswordModal. */
defineProps<{
  /** Whether a save is in flight. */
  saving?: boolean
}>()

const emit = defineEmits<{
  /** Emitted with the new password when saved. */
  save: [password: string]
  /** Emitted when the modal is closed. */
  close: []
}>()

/** New password value. */
const password = ref('')

/** Error if password is empty. */
const fieldError = ref('')

/**
 * Validates and emits the save event.
 *
 * @returns Nothing.
 */
const handleSave = (): void => {
  if (!password.value.trim()) {
    fieldError.value = t('clients.changePassword.passwordIsRequired')
    return
  }
  fieldError.value = ''
  emit('save', password.value)
}
</script>

<template>
  <UiDialog :title="t('shared.changePassword')" size="sm" @close="emit('close')">
    <UiField :label="t('clients.changePassword.newPassword')" :error="fieldError || undefined">
      <template #default="{ id, invalid, describedby }">
        <UiInput
          :id="id"
          v-model="password"
          type="password"
          :invalid="invalid"
          :aria-describedby="describedby"
          :placeholder="t('clients.changePassword.enterNewPassword')"
          data-testid="clients-change-password-input"
          @keydown.enter="handleSave"
        />
      </template>
    </UiField>
    <template #footer>
      <UiButton
        :variant="UiButtonVariant.Secondary"
        data-testid="clients-change-password-cancel"
        @click="emit('close')"
      >
        {{ t('shared.cancel') }}
      </UiButton>
      <UiButton
        :variant="UiButtonVariant.Primary"
        :loading="saving"
        data-testid="clients-change-password-save"
        @click="handleSave"
      >
        {{ t('shared.save') }}
      </UiButton>
    </template>
  </UiDialog>
</template>
