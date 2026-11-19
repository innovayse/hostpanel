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
 * The claim step runs signed in: `POST /api/auth/setup` is `[Authorize]`, takes no account
 * fields because it grants a role to the caller rather than creating anybody, and answers
 * `{ success }`, never a token.
 *
 * **On an SSO deployment this screen never appears.** The router guard sends it to /login
 * unless the API reported `local` and reported setup as outstanding. Registration is a
 * local-mode concept and under an external sign-on provider its endpoint answers 404;
 * there the setup token is asked for on the sign-in screen instead.
 *
 * Every failure shown here is the sentence the API returned. There are no client-side
 * validation rules: the password rules belong to the server, which is the only place they
 * can actually be enforced.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { SignInResult } from '@/types/auth'
import AuthShell from '@/components/auth/AuthShell.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { AppRoute } from '@/lib/routes'
import { UiActionKind } from '@/utils/ui/actions'
import { Icon } from '@/lib/icons'

/** Translation function. */
const { t } = useI18n()

/** Router, for the hand-off to the panel. */
const router = useRouter()

/** Auth store: registration, session and every refusal sentence. */
const authStore = useAuthStore()

/** Closed set of the two halves of the bootstrap. */
const SetupStep = {
  /** Create the first account. */
  Account: 'account',
  /** Claim the Admin role. */
  Claim: 'claim',
} as const

/** Which half of the bootstrap the operator is on. */
type SetupStep = (typeof SetupStep)[keyof typeof SetupStep]

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
 * only runs in the browser is not a rule: the endpoint is reachable without this page.
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
const step = ref<SetupStep>(SetupStep.Account)

/** Line under the title for the current step. */
const subtitle = computed<string>(() =>
  step.value === SetupStep.Account
    ? t('auth.setup.step1Of2Create')
    : t('auth.setup.step2Of2Claim'),
)

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
  if (!created) {
    return
  }

  const outcome = await authStore.signInLocal(email.value, password.value)
  // 'totp' cannot happen on an account created seconds ago, there is no second factor
  // to have enrolled, but it is not treated as success either: only a session that
  // actually exists lets the next step work.
  if (outcome !== SignInResult.Done) {
    return
  }

  password.value = ''
  step.value = SetupStep.Claim
}

/**
 * Claims the Admin role for the account just created, then enters the panel.
 *
 * @returns Promise resolving once the attempt has been handled.
 */
const handleClaimAdmin = async (): Promise<void> => {
  const granted = await authStore.claimAdminRole(setupToken.value)
  if (!granted) {
    return
  }

  setupToken.value = ''
  await router.push(AppRoute.Dashboard)
}

/** Resumes at the claim step when a session already exists. */
onMounted(() => {
  // The guard already loaded the mode and the session before this component was created,
  // so this only has to read the answer.
  if (authStore.isAuthenticated) {
    step.value = SetupStep.Claim
  }
})
</script>

<template>
  <AuthShell :title="t('auth.setup.initialSetup')" :subtitle="subtitle">
    <UiStack gap="md">
      <!-- Whatever went wrong, in the API's own words. -->
      <UiAlert v-if="authStore.error" variant="error" data-testid="setup-error">
        {{ authStore.error }}
      </UiAlert>

      <!-- Step 1: the first account. -->
      <UiForm
        v-if="step === SetupStep.Account"
        stacked
        data-testid="setup-account-form"
        @submit.prevent="handleCreateAccount"
      >
        <UiGrid :columns="2" gap="sm">
          <UiInput
            v-model="firstName"
            :label="t('auth.setup.firstName')"
            :placeholder="t('shared.john')"
            autocomplete="given-name"
            required
            :autofocus="true"
            :disabled="authStore.loading"
            data-testid="setup-first-name"
          />
          <UiInput
            v-model="lastName"
            :label="t('auth.setup.lastName')"
            :placeholder="t('shared.doe')"
            autocomplete="family-name"
            required
            :disabled="authStore.loading"
            data-testid="setup-last-name"
          />
        </UiGrid>

        <UiInput
          v-model="email"
          :label="t('auth.setup.emailAddress')"
          type="email"
          autocomplete="username"
          :placeholder="t('auth.login.emailPlaceholder')"
          required
          :disabled="authStore.loading"
          data-testid="setup-email"
        />

        <UiInput
          v-model="password"
          :label="t('shared.password')"
          type="password"
          autocomplete="new-password"
          :placeholder="t('auth.setup.chooseAPassword')"
          required
          :disabled="authStore.loading"
          data-testid="setup-password"
        />

        <UiButton :action="UiActionKind.Add"
          variant="primary"
          block
          type="submit"
          :loading="authStore.loading"
          data-testid="setup-create-account"
        >
          {{ t('auth.setup.createAccount') }}
        </UiButton>
      </UiForm>

      <!-- Step 2: the Admin claim. -->
      <UiForm
        v-else
        stacked
        data-testid="setup-claim-form"
        @submit.prevent="handleClaimAdmin"
      >
        <UiText tone="secondary" size="md">
          {{ t('auth.setup.signedInAsEmail', { email: authStore.user?.email }) }}
        </UiText>

        <!--
          The token field is always shown while setup is outstanding. It is the answer
          to a real problem rather than ceremony: registration is public, so on a box that
          is reachable before its owner has finished configuring it, whoever claimed first
          would own the installation.
        -->
        <UiText tone="secondary" size="md">
          {{ t('auth.setup.theApiPrintedASetup') }}
          {{ t('auth.login.logsCommand') }}
          {{ t('auth.setup.andPasteItHereRestarting') }}
        </UiText>
        <UiInput
          v-model="setupToken"
          :label="t('auth.setup.setupToken')"
          autocomplete="off"
          :placeholder="t('auth.setup.pasteTheTokenFromThe')"
          required
          :autofocus="true"
          :disabled="authStore.loading"
          data-testid="setup-token"
        />

        <UiButton :icon="Icon.ShieldCheck"
          variant="primary"
          block
          type="submit"
          :loading="authStore.loading"
          data-testid="setup-claim-admin"
        >
          {{ t('auth.setup.claimTheAdministratorRole') }}
        </UiButton>
      </UiForm>
    </UiStack>
  </AuthShell>
</template>
