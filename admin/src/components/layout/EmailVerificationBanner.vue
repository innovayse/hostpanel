<script setup lang="ts">
/**
 * Banner overlay shown when the current admin's email is not yet verified.
 *
 * Displays over page content (not the sidebar) with a message to check inbox.
 * Automatically hides when emailVerified becomes true.
 */
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiActionKind } from '@/utils/ui/actions'

/** Translation function. */
const { t } = useI18n()

/** Auth store: whether the account's email is verified. */
const authStore = useAuthStore()

/** True while the resend request is in flight. */
const resending = ref(false)

/** True once the API accepted a resend, so the banner can confirm it. */
const resent = ref(false)

/** Asks the store to mail the verification link again and records the outcome. */
const resend = async (): Promise<void> => {
  resending.value = true
  resent.value = await authStore.resendVerification()
  resending.value = false
}
</script>

<template>
  <UiStack
    v-if="authStore.emailVerified === false"
    align="center"
    justify="center"
    class="absolute inset-0 z-20 bg-surface-base/80 backdrop-blur-sm"
    data-testid="email-verification-banner"
  >
    <UiCard padding="lg" elevated class="mx-4 max-w-md text-center">
      <UiStack gap="sm">
        <UiHeading :level="2" variant="section">{{ t('layout.emailVerificationBanner.verifyYourEmail') }}</UiHeading>
        <UiText tone="secondary" size="lg">
          {{ t('layout.emailVerificationBanner.pleaseCheckYourInboxAnd') }}
        </UiText>
        <UiText tone="muted" size="sm">
          {{ t('layout.emailVerificationBanner.didnTReceiveTheEmail') }}
        </UiText>
        <UiAlert v-if="resent" variant="success" data-testid="email-verification-resent">
          {{ t('layout.emailVerificationBanner.resent') }}
        </UiAlert>
        <UiAlert v-else-if="authStore.error" variant="error" data-testid="email-verification-error">
          {{ authStore.error }}
        </UiAlert>
        <UiButton :action="UiActionKind.Refresh"
          variant="secondary"
          :loading="resending"
          data-testid="email-verification-resend"
          @click="resend"
        >
          {{ t('layout.emailVerificationBanner.resend') }}
        </UiButton>
      </UiStack>
    </UiCard>
  </UiStack>
</template>
