<template>
  <section v-if="!compact" id="dashboard" class="border-b border-nova-border py-16 lg:py-24">
    <div class="mx-auto grid max-w-[1240px] items-center gap-12 px-4 sm:px-6 lg:grid-cols-2 lg:px-8">
      <div class="min-w-0">
        <p class="text-[13px] font-semibold uppercase tracking-[0.1em] text-nova-brand">
          {{ t('nova.dashboard.eyebrow') }}
        </p>
        <h2 class="mt-3 text-[clamp(1.6rem,3.4vw,2.4rem)] font-bold tracking-tight text-nova-ink">
          {{ t('nova.dashboard.title') }}
        </h2>
        <p class="mt-4 max-w-[520px] text-[17px] leading-[1.7] text-nova-muted">
          {{ t('nova.dashboard.lead') }}
        </p>
        <NuxtLink
          :to="localePath('/client/login')"
          class="mt-7 inline-flex min-h-[48px] items-center rounded-xl border border-nova-border px-6 text-[15px] font-semibold text-nova-ink transition-colors hover:border-nova-brand hover:text-nova-brand"
        >{{ t('nova.dashboard.cta') }}</NuxtLink>
      </div>

      <div class="n-reveal">
        <NovaDashboardWindow />
      </div>
    </div>
  </section>

  <div v-else class="n-reveal min-w-0">
    <NovaDashboardWindow />
  </div>
</template>

<script setup lang="ts">
/**
 * nova control-panel preview.
 *
 * Drawn in CSS rather than shipped as an image. No screenshot of this panel
 * exists in the repository, and a stock dashboard image would be a picture of
 * software the visitor is not being sold — so the preview names the areas the
 * client area really has and shows nothing it cannot name.
 *
 * `compact` drops the copy and the section wrapper, which is how the hero
 * reuses the same window without a second copy of the markup or a second h2.
 */
import NovaDashboardWindow from '~/templates/nova/sections/DashboardWindow.vue'

withDefaults(defineProps<{
  /** Render the window alone, for use inside another section. */
  compact?: boolean
}>(), { compact: false })

const { t } = useI18n()
const localePath = useLocalePath()
</script>
