<template>
  <ClientAuthShell :title="$t('client.forgot.title')" :subtitle="$t('client.forgot.subtitle')">
    <ClientOutcomePanel
      v-if="sent"
      :icon="MailCheck"
      tone="success"
      :title="$t('client.forgot.successTitle')"
      :message="$t('client.forgot.successMessage', { email: submittedEmail })"
    >
      <UiBackLink :to="CLIENT_LOGIN_PATH" testid="forgot-sent-back" class="mt-4" compact>
        {{ $t('client.forgot.backToSignIn') }}
      </UiBackLink>
    </ClientOutcomePanel>

    <UiForm v-else :error="error" spacing="lg" data-testid="forgot-form" @submit="handleSubmit">
      <UiInput
        v-model="email"
        type="email"
        :label="$t('client.forgot.email')"
        :placeholder="$t('client.forgot.emailPlaceholder')"
        :error="fieldErrors.email"
        icon="mdi:email"
        autocomplete="email"
        testid="forgot-email"
        required
      />
      <UiButton type="submit" variant="primary" size="lg" full-width :loading="loading" data-testid="forgot-submit">
        <Send v-if="!loading" :size="18" :stroke-width="2" class="mr-2" />
        {{ $t('client.forgot.submit') }}
      </UiButton>
      <div class="text-center">
        <UiBackLink :to="CLIENT_LOGIN_PATH" testid="forgot-back" compact>
          {{ $t('client.forgot.backToSignIn') }}
        </UiBackLink>
      </div>
    </UiForm>
  </ClientAuthShell>
</template>

<script setup lang="ts">
/**
 * The password-reset request form and its "mail sent" state.
 *
 * Shared by `/client/forgot` and `/client/forgot-password`, so the two routes cannot drift
 * apart. The request, its in-flight flag and a refusal's
 * sentence belong to the auth store; this component renders them.
 */
import { CLIENT_LOGIN_PATH } from '~/constants/clientRoutes'
import { MailCheck, Send } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useAuthStore } from '~/stores/auth'
import { ActionOutcome } from '~/types/actionoutcome'

/** Auth store; owns the requests, in-flight flags and refusal sentences this file renders. */
const authStore = useAuthStore()

/** Whether the request is in flight, the API's sentence for a refusal and its field messages, all owned by the store. */
const { resetRequesting: loading, resetRequestError: error, fieldErrors } = storeToRefs(authStore)

/** Address the visitor typed. */
const email = ref('')

/** Address the confirmation panel echoes back, frozen at submit time. */
const submittedEmail = ref('')

/** True once the backend has accepted the request. */
const sent = ref(false)

/**
 * Asks for a reset mail and switches to the confirmation state.
 *
 * The confirmation is only shown when the request actually succeeded; a failure stays in the
 * store's error state.
 *
 * @returns Nothing.
 */
const handleSubmit = async (): Promise<void> => {
  const outcome = await authStore.requestPasswordReset(email.value)
  if (outcome === ActionOutcome.Succeeded) {
    submittedEmail.value = email.value
    sent.value = true
  }
}
</script>
