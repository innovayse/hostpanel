<template>
  <article
    class="relative flex h-full flex-col rounded-2xl border bg-nova-surface p-6 transition-[transform,border-color] motion-safe:hover:-translate-y-1"
    :class="plan.popular ? 'border-nova-accent shadow-[0_24px_50px_-30px_rgba(0,0,0,0.5)]' : 'border-nova-border'"
  >
    <p
      v-if="plan.popular"
      class="absolute -top-3 left-6 rounded-full bg-nova-accent px-3 py-1 text-[12px] font-extrabold uppercase tracking-[0.06em] text-[#12212a]"
    >{{ t('nova.plans.popular') }}</p>

    <h3 class="text-lg font-bold text-nova-ink">{{ plan.name }}</h3>
    <p v-if="plan.description" class="mt-1.5 text-sm leading-relaxed text-nova-muted">
      {{ plan.description }}
    </p>

    <p class="mt-6 flex flex-wrap items-end gap-x-2">
      <span class="text-[2.4rem] font-extrabold leading-none tracking-tight text-nova-ink">
        {{ yearly ? plan.priceAnnual : plan.priceMonthly }}
      </span>
      <span class="pb-1 text-sm text-nova-muted">
        {{ yearly ? t('nova.plans.perMonthAnnual') : t('nova.plans.perMonth') }}
      </span>
    </p>

    <!--
      Shown on the yearly view only, and only when the two prices actually make
      the saving true. There is no renewal price anywhere in the API, so none is
      printed: a renewal figure invented here is the one number a hosting page
      must never get wrong.
    -->
    <p
      v-if="yearly && plan.discountPercent"
      class="mt-2.5 inline-flex w-fit rounded-lg bg-nova-surface-2 px-2.5 py-1 text-[13px] font-semibold text-nova-success"
    >{{ t('nova.plans.save', { percent: plan.discountPercent }) }}</p>

    <ul v-if="plan.features.length" class="mt-6 flex flex-col gap-2.5">
      <li v-for="line in plan.features" :key="line" class="flex items-start gap-2.5 text-[14px] text-nova-ink">
        <Icon name="lucide:check" class="mt-0.5 h-4 w-4 shrink-0 text-nova-success" aria-hidden="true" />
        <span class="min-w-0">{{ line }}</span>
      </li>
    </ul>

    <NuxtLink
      :to="plan.href"
      class="mt-7 flex min-h-[48px] items-center justify-center rounded-xl px-5 text-[15px] font-bold transition-[filter,border-color]"
      :class="plan.popular
        ? 'bg-nova-accent text-[#12212a] hover:brightness-95'
        : 'border border-nova-border text-nova-ink hover:border-nova-brand hover:text-nova-brand'"
    >
      {{ t('nova.plans.cta') }}
      <span class="sr-only"> — {{ plan.name }}</span>
    </NuxtLink>
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
import type { NovaPlan } from '~/templates/nova/types'

defineProps<{
  plan: NovaPlan
  /** Which price the card is showing. */
  yearly: boolean
}>()

const { t } = useI18n()
</script>
