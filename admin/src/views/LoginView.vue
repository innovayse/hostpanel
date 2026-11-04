<script setup lang="ts">
/**
 * Admin login page.
 *
 * The page draws whatever the deployment actually supports, which it does not guess:
 * `GET /api/auth/mode` is called before anything is rendered.
 *
 * - **sso**: one button, which hands the browser to single sign-on. The API performs
 *   the token exchange and returns a session cookie; nothing is exchanged here.
 * - **local**: an email/password form, and a TOTP field after it when the account has
 *   a second factor. This is the standalone, open-source path.
 *
 * It also surfaces first-run bootstrap: while nobody holds the Admin role,
 * `/auth/setup-required` says so, and a signed-in operator is offered the one call
 * that claims it.
 *
 * Every failure shown on this page is the sentence the API returned. Nothing here is
 * submitted by navigating, and no state is read out of the query string.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useDemoStore } from '@/stores/demo'
import { AuthMode, SignInResult } from '@/types/auth'
import AuthShell from '@/components/auth/AuthShell.vue'
import DemoBanner from '@/components/layout/DemoBanner.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { TWO_FACTOR_CODE_LENGTH } from '@/lib/limits'
import { AppRoute } from '@/lib/routes'
import { REDIRECT_QUERY_PARAM, safeRedirectTarget } from '@/lib/redirectTarget'

/** Translation function. */
const { t } = useI18n()

/** Router, for the hand-off to the panel and to first-run setup. */
const router = useRouter()

/** Current route, whose `redirect` query says where the visitor was heading before signing in. */
const route = useRoute()

/** Auth store: mode, session and every refusal sentence. */
const authStore = useAuthStore()

/** Demo store: the published demo account, when the deployment is a demo. */
const demo = useDemoStore()

/** Email field for local-mode sign-in. */
const email = ref('')

/** Password field for local-mode sign-in. */
const password = ref('')

/** TOTP code field, shown only once the API asked for a second factor. */
const totpCode = ref('')

/** True while the initial `/auth/mode` + `/auth/setup-required` pair is in flight. */
const resolvingMode = ref(true)

/**
 * True once a local sign-in succeeded but the Admin role is still unclaimed, so the
 * bootstrap action is offered before leaving the page.
 */
const offerBootstrap = ref(false)

/**
 * The setup token from the API's log, asked for only when the API said one is needed.
 * Held in memory for the life of the attempt and never stored: it is a one-time secret
 * for this one call.
 */
const setupToken = ref('')

/** Whether the deployment owns its accounts (an email/password form instead of SSO). */
const isLocal = computed<boolean>(() => authStore.mode === AuthMode.Local)

/** Line under the title: depends on who owns the accounts. */
const subtitle = computed<string>(() =>
  isLocal.value
    ? t('auth.login.signInWithYourAdministrator')
    : t('auth.login.signInWithYourAccount'),
)

/**
 * Navigates to the page the visitor originally asked for, or the dashboard when there is none or
 * the requested value is not a safe in-app path.
 *
 * @returns Promise resolving once the navigation has happened.
 */
const goToDestination = async (): Promise<void> => {
  await router.push(safeRedirectTarget(route.query[REDIRECT_QUERY_PARAM]) ?? AppRoute.Dashboard)
}

/**
 * Sends the operator on to the panel, or stops to offer the Admin claim first.
 *
 * @returns Promise resolving once the navigation or the hand-off has happened.
 */
const finishSignIn = async (): Promise<void> => {
  if (authStore.setupRequired) {
    offerBootstrap.value = true
    return
  }
  await goToDestination()
}

/** Fills the form with the published demo Admin account; the operator still presses Sign in. */
const handleUseDemoAccount = (): void => {
  if (demo.adminAccount === null) {
    return
  }
  email.value = demo.adminAccount.email
  password.value = demo.adminAccount.password
}

/**
 * Starts the SSO login flow: navigates the browser away from this app. Only reachable
 * when the API reported `sso`.
 *
 * @returns Nothing; the document navigates away.
 */
const handleSsoLogin = (): void => {
  authStore.login()
}

/**
 * Submits email and password to the API and re-renders on the answer: either onward to
 * the panel, or into the TOTP step, or showing the API's rejection.
 *
 * @returns Promise resolving once the attempt has been handled.
 */
const handleLocalLogin = async (): Promise<void> => {
  const outcome = await authStore.signInLocal(email.value, password.value)
  // The TOTP outcome needs no action here: the store now reports awaitingTwoFactor and the
  // template swaps in the code field. A failure left the reason in authStore.error.
  if (outcome === SignInResult.Done) {
    await finishSignIn()
  }
}

/**
 * Submits the authenticator code that completes a pending sign-in.
 *
 * @returns Promise resolving once the attempt has been handled.
 */
const handleTwoFactor = async (): Promise<void> => {
  const outcome = await authStore.submitTwoFactor(totpCode.value)
  if (outcome === SignInResult.Done) {
    totpCode.value = ''
    await finishSignIn()
  }
}

/** Abandons the TOTP step and returns to the credential form. */
const handleCancelTwoFactor = (): void => {
  totpCode.value = ''
  authStore.cancelTwoFactor()
}

/**
 * Claims the Admin role for the account that just signed in, then continues into the
 * panel. On failure the API's reason stays on screen and the operator stays put.
 *
 * @returns Promise resolving once the claim has been handled.
 */
const handleClaimAdmin = async (): Promise<void> => {
  const granted = await authStore.claimAdminRole(setupToken.value)
  if (!granted) {
    return
  }
  setupToken.value = ''
  await goToDestination()
}

/**
 * Continues into the panel without claiming the Admin role.
 *
 * @returns Promise resolving once the navigation has happened.
 */
const handleSkip = async (): Promise<void> => {
  await goToDestination()
}

/**
 * Sends an operator with no account at all to the first-run screen, which can create
 * one. Reachable only while setup is outstanding on a local-mode deployment, which is
 * the same condition the router guard enforces on that route.
 *
 * @returns Promise resolving once the navigation has happened.
 */
const handleGoToSetup = async (): Promise<void> => {
  await router.push(AppRoute.Setup)
}

/** Retries the mode lookup after it failed, without reloading the page. */
const handleRetryMode = async (): Promise<void> => {
  resolvingMode.value = true
  await authStore.loadMode()
  resolvingMode.value = false
}

/** Asks the API how this deployment signs people in before anything is drawn. */
onMounted(async () => {
  await authStore.loadMode()
  resolvingMode.value = false
})
</script>

<template>
  <DemoBanner />
  <AuthShell :title="t('auth.login.adminPanel')" :subtitle="subtitle">
    <UiStack gap="md">
      <!-- First-run notice: shown before and after sign-in, because it is true either way. -->
      <UiAlert
        v-if="authStore.setupRequired && !offerBootstrap"
        variant="warning"
        data-testid="login-setup-notice"
      >
        {{ t('auth.login.noAccountHoldsTheAdmin') }}
      </UiAlert>

      <!-- Signed in, but not as an Admin: say so and offer a way out instead of the panel. -->
      <UiAlert
        v-if="authStore.isAuthenticated && !authStore.isAdmin && !authStore.setupRequired"
        variant="error"
        data-testid="login-not-admin"
      >
        {{ t('auth.login.notAnAdmin') }}
      </UiAlert>

      <!-- Whatever went wrong, in the API's own words. -->
      <UiAlert v-if="authStore.error" data-testid="login-error" variant="error">
        {{ authStore.error }}
      </UiAlert>

      <!-- Mode still unknown: nothing can be drawn honestly yet. -->
      <UiText v-if="resolvingMode" tone="secondary" size="md" data-testid="login-resolving">
        {{ t('auth.login.checkingHowThisDeploymentSigns') }}
      </UiText>

      <!-- Mode lookup failed. Retry is the recovery action; it re-requests, not reloads. -->
      <UiButton
        v-else-if="authStore.mode === null"
        variant="secondary"
        block
        type="button"
        data-testid="login-retry"
        @click="handleRetryMode"
      >
        {{ t('auth.login.tryAgain') }}
      </UiButton>

      <!-- Signed in, Admin unclaimed: the one call that claims it. -->
      <UiStack v-else-if="offerBootstrap" gap="md">
        <UiText tone="secondary" size="md">
          {{ t('auth.login.signedInAsEmailNobody', { email: authStore.user?.email }) }}
        </UiText>

        <!--
          The claim is gated on the token the API printed to its log, because being
          signed in does not prove the caller runs this installation, and otherwise
          whoever claimed first would own a box that was reachable before its owner had
          finished configuring it.
        -->
        <UiText tone="secondary" size="md">
          {{ t('auth.login.theApiPrintedASetup') }}
          {{ t('auth.login.logsCommand') }}
          {{ t('auth.login.andPasteItHereRestarting') }}
        </UiText>
        <UiInput
          v-model="setupToken"
          :label="t('auth.login.setupToken')"
          autocomplete="off"
          :placeholder="t('auth.login.pasteTheTokenFromThe')"
          :autofocus="true"
          :disabled="authStore.loading"
          data-testid="login-setup-token"
        />

        <UiButton
          variant="primary"
          block
          type="button"
          :loading="authStore.loading"
          data-testid="login-claim-admin"
          @click="handleClaimAdmin"
        >
          {{ t('auth.login.claimTheAdminRole') }}
        </UiButton>
        <UiButton
          variant="secondary"
          block
          type="button"
          data-testid="login-skip"
          @click="handleSkip"
        >
          {{ t('auth.login.skipForNow') }}
        </UiButton>
      </UiStack>

      <!-- Local mode, second factor outstanding. -->
      <UiForm
        v-else-if="isLocal && authStore.awaitingTwoFactor"
        stacked
        @submit.prevent="handleTwoFactor"
      >
        <UiText tone="secondary" size="md">
          {{ t('auth.login.enterTheCurrentCodeFrom') }}
        </UiText>
        <UiInput
          v-model="totpCode"
          :label="t('auth.login.authenticationCode')"
          inputmode="numeric"
          autocomplete="one-time-code"
          :placeholder="t('auth.login.codePlaceholder')"
          :maxlength="TWO_FACTOR_CODE_LENGTH"
          :autofocus="true"
          required
          :disabled="authStore.loading"
          data-testid="login-totp"
        />
        <UiButton
          variant="primary"
          block
          type="submit"
          :loading="authStore.loading"
          data-testid="login-totp-submit"
        >
          {{ t('auth.login.verifyCode') }}
        </UiButton>
        <UiButton
          variant="secondary"
          block
          type="button"
          data-testid="login-totp-cancel"
          @click="handleCancelTwoFactor"
        >
          {{ t('auth.login.useADifferentAccount') }}
        </UiButton>
      </UiForm>

      <!-- Local mode, credentials. -->
      <UiForm
        v-else-if="isLocal"
        stacked
        @submit.prevent="handleLocalLogin"
      >
        <UiInput
          v-model="email"
          data-testid="login-email"
          :label="t('auth.login.emailAddress')"
          type="email"
          autocomplete="username"
          :placeholder="t('auth.login.emailPlaceholder')"
          required
          :disabled="authStore.loading"
        />

        <UiInput
          v-model="password"
          data-testid="login-password"
          :label="t('shared.password')"
          type="password"
          autocomplete="current-password"
          :placeholder="t('auth.login.yourPassword')"
          required
          :disabled="authStore.loading"
        />

        <!-- Demo deployment: the published Admin account, one click to prefill. -->
        <UiAlert v-if="demo.enabled && demo.adminAccount !== null" variant="info" data-testid="demo-login">
          <UiStack gap="xs">
            <UiText size="sm">{{ t('demo.login.hint') }}</UiText>
            <UiText size="sm" data-testid="demo-login-email">
              {{ t('demo.login.email', { email: demo.adminAccount.email }) }}
            </UiText>
            <UiText size="sm" data-testid="demo-login-password">
              {{ t('demo.login.password', { password: demo.adminAccount.password }) }}
            </UiText>
            <UiButton
              variant="secondary"
              size="sm"
              type="button"
              data-testid="demo-login-use"
              @click="handleUseDemoAccount"
            >
              {{ t('demo.login.useAccount') }}
            </UiButton>
          </UiStack>
        </UiAlert>

        <UiButton
          variant="primary"
          block
          data-testid="login-submit"
          type="submit"
          :loading="authStore.loading"
        >
          {{ t('auth.login.signIn') }}
        </UiButton>

        <!--
          A fresh standalone install has no accounts at all, so there is nobody to sign
          in as and this form is a dead end. The way out is the first-run screen, and
          this is the only thing that routes to it.
        -->
        <UiButton
          v-if="authStore.setupRequired"
          variant="secondary"
          block
          type="button"
          data-testid="login-go-setup"
          @click="handleGoToSetup"
        >
          {{ t('auth.login.setUpTheFirstAccount') }}
        </UiButton>
      </UiForm>

      <!-- SSO mode: one button that hands the browser to the SSO. -->
      <UiButton
        v-else
        variant="primary"
        block
        type="button"
        :loading="authStore.loading"
        data-testid="login-sso"
        @click="handleSsoLogin"
      >
        {{ t('auth.login.signInWithSso') }}
      </UiButton>
    </UiStack>
  </AuthShell>
</template>
