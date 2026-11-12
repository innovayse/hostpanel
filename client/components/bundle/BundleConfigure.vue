<template>
  <UiStack gap="none" class="bg-ui-page py-10 md:py-16" data-testid="bundle-configure">
    <UiContainer>
      <div>
        <UiLink :to="localePath('/products')" tone="muted" :underline="false" class="mb-8 inline-flex items-center gap-2 text-sm" testid="bundle-back">
          <ArrowLeft :size="16" :stroke-width="2" />
          {{ $t('bundles.back') }}
        </UiLink>

        <!-- Loading -->
        <div v-if="pending" class="grid gap-8 md:grid-cols-[1fr_360px]" data-testid="bundle-loading">
          <UiSkeleton shape="block" class="h-64" />
          <UiSkeleton shape="block" class="h-80" />
        </div>

        <!-- Not found -->
        <UiEmptyState v-else-if="!bundle" :icon="AlertCircle" :title="$t('bundles.notFound')" data-testid="bundle-missing">
          <UiLink :to="localePath('/products')" testid="bundle-missing-back">{{ $t('bundles.back') }}</UiLink>
        </UiEmptyState>

        <div v-else class="grid items-start gap-8 md:grid-cols-[1fr_360px]">
          <!-- What the bundle is made of -->
          <div class="space-y-6">
            <div>
              <UiHeading :level="1" class="text-2xl md:text-3xl" data-testid="bundle-name">{{ bundle.name }}</UiHeading>
              <UiText v-if="bundle.description" tone="muted" class="mt-3 leading-relaxed">{{ bundle.description }}</UiText>
              <UiBadge v-if="discountLabel" class="mt-3" data-testid="bundle-discount">{{ discountLabel }}</UiBadge>
            </div>

            <UiCard
              v-for="row in rows"
              :key="row.productId"
              class="space-y-3"
              :data-testid="`bundle-component-${row.productId}`"
            >
              <div class="flex flex-wrap items-center justify-between gap-2">
                <UiHeading :level="2" size="subsection">{{ row.name }}</UiHeading>
                <UiBadge v-if="row.isPrimary" variant="info">{{ $t('bundles.primary') }}</UiBadge>
              </div>
              <UiSelect
                v-if="row.options.length > 0"
                :label="$t('bundles.cycle')"
                :placeholder="$t('bundles.cyclePlaceholder')"
                :model-value="row.selected?.cycle"
                :options="cycleOptions(row)"
                :testid="`bundle-cycle-${row.productId}`"
                @update:model-value="value => emit('update:cycle', row.productId, value as PricingCycle)"
              />
              <UiAlert v-else variant="warning" :data-testid="`bundle-unpriced-${row.productId}`">{{ $t('bundles.unpriced') }}</UiAlert>
            </UiCard>

            <!-- The domain step, when the bundle includes a domain -->
            <slot name="domain" />
          </div>

          <!-- Order summary -->
          <div class="sticky top-24">
            <UiCard padding="none" class="overflow-hidden" data-testid="bundle-summary">
              <div class="border-b border-gray-200 bg-white px-6 py-4 dark:border-white/10 dark:bg-white/5">
                <UiHeading :level="2" size="subsection">{{ $t('bundles.orderSummary') }}</UiHeading>
              </div>

              <div class="space-y-4 p-6">
                <div class="space-y-2">
                  <div v-for="line in lines" :key="line.id" class="flex justify-between gap-3 text-sm" data-testid="bundle-line">
                    <span class="min-w-0 text-gray-600 dark:text-gray-400">
                      {{ line.name }}
                      <span class="block text-xs">{{ line.detail }}</span>
                    </span>
                    <span class="whitespace-nowrap text-gray-900 dark:text-white">{{ line.price }}</span>
                  </div>
                </div>

                <div class="space-y-2 border-t border-gray-200 pt-3 dark:border-white/10">
                  <CheckoutBundleSummary :name="bundle.name" :offer="discountLabel" :discount="discount" />
                  <div class="flex justify-between">
                    <span class="text-2xl font-bold text-gray-900 dark:text-white" data-testid="bundle-total">{{ total }}</span>
                  </div>
                  <UiText size="xs" tone="subtle" class="text-right">{{ $t('bundles.summary.estimate') }}</UiText>
                </div>

                <UiText v-if="!complete" size="xs" tone="subtle" data-testid="bundle-incomplete">{{ $t('bundles.summary.incomplete') }}</UiText>

                <UiButton size="lg" full-width :disabled="!complete" data-testid="bundle-continue" @click="emit('continue')">
                  {{ $t('bundles.continue') }}
                  <ArrowRight :size="18" :stroke-width="2.5" class="ml-2" />
                </UiButton>
              </div>
            </UiCard>
          </div>
        </div>
      </div>
    </UiContainer>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * The order screen of one bundle: its components with a billing-cycle choice each, the domain step
 * (the `domain` slot, present when the bundle includes a domain) and the order summary with the
 * discount, the estimated total and the Continue button.
 *
 * Presentational: the page reads the bundle and the prices, owns the choices and the cart. This
 * component shows a skeleton while `pending`, a notice when the bundle was not found, and the
 * screen otherwise. The total is an estimate; the backend applies the real discount.
 */
import { AlertCircle, ArrowLeft, ArrowRight } from 'lucide-vue-next'
import type { BundleComponentRow } from '~/types/bundlecomponentrow'
import type { BundleSummaryLine } from '~/types/bundlesummaryline'
import type { CatalogBundle } from '~/types/catalogbundle'
import type { PricingCycle } from '~/types/pricingcycle'

/** Props of BundleConfigure. */
interface Props {
  /** Whether the bundle is still being read. */
  pending: boolean
  /** The bundle; null when none matches the address. */
  bundle: CatalogBundle | null
  /** The bundle's components with the visitor's cycle choices. */
  rows: BundleComponentRow[]
  /** The bundle's discount in words; empty when it has none. */
  discountLabel: string
  /** The priced lines of the summary. */
  lines: BundleSummaryLine[]
  /** The estimated discount, formatted. */
  discount: string
  /** The estimated total, formatted. */
  total: string
  /** Whether everything the bundle needs is chosen. */
  complete: boolean
}

defineProps<Props>()

/** Events of BundleConfigure. */
const emit = defineEmits<{
  /** The visitor chose another billing cycle for a component. */
  'update:cycle': [productId: number, cycle: PricingCycle]
  /** The visitor pressed Continue. */
  continue: []
}>()

/** Prefixes a path with the active language. */
const localePath = useLocalePath()

/**
 * The list options of a component's cycle choice.
 *
 * @param row The component.
 * @returns One option per priced cycle, labelled with its price.
 */
const cycleOptions = (row: BundleComponentRow): { value: string, label: string }[] =>
  row.options.map(option => ({ value: option.cycle, label: `${option.label} - ${option.price}` }))
</script>
