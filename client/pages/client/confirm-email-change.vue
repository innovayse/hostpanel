<template>
  <ClientAuthShell :show-logo="false">
    <UiSkeleton v-if="status === EmailConfirmationStatus.Pending" :lines="2" data-testid="confirm-email-change-pending" />

    <ClientOutcomePanel
      v-else-if="status === EmailConfirmationStatus.Confirmed"
      :icon="CheckCircle"
      tone="success"
      :title="$t('client.emailChange.confirmed')"
      data-testid="confirm-email-change-success"
    >
      <UiText size="sm" tone="muted">{{ $t('client.emailChange.signInAgain') }}</UiText>
      <UiLink :to="CLIENT_LOGIN_PATH" :underline="false" testid="confirm-email-change-signin" class="font-medium">
        {{ $t('client.confirmEmail.signIn') }}
      </UiLink>
    </ClientOutcomePanel>

    <ClientOutcomePanel
      v-else
      :icon="AlertCircle"
      tone="danger"
      :title="message"
      data-testid="confirm-email-change-failed"
    >
      <UiLink :to="CLIENT_ACCOUNT_PATH" :underline="false" testid="confirm-email-change-account" class="font-medium">
        {{ $t('client.emailChange.backToAccount') }}
      </UiLink>
    </ClientOutcomePanel>
  </ClientAuthShell>
</template>

<script setup lang="ts">
/**
 * Sign-in address change landing page: the link mailed to the new address opens it. Reads the
 * address and token out of the link and hands them to the account store, which makes the change
 * effective and ends this browser's session, then renders the status and sentence the store holds.
 *
 * Behind the sign-in like the rest of the client area: the token only ever matches the account it
 * was issued to, so the person signs in (with the current address) before it is accepted.
 */
import { AlertCircle, CheckCircle } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { CLIENT_ACCOUNT_PATH, CLIENT_LOGIN_PATH } from '~/constants/clientRoutes'
import { useAccountStore } from '~/stores/account'
import { EmailConfirmationStatus } from '~/types/emailconfirmationstatus'

definePageMeta({ layout: false, middleware: 'client-auth' })

/** The current route, read for its query. */
const route = useRoute()
/** Account store; owns the request, the status and the refusal sentence this page renders. */
const account = useAccountStore()

/** Which of the three panels the page shows, and the failure sentence, both owned by the store. */
const { emailChangeConfirmStatus: status, emailChangeConfirmError: message } = storeToRefs(account)

onMounted(async () => {
  await account.confirmEmailChange(String(route.query.email ?? ''), String(route.query.token ?? ''))
})
</script>
