<script setup lang="ts">
/**
 * The action row at the end of a form or a dialog body: cancel first, the primary action last.
 *
 * Right-aligned from `sm` up. On phones the buttons stack at the full width of the form with the
 * primary action on top, so every form ends the same way and a thumb always finds it.
 */
import UiStack from '@/components/ui/UiStack.vue'
import { UiDirection, UiJustify } from '@/utils/ui/layout'
</script>

<template>
  <UiStack
    :direction="UiDirection.Row"
    gap="sm"
    align="center"
    :justify="UiJustify.End"
    class="max-sm:flex-col-reverse max-sm:items-stretch"
    data-testid="ui-form-actions"
  >
    <slot />
  </UiStack>
</template>
