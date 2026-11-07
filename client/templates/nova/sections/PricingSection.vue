<template>
  <UiSection id="plans" padding="lg" class="border-b border-nova-border">
    <UiStack direction="row" wrap align="end" justify="between" gap="lg">
      <NovaSectionHeader
        v-if="showHeader"
        :eyebrow="t('nova.plans.eyebrow')"
        :title="t('nova.plans.title')"
        :lead="t('nova.plans.lead')"
        class="min-w-0"
      />

      <!-- Only worth showing when there is a choice; many cycles scroll inside the control, not the page. -->
      <UiSegmentedControl
        v-if="cycles.length > 1"
        :model-value="activeCycle"
        :options="options"
        :label="t('nova.plans.billingLabel')"
        testid="nova-billing-toggle"
        @update:model-value="emit('update:cycle', $event)"
      />
    </UiStack>

    <!--
      The catalogue not answering is not the visitor's fault and not their
      problem to debug, so the empty state says what it is and stops.
    -->
    <UiCard
      v-if="novaPlans.length === 0"
      padding="xl"
      class="mt-10 !border-nova-border !bg-nova-surface"
      data-testid="nova-plans-empty"
    >
      <UiText size="inherit" tone="inherit" weight="semibold" class="text-[17px] text-nova-ink">
        {{ t('nova.plans.empty') }}
      </UiText>
      <UiText size="inherit" tone="inherit" class="mt-2 text-[15px] text-nova-muted">
        {{ t('nova.plans.emptyHint') }}
      </UiText>
    </UiCard>

    <UiBalancedGrid v-else :count="novaPlans.length" class="mt-10" data-testid="nova-plan-grid">
      <NovaPricingCard
        v-for="plan in novaPlans"
        :key="plan.id"
        :plan="plan"
        :cycle="activeCycle"
      />
    </UiBalancedGrid>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * nova pricing section.
 *
 * Plans and their formatted prices arrive as props, templates never fetch,
 * and the billing toggle is reported upward rather than owned here, so the page
 * can keep the hosting page and the homepage on the same state.
 */
import NovaPricingCard from '~/templates/nova/sections/PricingCard.vue'
import NovaSectionHeader from '~/templates/nova/sections/NovaSectionHeader.vue'
import { PRICING_CYCLE_I18N_KEY } from '~/constants/pricingCycle'
import { toNovaPlans } from '~/templates/nova/pricing'
import type { PlanCard } from '~/types/plancard'
import { PricingCycle } from '~/types/pricingcycle'
import type { SegmentedOption } from '~/types/segmentedoption'
import { cycleSegments, effectiveCycle, planCardCycles } from '~/utils/planCycles'

/** Props of PricingSection. */
const props = withDefaults(defineProps<{
  /** Plans as the catalogue store formats them. */
  plans?: PlanCard[]
  /** The billing cycle the prices are shown for; falls back to the shortest one the plans offer. */
  cycle?: PricingCycle
  /** Off on the hosting page, whose own hero already says the same thing. */
  showHeader?: boolean
}>(), { plans: () => [], cycle: PricingCycle.Monthly, showHeader: true })

/** Emits the section raises. */
const emit = defineEmits<{
  /** The reader chose another billing cycle. */
  'update:cycle': [value: PricingCycle]
}>()

/** Translator for the copy. */
const { t } = useI18n()

/** The plans in the shape the cards render. */
const novaPlans = computed(() => toNovaPlans(props.plans))

/** Every cycle at least one plan is priced on, shortest first. */
const cycles = computed(() => planCardCycles(props.plans))

/** The cycle in effect: the reader's pick, or the shortest one on offer when no plan has it. */
const activeCycle = computed(() => effectiveCycle(cycles.value, props.cycle))

/** The toggle's segments, one per cycle on offer. */
const options = computed<SegmentedOption<PricingCycle>[]>(() =>
  cycleSegments(cycles.value, cycle => t(`hosting.cycles.${PRICING_CYCLE_I18N_KEY[cycle]}`), 'nova-billing'))
</script>
