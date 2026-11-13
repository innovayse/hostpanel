<template>
  <ClientAuthShell :title="$t('client.acceptInvite.title')" :subtitle="$t('client.acceptInvite.subtitle')">
    <!-- Invalid token state -->
    <ClientOutcomePanel
      v-if="invalidToken"
      :icon="AlertCircle"
      tone="danger"
      :title="$t('client.acceptInvite.invalidTitle')"
      :message="$t('client.acceptInvite.invalidMessage')"
      data-testid="invite-invalid"
    >
      <UiBackLink :to="CLIENT_LOGIN_PATH" testid="invite-invalid-back" class="mt-3" compact>
        {{ $t('client.acceptInvite.backToSignIn') }}
      </UiBackLink>
    </ClientOutcomePanel>

    <!-- Sign-in-required state: the invitation is fine, the visitor just has no session -->
    <ClientOutcomePanel
      v-else-if="signInRequired"
      :icon="LogIn"
      tone="primary"
      :title="$t('client.acceptInvite.signInRequiredTitle')"
      :message="error"
      data-testid="invite-sign-in-required"
    >
      <UiButton variant="primary" size="lg" full-width class="mt-3" data-testid="invite-sign-in" @click="goToSignIn">
        <LogIn :size="18" :stroke-width="2" class="mr-2" />
        {{ $t('client.acceptInvite.signInAction') }}
      </UiButton>
    </ClientOutcomePanel>

    <!-- Form state -->
    <UiForm v-else :error="error" spacing="lg" data-testid="invite-form" @submit="handleSubmit">
      <UiText tone="muted" size="sm" class="leading-relaxed">{{ $t('client.acceptInvite.description') }}</UiText>
      <UiButton type="submit" variant="primary" size="lg" full-width :loading="loading" data-testid="invite-submit">
        <UserPlus v-if="!loading" :size="18" :stroke-width="2" class="mr-2" />
        {{ $t('client.acceptInvite.submit') }}
      </UiButton>
      <UiStack align="center">
        <UiBackLink :to="CLIENT_LOGIN_PATH" testid="invite-back" compact>
          {{ $t('client.acceptInvite.backToSignIn') }}
        </UiBackLink>
      </UiStack>
    </UiForm>
  </ClientAuthShell>
</template>

<script setup lang="ts">
/**
 * Accept invitation page: links the signed-in account to the client that invited it.
 *
 * Reads the invitation token out of the link's `?token=` query and posts it to
 * `/api/portal/auth/accept-invite`. On success the person is a user of that client account
 * and lands on the dashboard.
 *
 * **It asks for no password.** The backend command carries a token only and resolves who
 * accepted from the credential, so whoever accepts must already be signed in; a password
 * collected here would have nowhere to go. The page offers the sign-in step instead.
 */
import {
  CLIENT_DASHBOARD_PATH,
  CLIENT_LOGIN_PATH,
  CLIENT_REDIRECT_QUERY,
  RETURN_TO_QUERY,
  SSO_AUTHORIZE_PATH
} from '~/constants/clientRoutes'
import { AlertCircle, LogIn, UserPlus } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useAuthStore } from '~/stores/auth'
import { InviteOutcome } from '~/types/inviteoutcome'

definePageMeta({ layout: false })

/** The current route, read for its query and path. */
const route = useRoute()
/** Translator for the strings this file builds itself. */
const { t } = useI18n()
/** Runtime config; its public half carries the auth mode and the portal URLs. */
const config = useRuntimeConfig()
/** Whether the platform SSO, not a local form, is the identity provider. */
const { isSso } = useAuthMode()
/** Auth store; owns the requests, in-flight flags and refusal sentences this file renders. */
const authStore = useAuthStore()

// A refusal from an earlier visit (the sign-in-required step, before signing in and coming back
// to this link) is not this visit's state.
authStore.clearInvite()

/** Invitation token extracted from URL query. */
const token = computed(() => (typeof route.query.token === 'string' ? route.query.token : ''))

/** True when no token is present in the URL: the one check made before any request. */
const invalidToken = computed(() => !token.value)

/** True while the accept-invite request is in flight, and how the last attempt ended; owned by the store. */
const { inviting: loading, inviteOutcome } = storeToRefs(authStore)

/**
 * True when the last refusal was the sign-in-required one, so the page shows the sign-in
 * step instead of the accept button. Every failure carries its recovery action; this is that
 * failure's.
 */
const signInRequired = computed(() => inviteOutcome.value === InviteOutcome.SignInRequired)

/**
 * Error message to display, empty when no error.
 *
 * The one sentence this frontend still words itself, and only because the backend never
 * sees the request it answers: `server/api/portal/auth/accept-invite.post.ts` refuses
 * before any call is possible, so there is no API wording to read. Everything else
 * (expired, already accepted) renders what the API said, in the caller's own language.
 */
const error = computed(() => signInRequired.value
  ? t('client.acceptInvite.signInRequired')
  : authStore.inviteError)

/**
 * Sends the visitor to sign in and back to this exact URL, token and all.
 *
 * Mirrors what `middleware/client-auth.ts` does for the rest of the client area, because
 * this page is deliberately outside that middleware: the invitation link has to render its
 * own explanation rather than bounce a stranger straight into an identity provider.
 *
 * @returns The navigation, so a caller can await it.
 */
const goToSignIn = (): ReturnType<typeof navigateTo> => {
  const returnTo = encodeURIComponent(route.fullPath)

  if (isSso.value) {
    const baseUrl = (config.public.baseUrl as string) || window.location.origin
    return navigateTo(`${baseUrl}${SSO_AUTHORIZE_PATH}?${RETURN_TO_QUERY}=${returnTo}`, { external: true })
  }

  return navigateTo(`${CLIENT_LOGIN_PATH}?${CLIENT_REDIRECT_QUERY}=${returnTo}`)
}

/**
 * Posts the invitation token through the auth store and leaves on acceptance.
 *
 * On success it leaves with a full page load rather than a router push. Accepting grants the
 * Client role and links the account to a client that the running SPA has already decided it
 * does not have, `stores/client.ts` caches that answer for the session, so a router push
 * would land on a dashboard still rendering the no-profile notice.
 *
 * @returns Nothing; a refusal stays in the store's state.
 */
const handleSubmit = async (): Promise<void> => {
  const outcome = await authStore.acceptInvite(token.value)
  if (outcome === InviteOutcome.Accepted) {
    // Full page reload so the new role and cookies are picked up by middleware
    window.location.href = CLIENT_DASHBOARD_PATH
  }
}
</script>
