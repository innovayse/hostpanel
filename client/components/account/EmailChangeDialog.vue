<template>
  <UiDialog
    :model-value="modelValue"
    :title="$t('client.emailChange.title')"
    size="md"
    @update:model-value="emit('update:modelValue', $event)"
  >
    <UiStack gap="md" data-testid="account-email-change-dialog">
      <UiText size="sm" tone="muted">{{ $t('client.emailChange.desc') }}</UiText>

      <UiForm id="email-change-form" data-testid="email-change-form" :error="emailChangeError" spacing="sm" @submit="onSubmit">
        <UiInput
          v-model="input.newEmail"
          type="email"
          :label="$t('client.emailChange.newEmail')"
          :placeholder="$t('client.account.placeholders.email')"
          :error="errors.newEmail"
          autocomplete="email"
          required
          testid="account-email-change-new"
        />
        <UiInput
          v-model="input.currentPassword"
          type="password"
          :label="$t('client.emailChange.currentPassword')"
          :placeholder="$t('client.twoFactor.currentPasswordPlaceholder')"
          :error="errors.currentPassword"
          autocomplete="current-password"
          required
          testid="account-email-change-password"
        />
      </UiForm>
    </UiStack>

    <!-- Pinned footer; the button submits the body's form through its `form` attribute. -->
    <template #footer>
      <UiButton type="submit" form="email-change-form" :loading="emailChangeSaving" data-testid="account-email-change-submit">
        <Mail v-if="!emailChangeSaving" :size="15" :stroke-width="2" class="mr-1.5" />
        {{ $t('client.emailChange.submit') }}
      </UiButton>
    </template>
  </UiDialog>
</template>

<script setup lang="ts">
/**
 * EmailChangeDialog: collects the new sign-in address and the current password, and asks the
 * account store to mail a confirmation link to the new address.
 *
 * The shape of the address and a typed password are checked here first; everything else (a wrong
 * password, an address in use) is the API's sentence, kept by the store. On success the dialog
 * closes and the card shows where the link went.
 *
 * @module components/account/EmailChangeDialog
 */

import { Mail } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useAccountStore } from '~/stores/account'
import { ActionOutcome } from '~/types/actionoutcome'
import type { EmailChangeInput } from '~/types/emailchangeinput'
import { validateEmailChange } from '~/utils/profileValidation'

/** Props of {@link EmailChangeDialog}. */
interface Props {
  /** Whether the dialog is open. */
  modelValue: boolean
}

/** The props. */
const props = defineProps<Props>()

/** Events of {@link EmailChangeDialog}. */
const emit = defineEmits<{
  /** The dialog asked to open or close. */
  'update:modelValue': [value: boolean]
}>()

/** Translator for the local validation reasons. */
const { t } = useI18n()
/** The account store that sends the request. */
const account = useAccountStore()
/** Progress, the API's sentence and the per-field messages. */
const { emailChangeSaving, emailChangeError, fieldErrors } = storeToRefs(account)

/** The draft: the new address and the current password. */
const input = reactive<EmailChangeInput>({ newEmail: '', currentPassword: '' })

/** The i18n key of each field the local check refused, by field name. */
const localErrors = ref<Record<string, string>>({})

/** The message on each field: the local check's reason, else the API's per-field message. */
const errors = computed<Record<string, string>>(() => {
  const local = Object.fromEntries(Object.entries(localErrors.value).map(([field, key]) => [field, t(key)]))
  return { ...fieldErrors.value, ...local }
})

// Every opening starts from an empty draft and no stale refusal.
watch(() => props.modelValue, (open) => {
  if (open) {
    input.newEmail = ''
    input.currentPassword = ''
    localErrors.value = {}
    account.clearModalErrors()
  }
})

/** Checks the draft and, when it passes, asks for the change; closes on success. */
const onSubmit = async (): Promise<void> => {
  localErrors.value = validateEmailChange(input)
  if (Object.keys(localErrors.value).length > 0) {
    return
  }
  if (await account.startEmailChange({ newEmail: input.newEmail.trim(), currentPassword: input.currentPassword }) === ActionOutcome.Succeeded) {
    emit('update:modelValue', false)
  }
}
</script>
