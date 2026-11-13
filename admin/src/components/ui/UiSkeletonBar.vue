<script setup lang="ts">
/**
 * One placeholder bar with a shimmer: a soft band sweeps across it while content loads.
 *
 * It paints nothing but the resting fill and the sweep, so the caller's classes give it its size and
 * rounding (`h-3 w-full rounded`). Under `prefers-reduced-motion` the sweep stops and the bar stays
 * a still, filled shape. Decorative: the container that holds bars carries `aria-hidden` and
 * `aria-busy`.
 */
</script>

<template>
  <div class="relative overflow-hidden bg-surface-hover" data-testid="ui-skeleton-bar">
    <div class="absolute inset-0 -translate-x-full bg-linear-to-r from-transparent via-border-strong to-transparent animate-[shimmer_1.6s_ease-in-out_infinite] motion-reduce:animate-none" />
  </div>
</template>
