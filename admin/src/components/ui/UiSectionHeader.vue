<script setup lang="ts">
/**
 * Header row of a card or a section: the title on the left, the actions on the right.
 *
 * `level` is the semantic heading level and `variant` its look (see `UiHeading`). The `actions` slot
 * takes the buttons or links of the block; on a narrow screen they wrap under the title instead of
 * overflowing it. It is the one place the "title left, actions right" row is written.
 */
import UiHeading from '@/components/ui/UiHeading.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { UiAlignItems, UiDirection, UiJustify } from '@/utils/ui/layout'
import type { UiHeadingVariant } from '@/utils/ui/text'

/** Props for UiSectionHeader. */
withDefaults(defineProps<{
  /** Semantic heading level, 1 to 6. */
  level?: 1 | 2 | 3 | 4 | 5 | 6
  /** Visual look of the title. */
  variant?: UiHeadingVariant
}>(), {
  level: 2,
  variant: 'card',
})
</script>

<template>
  <UiStack
    :direction="UiDirection.Row"
    :justify="UiJustify.Between"
    :align="UiAlignItems.Center"
    wrap
    data-testid="ui-section-header"
  >
    <UiHeading :level="level" :variant="variant">
      <slot />
    </UiHeading>
    <UiStack
      v-if="$slots.actions"
      :direction="UiDirection.Row"
      gap="sm"
      :align="UiAlignItems.Center"
      wrap
      data-testid="ui-section-header-actions"
    >
      <slot name="actions" />
    </UiStack>
  </UiStack>
</template>
