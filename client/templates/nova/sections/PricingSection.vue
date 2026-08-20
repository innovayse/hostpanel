<template>
  <section id="plans" class="border-b border-nova-border py-16 lg:py-24">
    <div class="mx-auto max-w-[1240px] px-4 sm:px-6 lg:px-8">
      <div class="flex flex-wrap items-end justify-between gap-6">
        <div v-if="showHeader" class="min-w-0">
          <p class="text-[13px] font-semibold uppercase tracking-[0.1em] text-nova-brand">
            {{ t('nova.plans.eyebrow') }}
          </p>
          <h2 class="mt-3 text-[clamp(1.6rem,3.4vw,2.4rem)] font-bold tracking-tight text-nova-ink">
            {{ t('nova.plans.title') }}
          </h2>
          <p class="mt-3 max-w-[620px] text-[16px] leading-[1.7] text-nova-muted">
            {{ t('nova.plans.lead') }}
          </p>
        </div>

        <div
          role="group"
          :aria-label="t('nova.plans.billingLabel')"
          class="flex items-center gap-1 rounded-full border border-nova-border p-1"
        >
          <button
            v-for="option in options"
            :key="option.key"
            type="button"
            class="min-h-[44px] rounded-full px-5 text-sm font-semibold transition-colors"
            :class="option.selected ? 'bg-nova-accent text-[#12212a]' : 'text-nova-muted hover:text-nova-ink'"
            :aria-pressed="option.selected"
            @click="emit('update:yearly', option.value)"
          >{{ t(option.labelKey) }}</button>
        </div>
      </div>

      <!--
        The catalogue not answering is not the visitor's fault and not their
        problem to debug, so the empty state says what it is and stops.
      -->
      <div v-if="novaPlans.length === 0" class="mt-10 rounded-2xl border border-nova-border bg-nova-surface p-8">
        <p class="text-[17px] font-semibold text-nova-ink">{{ t('nova.plans.empty') }}</p>
        <p class="mt-2 text-[15px] text-nova-muted">{{ t('nova.plans.emptyHint') }}</p>
      </div>

      <div
        v-else
        class="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-3"
      >
        <NovaPricingCard
          v-for="plan in novaPlans"
          :key="plan.id"
          :plan="plan"
          :yearly="yearly"
        />
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
/**
 * nova pricing section.
 *
 * Plans and their formatted prices arrive as props — templates never fetch —
 * and the billing toggle is reported upward rather than owned here, so the page
 * can keep the hosting page and the homepage on the same state.
 */
import NovaPricingCard from '~/templates/nova/sections/PricingCard.vue'
import { toNovaPlans } from '~/templates/nova/pricing'
import type { PlanCard } from '~/templates/aurora/types'

const props = withDefaults(defineProps<{
  plans?: PlanCard[]
  yearly?: boolean
  /** Off on the hosting page, whose own hero already says the same thing. */
  showHeader?: boolean
}>(), { plans: () => [], yearly: false, showHeader: true })

const emit = defineEmits<{ 'update:yearly': [value: boolean] }>()

const { t } = useI18n()

const novaPlans = computed(() => toNovaPlans(props.plans))

const options = computed(() => [
  { key: 'monthly', labelKey: 'nova.plans.monthly', value: false, selected: !props.yearly },
  { key: 'yearly', labelKey: 'nova.plans.yearly', value: true, selected: props.yearly },
])
</script>
