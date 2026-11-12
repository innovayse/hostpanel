<script setup lang="ts">
/**
 * The fraud panel of an order that fraud detection scored: the client IP address, the score and the risk reasons in the
 * operator's language. While the order is held for review it offers Approve and Reject; the screen runs both, this
 * component only reports the choice.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import OrderFact from '@/components/orders/OrderFact.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { FRAUD_REASON_OPTIONS } from '@/lib/constants'
import { Icon } from '@/lib/icons'
import { statusLabel } from '@/lib/tones'
import { OrderStatus } from '@/types/order'
import type { OrderDetail } from '@/types/order'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTone } from '@/utils/ui/tone'

/** Translates a key into the active locale. */
const { t } = useI18n()

/** Props for OrderFraudPanel. */
const props = defineProps<{
  /** The scored order. */
  order: OrderDetail
  /** True while an approve or reject is in flight, so neither can be sent twice. */
  busy: boolean
}>()

/** Events of OrderFraudPanel. */
const emit = defineEmits<{
  /** The operator chose to approve the order. */
  approve: []
  /** The operator chose to reject the order. */
  reject: []
}>()

/** Whether the order waits for a decision, which is when the two actions are offered. */
const underReview = computed<boolean>(() => props.order.status === OrderStatus.UnderReview)
</script>

<template>
  <UiCard padding="md" data-testid="order-fraud">
    <UiStack>
      <UiHeading :level="2" variant="section">
        {{ t('orders.fraud.title') }}
      </UiHeading>
      <UiText v-if="underReview" tone="warning" size="sm" data-testid="order-fraud-held">
        {{ t('orders.fraud.held') }}
      </UiText>
      <OrderFact :label="t('shared.ipAddress')">
        <span data-testid="order-fraud-ip">{{ order.ipAddress ?? '-' }}</span>
      </OrderFact>
      <OrderFact :label="t('orders.fraud.score')">
        <UiBadge :tone="UiTone.Danger" small data-testid="order-fraud-score">
          {{ order.fraudScore }}
        </UiBadge>
      </OrderFact>
      <UiStack gap="xs">
        <UiText tone="muted" size="sm">
          {{ t('orders.fraud.reasons') }}
        </UiText>
        <UiStack v-if="order.fraudReasons.length > 0" direction="row" gap="xs" wrap data-testid="order-fraud-reasons">
          <UiBadge
            v-for="reason in order.fraudReasons"
            :key="reason"
            :tone="UiTone.Warning"
            small
            :data-testid="`order-fraud-reason-${reason}`"
          >
            {{ statusLabel(FRAUD_REASON_OPTIONS, reason) }}
          </UiBadge>
        </UiStack>
        <UiText v-else tone="muted" size="sm" data-testid="order-fraud-no-reasons">
          {{ t('orders.fraud.noReasons') }}
        </UiText>
      </UiStack>
      <UiStack v-if="underReview" direction="row" gap="sm" wrap>
        <UiButton
          :variant="UiButtonVariant.Secondary"
          size="sm"
          :disabled="busy"
          data-testid="order-fraud-approve"
          @click="emit('approve')"
        >
          <UiIcon :markup="Icon.Check" size="sm" />
          {{ t('orders.fraud.approve') }}
        </UiButton>
        <UiButton
          :variant="UiButtonVariant.Danger"
          size="sm"
          :disabled="busy"
          data-testid="order-fraud-reject"
          @click="emit('reject')"
        >
          <UiIcon :markup="Icon.Close" size="sm" />
          {{ t('orders.fraud.reject') }}
        </UiButton>
      </UiStack>
    </UiStack>
  </UiCard>
</template>
