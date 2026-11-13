<template>
  <ClientAuthShell :show-logo="false">
    <UiSkeleton v-if="status === EmailConfirmationStatus.Pending" :lines="2" data-testid="confirm-pending" />

    <ClientOutcomePanel
      v-else-if="status === EmailConfirmationStatus.Confirmed"
      :icon="CheckCircle"
      tone="success"
      :title="$t('client.confirmEmail.confirmed')"
      data-testid="confirm-success"
    >
      <UiLink :to="CLIENT_LOGIN_PATH" :underline="false" testid="confirm-signin" class="font-medium">
        {{ $t('client.confirmEmail.signIn') }}
      </UiLink>
    </ClientOutcomePanel>

    <ClientOutcomePanel
      v-else
      :icon="AlertCircle"
      tone="danger"
      :title="message"
      data-testid="confirm-failed"
    >
      <UiLink :to="CLIENT_REGISTER_PATH" :underline="false" testid="confirm-register" class="font-medium">
        {{ $t('client.confirmEmail.backToRegister') }}
      </UiLink>
    </ClientOutcomePanel>
  </ClientAuthShell>
</template>

<script setup lang="ts">
/**
 * Email confirmation landing page: reads the address and token out of the link and hands them to
 * the auth store, then renders the status and sentence the store holds.
 */
import { CLIENT_LOGIN_PATH, CLIENT_REGISTER_PATH } from '~/constants/clientRoutes'
import { AlertCircle, CheckCircle } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useAuthStore } from '~/stores/auth'
import { EmailConfirmationStatus } from '~/types/emailconfirmationstatus'

definePageMeta({ layout: false })

/** The current route, read for its query and path. */
const route = useRoute()
/** Auth store; owns the requests, in-flight flags and refusal sentences this file renders. */
const authStore = useAuthStore()

/** Which of the three panels the page shows, and the failure sentence, both owned by the store. */
const { confirmStatus: status, confirmError: message } = storeToRefs(authStore)

onMounted(async () => {
  await authStore.confirmEmail(route.query.email as string, route.query.token as string)
})
</script>
