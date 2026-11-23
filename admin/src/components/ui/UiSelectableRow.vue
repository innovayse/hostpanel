<script setup lang="ts">
/**
 * A row of a list that can be chosen, with room for a control beside it.
 *
 * The content (default slot) is the choosing control: a full-width native button that reports `select`, tells assistive
 * technology whether it is the chosen one (`aria-pressed`) and can be reached and pressed with the keyboard. The
 * `trailing` slot sits after it, outside the button, for a control of its own (a delete icon), so two controls are never
 * nested. A chosen row is drawn with the active surface, any other one lights up on hover.
 * `data-testid` and other attributes fall through to the row; `selectTestid` names the choosing button.
 */
import UiStack from '@/components/ui/UiStack.vue'
import { UiButtonType } from '@/utils/ui/button'
import { focusRingClasses } from '@/utils/ui/input'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'

/** Props for UiSelectableRow. */
defineProps<{
  /** Marks the row as the chosen one. */
  selected: boolean
  /** `data-testid` of the choosing button. */
  selectTestid?: string
}>()

/** Events emitted by UiSelectableRow. */
const emit = defineEmits<{
  /** The choosing button was pressed. */
  select: []
}>()

/** Look of the row, by whether it is the chosen one. */
const rowClasses = {
  selected: 'border-border-control-hover bg-surface-active',
  idle: 'border-transparent hover:bg-surface-hover',
}
</script>

<template>
  <UiStack
    :direction="UiDirection.Row"
    :align="UiAlignItems.Start"
    :gap="UiGap.Xs"
    class="rounded-control border p-1.5 transition-colors"
    :class="selected ? rowClasses.selected : rowClasses.idle"
    :data-selected="selected ? 'true' : undefined"
  >
    <button
      :type="UiButtonType.Button"
      class="min-w-0 flex-1 cursor-pointer rounded-control p-1.5 text-left"
      :class="focusRingClasses"
      :aria-pressed="selected"
      :data-testid="selectTestid"
      @click="emit('select')"
    >
      <slot />
    </button>
    <slot name="trailing" />
  </UiStack>
</template>
