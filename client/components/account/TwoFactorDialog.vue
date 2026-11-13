<template>
  <UiDialog
    :model-value="modelValue"
    :title="isEnable ? $t('client.twoFactor.setupTitle') : $t('client.twoFactor.disableTitle')"
    size="md"
    @update:model-value="emit('update:modelValue', $event)"
  >
    <UiStack gap="md" data-testid="account-2fa-dialog">
      <UiText size="sm" tone="muted">
        {{ isEnable ? $t('client.twoFactor.setupDesc') : $t('client.twoFactor.disableDesc') }}
      </UiText>

      <!-- The QR is drawn in this browser from a data: URL. A remote image would send the
           otpauth URI, and with it the TOTP secret, to a third party on every enrolment. -->
      <UiStack v-if="isEnable && qrDataUrl" align="center" gap="sm">
        <UiImage
          :src="qrDataUrl"
          :alt="$t('client.twoFactor.qrAlt')"
          :width="180"
          :height="180"
          rounded="md"
          class="bg-white p-3"
          testid="account-2fa-qr"
        />
        <UiText size="xs" tone="subtle">{{ $t('client.twoFactor.setupScanHint') }}</UiText>
      </UiStack>

      <UiCard v-if="isEnable && secret" padding="sm" data-testid="account-2fa-secret">
        <UiText size="xs" tone="subtle">{{ $t('client.twoFactor.setupSecretLabel') }}</UiText>
        <UiText size="sm" class="font-mono tracking-widest break-all">{{ secret }}</UiText>
      </UiCard>

      <UiForm id="two-factor-form" data-testid="two-factor-form" :error="tfaModalError" spacing="sm" @submit="onSubmit">
        <!-- Switching it off asks for the password as well: the code proves the device, the
             password proves the person, and a session someone else is holding has neither. -->
        <UiInput
          v-if="!isEnable"
          v-model="currentPassword"
          type="password"
          :label="$t('client.twoFactor.currentPassword')"
          :placeholder="$t('client.twoFactor.currentPasswordPlaceholder')"
          autocomplete="current-password"
          required
          testid="account-2fa-password"
        />
        <UiOtpInput
          v-model="code"
          :length="6"
          :label="$t('client.twoFactor.codeLabel')"
          :error="tfaModalError ? ' ' : ''"
          class="py-2"
        />
      </UiForm>
    </UiStack>

    <!-- Pinned footer; the button submits the body's form through its `form` attribute, so Enter in the code still submits. -->
    <template #footer>
      <UiButton type="submit" form="two-factor-form" :loading="tfaSaving" data-testid="account-2fa-submit">
        <ShieldCheck v-if="!tfaSaving" :size="15" :stroke-width="2" class="mr-1.5" />
        {{ submitLabel }}
      </UiButton>
    </template>
  </UiDialog>
</template>

<script setup lang="ts">
/**
 * TwoFactorDialog: the dialog that collects an authenticator code to enable or disable
 * two-factor, and the current password when disabling.
 *
 * In enable mode it also shows the QR and the manual secret. Submitting goes through the
 * account store, which refreshes the profile on success; the dialog then closes itself.
 *
 * @module components/account/TwoFactorDialog
 */

import { ShieldCheck } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useAccountStore } from '~/stores/account'
import { ActionOutcome } from '~/types/actionoutcome'
import { TwoFactorMode } from '~/types/twofactormode'

/** Props of {@link TwoFactorDialog}. */
interface Props {
  /** Whether the dialog is open. */
  modelValue: boolean
  /** Whether the code switches two-factor on or off. */
  mode: TwoFactorMode
  /** The manual-entry secret; shown only when enabling. */
  secret?: string
  /** The QR as a local data: URL; shown only when enabling. */
  qrDataUrl?: string
}

/** The props, with defaults. */
const props = withDefaults(defineProps<Props>(), {
  secret: '',
  qrDataUrl: ''
})

/** Events of {@link TwoFactorDialog}. */
const emit = defineEmits<{
  /** The dialog asked to open or close. */
  'update:modelValue': [value: boolean]
}>()

/** Translator for the submit label. */
const { t } = useI18n()
/** The account store that submits the code. */
const account = useAccountStore()
/** Submit progress and the API's refusal sentence. */
const { tfaSaving, tfaModalError } = storeToRefs(account)

/** The code typed so far. */
const code = ref('')

/** The current password typed so far; asked for only when switching two-factor off. */
const currentPassword = ref('')

/** Whether the dialog is enrolling (true) or disabling (false). */
const isEnable = computed<boolean>(() => props.mode === TwoFactorMode.Enable)

/** The submit button's text for the direction. */
const submitLabel = computed<string>(() => (isEnable.value ? t('client.twoFactor.confirmEnable') : t('client.twoFactor.confirmDisable')))

// Every opening starts from an empty code and no stale refusal.
watch(() => props.modelValue, (open) => {
  if (open) {
    code.value = ''
    currentPassword.value = ''
    account.clearModalErrors()
  }
})

/**
 * Submits the code (and, when switching off, the password) for the dialog's direction and closes
 * on success. Switching off signs this browser out, since the server ended every session.
 */
const onSubmit = async (): Promise<void> => {
  if (await account.submitTwoFactor(props.mode, code.value, currentPassword.value) === ActionOutcome.Succeeded) {
    emit('update:modelValue', false)
  }
}
</script>
