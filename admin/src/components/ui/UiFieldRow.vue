<script setup lang="ts">
/**
 * A field and the button that acts on it, side by side: a status select and its save button, a date and its save.
 *
 * The first child (the field) takes the space the row has left, never less than 12rem and never more than the row, and the
 * rest (the buttons) keep their own width and sit on the field's bottom edge. When the row is too narrow for both, which is
 * a matter of the card or dialog it sits in and not of the screen, the buttons wrap under the field instead of squeezing it
 * or pushing it out of its card.
 */
import UiStack from '@/components/ui/UiStack.vue'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
</script>

<template>
  <UiStack
    :direction="UiDirection.Row"
    :gap="UiGap.Sm"
    :align="UiAlignItems.End"
    wrap
    class="[&>:first-child]:min-w-0 [&>:first-child]:max-w-full [&>:first-child]:flex-[1_1_12rem]"
    data-testid="ui-field-row"
  >
    <slot />
  </UiStack>
</template>
