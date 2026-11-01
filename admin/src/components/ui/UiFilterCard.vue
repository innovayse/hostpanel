<script setup lang="ts">
/**
 * Filter card wrapper with named slots for the filter sections (`fields`, default, `dates`,
 * `actions`), so every list screen frames its filters the same way. The card is a size container:
 * a `UiGrid container` inside it picks its column count from the card's width, which the sidebar
 * narrows, rather than from the viewport.
 */
import UiCard from '@/components/ui/UiCard.vue'
</script>

<template>
  <UiCard class="@container p-4 mb-6 space-y-4">
    <slot name="fields" />
    <slot />
    <slot name="dates" />
    <slot name="actions" />
  </UiCard>
</template>
