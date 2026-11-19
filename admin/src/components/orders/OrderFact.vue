<script setup lang="ts">
/**
 * One labelled fact on an order card: a muted caption on the left and its value on the right.
 *
 * The value is the default slot, so it can be plain text, a link or a badge.
 */
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'

/** Props for OrderFact. */
defineProps<{
  /** Caption of the fact (already translated). */
  label: string
}>()
</script>

<template>
  <UiStack direction="row" justify="between" align="center">
    <UiText tone="muted" size="sm">
      {{ label }}
    </UiText>
    <UiText tone="secondary" size="md">
      <slot />
    </UiText>
  </UiStack>
</template>
