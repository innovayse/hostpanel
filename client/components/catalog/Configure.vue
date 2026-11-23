<template>
  <UiStack :gap="UiGap.None" class="bg-ui-page py-10 md:py-16 [.tpl-classic_&]:[--accent:rgb(var(--classic-brand))]" data-testid="configure">
    <UiContainer class="w-full">
      <UiLink :to="localePath(SITE_PRODUCTS_PATH)" :tone="UiLinkTone.Muted" :underline="false" class="inline-flex items-center gap-2 text-sm mb-8" testid="configure-back">
        <ArrowLeft :size="16" :stroke-width="2" />
        {{ $t('common.backToProducts') }}
      </UiLink>

      <!-- Loading -->
      <UiAsideLayout v-if="pending" data-testid="configure-loading">
        <template #main>
          <UiStack :gap="UiGap.Md">
            <UiSkeleton :shape="UiSkeletonShape.Block" :width="UiSkeletonWidth.Heading" class="h-8" />
            <UiSkeleton :shape="UiSkeletonShape.Block" class="h-4" />
            <UiSkeleton :shape="UiSkeletonShape.Block" :width="UiSkeletonWidth.ThreeQuarters" class="h-4" />
            <UiSkeleton :shape="UiSkeletonShape.Block" class="h-48 mt-6" />
          </UiStack>
        </template>
        <template #aside>
          <UiSkeleton :shape="UiSkeletonShape.Block" class="h-80" />
        </template>
      </UiAsideLayout>

      <!-- Not found -->
      <UiEmptyState v-else-if="!found" :icon="AlertCircle" :title="$t('configure.notFound')" data-testid="configure-missing">
        <UiLink :to="localePath(SITE_PRODUCTS_PATH)" :tone="UiLinkTone.Primary" testid="configure-missing-back">{{ $t('common.backToProducts') }}</UiLink>
      </UiEmptyState>

      <!-- Configure layout -->
      <UiAsideLayout v-else>
        <!-- Product details -->
        <template #main>
          <UiStack :gap="UiGap.Xl">
            <UiStack :gap="UiGap.None">
              <UiStack :direction="UiDirection.Row" :align="UiCrossAlign.Center" :gap="UiGap.Md" class="mb-4">
                <UiAccent v-if="icon && color" :color="color">
                  <UiAccentTile :icon="icon" />
                </UiAccent>
                <UiStack :gap="UiGap.None">
                  <UiText :size="UiTextSize.Xs" :tone="UiTextTone.Muted" class="uppercase tracking-widest mb-1">{{ groupName }}</UiText>
                  <UiHeading :level="UiHeadingLevel.H1" class="text-2xl md:text-3xl" data-testid="configure-product-name">{{ productName }}</UiHeading>
                </UiStack>
              </UiStack>
              <UiText v-if="description" :tone="UiTextTone.Muted" class="leading-relaxed">
                <UiText :as="UiTextTag.Span" :weight="UiTextWeight.Bold" :size="UiTextSize.Inherit">{{ productName }}</UiText> {{ description }}
              </UiText>
            </UiStack>

            <!-- Features -->
            <UiCard v-if="features.length > 0" data-testid="configure-features">
              <UiHeading :level="UiHeadingLevel.H2" :size="UiHeadingSize.Label" class="mb-4 tracking-widest">{{ $t('configure.includedFeatures') }}</UiHeading>
              <UiList :list-style="UiListStyle.Plain">
                <UiListItem v-for="feature in features" :key="feature" class="flex items-center gap-2 text-sm">
                  <CheckCircle :size="15" :stroke-width="2" class="text-cyan-700 dark:text-cyan-400 flex-shrink-0 [.tpl-classic_&]:text-classic-brand" />
                  {{ feature }}
                </UiListItem>
              </UiList>
            </UiCard>

            <!-- Billing cycle -->
            <UiStack :gap="UiGap.None">
              <UiHeading :level="UiHeadingLevel.H2" :size="UiHeadingSize.Label" class="mb-3 tracking-widest">{{ $t('configure.chooseBillingCycle') }}</UiHeading>
              <UiStack :gap="UiGap.Xs">
                <UiCard
                  v-for="option in cycles"
                  :key="option.cycle"
                  :padding="UiCardPadding.Sm"
                  :class="[selectedCycle === option.cycle ? 'ring-2 ring-cyan-500/60 [.tpl-classic_&]:ring-1 [.tpl-classic_&]:ring-classic-brand [.tpl-classic_&]:border-classic-brand' : '', 'flex items-center justify-between cursor-pointer transition-all duration-200']"
                  :data-testid="`configure-cycle-${option.cycle}`"
                  @click="emit('update:selectedCycle', option.cycle)"
                >
                  <UiRadio
                    name="configure-cycle"
                    :value="option.cycle"
                    :model-value="selectedCycle"
                    :label="option.label"
                    @update:model-value="emit('update:selectedCycle', option.cycle)"
                  />
                  <UiText :as="UiTextTag.Span" :weight="UiTextWeight.Bold">{{ option.price }}</UiText>
                </UiCard>
              </UiStack>
            </UiStack>
          </UiStack>
        </template>

        <!-- Order summary -->
        <template #aside>
          <UiCard :padding="UiCardPadding.None" class="overflow-hidden" data-testid="configure-summary">
            <UiStack :gap="UiGap.None" class="px-6 py-4 bg-white dark:bg-white/5 border-b border-gray-200 dark:border-white/10 [.tpl-classic_&]:border-classic-border [.tpl-classic_&]:bg-classic-surface-2">
              <UiHeading :level="UiHeadingLevel.H2" :size="UiHeadingSize.Subsection">{{ $t('configure.orderSummary') }}</UiHeading>
            </UiStack>

            <UiStack :gap="UiGap.Md" class="p-6">
              <UiStack :gap="UiGap.None">
                <UiText :weight="UiTextWeight.Semibold">{{ productName }}</UiText>
                <UiText :size="UiTextSize.Sm" :tone="UiTextTone.Muted" class="italic">{{ groupName }}</UiText>
              </UiStack>

              <UiStack :gap="UiGap.Xs" class="pt-2 border-t border-gray-200 dark:border-white/10">
                <UiKeyValue :label="productName" :value="selectedPrice" />
                <UiKeyValue :label="$t('configure.setupFees')" :value="setupFee" />
                <UiKeyValue :label="selectedCycleLabel" :value="selectedPrice" />
              </UiStack>

              <UiStack :gap="UiGap.None" class="pt-3 border-t border-gray-200 dark:border-white/10">
                <UiText :as="UiTextTag.Span" :size="UiTextSize.Inherit" :weight="UiTextWeight.Bold" class="text-2xl" data-testid="configure-total">{{ selectedPrice }}</UiText>
                <UiText :size="UiTextSize.Xs" :tone="UiTextTone.Muted" class="text-right mt-0.5">{{ $t('configure.totalDueToday') }}</UiText>
              </UiStack>

              <UiButton
                :variant="UiButtonVariant.Primary"
                :size="UiSize.Lg"
                full-width
                data-testid="configure-continue"
                @click="emit('continue')"
              >
                {{ $t('common.continue') }}
                <ArrowRight :size="18" :stroke-width="2.5" class="ml-2" />
              </UiButton>
            </UiStack>
          </UiCard>

          <UiText :size="UiTextSize.Xs" :tone="UiTextTone.Subtle" class="mt-4 text-center flex items-center justify-center gap-1.5">
            <HelpCircle :size="13" :stroke-width="2" />
            {{ $t('configure.haveQuestions') }}
            <UiLink :to="localePath(SITE_CONTACT_PATH)" :tone="UiLinkTone.Primary" :underline="false" class="hover:underline" testid="configure-contact">{{ $t('configure.clickHere') }}</UiLink>
          </UiText>
        </template>
      </UiAsideLayout>
    </UiContainer>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * The configure screen of one product: its description and features, the billing-cycle choice and
 * the order summary with the Continue button.
 *
 * Presentational: the page reads the product, formats the prices and owns the cart. This component
 * shows a skeleton while `pending`, a notice when the product was not `found`, and the screen
 * otherwise.
 */
import { UiButtonVariant, UiCardPadding, UiCrossAlign, UiDirection, UiGap, UiHeadingLevel, UiHeadingSize, UiLinkTone, UiListStyle, UiSize, UiSkeletonShape, UiSkeletonWidth, UiTextSize, UiTextTag, UiTextTone, UiTextWeight } from '~/utils/ui/enums'
import { AlertCircle, ArrowLeft, ArrowRight, CheckCircle, HelpCircle } from 'lucide-vue-next'
import type { PricingCycle } from '~/types/pricingcycle'
import type { PricingCycleOption } from '~/types/pricingcycleoption'
import { SITE_CONTACT_PATH, SITE_PRODUCTS_PATH } from '~/constants/siteRoutes'

/** Props of Configure. */
interface Props {
  /** Whether the product is still being read. */
  pending: boolean
  /** Whether a product matches the address. */
  found: boolean
  /** Name of the product's family. */
  groupName: string
  /** Name of the product. */
  productName: string
  /** Leading prose of the product description; empty for bullet-only copy. */
  description: string
  /** Feature bullets of the description. */
  features: string[]
  /** Full icon name of the family, e.g. `mdi:server`; null when the family is unknown. */
  icon: string | null
  /** Accent colour of the family as a hex string; null when the family is unknown. */
  color: string | null
  /** The cycles the product is priced for. */
  cycles: PricingCycleOption[]
  /** The selected cycle. */
  selectedCycle: PricingCycle
  /** The selected cycle's name in the reader's language. */
  selectedCycleLabel: string
  /** The price for the selected cycle, formatted. */
  selectedPrice: string
  /** The setup fee, formatted. */
  setupFee: string
}

defineProps<Props>()

/** Events of Configure. */
const emit = defineEmits<{
  /** The visitor chose another billing cycle. */
  'update:selectedCycle': [cycle: PricingCycle]
  /** The visitor pressed Continue. */
  continue: []
}>()

/** Prefixes a path with the active language. */
const localePath = useLocalePath()
</script>
