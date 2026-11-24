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
import { UiButtonVariant } from '@/utils/ui/button'
import { UiCardPadding } from '@/utils/ui/card'
import { UiAlignItems, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiHeadingVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

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
    :align="UiAlignItems.Center"
    :justify="UiJustify.Center"
    class="absolute inset-0 z-20 bg-surface-base/80 backdrop-blur-sm"
    data-testid="email-verification-banner"
  >
    <UiCard :padding="UiCardPadding.Lg" elevated class="mx-4 max-w-md text-center">
      <UiStack :gap="UiGap.Sm">
        <UiHeading :level="2" :variant="UiHeadingVariant.Section">{{ t('layout.emailVerificationBanner.verifyYourEmail') }}</UiHeading>
        <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Lg">
          {{ t('layout.emailVerificationBanner.pleaseCheckYourInboxAnd') }}
        </UiText>
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
          {{ t('layout.emailVerificationBanner.didnTReceiveTheEmail') }}
        </UiText>
        <UiAlert v-if="resent" :variant="UiTone.Success" data-testid="email-verification-resent">
          {{ t('layout.emailVerificationBanner.resent') }}
        </UiAlert>
        <UiAlert v-else-if="authStore.error" :variant="UiTone.Danger" data-testid="email-verification-error">
          {{ authStore.error }}
        </UiAlert>
        <UiButton :action="UiActionKind.Refresh"
          :variant="UiButtonVariant.Secondary"
          :loading="resending"
          :title="t('layout.emailVerificationBanner.resendTitle')"
          :aria-label="t('layout.emailVerificationBanner.resendTitle')"
          data-testid="email-verification-resend"
          @click="resend"
        >
          {{ t('layout.emailVerificationBanner.resend') }}
        </UiButton>
      </UiStack>
    </UiCard>
  </UiStack>
</template>
