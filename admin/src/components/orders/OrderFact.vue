<script setup lang="ts">
/**
 * One labelled fact on an order card: a muted caption on the left and its value on the right.
 *
 * The value is the default slot, so it can be plain text, a link or a badge.
 */
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiAlignItems, UiDirection, UiJustify } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Props for OrderFact. */
defineProps<{
  /** Caption of the fact (already translated). */
  label: string
}>()
</script>

<template>
  <UiStack :direction="UiDirection.Row" :justify="UiJustify.Between" :align="UiAlignItems.Center">
    <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
      {{ label }}
    </UiText>
    <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Md">
      <slot />
    </UiText>
  </UiStack>
</template>
