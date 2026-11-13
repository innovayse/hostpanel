<template>
  <UiCard data-testid="credit-balance-card">
    <UiStack gap="md">
      <UiStack direction="row" justify="between" align="center" wrap>
        <UiStack gap="xs">
          <UiText size="sm" tone="muted">{{ $t('client.credit.balance') }}</UiText>
          <UiSkeleton v-if="loading" shape="block" class="h-9 w-40" data-testid="credit-balance-loading" />
          <UiHeading v-else :level="2" size="section" data-testid="credit-balance">{{ amount }}</UiHeading>
        </UiStack>
        <UiStack direction="row" gap="sm" align="center">
          <UiButton v-if="compact" :to="CLIENT_CREDIT_PATH" variant="subtle" size="sm" data-testid="credit-view">
            {{ $t('client.credit.view') }}
          </UiButton>
          <UiButton :to="CLIENT_CREDIT_ADD_PATH" size="sm" data-testid="credit-add-funds">
            <Plus :size="14" :stroke-width="2" class="mr-1.5" />
            {{ $t('client.credit.addFunds') }}
          </UiButton>
        </UiStack>
      </UiStack>
      <UiText v-if="!compact" size="sm" tone="muted" data-testid="credit-balance-note">{{ $t('client.credit.balanceNote') }}</UiText>
    </UiStack>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * BalanceCard: the client's credit balance with the way to add funds.
 *
 * The balance is shown in the client's billing currency, as the backend sent it. The compact form is the dashboard widget:
 * the same figure and button, plus a link to the credit page where the history is.
 */
import { computed } from 'vue'
import { Plus } from 'lucide-vue-next'
import { CLIENT_CREDIT_ADD_PATH, CLIENT_CREDIT_PATH } from '~/constants/clientRoutes'
import type { ClientCredit } from '~/types/clientcredit'
import { formatCurrency } from '~/utils/formatCurrency'

/** Props of BalanceCard. */
interface Props {
  /** The client's credit; null until it is read or when it could not be. */
  credit: ClientCredit | null
  /** Whether the credit is still being read. */
  loading?: boolean
  /** The dashboard widget form: a link to the credit page, no explanation. */
  compact?: boolean
}

/** The props of the component. */
const props = withDefaults(defineProps<Props>(), { loading: false, compact: false })

/** The balance as the card shows it; a balance that is not known yet reads as zero. */
const amount = computed<string>(() =>
  formatCurrency(props.credit?.balance ?? 0, { code: props.credit?.currency }))
</script>
