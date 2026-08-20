<template>
  <section class="border-b border-nova-border py-16 lg:py-24">
    <div class="mx-auto max-w-[1240px] px-4 sm:px-6 lg:px-8">
      <div class="max-w-[640px]">
        <p class="flex flex-wrap items-center gap-3">
          <span class="text-[13px] font-semibold uppercase tracking-[0.1em] text-nova-brand">
            {{ t('nova.migration.eyebrow') }}
          </span>
          <!--
            Whether a migration is free is a commercial decision, and nothing in
            the backend records it. The badge waits to be switched on from
            Admin → Settings rather than being printed as a promise nobody made.
          -->
          <span
            v-if="freeMigration"
            class="rounded-full bg-nova-accent px-3 py-1 text-[12px] font-extrabold uppercase tracking-[0.06em] text-[#12212a]"
          >{{ t('nova.migration.free') }}</span>
        </p>

        <h2 class="mt-3 text-[clamp(1.6rem,3.4vw,2.4rem)] font-bold tracking-tight text-nova-ink">
          {{ t('nova.migration.title') }}
        </h2>
        <p class="mt-3 text-[16px] leading-[1.7] text-nova-muted">{{ t('nova.migration.lead') }}</p>
      </div>

      <ol class="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
        <li
          v-for="(step, index) in MIGRATION_STEPS"
          :key="step.key"
          class="relative rounded-2xl border border-nova-border bg-nova-surface p-6"
        >
          <span
            class="grid h-9 w-9 place-items-center rounded-full bg-nova-surface-2 text-[15px] font-extrabold text-nova-brand"
            aria-hidden="true"
          >{{ index + 1 }}</span>
          <h3 class="mt-4 text-[16px] font-bold text-nova-ink">{{ t(step.titleKey) }}</h3>
          <p class="mt-2 text-[15px] leading-relaxed text-nova-muted">{{ t(step.bodyKey) }}</p>
        </li>
      </ol>

      <NuxtLink
        :to="localePath('/contact')"
        class="mt-9 inline-flex min-h-[48px] items-center rounded-xl border border-nova-border px-6 text-[15px] font-semibold text-nova-ink transition-colors hover:border-nova-brand hover:text-nova-brand"
      >{{ t('nova.migration.cta') }}</NuxtLink>
    </div>
  </section>
</template>

<script setup lang="ts">
/**
 * nova migration walkthrough.
 *
 * An ordered list, because the steps happen in that order and the numbering is
 * information rather than decoration — the visible digits are hidden from
 * assistive technology, which gets the ordering from the list itself.
 */
import { MIGRATION_FREE_SETTING, MIGRATION_STEPS } from '~/templates/nova/content'

const { t } = useI18n()
const localePath = useLocalePath()
const { get } = usePortalSettings()

const freeMigration = computed(() => get(MIGRATION_FREE_SETTING[0], MIGRATION_FREE_SETTING[1]) === 'true')
</script>
