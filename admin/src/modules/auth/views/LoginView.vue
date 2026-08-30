<script setup lang="ts">
/**
 * @description
 * Admin login page — full-screen dark with brand gradient accents.
 *
 * The page draws whatever the deployment actually supports, which it does not guess:
 * `GET /api/auth/mode` is called before anything is rendered.
 *
 * - **sso** — one button, which hands the browser to Innovayse SSO. The API performs
 *   the token exchange and returns a session cookie; nothing is exchanged here.
 * - **local** — an email/password form, and a TOTP field after it when the account has
 *   a second factor. This is the standalone, open-source path, and until the mode call
 *   existed the page offered the SSO button here too, with no route behind it.
 *
 * It also surfaces first-run bootstrap: while nobody holds the Admin role,
 * `/auth/setup-required` says so, and a signed-in operator is offered the one call
 * that claims it.
 *
 * Every failure shown on this page is the sentence the API returned. Nothing here is
 * submitted by navigating, and no state is read out of the query string.
 */
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/authStore'
import AppAlert from '../../../components/AppAlert.vue'
import AppTextField from '../../../components/AppTextField.vue'
import AppGradientButton from '../../../components/AppGradientButton.vue'

const router = useRouter()
const authStore = useAuthStore()

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
  await router.push('/dashboard')
}

/**
 * Starts the SSO login flow — navigates the browser away from this app. Only reachable
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
  if (outcome === 'done') await finishSignIn()
  // 'totp' needs no action here — the store now reports awaitingTwoFactor and the
  // template swaps in the code field. 'failed' left the reason in authStore.error.
}

/**
 * Submits the authenticator code that completes a pending sign-in.
 *
 * @returns Promise resolving once the attempt has been handled.
 */
const handleTwoFactor = async (): Promise<void> => {
  const outcome = await authStore.submitTwoFactor(totpCode.value)
  if (outcome === 'done') {
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
  const granted = await authStore.claimAdminRole(
    authStore.setupTokenRequired ? setupToken.value : undefined,
  )
  if (!granted) return
  setupToken.value = ''
  await router.push('/dashboard')
}

/**
 * Sends an operator with no account at all to the first-run screen, which can create
 * one. Reachable only while setup is outstanding on a local-mode deployment, which is
 * the same condition the router guard enforces on that route.
 *
 * @returns Promise resolving once the navigation has happened.
 */
const handleGoToSetup = async (): Promise<void> => {
  await router.push('/setup')
}

/** Retries the mode lookup after it failed, without reloading the page. */
const handleRetryMode = async (): Promise<void> => {
  resolvingMode.value = true
  await authStore.loadMode()
  resolvingMode.value = false
}

onMounted(async () => {
  await authStore.loadMode()
  resolvingMode.value = false
})
</script>

<template>
  <!-- Root: dark bg + ambient orbs -->
  <div class="relative min-h-dvh bg-surface-base flex items-center justify-center overflow-hidden">

    <!-- Orb blue (top-left) -->
    <div class="login-orb login-orb--blue pointer-events-none absolute -top-32 -left-24 w-[480px] h-[480px] rounded-full opacity-20 blur-[90px]" />
    <!-- Orb purple (bottom-right) -->
    <div class="login-orb login-orb--purple pointer-events-none absolute -bottom-24 -right-20 w-[420px] h-[420px] rounded-full opacity-20 blur-[90px]" />

    <!-- Dot-grid overlay -->
    <div class="login-grid pointer-events-none absolute inset-0" />

    <!-- Card -->
    <div class="login-card relative z-10 w-full max-w-sm mx-4 bg-surface-card/85 backdrop-blur-2xl border border-white/[0.06] rounded-2xl p-10 shadow-2xl">
      <!-- Logo -->
      <div class="flex items-center gap-2.5 mb-8">
        <div class="w-9 h-9 flex items-center justify-center rounded-[10px] bg-primary-500/10 border border-primary-500/20 shrink-0">
          <svg width="18" height="18" viewBox="0 0 22 22" fill="none">
            <path d="M11 2L20 7V15L11 20L2 15V7L11 2Z" stroke="url(#lg1)" stroke-width="1.5" fill="none" />
            <path d="M11 7L16 10V14L11 17L6 14V10L11 7Z" fill="url(#lg1)" opacity="0.7" />
            <defs>
              <linearGradient id="lg1" x1="2" y1="2" x2="20" y2="20">
                <stop offset="0%" stop-color="#0ea5e9" />
                <stop offset="100%" stop-color="#a855f7" />
              </linearGradient>
            </defs>
          </svg>
        </div>
        <span class="font-display font-bold text-[1.05rem] gradient-brand-text">Innovayse</span>
      </div>

      <!-- Heading -->
      <div class="mb-7">
        <h1 class="font-display text-[1.6rem] font-bold text-text-primary tracking-tight leading-none mb-1.5">
          Admin Panel
        </h1>
        <p class="text-sm text-text-secondary">
          {{ authStore.mode === 'local' ? 'Sign in with your administrator account' : 'Sign in with your Innovayse account' }}
        </p>
      </div>

      <!-- First-run notice: shown before and after sign-in, because it is true either way. -->
      <AppAlert v-if="authStore.setupRequired && !offerBootstrap" variant="warning" class="mb-4">
        No account holds the Admin role yet. Sign in and you can claim it.
      </AppAlert>

      <!-- Whatever went wrong, in the API's own words. -->
      <AppAlert v-if="authStore.error" variant="error" class="mb-4">
        {{ authStore.error }}
      </AppAlert>

      <!-- Mode still unknown: nothing can be drawn honestly yet. -->
      <p v-if="resolvingMode" class="text-sm text-text-secondary">
        Checking how this deployment signs people in…
      </p>

      <!-- Mode lookup failed. Retry is the recovery action; it re-requests, not reloads. -->
      <AppGradientButton
        v-else-if="authStore.mode === null"
        variant="quiet"
        @click="handleRetryMode"
      >
        Try again
      </AppGradientButton>

      <!-- Signed in, Admin unclaimed: the one call that claims it. -->
      <div v-else-if="offerBootstrap" class="flex flex-col gap-4">
        <p class="text-sm text-text-secondary">
          Signed in as {{ authStore.user?.email }}. Nobody holds the Admin role on this
          installation yet — claiming it grants full access to this account.
        </p>

        <!--
          Local-mode installations gate the claim on the token the API printed to its
          log, because registration is public and otherwise whoever claimed first would
          own a box that was reachable before its owner had finished configuring it.
          Whether the field is wanted is the API's answer, not this page's guess: under
          SSO no token exists and none is asked for.
        -->
        <template v-if="authStore.setupTokenRequired">
          <p class="text-sm text-text-secondary">
            The API printed a setup token to its log when it started. Find it with
            <code class="text-text-primary">docker compose logs hostpanel-api</code> and
            paste it here. Restarting the API prints it again.
          </p>
          <AppTextField
            v-model="setupToken"
            label="Setup token"
            autocomplete="off"
            placeholder="Paste the token from the server log"
            :autofocus="true"
            :disabled="authStore.loading"
          />
        </template>

        <AppGradientButton type="button" :loading="authStore.loading" @click="handleClaimAdmin">
          Claim the Admin role
        </AppGradientButton>
        <AppGradientButton variant="quiet" @click="router.push('/dashboard')">
          Skip for now
        </AppGradientButton>
      </div>

      <!-- Local mode, second factor outstanding. -->
      <form
        v-else-if="authStore.mode === 'local' && authStore.awaitingTwoFactor"
        class="flex flex-col gap-4"
        @submit.prevent="handleTwoFactor"
      >
        <p class="text-sm text-text-secondary">
          Enter the current code from your authenticator app.
        </p>
        <AppTextField
          v-model="totpCode"
          label="Authentication code"
          inputmode="numeric"
          autocomplete="one-time-code"
          placeholder="123456"
          :maxlength="6"
          :autofocus="true"
          required
          :disabled="authStore.loading"
        />
        <AppGradientButton type="submit" :loading="authStore.loading">
          Verify code
        </AppGradientButton>
        <AppGradientButton variant="quiet" @click="handleCancelTwoFactor">
          Use a different account
        </AppGradientButton>
      </form>

      <!-- Local mode, credentials. -->
      <form
        v-else-if="authStore.mode === 'local'"
        class="flex flex-col gap-4"
        @submit.prevent="handleLocalLogin"
      >
        <AppTextField
          v-model="email"
          label="Email address"
          type="email"
          autocomplete="username"
          placeholder="admin@example.com"
          required
          :disabled="authStore.loading"
        >
          <template #icon>
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
              <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z" />
              <polyline points="22,6 12,13 2,6" />
            </svg>
          </template>
        </AppTextField>

        <AppTextField
          v-model="password"
          label="Password"
          type="password"
          autocomplete="current-password"
          placeholder="Your password"
          required
          :disabled="authStore.loading"
        >
          <template #icon>
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
              <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
              <path d="M7 11V7a5 5 0 0 1 10 0v4" />
            </svg>
          </template>
        </AppTextField>

        <AppGradientButton type="submit" :loading="authStore.loading">
          Sign in
        </AppGradientButton>

        <!--
          A fresh standalone install has no accounts at all, so there is nobody to sign
          in as and this form is a dead end. The way out is the first-run screen, and
          this is the only thing that routes to it.
        -->
        <AppGradientButton
          v-if="authStore.setupRequired"
          variant="quiet"
          @click="handleGoToSetup"
        >
          Set up the first account
        </AppGradientButton>
      </form>

      <!-- SSO mode — unchanged: one button that hands the browser to the SSO. -->
      <AppGradientButton
        v-else
        type="button"
        :loading="authStore.loading"
        @click="handleSsoLogin"
      >
        Sign in with Innovayse SSO
      </AppGradientButton>
    </div>
  </div>
</template>

<style>
/*
 * Decoration only. These were inline `style` attributes on the elements above until
 * the page was rewritten — which meant the gradients and the animation timings could
 * not be reused or corrected anywhere else.
 */
.login-orb {
  animation: drift-a 12s ease-in-out infinite alternate;
}

.login-orb--blue {
  background: radial-gradient(circle, #0ea5e9 0%, transparent 70%);
}

.login-orb--purple {
  background: radial-gradient(circle, #8b5cf6 0%, transparent 70%);
  animation: drift-a 15s ease-in-out infinite alternate-reverse;
}

.login-grid {
  background-image:
    linear-gradient(rgba(14, 165, 233, 0.04) 1px, transparent 1px),
    linear-gradient(90deg, rgba(14, 165, 233, 0.04) 1px, transparent 1px);
  background-size: 48px 48px;
  mask-image: radial-gradient(ellipse 80% 80% at 50% 50%, black 40%, transparent 100%);
}

.login-card {
  animation: card-in 0.5s cubic-bezier(0.16, 1, 0.3, 1) both;
}

@keyframes drift-a {
  from { transform: translate(0, 0); }
  to   { transform: translate(40px, 30px); }
}

@keyframes card-in {
  from { opacity: 0; transform: translateY(18px) scale(0.98); }
  to   { opacity: 1; transform: translateY(0) scale(1); }
}
</style>
