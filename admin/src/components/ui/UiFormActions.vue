<script setup lang="ts">
/**
 * The action row at the end of a form or a dialog body: cancel first, the primary action last.
 *
 * Right-aligned from `sm` up. On phones the buttons stack at the full width of the form with the
 * primary action on top, so every form ends the same way and a thumb always finds it. The row is also a sticky save bar
 * there: it sticks to the bottom of the scroller (above the bottom safe area) on the shell's frosted surface, so a long
 * form can be saved without scrolling back down.
 */
import UiStack from '@/components/ui/UiStack.vue'
import { ACTION_ROW_PHONE_CLASSES } from '@/utils/ui/actions'
import { UiDirection, UiJustify } from '@/utils/ui/layout'
</script>

<template>
  <UiStack
    :direction="UiDirection.Row"
    gap="sm"
    align="center"
    :justify="UiJustify.End"
    class="max-sm:sticky max-sm:bottom-0 max-sm:z-10 max-sm:border-t max-sm:border-border-shell max-sm:bg-surface-elevated max-sm:pt-3 max-sm:pb-[max(0.75rem,env(safe-area-inset-bottom))]"
    :class="ACTION_ROW_PHONE_CLASSES"
    data-testid="ui-form-actions"
  >
    <slot />
  </UiStack>
</template>
