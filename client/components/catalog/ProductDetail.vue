<template>
  <UiAccent :color="product.color">
    <UiStack gap="none" class="bg-ui-page" data-testid="product-detail">
      <!-- Header block: the family's mark, name, description and key bullets. -->
      <UiSection
        :contained="false"
        padding="lg"
        :class="[HEADER_CLASSES, ACCENT_SCOPE_CLASS]"
        data-testid="product-header"
      >
        <UiBackdrop variant="aurora" class="[.tpl-classic_&]:hidden" />

        <UiContainer class="relative z-10">
          <slot name="breadcrumbs" />

          <UiStack direction="row" align="center" gap="md">
            <UiAccentTile :icon="product.icon" size="lg" />
            <UiHeading
              :level="UiHeadingLevel.H1"
              size="display"
              class="min-w-0 break-words [.tpl-classic_&]:text-3xl [.tpl-classic_&]:font-semibold [.tpl-classic_&]:text-classic-ink md:[.tpl-classic_&]:text-4xl"
              data-testid="product-name"
            >
              {{ product.name }}
            </UiHeading>
          </UiStack>

          <!-- Interpolated, never HTML: the description is operator-authored and has been stripped of its tags. -->
          <UiText
            v-if="product.description"
            size="lg"
            tone="muted"
            class="mt-5 max-w-[75ch] [.tpl-classic_&]:text-classic-muted"
            data-testid="product-description"
          >
            {{ product.description }}
          </UiText>

          <UiList
            v-if="product.features.length"
            list-style="bare"
            class="mt-6 grid gap-x-8 gap-y-3 sm:grid-cols-2 lg:grid-cols-3"
            data-testid="product-features"
          >
            <UiListItem
              v-for="feature in product.features"
              :key="feature"
              class="flex items-start gap-3 text-sm text-gray-700 dark:text-gray-300 [.tpl-classic_&]:text-classic-ink"
            >
              <CheckCircle :size="18" :stroke-width="2" :class="[ACCENT_CLASSES.text, 'mt-0.5 flex-shrink-0']" />
              <UiText as="span" size="inherit" tone="inherit" class="min-w-0 break-words">{{ feature }}</UiText>
            </UiListItem>
          </UiList>
        </UiContainer>
      </UiSection>

      <!-- Plans block: heading, billing-cycle choice and the balanced grid of plan cards. -->
      <UiSection :contained="false" padding="lg" :class="ACCENT_SCOPE_CLASS" data-testid="product-plans">
        <UiContainer>
          <CatalogProductSectionHeader :title="$t('products.pricing.title')" class="mb-8" />

          <UiStack v-if="cycles.length > 1" direction="row" justify="center" gap="none" class="mb-8 md:mb-10">
            <UiSegmentedControl
              :model-value="selectedCycle"
              :options="cycleOptions"
              :label="$t('hosting.billingCycles')"
              size="sm"
              testid="product-cycles"
              class="max-w-full overflow-x-auto no-scrollbar"
              @update:model-value="emit('update:selectedCycle', $event)"
            />
          </UiStack>

          <UiBalancedGrid :count="product.plans.length" data-testid="product-plans-grid">
            <CatalogProductPlanCard
              v-for="(plan, index) in product.plans"
              :key="plan.id"
              :plan="plan"
              :price="priceFor(plan)"
              :cycle="selectedCycle"
              :in-cart="isInCart(plan)"
              :popular="plan.id === popularPlanId"
              :trial-url="trialUrlFor(plan)"
              class="animate-reveal-up motion-reduce:animate-none"
              :class="revealDelay(index * 100)"
              @add-to-cart="emit('addToCart', $event)"
              @trial="emit('trial', $event)"
            />
          </UiBalancedGrid>
        </UiContainer>
      </UiSection>

      <!-- Comparison block: only when the plans share enough specification lines. -->
      <UiSection
        v-if="comparable"
        :id="COMPARE_PLANS_ANCHOR"
        :contained="false"
        padding="lg"
        :class="[SURFACE_CLASSES, ACCENT_SCOPE_CLASS]"
        data-testid="product-compare"
      >
        <UiContainer>
          <CatalogProductSectionHeader :title="$t('hosting.comparison.title')" :subtitle="$t('hosting.comparison.subtitle')" class="mb-8 md:mb-10" />
          <CatalogProductPlansTable :plans="product.plans" :rows="comparisonRows" :price-for="priceFor" />
        </UiContainer>
      </UiSection>

      <!-- FAQ block, drawn by the page; it renders nothing when the operator has no questions. -->
      <slot name="faq" />

      <!-- Bottom band: for a visitor who is still unsure. -->
      <UiSection :contained="false" padding="lg" :class="[SURFACE_CLASSES, 'border-t border-gray-200 dark:border-white/10 [.tpl-classic_&]:border-classic-border']" data-testid="product-help">
        <UiContainer>
          <UiStack align="center" gap="none" class="text-center">
            <UiHeading
              :level="UiHeadingLevel.H2"
              size="bare"
              class="mb-4 text-3xl font-bold tracking-tight text-gray-900 dark:text-white md:text-4xl [.tpl-classic_&]:text-classic-ink"
            >
              {{ $t('products.help.title') }}
            </UiHeading>
            <UiText size="lg" tone="muted" class="mb-8 max-w-[75ch] [.tpl-classic_&]:text-classic-muted">{{ $t('products.help.subtitle') }}</UiText>
            <UiStack direction="row" wrap justify="center" gap="md">
              <UiButton :to="compareHref" size="lg" data-testid="product-compare-link">{{ $t('products.help.compare') }}</UiButton>
              <UiButton :to="localePath('/contact')" :variant="UiButtonVariant.Outline" size="lg" data-testid="product-contact">
                {{ $t('products.help.contact') }}
              </UiButton>
            </UiStack>
          </UiStack>
        </UiContainer>
      </UiSection>
    </UiStack>
  </UiAccent>
</template>

<script setup lang="ts">
/**
 * The body of a product family's detail page, as one column of blocks inside one container width:
 * the header (mark, family name, description, key bullets), the plans (heading, billing-cycle
 * choice, a balanced grid of plan cards), the specification comparison (only when the plans are
 * comparable), a slot for the FAQ and the closing "not sure which plan" band.
 *
 * It is presentational. The page owns the data and the cart: it passes the family, the cycles on
 * offer, the chosen cycle, the comparison rows and three lookups (`priceFor`, `isInCart`,
 * `trialUrlFor`), and reacts to the events. The sections are painted in the family's own accent
 * colour (the brand colour under classic) and share the vertical rhythm of the other pages.
 */
import { CheckCircle } from 'lucide-vue-next'
import { PRICING_CYCLE_I18N_KEY } from '~/constants/pricingCycle'
import { SITE_HOSTING_PATH, SITE_PRODUCTS_PATH } from '~/constants/siteRoutes'
import type { ComparisonRow } from '~/types/comparisonrow'
import type { PricingCycle } from '~/types/pricingcycle'
import type { ProductDetail } from '~/types/productdetail'
import type { ProductPlan } from '~/types/productplan'
import type { SegmentedOption } from '~/types/segmentedoption'
import { isComparable } from '~/utils/comparisonRows'
import { recommendedPlanId } from '~/utils/productFamilies'
import { revealDelay } from '~/utils/reveal'
import { ACCENT_CLASSES } from '~/utils/ui/accent'
import { UiButtonVariant, UiHeadingLevel } from '~/utils/ui/enums'

/** Props of ProductDetail. */
interface Props {
  /** The family to show. */
  product: ProductDetail
  /** The billing cycles at least one plan is priced for. */
  cycles: PricingCycle[]
  /** The cycle the prices are shown for. */
  selectedCycle: PricingCycle
  /** The price of a plan for the selected cycle, formatted; empty when it has none. */
  priceFor: (plan: ProductPlan) => string
  /** Whether a plan is already in the cart for the selected cycle. */
  isInCart: (plan: ProductPlan) => boolean
  /** The external order page of a plan, or null when this deployment has none. */
  trialUrlFor: (plan: ProductPlan) => string | null
  /** Specification rows aligned to the plans, as `buildComparisonRows` made them. */
  comparisonRows?: ComparisonRow[]
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  comparisonRows: () => [],
})

/** Events of ProductDetail. */
const emit = defineEmits<{
  /** The visitor chose another billing cycle. */
  'update:selectedCycle': [cycle: PricingCycle]
  /** The visitor asked to add a plan to the cart. */
  addToCart: [plan: ProductPlan]
  /** The visitor asked to start a trial of a plan. */
  trial: [plan: ProductPlan]
}>()

/** Id of the comparison block, which the "Compare plans" link points at. */
const COMPARE_PLANS_ANCHOR = 'compare-plans'

/** The header band: flat and hairline-ruled under classic, with the aurora backdrop elsewhere. */
const HEADER_CLASSES = 'relative overflow-hidden [.tpl-classic_&]:border-b [.tpl-classic_&]:border-classic-border [.tpl-classic_&]:bg-classic-surface-2 [.tpl-classic_&]:py-10 md:[.tpl-classic_&]:py-14'

/** The raised band the comparison and the closing band sit on. */
const SURFACE_CLASSES = 'bg-ui-surface [.tpl-classic_&]:bg-classic-surface-2'

/** Points the accent at the brand colour under classic, whose pages carry one colour only. */
const ACCENT_SCOPE_CLASS = '[.tpl-classic_&]:[--accent:rgb(var(--classic-brand))]'

/** Translation helpers. */
const { t } = useI18n()

/** Prefixes a path with the active language. */
const localePath = useLocalePath()

/** The billing-cycle choice, one segment per cycle on offer. */
const cycleOptions = computed<SegmentedOption<PricingCycle>[]>(() =>
  props.cycles.map((cycle) => ({
    value: cycle,
    label: t(`products.pricing.cycles.${PRICING_CYCLE_I18N_KEY[cycle]}`),
    testid: `product-cycle-${cycle}`,
  })))

/** The plan that wears the recommended badge, or null. */
const popularPlanId = computed(() => recommendedPlanId(props.product.plans))

/** Whether the plans share enough specification lines for a comparison to be worth showing. */
const comparable = computed(() => isComparable(props.comparisonRows))

/**
 * Where "Compare plans" leads: this page's own comparison when it has one, otherwise the hosting
 * page's comparison for the hosting family and the products page for every other family.
 */
const compareHref = computed(() => {
  if (comparable.value) {
    return `#${COMPARE_PLANS_ANCHOR}`
  }
  if (props.product.learnMoreUrl === SITE_HOSTING_PATH) {
    return `${localePath(SITE_HOSTING_PATH)}#${COMPARE_PLANS_ANCHOR}`
  }
  return localePath(SITE_PRODUCTS_PATH)
})
</script>
