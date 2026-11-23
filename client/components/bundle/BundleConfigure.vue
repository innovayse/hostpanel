<template>
  <UiSection :padding="UiSectionPadding.Md" :contained="false" class="bg-ui-page" data-testid="bundle-configure">
    <UiContainer>
      <UiBackLink :to="localePath(SITE_PRODUCTS_PATH)" :label="$t('common.backToProducts')" testid="bundle-back" class="mb-8" />

      <!-- Loading -->
      <UiSplit v-if="pending" :ratio="UiSplitRatio.StartWide" :gap="UiGap.Xl" data-testid="bundle-loading">
        <template #start>
          <UiSkeleton :shape="UiSkeletonShape.Block" class="h-64" />
        </template>
        <template #end>
          <UiSkeleton :shape="UiSkeletonShape.Block" class="h-80" />
        </template>
      </UiSplit>

      <!-- Not found -->
      <UiEmptyState v-else-if="!bundle" :icon="AlertCircle" :title="$t('bundles.notFound')" data-testid="bundle-missing">
        <UiLink :to="localePath(SITE_PRODUCTS_PATH)" :tone="UiLinkTone.Primary" testid="bundle-missing-back">{{ $t('common.backToProducts') }}</UiLink>
      </UiEmptyState>

      <UiSplit v-else :ratio="UiSplitRatio.StartWide" :gap="UiGap.Xl">
        <!-- What the bundle is made of -->
        <template #start>
          <UiStack :gap="UiGap.Lg">
            <UiStack :gap="UiGap.Sm" :align="UiCrossAlign.Start">
              <UiHeading :level="UiHeadingLevel.H1" :size="UiHeadingSize.Section" data-testid="bundle-name">{{ bundle.name }}</UiHeading>
              <UiText v-if="bundle.description" :tone="UiTextTone.Muted" class="leading-relaxed">{{ bundle.description }}</UiText>
              <UiBadge v-if="discountLabel" data-testid="bundle-discount">{{ discountLabel }}</UiBadge>
            </UiStack>

            <UiCard
              v-for="row in rows"
              :key="row.productId"
              :data-testid="`bundle-component-${row.productId}`"
            >
              <UiStack :gap="UiGap.Sm">
                <UiStack :direction="UiDirection.Row" wrap :align="UiCrossAlign.Center" :justify="UiJustify.Between" :gap="UiGap.Xs">
                  <UiHeading :level="UiHeadingLevel.H2" :size="UiHeadingSize.Subsection">{{ row.name }}</UiHeading>
                  <UiBadge v-if="row.isPrimary" :variant="UiTone.Info">{{ $t('bundles.primary') }}</UiBadge>
                </UiStack>
                <UiSelect
                  v-if="row.options.length > 0"
                  :label="$t('bundles.cycle')"
                  :placeholder="$t('bundles.cyclePlaceholder')"
                  :model-value="row.selected?.cycle"
                  :options="cycleOptions(row)"
                  :testid="`bundle-cycle-${row.productId}`"
                  @update:model-value="value => emit('update:cycle', row.productId, value as PricingCycle)"
                />
                <UiAlert v-else :variant="UiAlertVariant.Warning" :data-testid="`bundle-unpriced-${row.productId}`">{{ $t('bundles.unpriced') }}</UiAlert>
              </UiStack>
            </UiCard>

            <!-- The domain step, when the bundle includes a domain -->
            <slot name="domain" />
          </UiStack>
        </template>

        <!-- Order summary -->
        <template #end>
          <UiCard class="sticky top-24" data-testid="bundle-summary">
            <UiCardHeader :title="$t('bundles.orderSummary')" />

            <UiStack>
              <UiStack :gap="UiGap.Xs">
                <UiStack
                  v-for="line in lines"
                  :key="line.id"
                  :direction="UiDirection.Row"
                  :justify="UiJustify.Between"
                  :gap="UiGap.Sm"
                  data-testid="bundle-line"
                >
                  <UiStack :gap="UiGap.None" class="min-w-0">
                    <UiText :size="UiTextSize.Sm" :tone="UiTextTone.Muted">{{ line.name }}</UiText>
                    <UiText :size="UiTextSize.Xs" :tone="UiTextTone.Muted">{{ line.detail }}</UiText>
                  </UiStack>
                  <UiText :as="UiTextTag.Span" :size="UiTextSize.Sm" class="whitespace-nowrap">{{ line.price }}</UiText>
                </UiStack>
              </UiStack>

              <UiDivider />

              <UiStack :gap="UiGap.Xs">
                <CheckoutBundleSummary :name="bundle.name" :offer="discountLabel" :discount="discount" />
                <UiText :size="UiTextSize.Inherit" :weight="UiTextWeight.Bold" class="text-2xl" data-testid="bundle-total">{{ total }}</UiText>
                <UiText :size="UiTextSize.Xs" :tone="UiTextTone.Subtle" class="text-right">{{ $t('bundles.summary.estimate') }}</UiText>
              </UiStack>

              <UiText v-if="!complete" :size="UiTextSize.Xs" :tone="UiTextTone.Subtle" data-testid="bundle-incomplete">{{ $t('bundles.summary.incomplete') }}</UiText>

              <UiButton :variant="UiButtonVariant.Primary" :size="UiSize.Lg" full-width icon="mdi:arrow-right" :disabled="!complete" data-testid="bundle-continue" @click="emit('continue')">
                {{ $t('common.continue') }}
              </UiButton>
            </UiStack>
          </UiCard>
        </template>
      </UiSplit>
    </UiContainer>
  </UiSection>
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
import { UiAlertVariant, UiButtonVariant, UiCrossAlign, UiDirection, UiGap, UiHeadingLevel, UiHeadingSize, UiJustify, UiLinkTone, UiSectionPadding, UiSize, UiSkeletonShape, UiSplitRatio, UiTextSize, UiTextTag, UiTextTone, UiTextWeight, UiTone } from '~/utils/ui/enums'
import { AlertCircle } from 'lucide-vue-next'
import type { BundleComponentRow } from '~/types/bundlecomponentrow'
import type { BundleSummaryLine } from '~/types/bundlesummaryline'
import type { CatalogBundle } from '~/types/catalogbundle'
import type { PricingCycle } from '~/types/pricingcycle'
import { SITE_PRODUCTS_PATH } from '~/constants/siteRoutes'

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
