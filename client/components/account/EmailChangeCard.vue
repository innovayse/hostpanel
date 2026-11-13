<template>
  <UiCard data-testid="account-email-change-card">
    <UiStack gap="sm">
      <UiStack direction="row" align="center" justify="between" gap="md">
        <UiStack direction="row" align="center" gap="sm" class="min-w-0">
          <AtSign :size="22" :stroke-width="1.5" class="text-gray-500 dark:text-gray-400 flex-shrink-0" />
          <UiStack gap="none" class="min-w-0">
            <UiText size="sm" weight="semibold">{{ $t('client.emailChange.cardTitle') }}</UiText>
            <UiText size="xs" tone="muted" class="break-all" data-testid="account-email-change-current">{{ email }}</UiText>
          </UiStack>
        </UiStack>
        <UiButton size="sm" variant="subtle" data-testid="account-email-change-open" @click="dialogOpen = true">
          {{ $t('client.emailChange.open') }}
        </UiButton>
      </UiStack>

      <UiAlert
        v-if="emailChangeSentTo"
        :variant="UiAlertVariant.Info"
        :title="$t('client.emailChange.sentTitle')"
        data-testid="account-email-change-sent"
      >
        {{ $t('client.emailChange.sentBody', { email: emailChangeSentTo }) }}
      </UiAlert>
    </UiStack>

    <AccountEmailChangeDialog v-model="dialogOpen" />
  </UiCard>
</template>

<script setup lang="ts">
/**
 * EmailChangeCard: shows the address the account signs in with and starts changing it.
 *
 * A change is only asked for here; it takes effect when the link mailed to the new address is
 * followed. Until then the card says where the link went, so the person knows to check that inbox.
 *
 * @module components/account/EmailChangeCard
 */

import { AtSign } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useAccountStore } from '~/stores/account'
import { UiAlertVariant } from '~/utils/ui/enums'

/** Props of {@link EmailChangeCard}. */
interface Props {
  /** The address the account signs in with now. */
  email: string
}

defineProps<Props>()

/** Where the last confirmation link went; null until one was sent. */
const { emailChangeSentTo } = storeToRefs(useAccountStore())

/** Whether the change dialog is open. */
const dialogOpen = ref(false)
</script>
