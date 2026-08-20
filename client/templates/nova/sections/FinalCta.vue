<template>
  <section class="py-16 lg:py-24">
    <div class="mx-auto max-w-[1240px] px-4 sm:px-6 lg:px-8">
      <div
        class="rounded-3xl border border-nova-border bg-[linear-gradient(135deg,var(--n-surface-2),var(--n-surface))] px-6 py-14 text-center sm:px-12"
      >
        <h2 class="mx-auto max-w-[620px] text-[clamp(1.7rem,4vw,2.7rem)] font-extrabold leading-tight tracking-tight text-nova-ink">
          {{ t('nova.finalCta.title') }}
        </h2>
        <p class="mx-auto mt-4 max-w-[520px] text-[17px] leading-[1.7] text-nova-muted">
          {{ t('nova.finalCta.lead') }}
        </p>

        <div class="mt-9 flex flex-wrap justify-center gap-3">
          <NuxtLink
            :to="localePath('/hosting')"
            class="inline-flex min-h-[52px] items-center rounded-xl bg-nova-accent px-8 text-base font-bold text-[#12212a] transition-[filter,transform] hover:brightness-95 motion-safe:hover:-translate-y-0.5"
          >{{ t('nova.finalCta.primary') }}</NuxtLink>
          <NuxtLink
            :to="localePath('/hosting#plans')"
            class="inline-flex min-h-[52px] items-center rounded-xl border border-nova-border bg-nova-bg px-8 text-base font-semibold text-nova-ink transition-colors hover:border-nova-brand hover:text-nova-brand"
          >{{ t('nova.finalCta.secondary') }}</NuxtLink>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
/**
 * nova closing call to action. The amber button is the page's highest-contrast
 * element on purpose — it is the one thing the section is asking for.
 */
const { t } = useI18n()
const localePath = useLocalePath()
</script>
