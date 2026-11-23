<template>
  <UiAlert
    v-if="store.emailVerified === false"
    :variant="variant"
    :title="$t('client.verifyEmail.title')"
    data-testid="verify-email-banner"
  >
    <UiText :as="UiTextTag.Span" size="inherit" tone="inherit" data-testid="verify-email-message">{{ store.resendMessage || $t('client.verifyEmail.body') }}</UiText>

    <template #action>
      <UiButton
        size="sm"
        variant="subtle"
        :loading="store.resending"
        data-testid="verify-email-resend"
        @click="store.resendVerification()"
      >
        {{ $t('client.verifyEmail.resend') }}
      </UiButton>
    </template>
  </UiAlert>
</template>

<script setup lang="ts">
/**
 * Banner of the signed-in client area shown while the account's email address is unconfirmed.
 *
 * Asks the auth store whether the address is confirmed when it mounts and renders only on an
 * explicit `false`. The button asks for the mail again; the store keeps the backend's sentence
 * (or the local confirmation) and this component only renders it.
 */
import { useAuthStore } from '~/stores/auth'
import { ActionOutcome } from '~/types/actionoutcome'
import { UiAlertVariant, UiTextTag } from '~/utils/ui/enums'

/** Auth store; owns the verification state, the resend request and its sentence. */
const store = useAuthStore()

/** Warning until a resend was refused, then an error; success turns it to success. */
const variant = computed<UiAlertVariant>(() => {
  if (store.resendOutcome === ActionOutcome.Failed) {
    return UiAlertVariant.Error
  }
  return store.resendOutcome === ActionOutcome.Succeeded ? UiAlertVariant.Success : UiAlertVariant.Warning
})

onMounted(async () => {
  await store.loadEmailVerified()
})
</script>
