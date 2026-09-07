<script setup lang="ts">
/**
 * @description
 * First-run setup for a standalone (`AUTH_MODE=local`) installation.
 *
 * This is the screen a person who has just cloned and started this repository sees. On a
 * genuinely fresh install there are no accounts at all, so it does the whole bootstrap in
 * two steps: create the first account, then claim the Admin role with the setup token the
 * API printed to its log.
 *
 * **What it used to be, and why it could not work.** It posted
 * `{ email, password, firstName, lastName }` anonymously to `POST /api/auth/setup` and
 * expected an access token back. Every part of that had moved on: the endpoint is
 * `[Authorize]`, so an anonymous post is refused before the body is looked at; it takes no
 * account fields, because it grants a role to the caller rather than creating anybody; and
 * it answers `{ success }`, never a token. It also wrote its own refusal sentences
 * (`'Setup failed. Please check your inputs and try again.'`) over whatever the server had
 * explained, and its two client-only rules ran nowhere the endpoint could enforce them. It
 * would have failed on any deployment that reached it — and nothing routed to it, so
 * nothing did.
 *
 * **On an SSO deployment this screen never appears.** The router guard sends it to /login
 * unless the API reported `local` and reported setup as outstanding — registration and the
 * setup token are both local-mode concepts, and under SSO the corresponding endpoints
 * answer 404.
 *
 * Every failure shown here is the sentence the API returned. There are no client-side
 * validation rules: the password rules belong to the server, which is the only place they
 * can actually be enforced.
 */
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/authStore'
import UiAlert from '../../../components/ui/UiAlert.vue'
import UiTextField from '../../../components/ui/UiTextField.vue'
import UiGradientButton from '../../../components/ui/UiGradientButton.vue'

const router = useRouter()
const authStore = useAuthStore()

/** Which half of the bootstrap the operator is on. */
type SetupStep = 'account' | 'claim'

/** First name field value. */
const firstName = ref('')

/** Last name field value. */
const lastName = ref('')

/** Email field value. */
const email = ref('')

/**
 * Password field value.
 *
 * One field, not a field and a confirmation. A confirmation is a rule, and a rule that
 * only runs in the browser is not a rule — the endpoint is reachable without this page.
 * `POST /api/auth/register` has no confirmation field to validate against, so asking for
 * one here would have been a check the server could never make.
 */
const password = ref('')

/** The setup token from the API's log, when this installation asks for one. */
const setupToken = ref('')

/**
 * Which step is showing. It starts on 'claim' when a session already exists, so an
 * operator who refreshed, or whose browser was closed between the two halves, resumes
 * instead of being asked to register an account they already created.
 */
const step = ref<SetupStep>('account')

/**
 * Creates the first account and signs in with it, then moves on to claiming Admin.
 *
 * The sign-in is not a second thing the operator has to do: registration answers with an
 * id, not a session, and claiming the role requires one.
 *
 * @returns Promise resolving once the attempt has been handled.
 */
const handleCreateAccount = async (): Promise<void> => {
  const created = await authStore.registerFirstAccount(
    firstName.value, lastName.value, email.value, password.value,
  )
  if (!created) return

  const outcome = await authStore.signInLocal(email.value, password.value)
  // 'totp' cannot happen on an account created seconds ago — there is no second factor
  // to have enrolled — but it is not treated as success either: only a session that
  // actually exists lets the next step work.
  if (outcome !== 'done') return

  password.value = ''
  step.value = 'claim'
}

/**
 * Claims the Admin role for the account just created, then enters the panel.
 *
 * @returns Promise resolving once the attempt has been handled.
 */
const handleClaimAdmin = async (): Promise<void> => {
  const granted = await authStore.claimAdminRole(
    authStore.setupTokenRequired ? setupToken.value : undefined,
  )
  if (!granted) return

  setupToken.value = ''
  await router.push('/dashboard')
}

onMounted(() => {
  // The guard already loaded the mode and the session before this component was created,
  // so this only has to read the answer.
  if (authStore.isAuthenticated) step.value = 'claim'
})
</script>

<template>
  <!-- Root: dark bg + ambient orbs, matching the sign-in screen. -->
  <div class="relative min-h-dvh bg-surface-base flex items-center justify-center overflow-hidden">

    <!-- Orb blue (top-left) -->
    <div class="setup-orb setup-orb--blue pointer-events-none absolute -top-32 -left-24 w-[480px] h-[480px] rounded-full opacity-20 blur-[90px]" />
    <!-- Orb purple (bottom-right) -->
    <div class="setup-orb setup-orb--purple pointer-events-none absolute -bottom-24 -right-20 w-[420px] h-[420px] rounded-full opacity-20 blur-[90px]" />

    <!-- Dot-grid overlay -->
    <div class="setup-grid pointer-events-none absolute inset-0" />

    <!-- Card -->
    <div class="setup-card relative z-10 w-full max-w-sm mx-4 bg-surface-card/85 backdrop-blur-2xl border border-white/[0.06] rounded-2xl p-10 shadow-2xl">

      <!-- Logo -->
      <div class="flex items-center gap-2.5 mb-8">
        <div class="w-9 h-9 flex items-center justify-center rounded-[10px] bg-primary-500/10 border border-primary-500/20 shrink-0">
          <svg width="18" height="18" viewBox="0 0 22 22" fill="none">
            <path d="M11 2L20 7V15L11 20L2 15V7L11 2Z" stroke="url(#setup-lg)" stroke-width="1.5" fill="none" />
            <path d="M11 7L16 10V14L11 17L6 14V10L11 7Z" fill="url(#setup-lg)" opacity="0.7" />
            <defs>
              <linearGradient id="setup-lg" x1="2" y1="2" x2="20" y2="20">
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
          Initial setup
        </h1>
        <p class="text-sm text-text-secondary">
          {{ step === 'account'
            ? 'Step 1 of 2 — create the first account on this installation'
            : 'Step 2 of 2 — claim the administrator role' }}
        </p>
      </div>

      <!-- Whatever went wrong, in the API's own words. -->
      <UiAlert v-if="authStore.error" variant="error" class="mb-4">
        {{ authStore.error }}
      </UiAlert>

      <!-- Step 1: the first account. -->
      <form
        v-if="step === 'account'"
        class="flex flex-col gap-4"
        @submit.prevent="handleCreateAccount"
      >
        <div class="grid grid-cols-2 gap-3">
          <UiTextField
            v-model="firstName"
            label="First name"
            placeholder="John"
            autocomplete="given-name"
            required
            :autofocus="true"
            :disabled="authStore.loading"
          />
          <UiTextField
            v-model="lastName"
            label="Last name"
            placeholder="Doe"
            autocomplete="family-name"
            required
            :disabled="authStore.loading"
          />
        </div>

        <UiTextField
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
        </UiTextField>

        <UiTextField
          v-model="password"
          label="Password"
          type="password"
          autocomplete="new-password"
          placeholder="Choose a password"
          required
          :disabled="authStore.loading"
        >
          <template #icon>
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
              <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
              <path d="M7 11V7a5 5 0 0 1 10 0v4" />
            </svg>
          </template>
        </UiTextField>

        <UiGradientButton type="submit" :loading="authStore.loading">
          Create account
        </UiGradientButton>
      </form>

      <!-- Step 2: the Admin claim. -->
      <form
        v-else
        class="flex flex-col gap-4"
        @submit.prevent="handleClaimAdmin"
      >
        <p class="text-sm text-text-secondary">
          Signed in as {{ authStore.user?.email }}.
        </p>

        <!--
          The token field appears only when the API said one is wanted. It is the answer
          to a real problem rather than ceremony: registration is public, so on a box that
          is reachable before its owner has finished configuring it, whoever claimed first
          would own the installation.
        -->
        <template v-if="authStore.setupTokenRequired">
          <p class="text-sm text-text-secondary">
            The API printed a setup token to its log when it started. Find it with
            <code class="text-text-primary">docker compose logs hostpanel-api</code> and
            paste it here. Restarting the API prints it again.
          </p>
          <UiTextField
            v-model="setupToken"
            label="Setup token"
            autocomplete="off"
            placeholder="Paste the token from the server log"
            required
            :autofocus="true"
            :disabled="authStore.loading"
          />
        </template>

        <UiGradientButton type="submit" :loading="authStore.loading">
          Claim the administrator role
        </UiGradientButton>
      </form>
    </div>
  </div>
</template>

<style>
/*
 * Decoration only, and in a stylesheet rather than inline `style` attributes — the
 * gradients and animation timings are shared with the sign-in screen and could not be
 * corrected in one place while they were pasted onto elements.
 */
.setup-orb {
  animation: drift-a 12s ease-in-out infinite alternate;
}

.setup-orb--blue {
  background: radial-gradient(circle, #0ea5e9 0%, transparent 70%);
}

.setup-orb--purple {
  background: radial-gradient(circle, #8b5cf6 0%, transparent 70%);
  animation: drift-a 15s ease-in-out infinite alternate-reverse;
}

.setup-grid {
  background-image:
    linear-gradient(rgba(14, 165, 233, 0.04) 1px, transparent 1px),
    linear-gradient(90deg, rgba(14, 165, 233, 0.04) 1px, transparent 1px);
  background-size: 48px 48px;
  mask-image: radial-gradient(ellipse 80% 80% at 50% 50%, black 40%, transparent 100%);
}

.setup-card {
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
