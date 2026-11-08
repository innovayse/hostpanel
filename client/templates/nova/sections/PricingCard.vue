<template>
  <article class="h-full" :data-testid="`nova-plan-${plan.id}`">
    <UiCard
      padding="lg"
      class="relative flex h-full flex-col transition-[transform,border-color] motion-safe:hover:-translate-y-1 !bg-nova-surface"
      :class="plan.popular ? '!border-nova-accent shadow-nova-card' : '!border-nova-border'"
    >
      <UiText
        v-if="plan.popular"
        as="span"
        size="inherit"
        tone="inherit"
        class="absolute -top-3 left-6 rounded-full bg-nova-accent px-3 py-1 text-[12px] font-extrabold uppercase tracking-[0.06em] text-nova-on-accent"
      >
        {{ plan.isFeatured ? t('nova.plans.recommended') : t('nova.plans.popular') }}
      </UiText>

      <UiText
        v-else-if="plan.isFree"
        as="span"
        size="inherit"
        tone="inherit"
        class="absolute -top-3 left-6 rounded-full border border-nova-brand bg-nova-bg px-3 py-1 text-[12px] font-extrabold uppercase tracking-[0.06em] text-nova-brand"
      >
        {{ t('nova.plans.freeBadge') }}
      </UiText>

      <UiHeading :level="3" size="bare" class="text-lg font-bold text-nova-ink">{{ plan.name }}</UiHeading>
      <UiText
        v-if="plan.description"
        size="inherit"
        tone="inherit"
        class="mt-1.5 text-sm leading-relaxed text-nova-muted"
      >
        {{ plan.description }}
      </UiText>

      <UiStack direction="row" wrap align="end" gap="none" class="mt-6 gap-x-2">
        <span class="text-[2.4rem] font-extrabold leading-none tracking-tight text-nova-ink">
          {{ plan.isFree ? plan.priceMonthly : cardCyclePrice(plan, cycle) }}
        </span>
        <!--
          A free plan costs the same whichever billing period is selected, so
          saying "per month, billed yearly" over a zero would be describing an
          invoice that never arrives.
        -->
        <span class="pb-1 text-sm text-nova-muted">
          {{ plan.isFree
            ? t('nova.plans.freeForever')
            : perMonthLabel }}
        </span>
      </UiStack>

      <!--
        Shown on the annual cycle only, and only when the two prices actually make
        the saving true. There is no renewal price anywhere in the API, so none is
        printed: a renewal figure invented here is the one number a hosting page
        must never get wrong.
      -->
      <UiText
        v-if="cycle === PricingCycle.Annual && plan.discountPercent"
        size="inherit"
        tone="inherit"
        weight="semibold"
        class="mt-2.5 inline-flex w-fit rounded-lg bg-nova-surface-2 px-2.5 py-1 text-[13px] text-nova-success"
      >
        {{ t('nova.plans.save', { percent: plan.discountPercent }) }}
      </UiText>

      <UiList v-if="plan.features.length" list-style="bare" class="mt-6 flex flex-col gap-2.5">
        <li v-for="line in plan.features" :key="line" class="flex items-start gap-2.5 text-[14px] text-nova-ink">
          <Icon name="lucide:check" class="mt-0.5 h-4 w-4 shrink-0 text-nova-success" aria-hidden="true" />
          <span class="min-w-0">{{ line }}</span>
        </li>
      </UiList>

      <NovaCta
        :to="plan.href"
        :tone="plan.popular ? 'accent' : 'outline'"
        size="card"
        full-width
        class="mt-7 font-bold"
        :data-testid="`nova-plan-cta-${plan.id}`"
      >
        {{ t('nova.plans.cta') }}
        <span class="sr-only"> — {{ plan.name }}</span>
      </NovaCta>
    </UiCard>
  </article>
</template>

<script setup lang="ts">
/**
 * One nova pricing card.
 *
 * Presentation only: which plan is popular and what the yearly saving is were
 * both decided in `pricing.ts` before the plan reached this component, so the
 * card never has to reason about the other cards in the row.
 *
 * The call to action repeats the plan name to screen readers, because a column
 * of buttons that all read "Choose plan" is unusable out of visual context.
 */
import { PRICING_CYCLE_I18N_KEY } from '~/constants/pricingCycle'
import NovaCta from '~/templates/nova/sections/NovaCta.vue'
import type { NovaPlan } from '~/templates/nova/types/novaplan'
import { PricingCycle } from '~/types/pricingcycle'
import { cardCyclePrice } from '~/utils/planCycles'

/** Props of PricingCard. */
const props = defineProps<{
  /** The plan to show, already carrying its badge and discount. */
  plan: NovaPlan
  /** The billing cycle whose per-month price the card is showing. */
  cycle: PricingCycle
}>()

/** Translator for the copy. */
const { t } = useI18n()

/** The unit shown beside a price: per month, plus the cycle it is billed on when that is not monthly. */
const perMonthLabel = computed(() => props.cycle === PricingCycle.Monthly
  ? t('nova.plans.perMonth')
  : t('nova.plans.perMonthBilled', { cycle: t(`hosting.cycles.${PRICING_CYCLE_I18N_KEY[props.cycle]}`) }))
</script>
