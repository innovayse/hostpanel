<template>
  <section id="hosting" class="relative border-t border-line px-[clamp(20px,5vw,48px)] py-[clamp(56px,8vw,84px)]">
    <div class="flex flex-wrap items-end justify-between gap-10" :class="showHeader ? '' : 'justify-end'">
      <div v-if="showHeader">
        <div class="text-[13px] uppercase tracking-[0.12em] text-ac1">{{ t('aurora.plans.eyebrow') }}</div>
        <h2 class="mt-3 font-display text-[clamp(27px,4vw,40px)] font-bold -tracking-[0.02em] text-tx">
          {{ t('aurora.plans.title') }}
        </h2>
      </div>

      <div class="flex items-center gap-3 rounded-full border border-line2 p-1.5">
        <button
          type="button"
          class="rounded-full px-[18px] py-2.5 text-sm font-semibold"
          :class="yearly ? 'text-mut' : 'bg-brand text-[#08090F]'"
          @click="emit('update:yearly', false)"
        >{{ t('aurora.plans.monthly') }}</button>
        <button
          type="button"
          class="rounded-full px-[18px] py-2.5 text-sm font-semibold"
          :class="yearly ? 'bg-brand text-[#08090F]' : 'text-mut'"
          @click="emit('update:yearly', true)"
        >{{ t('aurora.plans.yearly') }}</button>
      </div>
    </div>

    <p v-if="plans.length === 0" class="mt-10 text-[15px] text-mut2">{{ t('aurora.plans.empty') }}</p>

    <!--
      A plan that costs nothing is not competing with the others on price, so it does
      not belong in a row that compares prices. It gets the full width above them.
    -->
    <div
      v-if="freePlan"
      class="mt-10 flex flex-wrap items-center justify-between gap-x-12 gap-y-8 rounded-[22px] border border-ac1/25 bg-surf p-[clamp(24px,4vw,40px)]"
    >
      <div class="min-w-[260px] flex-1">
        <div class="inline-flex items-center gap-2 rounded-full border border-ac1/30 px-3 py-1 text-[12px] uppercase tracking-[0.1em] text-ac1">
          {{ t('aurora.plans.freeBadge') }}
        </div>
        <div class="mt-3.5 font-display text-[clamp(24px,3.4vw,32px)] font-bold -tracking-[0.02em] text-tx">
          {{ freePlan.name }}
        </div>
        <p v-if="freePlan.description" class="mt-2 max-w-[560px] text-[15px] leading-relaxed text-mut">
          {{ freePlan.description }}
        </p>

        <ul v-if="freePlan.features.length" class="mt-5 flex flex-wrap gap-x-6 gap-y-2.5">
          <li v-for="line in freePlan.features" :key="line" class="flex items-center gap-2 text-sm text-tx2">
            <Icon name="lucide:check" class="h-4 w-4 shrink-0 text-ok" />{{ line }}
          </li>
        </ul>
      </div>

      <div class="flex min-w-[220px] flex-col items-start gap-4">
        <div>
          <div class="font-display text-[clamp(40px,6vw,56px)] font-bold -tracking-[0.03em] text-tx">
            {{ freePlan.priceMonthly }}
          </div>
          <div class="mt-1 text-[13px] uppercase tracking-[0.1em] text-mut2">
            {{ t('aurora.plans.freeForever') }}
          </div>
        </div>
        <NuxtLink
          :to="freePlan.href"
          class="w-full rounded-xl bg-brand px-6 py-3.5 text-center text-[15px] font-bold text-[#08090F] hover:brightness-110"
        >{{ t('aurora.plans.cta') }}</NuxtLink>
      </div>
    </div>

    <div v-if="paidPlans.length" class="mt-10 grid gap-5 [grid-template-columns:repeat(auto-fit,minmax(min(100%,260px),1fr))]">
      <div
        v-for="plan in paidPlans"
        :key="plan.id"
        class="rounded-[20px] border border-line bg-surf p-7"
      >
        <div class="text-[17px] font-bold text-tx">{{ plan.name }}</div>
        <div v-if="plan.description" class="mt-1.5 text-sm text-mut2">{{ plan.description }}</div>

        <ul v-if="plan.features.length" class="mt-3.5 flex flex-col gap-2">
          <li v-for="line in plan.features" :key="line" class="flex items-start gap-2 text-[13px] text-tx2">
            <Icon name="lucide:check" class="mt-0.5 h-3.5 w-3.5 shrink-0 text-ok" />
            <span class="min-w-0">{{ line }}</span>
          </li>
        </ul>

        <div class="mt-[22px] flex items-end gap-2">
          <span class="text-[38px] font-bold -tracking-[0.02em] text-tx">
            {{ yearly ? plan.priceAnnual : plan.priceMonthly }}
          </span>
          <span class="pb-2 text-sm text-mut2">
            {{ yearly ? t('aurora.plans.perMonthAnnual') : t('aurora.plans.perMonth') }}
          </span>
        </div>

        <NuxtLink
          :to="plan.href"
          class="mt-[26px] block rounded-xl border border-line2 py-3.5 text-center text-[15px] font-bold text-tx hover:border-ac1 hover:text-ac1"
        >{{ t('aurora.plans.cta') }}</NuxtLink>
      </div>
    </div>

    <!--
      A fallback, not a companion to the per-card lists above. An operator whose
      descriptions are plain summaries gets no feature lines at all, and this keeps
      the section from looking bare; where the plans do carry their own features,
      repeating a single "included in every plan" list beside them would contradict
      the cards the moment two plans differed.
    -->
    <div v-if="plans.length && !plans.some(p => p.features.length)" class="mt-9 rounded-[18px] border border-line bg-surf p-6">
      <div class="text-[13px] uppercase tracking-[0.1em] text-ac2">{{ t('aurora.plans.includedTitle') }}</div>
      <div class="mt-4 grid gap-3 [grid-template-columns:repeat(auto-fit,minmax(min(100%,200px),1fr))]">
        <div v-for="line in included" :key="line" class="flex items-center gap-2.5 text-[15px] text-tx2">
          <Icon name="lucide:check" class="h-4 w-4 text-ok" />{{ line }}
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
/**
 * aurora hosting plan cards.
 *
 * Presentation only — plans and their formatted prices arrive as props, and the
 * billing-period toggle is reported upward rather than owned here.
 */
import type { PlanCard } from '~/templates/aurora/types'

const props = withDefaults(defineProps<{
  plans?: PlanCard[]
  yearly?: boolean
  /** Off on the hosting page, whose own hero already states the same thing. */
  showHeader?: boolean
}>(), { plans: () => [], yearly: false, showHeader: true })

const emit = defineEmits<{ 'update:yearly': [value: boolean] }>()

const { t } = useI18n()

// The free plan is pulled out of the row and shown on its own above it; the rest keep
// the grid. `find` rather than a filter: two free plans would be a catalogue mistake,
// and featuring the first is better than stacking banners.
const freePlan = computed(() => props.plans.find(plan => plan.isFree))
const paidPlans = computed(() => props.plans.filter(plan => !plan.isFree))

const included = computed(() => [
  t('aurora.plans.included1'),
  t('aurora.plans.included2'),
  t('aurora.plans.included3'),
  t('aurora.plans.included4'),
])
</script>
