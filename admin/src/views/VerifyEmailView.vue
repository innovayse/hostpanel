<script setup lang="ts">
/**
 * Email verification landing page.
 *
 * Receives token and email from URL query params, calls the verify API,
 * and shows success or failure message. A refusal is shown as the sentence the API returned.
 */
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import AuthShell from '@/components/auth/AuthShell.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiLoader from '@/components/ui/UiLoader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { AppRoute } from '@/lib/routes'

/** Translation function. */
const { t } = useI18n()

/** Current route, whose query carries the token and the address. */
const route = useRoute()

/** Auth store, which performs the verification call. */
const auth = useAuthStore()

/** True while the verification request is in flight. */
const loading = ref(true)

/** True when verification succeeded. */
const success = ref(false)

/** Error message shown when the link is unusable or verification fails. */
const error = ref<string | null>(null)

/** Reads the link's token and address, verifies them and records the outcome. */
onMounted(async () => {
  const token = route.query.token as string | undefined
  const email = route.query.email as string | undefined

  if (!token || !email) {
    error.value = t('auth.verifyEmail.invalidVerificationLinkMissingTokenOr')
    loading.value = false
    return
  }

  success.value = await auth.verifyEmail(token, email)
  if (!success.value) {
    error.value = auth.error
  }
  loading.value = false
})
</script>

<template>
  <AuthShell
    :title="loading ? t('auth.verifyEmail.title') : success ? t('auth.verifyEmail.emailVerified') : t('auth.verifyEmail.verificationFailed')"
  >
    <!-- Loading -->
    <UiLoader v-if="loading" data-testid="verify-email-loading" />

    <!-- Success -->
    <UiStack v-else-if="success" gap="md" data-testid="verify-email-success">
      <UiText tone="secondary" size="md">
        {{ t('auth.verifyEmail.yourEmailHasBeenSuccessfully') }}
      </UiText>
      <UiLink
        :to="AppRoute.Login"
        variant="primary"
        size="lg"
        class="w-full"
        data-testid="verify-email-go-login"
      >
        {{ t('auth.verifyEmail.goToAdminPanel') }}
      </UiLink>
    </UiStack>

    <!-- Error -->
    <UiStack v-else gap="md" data-testid="verify-email-failure">
      <UiAlert variant="error" data-testid="verify-email-error">
        {{ error }}
      </UiAlert>
      <UiLink :to="AppRoute.Login" variant="link" data-testid="verify-email-back-login">
        {{ t('auth.verifyEmail.backToLogin') }}
      </UiLink>
    </UiStack>
  </AuthShell>
</template>
