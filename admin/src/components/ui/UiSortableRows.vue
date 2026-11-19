<script setup lang="ts" generic="T">
/**
 * The rows of a table the user reorders by dragging their `.drag-handle`.
 *
 * It is the drag-and-drop body of a `UiTable`: a `<tbody>` in the wide table and, on a phone, where the table is a list of cards, a
 * `div` that takes no box of its own (`display: contents`, presented as nothing), so the cards stay direct items of the card list. The rows
 * are the `item` slot (a `UiTableRow` each); `v-model` holds the order and `end` is emitted when a drag ends, which is when a screen saves it.
 */
import { computed, inject } from 'vue'
import draggable from 'vuedraggable'
import { UI_TABLE_CONTEXT } from '@/utils/ui/injection'

/** Props for UiSortableRows. */
defineProps<{
  /** The rows in their current order. */
  modelValue: T[]
  /** The property of a row that identifies it. */
  itemKey: string
}>()

const emit = defineEmits<{
  /** The order after a drag. */
  'update:modelValue': [rows: T[]]
  /** A drag ended. */
  end: []
}>()

/** The table around the rows, which says whether it is drawn as cards. */
const table = inject(UI_TABLE_CONTEXT, null)

/** True while the table is a list of cards. */
const phone = computed<boolean>(() => table?.phone === true)
</script>

<template>
  <draggable
    :model-value="modelValue"
    :tag="phone ? 'div' : 'tbody'"
    :item-key="itemKey"
    :role="phone ? 'presentation' : undefined"
    :class="phone ? 'contents' : 'divide-y divide-border'"
    handle=".drag-handle"
    ghost-class="opacity-40"
    :animation="200"
    @update:model-value="emit('update:modelValue', $event)"
    @end="emit('end')"
  >
    <template #item="{ element, index }">
      <slot name="item" :element="element as T" :index="index as number" />
    </template>
  </draggable>
</template>
