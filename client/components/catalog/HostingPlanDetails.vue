<template>
  <UiStack :as="UiStackTag.Section" gap="none" class="py-8 md:py-20 bg-ui-surface relative overflow-hidden [.tpl-classic_&]:bg-classic-page md:[.tpl-classic_&]:py-12" data-testid="hosting-plan-details">
    <UiDecor class="inset-0">
      <UiDecor class="inset-0 bg-gradient-to-r from-secondary-500/5 via-transparent to-primary-500/5" />
      <UiDecor class="top-1/3 left-1/4 w-[400px] h-[400px] bg-primary-500/10 rounded-full blur-[130px] animate-blob animation-delay-4000" />
      <UiDecor class="bottom-1/4 right-1/4 w-[350px] h-[350px] bg-secondary-500/10 rounded-full blur-[120px] animate-blob" />
      <UiDecor class="top-0 left-1/3 w-px h-full bg-gradient-to-b from-transparent via-primary-500/10 to-transparent" />
      <UiDecor class="top-0 right-1/3 w-px h-full bg-gradient-to-b from-transparent via-secondary-500/10 to-transparent" />
      <UiDecor class="inset-0 opacity-[0.015] pattern-grid-60" />
    </UiDecor>
    <UiDecor class="top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/25" />
    <UiDecor class="bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/25" />

    <UiContainer class="relative z-10 w-full">
      <UiStack gap="lg">
        <!-- Billing cycles -->
        <UiCard
          v-if="cycles.length"
          :title="$t('hosting.billingCycles')"
          data-testid="hosting-plan-cycles"
          class="animate-reveal-up motion-reduce:animate-none"
        >
          <UiGrid cols="2" gap="sm" class="md:grid-cols-3 lg:grid-cols-6">
            <UiButton
              v-for="option in cycles"
              :key="option.cycle"
              :variant="selectedCycle === option.cycle ? 'outline' : 'subtle'"
              full-width
              :data-testid="`hosting-plan-cycle-${option.cycle}`"
              @click="emit('update:selectedCycle', option.cycle)"
            >
              <UiStack :as="UiStackTag.Span" gap="none" align="center">
                <UiText :as="UiTextTag.Span" size="xs" weight="semibold" tone="inherit">{{ option.label }}</UiText>
                <UiText :as="UiTextTag.Span" weight="bold" tone="inherit" class="mt-1">{{ option.price }}</UiText>
              </UiStack>
            </UiButton>
          </UiGrid>
        </UiCard>

        <!-- Features -->
        <UiCard
          v-if="features.length"
          padding="xl"
          data-testid="hosting-plan-features"
          class="animate-reveal-up motion-reduce:animate-none"
          :class="revealDelay(100)"
        >
          <UiHeading :level="2" size="subsection" class="text-xl mb-6">{{ $t('hosting.includedFeatures') }}</UiHeading>
          <UiGrid :as="UiGridTag.Ul" cols="1" gap="sm" class="md:grid-cols-2">
            <UiListItem
              v-for="(feature, index) in features"
              :key="feature"
              class="flex items-center gap-3 animate-reveal-up motion-reduce:animate-none"
              :class="revealDelay(index * 50)"
            >
              <CheckCircle :size="16" :stroke-width="2" class="text-primary-600 dark:text-primary-400 flex-shrink-0 [.tpl-classic_&]:text-classic-brand" />
              <UiText :as="UiTextTag.Span" size="sm" tone="inherit">{{ feature }}</UiText>
            </UiListItem>
          </UiGrid>
        </UiCard>

        <!-- Closing call to action -->
        <UiStack
          gap="none"
          align="center"
          class="text-center pt-4 animate-reveal-up motion-reduce:animate-none"
          :class="revealDelay(200)"
        >
          <UiButton
            variant="primary"
            size="lg"
            data-testid="hosting-plan-order-bottom"
            @click="emit('order')"
          >
            <ShoppingCart :size="20" :stroke-width="2" class="mr-2" />
            {{ inCart ? $t('hosting.inCart') : $t('hosting.orderNow') }}
          </UiButton>
          <UiText size="sm" tone="subtle" class="mt-3">
            <Lock :size="12" :stroke-width="2" class="inline mr-1" />
            {{ $t('cart.secureCheckout') }}
          </UiText>
        </UiStack>
      </UiStack>
    </UiContainer>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Body of a hosting plan page: the billing-cycle choice, the included features and the closing
 * order button.
 *
 * Presentational: the page owns the plan, the selected cycle and the cart.
 */

import { revealDelay } from '~/utils/reveal'
import { CheckCircle, Lock, ShoppingCart } from 'lucide-vue-next'
import type { PricingCycle } from '~/types/pricingcycle'
import type { PricingCycleOption } from '~/types/pricingcycleoption'
import { UiGridTag, UiStackTag, UiTextTag } from '~/utils/ui/enums'

/** Props of HostingPlanDetails. */
interface Props {
  /** The cycles the plan is priced for; the card is hidden when there are none. */
  cycles: PricingCycleOption[]
  /** The selected cycle. */
  selectedCycle: PricingCycle
  /** Bullet lines of the plan description. */
  features: string[]
  /** Whether the plan is already in the cart. */
  inCart: boolean
}

defineProps<Props>()

/** Events of HostingPlanDetails. */
const emit = defineEmits<{
  /** The visitor chose another billing cycle. */
  'update:selectedCycle': [cycle: PricingCycle]
  /** The visitor pressed the order button. */
  order: []
}>()
</script>
