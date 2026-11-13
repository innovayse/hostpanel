<template>
  <ClientAuthShell :title="$t('client.resetPassword.title')" :subtitle="$t('client.resetPassword.subtitle')">
    <!-- Invalid token state -->
    <ClientOutcomePanel
      v-if="invalidParams"
      :icon="AlertCircle"
      tone="danger"
      :title="$t('client.resetPassword.invalidTitle')"
      :message="$t('client.resetPassword.invalidMessage')"
      data-testid="reset-invalid"
    >
      <UiLink :to="CLIENT_FORGOT_PATH" :underline="false" testid="reset-request-new" class="mt-3 text-sm font-medium">
        {{ $t('client.resetPassword.requestNew') }}
      </UiLink>
    </ClientOutcomePanel>

    <!-- Success state -->
    <ClientOutcomePanel
      v-else-if="success"
      :icon="CheckCircle"
      tone="success"
      :title="$t('client.resetPassword.successTitle')"
      :message="$t('client.resetPassword.successMessage')"
      data-testid="reset-success"
    >
      <UiBackLink :to="CLIENT_LOGIN_PATH" testid="reset-success-signin" class="mt-3" compact>
        {{ $t('client.resetPassword.backToSignIn') }}
      </UiBackLink>
    </ClientOutcomePanel>

    <!-- Form state -->
    <UiForm v-else :error="error" spacing="lg" data-testid="reset-form" @submit="handleSubmit">
      <UiInput
        v-model="newPassword"
        type="password"
        :label="$t('client.resetPassword.newPassword')"
        :placeholder="$t('client.resetPassword.newPasswordPlaceholder')"
        :error="fieldErrors.newPassword"
        autocomplete="new-password"
        testid="reset-new-password"
        required
      />
      <UiInput
        v-model="confirmPassword"
        type="password"
        :label="$t('client.resetPassword.confirmPassword')"
        :placeholder="$t('client.resetPassword.confirmPasswordPlaceholder')"
        autocomplete="new-password"
        testid="reset-confirm-password"
        required
      />
      <UiButton type="submit" variant="primary" size="lg" full-width :loading="loading" data-testid="reset-submit">
        <KeyRound v-if="!loading" :size="18" :stroke-width="2" class="mr-2" />
        {{ $t('client.resetPassword.submit') }}
      </UiButton>
      <UiStack align="center">
        <UiBackLink :to="CLIENT_LOGIN_PATH" testid="reset-back" compact>
          {{ $t('client.resetPassword.backToSignIn') }}
        </UiBackLink>
      </UiStack>
    </UiForm>
  </ClientAuthShell>
</template>

<script setup lang="ts">
/**
 * Password reset page: reads the token and email from the link's query and lets the visitor set
 * a new password. The request and a refusal's sentence belong to the auth store.
 */
import { CLIENT_FORGOT_PATH, CLIENT_LOGIN_PATH } from '~/constants/clientRoutes'
import { AlertCircle, CheckCircle, KeyRound } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useAuthStore } from '~/stores/auth'
import { ActionOutcome } from '~/types/actionoutcome'

definePageMeta({ layout: false })

/** The current route, read for its query and path. */
const route = useRoute()
/** Translator for the strings this file builds itself. */
const { t } = useI18n()
/** Auth store; owns the requests, in-flight flags and refusal sentences this file renders. */
const authStore = useAuthStore()

/** Reset token from the link, empty when missing. */
const token = computed(() => (typeof route.query.token === 'string' ? route.query.token : ''))

/** Address from the link, empty when missing. */
const email = computed(() => (typeof route.query.email === 'string' ? route.query.email : ''))

/** True when the link lacks the token or the address, so there is nothing to submit. */
const invalidParams = computed(() => !token.value || !email.value)

/** New password as typed. */
const newPassword = ref('')

/** Confirmation as typed; never sent, only compared. */
const confirmPassword = ref('')

/** Whether the request is in flight, the API's refusal and its field messages, all owned by the store. */
const { resetting: loading, resetError, fieldErrors } = storeToRefs(authStore)

/** The sentence for the two password fields disagreeing; they are never sent, so the page checks them. */
const mismatchError = ref('')

/** What the form shows: the mismatch if there is one, else the API's own refusal. */
const error = computed(() => mismatchError.value || resetError.value)

/** True once the password was changed. */
const success = ref(false)

/**
 * Submits the new password through the auth store.
 *
 * @returns Nothing; a refusal stays in the store's error state.
 */
const handleSubmit = async (): Promise<void> => {
  mismatchError.value = ''
  if (newPassword.value !== confirmPassword.value) {
    mismatchError.value = t('client.resetPassword.errorMismatch')
    return
  }

  const outcome = await authStore.resetPassword(email.value, token.value, newPassword.value)
  success.value = outcome === ActionOutcome.Succeeded
}
</script>
