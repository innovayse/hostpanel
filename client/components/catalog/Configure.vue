<template>
  <UiStack gap="none" class="bg-ui-page py-10 md:py-16 [.tpl-classic_&]:[--accent:rgb(var(--classic-brand))]" data-testid="configure">
    <UiContainer>
      <div>
        <UiLink :to="localePath(SITE_PRODUCTS_PATH)" tone="muted" :underline="false" class="inline-flex items-center gap-2 text-sm mb-8" testid="configure-back">
          <ArrowLeft :size="16" :stroke-width="2" />
          {{ $t('configure.back') }}
        </UiLink>

        <!-- Loading -->
        <div v-if="pending" class="grid md:grid-cols-[1fr_360px] gap-8" data-testid="configure-loading">
          <div class="space-y-4">
            <div class="w-64"><UiSkeleton shape="block" class="h-8" /></div>
            <UiSkeleton shape="block" class="h-4" />
            <div class="w-3/4"><UiSkeleton shape="block" class="h-4" /></div>
            <UiSkeleton shape="block" class="h-48 mt-6" />
          </div>
          <UiSkeleton shape="block" class="h-80" />
        </div>

        <!-- Not found -->
        <UiEmptyState v-else-if="!found" :icon="AlertCircle" :title="$t('configure.notFound')" data-testid="configure-missing">
          <UiLink :to="localePath(SITE_PRODUCTS_PATH)" testid="configure-missing-back">{{ $t('configure.back') }}</UiLink>
        </UiEmptyState>

        <!-- Configure layout -->
        <div v-else class="grid md:grid-cols-[1fr_360px] gap-8 items-start">
          <!-- Product details -->
          <div class="space-y-8">
            <div>
              <div class="flex items-center gap-4 mb-4">
                <UiAccent v-if="icon && color" :color="color">
                  <UiAccentTile :icon="icon" />
                </UiAccent>
                <div>
                  <div class="text-xs text-gray-600 dark:text-gray-400 uppercase tracking-widest mb-1">{{ groupName }}</div>
                  <UiHeading :level="1" class="text-2xl md:text-3xl" data-testid="configure-product-name">{{ productName }}</UiHeading>
                </div>
              </div>
              <UiText v-if="description" tone="muted" class="leading-relaxed">
                <strong class="text-gray-900 dark:text-white">{{ productName }}</strong> {{ description }}
              </UiText>
            </div>

            <!-- Features -->
            <UiCard v-if="features.length > 0" data-testid="configure-features">
              <UiHeading :level="2" size="label" class="mb-4 tracking-widest">{{ $t('configure.includedFeatures') }}</UiHeading>
              <UiList list-style="plain">
                <UiListItem v-for="feature in features" :key="feature" class="flex items-center gap-2 text-sm">
                  <CheckCircle :size="15" :stroke-width="2" class="text-cyan-700 dark:text-cyan-400 flex-shrink-0 [.tpl-classic_&]:text-classic-brand" />
                  {{ feature }}
                </UiListItem>
              </UiList>
            </UiCard>

            <!-- Billing cycle -->
            <div>
              <UiHeading :level="2" size="label" class="mb-3 tracking-widest">{{ $t('configure.chooseBillingCycle') }}</UiHeading>
              <div class="space-y-2">
                <UiCard
                  v-for="option in cycles"
                  :key="option.cycle"
                  padding="sm"
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
                  <span class="font-bold text-gray-900 dark:text-white">{{ option.price }}</span>
                </UiCard>
              </div>
            </div>
          </div>

          <!-- Order summary -->
          <div class="sticky top-24">
            <UiCard padding="none" class="overflow-hidden" data-testid="configure-summary">
              <div class="px-6 py-4 bg-white dark:bg-white/5 border-b border-gray-200 dark:border-white/10 [.tpl-classic_&]:border-classic-border [.tpl-classic_&]:bg-classic-surface-2">
                <UiHeading :level="2" size="subsection">{{ $t('configure.orderSummary') }}</UiHeading>
              </div>

              <div class="p-6 space-y-4">
                <div>
                  <div class="font-semibold text-gray-900 dark:text-white">{{ productName }}</div>
                  <div class="text-sm text-gray-600 dark:text-gray-400 italic">{{ groupName }}</div>
                </div>

                <div class="space-y-2 pt-2 border-t border-gray-200 dark:border-white/10">
                  <div class="flex justify-between text-sm">
                    <span class="text-gray-600 dark:text-gray-400">{{ productName }}</span>
                    <span class="text-gray-900 dark:text-white">{{ selectedPrice }}</span>
                  </div>
                  <div class="flex justify-between text-sm">
                    <span class="text-gray-600 dark:text-gray-400">{{ $t('configure.setupFees') }}</span>
                    <span class="text-gray-600 dark:text-gray-400">{{ setupFee }}</span>
                  </div>
                  <div class="flex justify-between text-sm">
                    <span class="text-gray-600 dark:text-gray-400">{{ selectedCycleLabel }}</span>
                    <span class="text-gray-900 dark:text-white">{{ selectedPrice }}</span>
                  </div>
                </div>

                <div class="pt-3 border-t border-gray-200 dark:border-white/10">
                  <div class="flex justify-between">
                    <span class="text-2xl font-bold text-gray-900 dark:text-white" data-testid="configure-total">{{ selectedPrice }}</span>
                  </div>
                  <div class="text-xs text-gray-600 dark:text-gray-400 text-right mt-0.5">{{ $t('configure.totalDueToday') }}</div>
                </div>

                <UiButton
                  size="lg"
                  full-width
                  class="py-4 font-bold bg-gradient-to-r from-cyan-600 to-primary-600 hover:from-cyan-500 hover:to-primary-500 hover:scale-[1.02] shadow-lg [.tpl-classic_&]:bg-none [.tpl-classic_&]:shadow-none [.tpl-classic_&]:hover:scale-100 [.tpl-classic_&]:font-semibold"
                  data-testid="configure-continue"
                  @click="emit('continue')"
                >
                  {{ $t('configure.continue') }}
                  <ArrowRight :size="18" :stroke-width="2.5" class="ml-2" />
                </UiButton>
              </div>
            </UiCard>

            <UiText size="xs" tone="subtle" class="mt-4 text-center flex items-center justify-center gap-1.5">
              <HelpCircle :size="13" :stroke-width="2" />
              {{ $t('configure.haveQuestions') }}
              <UiLink :to="localePath(SITE_CONTACT_PATH)" :underline="false" class="hover:underline" testid="configure-contact">{{ $t('configure.clickHere') }}</UiLink>
            </UiText>
          </div>
        </div>
      </div>
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
