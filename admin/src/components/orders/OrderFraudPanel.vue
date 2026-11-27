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
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { FRAUD_REASON_OPTIONS } from '@/lib/constants'
import { UiActionKind } from '@/utils/ui/actions'
import { statusLabel } from '@/lib/tones'
import { OrderStatus } from '@/types/order'
import type { OrderDetail } from '@/types/order'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiCardPadding } from '@/utils/ui/card'
import { UiDirection, UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiHeadingVariant, UiTextSize, UiTextTag, UiTextTone } from '@/utils/ui/text'
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
  <UiCard :padding="UiCardPadding.Md" data-testid="order-fraud">
    <UiStack>
      <UiHeading :level="2" :variant="UiHeadingVariant.Section">
        {{ t('orders.fraud.title') }}
      </UiHeading>
      <UiText v-if="underReview" :tone="UiTextTone.Warning" :size="UiTextSize.Sm" data-testid="order-fraud-held">
        {{ t('orders.fraud.held') }}
      </UiText>
      <OrderFact :label="t('shared.ipAddress')">
        <UiText :as="UiTextTag.Span" data-testid="order-fraud-ip">{{ order.ipAddress ?? '-' }}</UiText>
      </OrderFact>
      <OrderFact :label="t('orders.fraud.score')">
        <UiBadge :tone="UiTone.Danger" small data-testid="order-fraud-score">
          {{ order.fraudScore }}
        </UiBadge>
      </OrderFact>
      <UiStack :gap="UiGap.Xs">
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
          {{ t('orders.fraud.reasons') }}
        </UiText>
        <UiStack v-if="order.fraudReasons.length > 0" :direction="UiDirection.Row" :gap="UiGap.Xs" wrap data-testid="order-fraud-reasons">
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
        <UiText v-else :tone="UiTextTone.Muted" :size="UiTextSize.Sm" data-testid="order-fraud-no-reasons">
          {{ t('orders.fraud.noReasons') }}
        </UiText>
      </UiStack>
      <UiStack v-if="underReview" :direction="UiDirection.Row" :gap="UiGap.Sm" wrap>
        <UiButton :action="UiActionKind.Confirm"
          :variant="UiButtonVariant.Secondary"
          :size="UiSize.Sm"
          :disabled="busy"
          data-testid="order-fraud-approve"
          @click="emit('approve')"
        >{{ t('orders.fraud.approve') }}
        </UiButton>
        <UiButton :action="UiActionKind.Reject"
          :variant="UiButtonVariant.Danger"
          :size="UiSize.Sm"
          :disabled="busy"
          data-testid="order-fraud-reject"
          @click="emit('reject')"
        >{{ t('orders.fraud.reject') }}
        </UiButton>
      </UiStack>
    </UiStack>
  </UiCard>
</template>
