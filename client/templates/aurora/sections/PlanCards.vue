<template>
  <UiSection id="hosting" padding="lg" :contained="false" class="relative border-t border-line">
    <UiStack direction="row" wrap align="end" :justify="showHeader ? 'between' : 'end'" gap="xl">
      <UiStack v-if="showHeader" gap="none">
        <UiText tone="inherit" size="inherit" :class="[EYEBROW_CLASS, 'text-ac1']">{{ t('aurora.plans.eyebrow') }}</UiText>
        <UiHeading :level="2" size="bare" :class="[SECTION_TITLE_CLASS, 'mt-3']">
          {{ t('aurora.plans.title') }}
        </UiHeading>
      </UiStack>

      <!-- Only worth showing when there is a choice; many cycles scroll inside the control, not the page. -->
      <UiSegmentedControl
        v-if="cycles.length > 1"
        :model-value="activeCycle"
        :options="billingOptions"
        :label="t('aurora.plans.billingLabel')"
        testid="billing-toggle"
        @update:model-value="emit('update:cycle', $event)"
      />
    </UiStack>

    <UiText v-if="plans.length === 0" tone="inherit" class="mt-10 text-[15px] text-mut2" data-testid="plans-empty">
      {{ t('aurora.plans.empty') }}
    </UiText>

    <!--
      A plan that costs nothing is not competing with the others on price, so it does
      not belong in a row that compares prices. It gets the full width above them.
    -->
    <UiCard
      v-if="freePlan"
      tone="bare"
      padding="none"
      data-testid="plan-free"
      class="mt-8 border-line-brand bg-surf shadow-card p-[clamp(24px,4vw,40px)] transition-colors duration-200 hover:border-ac1"
    >
      <UiStack direction="row" wrap align="center" justify="between" gap="xl">
        <UiStack gap="none" class="min-w-[260px] flex-1">
          <UiBadge variant="primary" rounded size="sm" class="self-start uppercase tracking-[0.1em]">
            {{ t('aurora.plans.freeBadge') }}
          </UiBadge>
          <UiHeading :level="3" size="bare" class="mt-3.5 font-display text-[clamp(24px,3.4vw,32px)] font-bold -tracking-[0.02em] text-tx">
            {{ freePlan.name }}
          </UiHeading>
          <UiText v-if="freePlan.description" tone="inherit" class="mt-2 max-w-[75ch] text-[15px] leading-relaxed text-mut">
            {{ freePlan.description }}
          </UiText>

          <UiList v-if="freePlan.features.length" list-style="bare" class="mt-5 flex flex-wrap gap-x-6 gap-y-2.5">
            <UiListItem v-for="line in freePlan.features" :key="line" class="flex items-center gap-2">
              <Icon name="lucide:check" class="h-4 w-4 shrink-0 text-ok" />
              <UiText as="span" size="sm" tone="inherit" class="text-tx2">{{ line }}</UiText>
            </UiListItem>
          </UiList>
        </UiStack>

        <UiStack align="start" gap="md" class="min-w-[220px]">
          <UiStack gap="none">
            <UiText
              as="span"
              size="inherit"
              tone="inherit"
              weight="bold"
              class="font-display text-[clamp(40px,6vw,56px)] -tracking-[0.03em] text-tx"
            >
              {{ freePlan.priceMonthly }}
            </UiText>
            <UiText as="span" size="inherit" tone="inherit" class="mt-1 text-[13px] uppercase tracking-[0.1em] text-mut2">
              {{ t('aurora.plans.freeForever') }}
            </UiText>
          </UiStack>
          <UiButton
            variant="bare"
            full-width
            :to="freePlan.href"
            :data-testid="`plan-choose-${freePlan.id}`"
            :class="[BRAND_BUTTON, 'px-6 py-3.5 text-[15px]']"
          >
            {{ t('aurora.plans.cta') }}
          </UiButton>
        </UiStack>
      </UiStack>
    </UiCard>

    <UiBalancedGrid v-if="paidPlans.length" :count="paidPlans.length" class="mt-10" data-testid="plan-grid">
      <UiCard
        v-for="plan in paidPlans"
        :key="plan.id"
        tone="bare"
        padding="xl"
        :data-testid="`plan-card-${plan.id}`"
        class="relative flex h-full flex-col bg-surf shadow-card transition-[border-color,transform] duration-200 hover:-translate-y-0.5 hover:border-ac1"
        :class="plan.isFeatured ? 'border-ac1' : 'border-line'"
      >
        <!-- The operator's recommended plan: a badge on the card edge and a brand-coloured border. -->
        <UiBadge
          v-if="plan.isFeatured"
          variant="primary"
          rounded
          size="sm"
          class="absolute -top-3 left-6 uppercase tracking-[0.1em]"
          :data-testid="`plan-recommended-${plan.id}`"
        >
          {{ t('aurora.plans.recommended') }}
        </UiBadge>
        <UiText as="span" size="inherit" tone="inherit" weight="bold" class="break-words text-[17px] text-tx">{{ plan.name }}</UiText>
        <!-- Clamped so a long description never makes one card taller than its row. -->
        <UiText v-if="plan.description" size="sm" tone="inherit" class="mt-1.5 line-clamp-2 break-words text-mut2">{{ plan.description }}</UiText>

        <UiList v-if="plan.features.length" list-style="bare" class="mt-3.5 flex flex-col gap-2">
          <UiListItem v-for="line in visibleFeatures(plan)" :key="line" class="flex items-start gap-2">
            <Icon name="lucide:check" class="mt-0.5 h-3.5 w-3.5 shrink-0 text-ok" />
            <UiText as="span" size="inherit" tone="inherit" class="min-w-0 break-words text-[13px] text-tx2">{{ line }}</UiText>
          </UiListItem>
          <UiListItem v-if="hiddenFeatureCount(plan) > 0" :data-testid="`plan-more-${plan.id}`">
            <NuxtLink :to="plan.href" class="text-[13px] text-ac1 hover:underline">
              {{ t('aurora.plans.moreFeatures', { count: hiddenFeatureCount(plan) }) }}
            </NuxtLink>
          </UiListItem>
        </UiList>

        <!-- mt-auto pins price and button to the card bottom so they align across cards of unequal height. -->
        <UiStack gap="none" class="mt-auto pt-[22px]">
          <UiStack direction="row" align="end" gap="xs">
            <UiText as="span" size="inherit" tone="inherit" weight="bold" class="text-[38px] -tracking-[0.02em] text-tx">
              {{ cardCyclePrice(plan, activeCycle) }}
            </UiText>
            <UiText as="span" size="sm" tone="inherit" class="pb-2 text-mut2">
              {{ perMonthLabel }}
            </UiText>
          </UiStack>

          <UiButton
            variant="bare"
            full-width
            :to="plan.href"
            :data-testid="`plan-choose-${plan.id}`"
            :class="[LINE_BUTTON, 'mt-[26px] py-3.5 text-[15px] font-bold']"
          >
            {{ t('aurora.plans.cta') }}
          </UiButton>
        </UiStack>
      </UiCard>
    </UiBalancedGrid>

    <!--
      A fallback, not a companion to the per-card lists above. An operator whose
      descriptions are plain summaries gets no feature lines at all, and this keeps
      the section from looking bare; where the plans do carry their own features,
      repeating a single "included in every plan" list beside them would contradict
      the cards the moment two plans differed.
    -->
    <UiCard
      v-if="showIncluded"
      tone="bare"
      data-testid="plans-included"
      class="mt-8 border-line bg-surf shadow-card"
    >
      <UiText as="span" size="inherit" tone="inherit" :class="[EYEBROW_CLASS, 'tracking-[0.1em] text-ac1']">
        {{ t('aurora.plans.includedTitle') }}
      </UiText>
      <UiGrid cols="auto" gap="md" class="mt-4">
        <UiStack v-for="line in included" :key="line" direction="row" align="center" gap="xs">
          <Icon name="lucide:check" class="h-4 w-4 shrink-0 text-ok" />
          <UiText as="span" size="inherit" tone="inherit" class="text-[15px] text-tx2">{{ line }}</UiText>
        </UiStack>
      </UiGrid>
    </UiCard>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * aurora hosting plan cards.
 *
 * Presentation only: plans and their formatted prices arrive as props, and the
 * billing-period toggle is reported upward rather than owned here.
 */
import { PLAN_FEATURE_LIMIT } from '~/constants/planFeatureLimit'
import { PRICING_CYCLE_I18N_KEY } from '~/constants/pricingCycle'
import { BRAND_BUTTON, LINE_BUTTON } from '~/templates/aurora/buttons'
import { EYEBROW_CLASS, SECTION_TITLE_CLASS } from '~/templates/aurora/typography'
import type { PlanCard } from '~/types/plancard'
import { PricingCycle } from '~/types/pricingcycle'
import type { SegmentedOption } from '~/types/segmentedoption'
import { cardCyclePrice, cycleSegments, effectiveCycle, planCardCycles } from '~/utils/planCycles'

/** Props with their defaults applied. */
const props = withDefaults(defineProps<{
  /** The plans to show. */
  plans?: PlanCard[]
  /** The billing cycle the prices are shown for; falls back to the shortest one the plans offer. */
  cycle?: PricingCycle
  /** Off on the hosting page, whose own hero already states the same thing. */
  showHeader?: boolean
  /** Most feature lines one card lists; the rest collapse into a "+N more" link. */
  maxFeatures?: number
}>(), { plans: () => [], cycle: PricingCycle.Monthly, showHeader: true, maxFeatures: PLAN_FEATURE_LIMIT })

/** Events this component raises. */
const emit = defineEmits<{
  /** The visitor switched the billing cycle. */
  'update:cycle': [value: PricingCycle]
}>()

/** Translator for the active locale. */
const { t } = useI18n()

// The free plan is pulled out of the row and shown on its own above it; the rest keep
// the grid. `find` rather than a filter: two free plans would be a catalogue mistake,
// and featuring the first is better than stacking banners.
/** The plan that costs nothing, if the catalogue has one. */
const freePlan = computed(() => props.plans.find(plan => plan.isFree))

/** Every other plan, compared on price in the grid. */
const paidPlans = computed(() => props.plans.filter(plan => !plan.isFree))

/**
 * The feature lines a card lists.
 *
 * @param plan - The plan.
 * @returns At most `maxFeatures` lines, so every card in a row stays the same height.
 */
const visibleFeatures = (plan: PlanCard): string[] => plan.features.slice(0, props.maxFeatures)

/**
 * How many feature lines a card leaves out.
 *
 * @param plan - The plan.
 * @returns The number of lines behind the "+N more" link; zero when all fit.
 */
const hiddenFeatureCount = (plan: PlanCard): number => Math.max(0, plan.features.length - props.maxFeatures)

/** Whether the shared "included in every plan" list stands in for per-plan feature lines. */
const showIncluded = computed(() => props.plans.length > 0 && !props.plans.some(plan => plan.features.length))

/**
 * The visible name of a billing cycle.
 *
 * @param cycle - The cycle.
 * @returns Its translated label.
 */
const cycleLabel = (cycle: PricingCycle): string => t(`hosting.cycles.${PRICING_CYCLE_I18N_KEY[cycle]}`)

/** Every cycle at least one plan is priced on, shortest first. */
const cycles = computed(() => planCardCycles(props.plans))

/** The cycle in effect: the visitor's pick, or the shortest one on offer when no plan has it. */
const activeCycle = computed(() => effectiveCycle(cycles.value, props.cycle))

/** The toggle's segments, one per cycle on offer. */
const billingOptions = computed<SegmentedOption<PricingCycle>[]>(() => cycleSegments(cycles.value, cycleLabel, 'billing'))

/** The unit shown beside a price: per month, plus the cycle it is billed on when that is not monthly. */
const perMonthLabel = computed(() => activeCycle.value === PricingCycle.Monthly
  ? t('aurora.plans.perMonth')
  : t('aurora.plans.perMonthBilled', { cycle: cycleLabel(activeCycle.value) }))

/** What every plan includes. */
const included = computed(() => [
  t('aurora.plans.included1'),
  t('aurora.plans.included2'),
  t('aurora.plans.included3'),
  t('aurora.plans.included4'),
])
</script>
