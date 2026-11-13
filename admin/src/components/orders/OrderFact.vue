<script setup lang="ts">
/**
 * One labelled fact on an order card: a muted caption on the left and its value on the right.
 *
 * The value is the default slot, so it can be plain text, a link or a badge. `loading` keeps the real
 * caption and draws a bar of a value's size in place of the slot while the order is still being read.
 */
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'

/** Props for OrderFact. */
withDefaults(defineProps<{
  /** Caption of the fact (already translated). */
  label: string
  /** Draws a placeholder bar instead of the value. */
  loading?: boolean
}>(), {
  loading: false,
})
</script>

<template>
  <UiStack direction="row" justify="between" align="center" :aria-busy="loading ? 'true' : undefined">
    <UiText tone="muted" size="sm">
      {{ label }}
    </UiText>
    <UiSkeletonBar v-if="loading" class="h-4 w-28 rounded-md" aria-hidden="true" data-testid="order-fact-loading" />
    <UiText v-else tone="secondary" size="md">
      <slot />
    </UiText>
  </UiStack>
</template>
