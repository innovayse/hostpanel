<template>
  <li class="flex h-full flex-col rounded-2xl border border-nova-border bg-nova-surface p-6 transition-[transform,border-color] motion-safe:hover:-translate-y-1 hover:border-nova-brand">
    <span class="grid h-11 w-11 place-items-center rounded-xl bg-nova-surface-2 text-nova-brand">
      <Icon :name="feature.icon" class="h-[22px] w-[22px]" aria-hidden="true" />
    </span>
    <h3 class="mt-5 text-[17px] font-bold text-nova-ink">{{ t(feature.titleKey) }}</h3>
    <p v-if="feature.bodyKey" class="mt-2.5 text-[15px] leading-relaxed text-nova-muted">
      {{ t(feature.bodyKey) }}
    </p>
  </li>
</template>

<script setup lang="ts">
/**
 * One nova capability card.
 *
 * A list item rather than a bare div: the grids that use it are lists of
 * capabilities, and a screen reader announcing "6 items" before reading them is
 * the whole reason to mark it up that way.
 *
 * Whether this card should appear at all was decided by `useVisibleFeatures`
 * before it was rendered.
 */
import type { Feature } from '~/templates/nova/types'

defineProps<{ feature: Feature }>()

const { t } = useI18n()
</script>
