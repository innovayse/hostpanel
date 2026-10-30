<template>
  <UiStack gap="none" class="overflow-x-hidden bg-classic-page" data-testid="classic-hosting">
    <!-- Hero: flat, left aligned, one brand accent -->
    <UiSection :contained="false" padding="lg" class="border-b border-classic-border bg-classic-surface-2 py-10 md:py-14">
      <UiContainer>
        <UiStack direction="row" align="center" gap="xs" class="inline-flex px-3 py-1.5 mb-4 rounded-md bg-classic-brand-soft">
          <Server :size="14" :stroke-width="2" class="text-classic-brand" />
          <UiText as="span" size="xs" tone="inherit" weight="medium" class="uppercase tracking-wider text-classic-brand">{{ $t('hosting.badge') }}</UiText>
        </UiStack>

        <UiHeading :level="UiHeadingLevel.H1" size="title" class="mb-3 text-3xl font-semibold leading-tight text-classic-ink md:text-4xl" data-testid="classic-hosting-title">
          {{ $t('hosting.title') }}
          <UiText as="span" size="inherit" tone="inherit" weight="semibold" class="text-classic-brand">{{ $t('hosting.titleHighlight') }}</UiText>
        </UiHeading>
        <UiText tone="inherit" class="max-w-[75ch] text-classic-muted md:text-lg">
          {{ $t('hosting.subtitle') }}
        </UiText>
      </UiContainer>
    </UiSection>

    <!-- Plans -->
    <UiSection :contained="false" padding="md" class="relative pt-8 md:pt-10 md:pb-16">
      <UiContainer>
        <!-- Loading -->
        <UiGrid v-if="pending" cols="3" gap="lg" class="py-12" data-testid="classic-hosting-loading">
          <UiSkeleton v-for="i in 3" :key="i" shape="block" class="h-[420px] rounded-lg" />
        </UiGrid>

        <!-- Error -->
        <UiStack v-else-if="error" align="center" gap="none" class="text-center py-16 px-4" data-testid="classic-hosting-error">
          <AlertCircle :size="40" :stroke-width="2" class="text-red-600 dark:text-red-400 mx-auto mb-4" />
          <UiText tone="inherit" class="text-classic-muted">{{ $t('hosting.loadError') }}</UiText>
          <UiButton variant="primary" size="sm" class="mt-4" data-testid="classic-hosting-retry" @click="refresh()">{{ $t('common.retry') }}</UiButton>
        </UiStack>

        <template v-else>
          <!-- Billing cycle tabs -->
          <UiStack direction="row" justify="center" gap="none" class="mb-8 md:mb-10">
            <UiSegmentedControl
              v-model="selectedCycle"
              :options="cycleOptions"
              :label="t('hosting.billingCycles')"
              size="sm"
              testid="classic-hosting-cycles"
              class="max-w-full overflow-x-auto no-scrollbar"
            />
          </UiStack>

          <!-- Free hosting -->
          <UiStack v-if="freePlan" gap="none" class="mb-12" data-testid="classic-hosting-free">
            <UiCard padding="none" class="p-6 md:p-8">
              <UiStack align="center" justify="between" gap="xl" class="lg:flex-row">
                <UiStack gap="none" class="flex-1 text-center lg:text-left">
                  <UiStack direction="row" align="center" gap="xs" class="inline-flex self-center lg:self-start px-2.5 py-1 rounded-md bg-classic-brand-soft mb-4">
                    <CheckCircle :size="14" class="text-classic-brand" />
                    <UiText as="span" size="xs" tone="inherit" weight="semibold" class="uppercase tracking-wider text-classic-brand">{{ $t('hosting.getStartedForFree') }}</UiText>
                  </UiStack>
                  <UiHeading :level="UiHeadingLevel.H2" size="title" class="mb-3 text-2xl font-semibold text-classic-ink md:text-3xl">
                    {{ formatPlanName(freePlan.name) }}
                  </UiHeading>
                  <!-- Interpolated, never `v-html`: the raw field is unsanitised operator HTML. -->
                  <UiText v-if="planSummary(freePlan)" tone="inherit" class="mb-5 max-w-[75ch] mx-auto lg:mx-0 leading-relaxed text-classic-muted">
                    {{ planSummary(freePlan) }}
                  </UiText>
                  <UiStack direction="row" wrap justify="center" gap="sm" class="lg:justify-start">
                    <UiStack
                      v-for="feature in planFeatures(freePlan).slice(0, 4)"
                      :key="feature"
                      direction="row"
                      align="center"
                      gap="xs"
                      class="text-xs md:text-sm text-classic-ink bg-classic-surface-2 px-3 py-2 rounded-md border border-classic-border"
                    >
                      <Check :size="14" class="text-classic-brand" />
                      {{ feature }}
                    </UiStack>
                  </UiStack>
                </UiStack>
                <UiStack align="center" gap="md" class="w-full lg:w-[280px] lg:items-end text-center lg:text-right">
                  <UiStack gap="none">
                    <UiText as="span" tone="inherit" weight="bold" class="block text-4xl text-classic-ink">{{ $t('hosting.free') }}</UiText>
                    <UiText as="span" size="xs" tone="inherit" weight="medium" class="uppercase tracking-wider text-classic-muted">{{ $t('hosting.foreverFree') }}</UiText>
                  </UiStack>
                  <UiStack gap="sm" class="w-full">
                    <UiButton variant="primary" size="lg" full-width data-testid="classic-hosting-free-add" @click="addToCart(freePlan)">
                      <ShoppingCart :size="18" class="mr-2" />
                      {{ cart.hasItem(freePlan.id, selectedCycle) ? $t('hosting.inCart') : $t('hosting.getStarted') }}
                    </UiButton>
                    <UiLink data-testid="classic-hosting-free-plan" :to="localePath(`/hosting/${planSlug(freePlan)}`)" tone="inherit" :underline="false" class="flex items-center justify-center gap-2 text-sm font-medium text-classic-brand hover:text-classic-brand-hover group">
                      {{ $t('hosting.viewAllFeatures') }}
                      <ArrowRight :size="14" class="group-hover:translate-x-0.5 transition-transform" />
                    </UiLink>
                  </UiStack>
                </UiStack>
              </UiStack>
            </UiCard>

            <!-- Separator -->
            <UiStack align="center" gap="none" class="text-center mt-12 mb-2">
              <UiText as="span" size="xs" tone="inherit" weight="semibold" class="block uppercase tracking-wider text-classic-muted">{{ $t('hosting.premiumSolutions') }}</UiText>
            </UiStack>
          </UiStack>

          <!-- Other plans grid -->
          <UiBalancedGrid :count="otherPlans.length" class="mb-8" data-testid="classic-hosting-plans">
            <UiStack
              v-for="(plan, index) in otherPlans"
              :key="plan.id"
              gap="none"
              class="relative animate-reveal-up motion-reduce:animate-none"
              data-testid="classic-hosting-plan"
              :class="revealDelay(index * 100)"
            >
              <!-- Outside the card: the card clips its overflow, which hid a badge sitting on its edge. -->
              <UiStack v-if="isFeatured(plan)" gap="none" class="absolute -top-3 left-1/2 -translate-x-1/2 z-20" data-testid="classic-hosting-recommended">
                <UiText as="span" size="xs" tone="inherit" weight="semibold" class="px-3 py-1 bg-classic-fill text-classic-on-brand rounded-md uppercase tracking-wider whitespace-nowrap">
                  {{ $t('hosting.recommended') }}
                </UiText>
              </UiStack>
              <UiCard
                padding="none"
                class="relative h-full p-6 flex flex-col overflow-hidden"
                :class="{ 'border-classic-brand ring-1 ring-classic-brand': isFeatured(plan) }"
              >
                <UiStack gap="none" class="mb-5">
                  <UiLink data-testid="classic-hosting-plan-link" :to="localePath(`/hosting/${planSlug(plan)}`)" tone="inherit" :underline="false" class="block mb-2">
                    <UiHeading :level="UiHeadingLevel.H2" size="title" class="text-xl font-semibold text-classic-ink hover:text-classic-brand transition-colors md:text-2xl">
                      {{ formatPlanName(plan.name) }}
                    </UiHeading>
                  </UiLink>
                  <!--
                    No tagline line: `ProductDto` has no such field, so it never rendered.
                    The summary below is parsed out of `description` and interpolated, never
                    `v-html` - the raw field is unsanitised operator HTML.
                  -->
                  <UiText v-if="planSummary(plan)" size="sm" tone="inherit" class="leading-relaxed line-clamp-2 min-h-[40px] text-classic-muted">
                    {{ planSummary(plan) }}
                  </UiText>
                </UiStack>

                <UiStack gap="none" class="mb-5 p-4 rounded-md bg-classic-surface-2 border border-classic-border">
                  <UiStack v-if="planPrice(plan, selectedCycle) !== null" direction="row" align="start" gap="none" class="mb-3 gap-1 items-baseline">
                    <UiText v-if="activeCurrencyPrefix" as="span" tone="inherit" weight="semibold" class="text-lg text-classic-muted">{{ activeCurrencyPrefix }}</UiText>
                    <UiText as="span" tone="inherit" weight="bold" class="text-4xl tabular-nums text-classic-ink" data-testid="classic-hosting-price">
                      {{ formatAmount(planPrice(plan, selectedCycle)) }}
                    </UiText>
                    <UiText v-if="payerCurrency.suffix" as="span" tone="inherit" weight="semibold" class="text-lg ml-1 text-classic-muted">{{ payerCurrency.suffix }}</UiText>
                    <UiText as="span" size="xs" tone="inherit" weight="medium" class="uppercase tracking-wider text-classic-muted">/ {{ cycleLabel(selectedCycle) }}</UiText>
                  </UiStack>
                  <UiStack v-else gap="none" class="mb-3">
                    <UiText as="span" tone="inherit" weight="bold" class="text-xl text-classic-ink">{{ $t('hosting.custom') }}</UiText>
                    <UiText as="span" size="xs" tone="inherit" class="mt-1 text-classic-muted">{{ $t('hosting.customContact') }}</UiText>
                  </UiStack>

                  <!-- Mini price list -->
                  <UiStack v-if="planCycles(plan).length > 1" gap="none" class="space-y-1 pt-3 border-t border-classic-border">
                    <UiButton
                      v-for="c in planCycles(plan).slice(0, 4)"
                      :key="c.key"
                      variant="ghost"
                      size="sm"
                      full-width
                      class="justify-between text-xs px-3 py-1.5 rounded-md"
                      :class="c.key === selectedCycle
                        ? 'bg-classic-brand-soft text-classic-brand font-semibold'
                        : 'text-classic-muted hover:text-classic-ink'"
                      :data-testid="`classic-hosting-mini-${c.key}`"
                      @click="selectedCycle = c.key"
                    >
                      <UiText as="span" tone="inherit" size="inherit" weight="medium">{{ cycleLabel(c.key) }}</UiText>
                      <UiText as="span" tone="inherit" size="inherit" weight="medium" class="font-mono">{{ c.price }}</UiText>
                    </UiButton>
                  </UiStack>
                </UiStack>

                <UiList v-if="planFeatures(plan).length" list-style="plain" class="space-y-3 mb-6 flex-1">
                  <UiListItem v-for="feature in planFeatures(plan)" :key="feature" class="flex items-start gap-3 text-sm text-classic-ink">
                    <Check :size="16" :stroke-width="2.5" class="mt-0.5 text-classic-brand flex-shrink-0" />
                    {{ feature }}
                  </UiListItem>
                </UiList>

                <UiStack gap="sm" class="mt-auto">
                  <UiButton
                    :variant="cart.hasItem(plan.id, selectedCycle) ? 'secondary' : 'primary'"
                    full-width
                    data-testid="classic-hosting-add"
                    @click="addToCart(plan)"
                  >
                    {{ cart.hasItem(plan.id, selectedCycle) ? $t('hosting.inCart') : $t('hosting.addToCart') }}
                  </UiButton>
                  <UiButton
                    data-testid="classic-hosting-plan-select"
                    :to="localePath(`/hosting/${planSlug(plan)}`)"
                    variant="outline"
                    full-width
                  >
                    {{ $t('hosting.getStarted') }}
                  </UiButton>
                </UiStack>
              </UiCard>
            </UiStack>
          </UiBalancedGrid>

          <!-- Agency section -->
          <UiStack v-if="agencyPlan" gap="none" class="mb-16" data-testid="classic-hosting-agency">
            <UiStack align="center" gap="none" class="text-center mb-6">
              <UiText as="span" size="xs" tone="inherit" weight="semibold" class="block uppercase tracking-wider text-classic-muted">{{ $t('hosting.enterpriseSolutions') }}</UiText>
            </UiStack>

            <UiCard padding="none" class="p-6 md:p-8 bg-classic-surface-2 animate-reveal-up motion-reduce:animate-none">
              <UiStack align="center" justify="between" gap="xl" class="lg:flex-row">
                <UiStack gap="none" class="flex-1 text-center lg:text-left">
                  <UiStack direction="row" align="center" gap="xs" class="inline-flex self-center lg:self-start px-2.5 py-1 rounded-md bg-classic-brand-soft mb-4">
                    <CheckCircle :size="14" class="text-classic-brand" />
                    <UiText as="span" size="xs" tone="inherit" weight="semibold" class="uppercase tracking-wider text-classic-brand">{{ $t('hosting.agencyBadge') }}</UiText>
                  </UiStack>
                  <UiHeading :level="UiHeadingLevel.H2" size="title" class="mb-3 text-2xl font-semibold text-classic-ink md:text-3xl">
                    {{ formatPlanName(agencyPlan.name) }}
                  </UiHeading>
                  <!-- Interpolated, never `v-html`: the raw field is unsanitised operator HTML. -->
                  <UiText v-if="planSummary(agencyPlan)" tone="inherit" class="mb-5 max-w-[75ch] mx-auto lg:mx-0 leading-relaxed text-classic-muted">
                    {{ planSummary(agencyPlan) }}
                  </UiText>
                  <UiStack direction="row" wrap justify="center" gap="sm" class="lg:justify-start">
                    <UiStack
                      v-for="feature in planFeatures(agencyPlan).slice(0, 6)"
                      :key="feature"
                      direction="row"
                      align="center"
                      gap="xs"
                      class="text-xs md:text-sm text-classic-ink bg-classic-surface px-3 py-2 rounded-md border border-classic-border"
                    >
                      <Check :size="14" class="text-classic-brand" />
                      {{ feature }}
                    </UiStack>
                  </UiStack>
                </UiStack>
                <UiStack align="center" gap="lg" class="w-full lg:w-[280px] lg:items-end text-center lg:text-right">
                  <UiStack gap="none">
                    <UiStack v-if="planPrice(agencyPlan, selectedCycle) !== null" direction="row" align="start" justify="center" gap="none" class="lg:justify-end gap-1 items-baseline">
                      <UiText v-if="activeCurrencyPrefix" as="span" tone="inherit" weight="semibold" class="text-xl text-classic-muted">{{ activeCurrencyPrefix }}</UiText>
                      <UiText as="span" tone="inherit" weight="bold" class="text-5xl tabular-nums leading-none text-classic-ink">
                        {{ formatAmount(planPrice(agencyPlan, selectedCycle)) }}
                      </UiText>
                      <UiText v-if="payerCurrency.suffix" as="span" tone="inherit" weight="semibold" class="text-2xl ml-2 text-classic-muted">{{ payerCurrency.suffix }}</UiText>
                    </UiStack>
                    <UiText size="xs" tone="inherit" weight="medium" class="mt-2 uppercase tracking-wider text-classic-muted">/ {{ cycleLabel(selectedCycle) }}</UiText>
                  </UiStack>
                  <UiStack gap="md" class="w-full">
                    <UiButton variant="primary" size="lg" full-width data-testid="classic-hosting-agency-add" @click="addToCart(agencyPlan)">
                      {{ cart.hasItem(agencyPlan.id, selectedCycle) ? $t('hosting.inCart') : $t('hosting.addToCart') }}
                    </UiButton>
                    <UiLink data-testid="classic-hosting-agency-plan" :to="localePath(`/hosting/${planSlug(agencyPlan)}`)" tone="inherit" :underline="false" class="flex items-center justify-center gap-2 text-sm font-medium text-classic-brand hover:text-classic-brand-hover group">
                      {{ $t('hosting.viewAllFeatures') }}
                      <ArrowRight :size="14" class="group-hover:translate-x-0.5 transition-transform" />
                    </UiLink>
                  </UiStack>
                </UiStack>
              </UiStack>
            </UiCard>
          </UiStack>

          <!-- Comparison table -->
          <UiStack v-if="comparable" id="compare-plans" gap="none" class="mt-12 overflow-hidden" data-testid="classic-hosting-compare">
            <UiStack align="center" gap="none" class="text-center mb-8">
              <UiHeading :level="UiHeadingLevel.H2" size="title" class="mb-3 text-2xl font-semibold text-classic-ink md:text-3xl">{{ $t('hosting.comparison.title') }}</UiHeading>
              <UiText tone="inherit" class="max-w-[75ch] mx-auto text-classic-muted md:text-lg">{{ $t('hosting.comparison.subtitle') }}</UiText>
            </UiStack>

            <UiCard padding="none" class="relative overflow-hidden">
              <UiStack gap="none" class="overflow-x-auto no-scrollbar scroll-smooth">
                <UiTable class="min-w-[320px] md:min-w-[1100px] border-0 rounded-none bg-transparent">
                  <UiTableHead>
                    <UiTableRow :hoverable="false" class="bg-classic-surface-2">
                      <UiTableTh class="!p-4 md:!p-6 !text-xs font-semibold sticky left-0 bg-classic-surface-2 text-classic-ink z-30 min-w-[140px] md:min-w-[260px]">
                        <UiStack direction="row" align="center" gap="xs">
                          <Server :size="16" class="text-classic-brand" />
                          {{ $t('hosting.comparison.feature') }}
                        </UiStack>
                      </UiTableTh>
                      <UiTableTh v-for="plan in comparedPlans" :key="plan.id" align="center" class="!p-4 md:!p-6 min-w-[120px]">
                        <UiText size="xs" tone="inherit" weight="semibold" class="md:text-sm mb-1 text-classic-ink leading-tight">
                          {{ formatPlanName(plan.name) }}
                        </UiText>
                        <UiText v-if="planPrice(plan, selectedCycle) !== null" size="xs" tone="inherit" weight="medium" class="text-xs tabular-nums text-classic-muted">
                          {{ formatMoney(planPrice(plan, selectedCycle), payerCurrency) }}
                        </UiText>
                      </UiTableTh>
                    </UiTableRow>
                  </UiTableHead>
                  <UiTableBody>
                    <!-- One row per specification line, aligned by the shared comparisonRows logic. -->
                    <UiTableRow v-for="row in comparisonRows" :key="row.label" group class="hover:bg-classic-surface-2" :data-testid="`classic-hosting-compare-row-${row.label}`">
                      <UiTableTd class="!p-4 md:!p-5 !text-xs md:!text-sm text-classic-muted font-medium sticky left-0 bg-classic-surface z-20 border-r border-classic-border">
                        {{ row.label }}
                      </UiTableTd>
                      <UiTableTd
                        v-for="(value, index) in row.values"
                        :key="`${row.label}-${comparedPlans[index]?.id ?? index}`"
                        align="center"
                        class="!p-4 md:!p-5 !text-xs md:!text-sm"
                        :class="value ? 'text-classic-ink' : 'text-classic-muted'"
                      >
                        <!-- A plan that does not carry this line reads as absent, not as zero. -->
                        {{ value || $t('hosting.comparison.notIncluded') }}
                      </UiTableTd>
                    </UiTableRow>
                    <!-- Bottom action row -->
                    <UiTableRow :hoverable="false">
                      <UiTableTd class="!p-4 md:!p-6 sticky left-0 bg-classic-surface z-20 border-r border-classic-border" />
                      <UiTableTd v-for="plan in comparedPlans" :key="plan.id" align="center" class="!p-4 md:!p-6">
                        <UiButton
                          :variant="cart.hasItem(plan.id, selectedCycle) ? 'secondary' : 'primary'"
                          size="sm"
                          class="px-4 md:px-6 whitespace-nowrap"
                          data-testid="classic-hosting-compare-add"
                          @click="addToCart(plan)"
                        >
                          {{ cart.hasItem(plan.id, selectedCycle) ? $t('hosting.inCart') : $t('hosting.select') }}
                        </UiButton>
                      </UiTableTd>
                    </UiTableRow>
                  </UiTableBody>
                </UiTable>
              </UiStack>
              <!-- Mobile scroll info -->
              <UiStack direction="row" align="center" justify="center" gap="xs" class="md:hidden py-3 bg-classic-surface-2 border-t border-classic-border">
                <ArrowRight :size="10" class="text-classic-muted" />
                <UiText as="span" size="xs" tone="inherit" weight="medium" class="uppercase tracking-wider text-classic-muted">{{ $t('common.scrollHorizontal') }}</UiText>
              </UiStack>
            </UiCard>
          </UiStack>
        </template>
      </UiContainer>
    </UiSection>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * ClassicHosting - the classic template's hosting page: hero, billing-cycle tabs, the free plan,
 * the plan grid, the agency plan and the comparison table, whose cells are each plan's own
 * catalogue feature lines.
 *
 * The catalogue is read through the catalogue store in the payer's own currency; prices are
 * formatted as sent, never converted. Cart actions go to the cart store.
 */

import { revealDelay } from '~/utils/reveal'
import { Server, CheckCircle, AlertCircle, ShoppingCart, Check, ArrowRight } from 'lucide-vue-next'
import { usePayerAsyncData } from '~/composables/usePayerAsyncData'
import { useCartStore } from '~/stores/cart'
import { useCurrencyStore } from '~/stores/currency'
import { useCatalogStore } from '~/stores/catalog'
import { HOSTING_GROUP_ID } from '~/constants/catalogGroup'
import { PRICING_CYCLES, PRICING_CYCLE_I18N_KEY } from '~/constants/pricingCycle'
import { formatMoney, formatMoneyDigits } from '~/utils/formatMoney'
import { cycleAmount } from '~/utils/productFamilies'
import { parseDescription } from '~/utils/catalogGroups'
import { isFreePlan } from '~/utils/isFreePlan'
import { UiHeadingLevel } from '~/utils/ui/enums'
import type { HostingPlan } from '~/types/hostingplan'
import type { SegmentedOption } from '~/types/segmentedoption'
import { PricingCycle } from '~/types/pricingcycle'
import type { PlanCard } from '~/types/plancard'
import type { ComparisonRow } from '~/types/comparisonrow'
import { isComparable } from '~/utils/comparisonRows'

/** Component props: the page's plan cards and the comparison rows aligned to them. */
const props = withDefaults(defineProps<{
  /** The plans in comparison-column order; each row value lines up with one. */
  plans?: PlanCard[]
  /** Specification rows built by `buildComparisonRows` in pages/hosting/index.vue. */
  comparisonRows?: ComparisonRow[]
}>(), { plans: () => [], comparisonRows: () => [] })

/** Fragment of a plan name that marks the agency plan; the catalogue has no flag for it. */
const AGENCY_NAME_MARKER = 'agency'

/**
 * Whether the plan is the one the operator marked as recommended. Only the first featured plan in
 * the list counts, so legacy data holding several still shows a single badge.
 *
 * @param plan - The plan.
 * @returns True for the featured plan.
 */
const isFeatured = (plan: HostingPlan): boolean => plan.id === catalogPlans.value.find(p => p.isFeatured === true)?.id

/** Translate function and the active locale. */
const { t, locale } = useI18n()

/** Prefixes a path with the active locale. */
const localePath = useLocalePath()

/** Cart store: adds plans and tells which are already in. */
const cart = useCartStore()

/** Currency store: which currency the payer is billed in. */
const currencyStore = useCurrencyStore()

/** Catalogue store: the hosting plans. */
const catalogStore = useCatalogStore()

onMounted(() => {
  currencyStore.init()
  currencyStore.load()
  cart.init()
})

/** The payer's currency to format and price with - never chosen by the page's language. */
const payerCurrency = computed(() => currencyStore.moneyCurrencyFor(currencyStore.payerCode))

/** Currency symbol shown before an amount, when the currency puts it there. */
const activeCurrencyPrefix = computed(() => payerCurrency.value.prefix ?? '')

/** Filter that picks the hosting group out of the catalogue. */
const hostingFilter = { gid: HOSTING_GROUP_ID }

/** The Nuxt instance, kept so a read that starts later still runs inside its context. */
const nuxtApp = useNuxtApp()

// The hosting plans are read in the payer's currency; the read runs again once `payerCode`
// resolves or changes, and `refresh` retries after a failure. The read runs inside the Nuxt
// context: during the server render the layout's own async setup interleaves with this page's,
// and a read that starts outside the context cannot reach the request (NUXT_E1001) and left the
// page on its error state.
/** Whether the plans are loading, and the retry action. */
const { pending, refresh } = await usePayerAsyncData('hosting:classic-plans', () => nuxtApp.runWithContext(() => catalogStore.loadProducts(hostingFilter)), {
  watch: [locale]
})

/** The store's failure sentence, shown in place of the plans. */
const error = computed(() => catalogStore.error)

/** All hosting plans. */
const catalogPlans = computed<HostingPlan[]>(() => catalogStore.productsFor(hostingFilter) as HostingPlan[])

/** Whether the plans share enough specification lines for the comparison table to add value. */
const comparable = computed(() => isComparable(props.comparisonRows))

/** The catalogue plans in the page's column order, so each column matches its row values. */
const comparedPlans = computed<HostingPlan[]>(() =>
  props.plans
    .map(card => catalogPlans.value.find(plan => plan.id === card.id))
    .filter((plan): plan is HostingPlan => plan !== undefined))

/**
 * Whether a plan is the agency one.
 *
 * @param plan - The plan.
 * @returns True when its name carries the agency marker.
 */
const isAgencyPlan = (plan: HostingPlan): boolean => plan.name.toLowerCase().includes(AGENCY_NAME_MARKER)

/** The free plan, shown in its own banner; free means a zero price, as in the catalogue store. */
const freePlan = computed(() => catalogPlans.value.find(p => isFreePlan(p.pricing)))

/** The agency plan, shown in its own banner. */
const agencyPlan = computed(() => catalogPlans.value.find(p => !isFreePlan(p.pricing) && isAgencyPlan(p)))

/** Every plan that is neither the free nor the agency one - the grid. */
const otherPlans = computed(() => catalogPlans.value.filter(p => !isFreePlan(p.pricing) && !isAgencyPlan(p)))

/** The billing cycle the prices are shown for. */
const selectedCycle = ref<PricingCycle>(PricingCycle.Monthly)

/**
 * Translated name of a billing cycle.
 *
 * @param cycle - The cycle.
 * @returns Its label.
 */
const cycleLabel = (cycle: PricingCycle): string =>
  t(`hosting.cycles.${PRICING_CYCLE_I18N_KEY[cycle]}`)

/**
 * The plan's price for one cycle, in the caller's own currency, as the API sent it.
 *
 * @param plan - The plan.
 * @param cycle - The billing cycle.
 * @returns The amount, or null when the plan carries no price for that cycle.
 */
const planPrice = (plan: HostingPlan, cycle: PricingCycle): number | null => cycleAmount(plan.pricing, cycle)

/** Cycles at least one plan is priced in - the segments of the cycle switch. */
const cycleOptions = computed<SegmentedOption<PricingCycle>[]>(() =>
  PRICING_CYCLES.filter(key => catalogPlans.value.some(p => planPrice(p, key) !== null))
    .map(key => ({ value: key, label: cycleLabel(key), testid: `classic-hosting-cycle-${key}` })))

/**
 * Formats just the grouped digits of an amount - no currency symbol - for markup that renders
 * the symbol separately at a different size. Decimal count comes from the payer's own
 * currency configuration, never a locale guess.
 *
 * @param amount - The amount, or null.
 * @returns The grouped digits, or '' for null.
 */
const formatAmount = (amount: number | null): string => {
  if (amount === null) {
    return ''
  }
  return formatMoneyDigits(amount, payerCurrency.value.decimals)
}

/**
 * The cycles a plan is priced in, each with its formatted price - the mini price list.
 *
 * @param plan - The plan.
 * @returns One entry per priced cycle.
 */
const planCycles = (plan: HostingPlan): Array<{ key: PricingCycle; price: string }> =>
  PRICING_CYCLES
    .filter(key => planPrice(plan, key) !== null)
    .map(key => ({ key, price: formatMoney(planPrice(plan, key), payerCurrency.value) }))

/**
 * Leading prose of a plan's description.
 *
 * `ProductDto` sends no `shortdescription`; the copy sits at the head of `description`.
 *
 * @param plan - The plan being rendered.
 * @returns The summary sentence, or `''` for a plan whose copy is bullets only.
 */
const planSummary = (plan: HostingPlan): string => parseDescription(plan.description || '').summary

/**
 * Feature lines of a plan's description.
 *
 * The copy is HTML, so `parseDescription` unwraps the tags, separates the leading prose and
 * strips the bullet markers.
 *
 * @param plan - The plan being rendered.
 * @returns The feature lines, markers stripped.
 */
const planFeatures = (plan: HostingPlan): string[] => parseDescription(plan.description || '').features

/**
 * Fixes the operator's misspelling of "business" in a plan name.
 *
 * @param name - The plan name.
 * @returns The corrected name.
 */
const formatPlanName = (name: string): string => {
  if (!name) {
    return ''
  }
  return name.replace(/Businnes/g, 'Business').replace(/businnes/g, 'business')
}

/**
 * The URL slug of a plan's detail page.
 *
 * @param plan - The plan.
 * @returns The lowercase, dash-separated name.
 */
const planSlug = (plan: HostingPlan): string =>
  formatPlanName(plan.name || '').toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, '')

/**
 * Adds a plan to the cart at the selected cycle's price.
 *
 * @param plan - The plan to add; nothing happens without a price or a payer currency.
 */
const addToCart = (plan: HostingPlan): void => {
  const amount = planPrice(plan, selectedCycle.value)
  if (amount === null || !currencyStore.payerCode) {
    return
  }
  cart.addItem({
    pid: plan.id,
    name: plan.name,
    billingcycle: selectedCycle.value,
    cycleLabel: cycleLabel(selectedCycle.value),
    amount,
    currency: currencyStore.payerCode
  })
}
</script>
