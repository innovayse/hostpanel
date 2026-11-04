<template>
  <ClientAuthShell :title="$t('client.login.title')" :subtitle="$t('client.login.subtitle')">
    <UiAlert v-if="authStore.error" variant="error" class="mb-4" data-testid="login-error">
      {{ authStore.error }}
    </UiAlert>

    <!-- SSO mode: the identity provider owns the credentials, so the page is one button. -->
    <UiButton
      v-if="isSso"
      href="/api/portal/auth/sso/authorize"
      variant="primary"
      size="lg"
      full-width
      data-testid="login-sso"
    >
      <Hexagon :size="20" :stroke-width="1.5" class="mr-3" />
      {{ $t('client.login.continueWithSso') }}
    </UiButton>

    <!-- Demo mode: the published customer account, one click from filled in -->
    <UiAlert v-if="demoAccount && !isSso" variant="info" class="mb-4" data-testid="login-demo">
      <UiStack gap="sm">
        <UiText as="span" size="sm" tone="inherit" data-testid="login-demo-hint">
          {{ $t('client.login.demoAccountHint', { email: demoAccount.email }) }}
        </UiText>
        <UiButton variant="outline" size="sm" data-testid="login-demo-use" @click="fillDemoAccount">
          {{ $t('client.login.useDemoAccount') }}
        </UiButton>
      </UiStack>
    </UiAlert>

    <!-- Local mode, second step: the account has a second factor on, so the code finishes the sign-in. -->
    <UiForm
      v-if="!isSso && authStore.twoFactorToken"
      spacing="md"
      data-testid="login-2fa-form"
      @submit="handleTwoFactor"
    >
      <UiAlert variant="info" data-testid="login-2fa-hint">
        <UiStack gap="sm">
          <UiText as="span" size="sm" tone="inherit" class="font-medium">
            {{ $t('client.login.twoFactorRequired') }}
          </UiText>
          <UiText as="span" size="sm" tone="inherit">
            {{ $t('client.login.twoFactorPrompt') }}
          </UiText>
        </UiStack>
      </UiAlert>
      <UiOtpInput
        v-model="code"
        :label="$t('client.login.twoFactorLabel')"
        :disabled="authStore.loading"
        @complete="handleTwoFactor"
      />
      <UiButton
        type="submit"
        variant="primary"
        size="lg"
        full-width
        :loading="authStore.loading"
        :disabled="code.length < 6"
        data-testid="login-2fa-submit"
      >
        {{ authStore.loading ? $t('client.login.submitting') : $t('client.login.twoFactorSubmit') }}
      </UiButton>
      <UiButton variant="ghost" size="sm" full-width data-testid="login-2fa-cancel" @click="cancelTwoFactor">
        {{ $t('client.login.twoFactorBack') }}
      </UiButton>
    </UiForm>

    <!-- Local mode -->
    <UiForm v-else-if="!isSso" spacing="md" data-testid="login-form" @submit="handleLogin">
      <UiInput
        v-model="email"
        type="email"
        :label="$t('client.login.email')"
        :placeholder="$t('client.login.emailPlaceholder')"
        :error="authStore.fieldErrors.email"
        autocomplete="email"
        testid="login-email"
        required
      />
      <UiInput
        v-model="password"
        type="password"
        :label="$t('client.login.password')"
        :placeholder="$t('client.login.passwordPlaceholder')"
        :error="authStore.fieldErrors.password"
        autocomplete="current-password"
        testid="login-password"
        required
      />
      <UiStack direction="row" justify="end">
        <UiLink :to="CLIENT_FORGOT_PASSWORD_PATH" :underline="false" testid="login-forgot" class="text-xs">
          {{ $t('client.login.forgotPassword') }}
        </UiLink>
      </UiStack>
      <UiButton type="submit" variant="primary" size="lg" full-width :loading="authStore.loading" data-testid="login-submit">
        {{ authStore.loading ? $t('client.login.submitting') : $t('client.login.signIn') }}
      </UiButton>
    </UiForm>

    <template v-if="!isSso" #footer>
      <UiText as="span" size="sm" tone="muted">
        {{ $t('client.login.noAccount') }}
        <UiLink :to="CLIENT_REGISTER_PATH" :underline="false" testid="login-register" class="font-medium">
          {{ $t('client.login.createAccount') }}
        </UiLink>
      </UiText>
    </template>
  </ClientAuthShell>
</template>

<script setup lang="ts">
/**
 * Client sign-in page.
 *
 * Under SSO it is a single button to the identity provider; in local mode it is the email and
 * password form. The request, its in-flight flag, the refusal sentence and the field messages
 * belong to the auth store; this page renders them and follows the redirect on success.
 */
import { CLIENT_FORGOT_PASSWORD_PATH, CLIENT_REGISTER_PATH } from '~/constants/clientRoutes'
import { Hexagon } from 'lucide-vue-next'
import { useAuthStore } from '~/stores/auth'
import { useDemoStore } from '~/stores/demo'
import { SignInOutcome } from '~/types/signinoutcome'
import { safeReturnPath } from '~/utils/returnPath'

definePageMeta({ layout: false, middleware: 'client-auth' })

/** Auth store; owns the sign-in request, its loading flag and the refusal sentence. */
const authStore = useAuthStore()

/** Demo store; supplies the published customer account while the deployment is a demo. */
const demoStore = useDemoStore()

/** The published demo customer account, or undefined outside a demo. */
const demoAccount = computed(() => (demoStore.enabled ? demoStore.clientAccount : undefined))

/** Whether the platform SSO, not this form, is the identity provider. */
const { isSso } = useAuthMode()

/** The router, used to navigate after a successful action. */
const router = useRouter()
/** The current route, read for its query and path. */
const route = useRoute()

/** Email field value. */
const email = ref('')

/** Password field value. */
const password = ref('')

/**
 * Fills the form with the published demo credentials; the visitor still submits it.
 *
 * @returns Nothing.
 */
const fillDemoAccount = (): void => {
  if (!demoAccount.value) {
    return
  }
  email.value = demoAccount.value.email
  password.value = demoAccount.value.password
}

/** The 6-digit authenticator code typed into the second step. */
const code = ref('')

/**
 * Follows the `redirect` query after a finished sign-in. The query value is attacker-controlled,
 * so only an internal path is followed.
 *
 * @returns Resolves once the navigation has been requested.
 */
const goToReturnPath = async (): Promise<void> => {
  await router.push(safeReturnPath(route.query.redirect))
}

/**
 * Submits the email and password. A second-factor account stays on this page: the store opens
 * the challenge and the template swaps to the code step.
 *
 * @returns Resolves when the attempt has ended.
 */
const handleLogin = async (): Promise<void> => {
  const outcome = await authStore.signIn(email.value, password.value)
  if (outcome === SignInOutcome.SignedIn) {
    await goToReturnPath()
  }
}

/**
 * Submits the authenticator code. A complete code submits itself, the button submits it too, so a
 * code already in flight is not sent twice.
 *
 * @returns Resolves when the attempt has ended; a wrong code is rendered from the store's error.
 */
const handleTwoFactor = async (): Promise<void> => {
  if (code.value.length < 6 || authStore.loading) {
    return
  }
  const outcome = await authStore.confirmTwoFactor(code.value)
  if (outcome === SignInOutcome.SignedIn) {
    await goToReturnPath()
  } else {
    code.value = ''
  }
}

/**
 * Leaves the code step for the password form.
 */
const cancelTwoFactor = (): void => {
  code.value = ''
  authStore.cancelTwoFactor()
}
</script>
