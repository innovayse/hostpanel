<script setup lang="ts">
/**
 * One placeholder bar with a shimmer: a soft band sweeps across it while content loads.
 *
 * It paints nothing but the resting fill and the sweep, so the caller's classes give it its height and
 * rounding (`h-3 rounded`) and the `width` prop its width. Under `prefers-reduced-motion` the sweep stops and the bar stays
 * a still, filled shape. Decorative: the container that holds bars carries `aria-hidden` and
 * `aria-busy`.
 */
import { computed } from 'vue'
import { widthClasses } from '@/utils/ui/layout'
import type { UiWidth } from '@/utils/ui/layout'

/** Props for UiSkeletonBar. */
const props = defineProps<{
  /** Fixed width of the bar; without it the caller's classes size it. */
  width?: UiWidth
}>()

/** Width class of the chosen width, none when the caller sizes the bar. */
const widthClass = computed<string>(() => (props.width === undefined ? '' : widthClasses[props.width]))
</script>

<template>
  <div :class="['relative overflow-hidden bg-surface-hover', widthClass]" data-testid="ui-skeleton-bar">
    <div class="absolute inset-0 -translate-x-full bg-linear-to-r from-transparent via-border-strong to-transparent animate-[shimmer_1.6s_ease-in-out_infinite] motion-reduce:animate-none" />
  </div>
</template>
