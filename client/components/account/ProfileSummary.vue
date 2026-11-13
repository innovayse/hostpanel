<template>
  <UiCard data-testid="account-profile-summary">
    <UiStack direction="row" align="center" gap="md">
      <UiAvatar :first-name="user.firstName" :last-name="user.lastName" :email="user.email" />
      <UiStack gap="none" class="flex-1 min-w-0">
        <UiHeading :level="UiHeadingLevel.H2" size="subsection" data-testid="account-full-name">
          {{ user.firstName }} {{ user.lastName }}
        </UiHeading>
        <UiText size="sm" tone="muted">{{ user.email }}</UiText>
      </UiStack>
      <UiBadge :variant="twoFactorEnabled ? 'success' : 'default'" rounded data-testid="account-2fa-badge">
        <ShieldCheck v-if="twoFactorEnabled" :size="12" :stroke-width="2.5" class="mr-1.5" />
        <ShieldOff v-else :size="12" :stroke-width="2.5" class="mr-1.5" />
        {{ twoFactorEnabled ? $t('client.twoFactor.enabled') : $t('client.twoFactor.disabled') }}
      </UiBadge>
    </UiStack>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * ProfileSummary: the avatar, name and email of the signed-in client with the two-factor badge.
 *
 * @module components/account/ProfileSummary
 */

import { ShieldCheck, ShieldOff } from 'lucide-vue-next'
import type { ClientUser } from '~/types/clientuser'
import { UiHeadingLevel } from '~/utils/ui/enums'

/** Props of {@link ProfileSummary}. */
interface Props {
  /** The signed-in client's profile. */
  user: ClientUser
  /** Whether two-factor is switched on for the account. */
  twoFactorEnabled: boolean
}

defineProps<Props>()
</script>
