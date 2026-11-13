<script setup lang="ts">
/**
 * Access-denied screen for a signed-in account that does not hold the Admin role.
 *
 * Drawn in the same centered glass card as the sign-in screens. The only way on is to sign out, which ends the session
 * and returns to the sign-in screen so another account can be used.
 */
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import AuthShell from '@/components/auth/AuthShell.vue'
import UiButton from '@/components/ui/UiButton.vue'
import { AppRoute } from '@/lib/routes'

/** Translation function. */
const { t } = useI18n()

/** Router, for the return to the sign-in screen. */
const router = useRouter()

/** Auth store, which ends the session. */
const auth = useAuthStore()

/**
 * Ends the session and returns to the sign-in screen.
 *
 * @returns Promise that resolves once the visitor is on the sign-in screen.
 */
const handleSignOut = async (): Promise<void> => {
  await auth.logout()
  await router.push(AppRoute.Login)
}
</script>

<template>
  <AuthShell :title="t('auth.forbidden.title')" :subtitle="t('auth.forbidden.description')">
    <UiButton variant="primary" block type="button" data-testid="forbidden-sign-out" @click="handleSignOut">
      {{ t('common.signOut') }}
    </UiButton>
  </AuthShell>
</template>
