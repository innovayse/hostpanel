<template>
  <section v-if="USE_CASES.length" class="border-b border-nova-border bg-nova-surface py-16 lg:py-24">
    <div class="mx-auto max-w-[1240px] px-4 sm:px-6 lg:px-8">
      <p class="text-[13px] font-semibold uppercase tracking-[0.1em] text-nova-brand">
        {{ t('nova.useCases.eyebrow') }}
      </p>
      <h2 class="mt-3 text-[clamp(1.6rem,3.4vw,2.4rem)] font-bold tracking-tight text-nova-ink">
        {{ t('nova.useCases.title') }}
      </h2>

      <ul class="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
        <li
          v-for="useCase in USE_CASES"
          :key="useCase.key"
          class="flex h-full gap-4 rounded-2xl border border-nova-border bg-nova-bg p-6 transition-colors hover:border-nova-brand"
        >
          <span class="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-nova-surface-2 text-nova-brand">
            <Icon :name="useCase.icon" class="h-[22px] w-[22px]" aria-hidden="true" />
          </span>
          <div class="min-w-0">
            <h3 class="text-[17px] font-bold text-nova-ink">{{ t(useCase.titleKey) }}</h3>
            <p class="mt-2 text-[15px] leading-relaxed text-nova-muted">{{ t(useCase.bodyKey) }}</p>
          </div>
        </li>
      </ul>
    </div>
  </section>
</template>

<script setup lang="ts">
/**
 * nova use-cases grid. Adding an audience is one entry in `content.ts` and its
 * two i18n keys; this file does not change.
 */
import { USE_CASES } from '~/templates/nova/content'

const { t } = useI18n()
</script>
