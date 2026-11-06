<script setup lang="ts" generic="T extends StorefrontEntity">
/**
 * The list page shell of a storefront content type: header with the Add button, a draggable table
 * ordered by sort order, edit and delete actions per row, and the delete confirmation.
 *
 * The page supplies the column captions and, per row, the cells between the drag handle and the
 * actions (`row` slot). Dragging reports the new order as id + sortOrder pairs; saving it, loading
 * the list and deleting stay with the page's store. A refused load offers a retry; the outcome of a
 * delete or reorder (the success sentence, or the API's refusal) shows above the header.
 */
import { computed, ref, watch } from 'vue'
import type { Ref } from 'vue'
import { useI18n } from 'vue-i18n'
import draggable from 'vuedraggable'
import SettingsLoading from '@/components/settings/SettingsLoading.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiRowAction } from '@/components/ui/UiRowActions.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { StorefrontListOutcome } from '@/types/storefront'
import type { StorefrontEntity, StorefrontListTestIds, StorefrontOrderItem } from '@/types/storefront'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** Props for StorefrontListTable. */
const props = withDefaults(defineProps<{
  /** Page title (already translated). */
  title: string
  /** Line under the title (already translated). */
  subtitle: string
  /** Label of the Add button (already translated). */
  addLabel: string
  /** Caption of the loading state (already translated). */
  loadingLabel: string
  /** Text shown when the list is empty (already translated). */
  emptyLabel: string
  /** Title of the delete question (already translated). */
  deleteTitle: string
  /** Label of the edit action (already translated). */
  editLabel: string
  /** Label of the delete action (already translated). */
  deleteLabel: string
  /** Label of the move-up action (already translated). */
  moveUpLabel: string
  /** Label of the move-down action (already translated). */
  moveDownLabel: string
  /** Test ids of the page, named in full by the page that uses the table. */
  testids: StorefrontListTestIds
  /** Column captions between the drag handle and the actions (already translated). */
  columns: string[]
  /** The entries, in any order; the table sorts them by sort order. */
  items: T[]
  /** True while the list is loading. */
  loading: boolean
  /** Sentence for a failed load, null when none. */
  error: string | null
  /** Sentence for a failed create, update, delete or reorder, null when none. */
  actionError: string | null
  /** True while a new order is being saved. */
  savingOrder: boolean
  /** The entry staged for deletion, null when no question is open. */
  deleteTarget: T | null
  /** True while the staged entry is being deleted. */
  deleting: boolean
  /** The question text for the staged entry (already translated). */
  deleteMessage: string
  /** What the last successful delete or reorder did, null when there is nothing to report. */
  outcome?: StorefrontListOutcome | null
}>(), {
  outcome: null,
})

const emit = defineEmits<{
  /** Emitted when the operator presses Add. */
  add: []
  /** Emitted with the entry the operator wants to edit. */
  edit: [item: T]
  /** Emitted with the entry the operator wants to delete (opens the question). */
  stageDelete: [item: T]
  /** Emitted when the operator confirms the delete question. */
  confirmDelete: []
  /** Emitted when the operator dismisses the delete question. */
  cancelDelete: []
  /** Emitted with the new order after a drag ends. */
  reorder: [order: StorefrontOrderItem[]]
  /** Emitted when the operator asks to load the refused list again. */
  retry: []
}>()

/** Success sentence of each list outcome. */
const OUTCOME_KEYS: Record<StorefrontListOutcome, string> = {
  [StorefrontListOutcome.Deleted]: 'shared.recordDeleted',
  [StorefrontListOutcome.OrderSaved]: 'storefront.list.orderSaved',
}

/** The outcome line: the API's refusal first, else the success sentence, else nothing. */
const outcomeMessage = computed<string | null>(() => {
  if (props.actionError) {
    return props.actionError
  }
  return props.outcome === null ? null : t(OUTCOME_KEYS[props.outcome])
})

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const outcomeTone = computed<UiTone>(() => (props.actionError ? UiTone.Danger : UiTone.Success))

defineSlots<{
  /**
   * The cells of one row between the drag handle and the actions.
   *
   * @param props - The slot props.
   * @param props.item - The entry the row shows.
   */
  row(props: { item: T }): unknown
}>()

/** Local mutable copy of the entries, which the drag library reorders in place. */
const local = ref<T[]>([]) as Ref<T[]>

/** Copies the entries from the props, sorted by sort order. */
const sync = (): void => {
  local.value = [...props.items].sort((a, b) => a.sortOrder - b.sortOrder)
}

watch(() => props.items, sync, { immediate: true })

/**
 * Moves an entry one place up or down and reports the new order; the keyboard alternative to dragging.
 *
 * @param index - Current position of the entry.
 * @param step - -1 to move up, 1 to move down.
 */
const move = (index: number, step: number): void => {
  const target = index + step
  const moved = local.value[index]
  const other = local.value[target]
  if (moved === undefined || other === undefined) {
    return
  }
  local.value[index] = other
  local.value[target] = moved
  onDragEnd()
}

/**
 * The actions of one row's menu: move up, move down, edit and delete.
 *
 * @param item - The entry of the row.
 * @param index - Position of the entry in the list.
 * @returns The menu items; moving is disabled at the ends of the list.
 */
const rowActions = (item: T, index: number): UiRowAction[] => {
  /**
   * Moves the entry one place up.
   * @returns Nothing.
   */
  const onMoveUp = (): void => move(index, -1)
  /**
   * Moves the entry one place down.
   * @returns Nothing.
   */
  const onMoveDown = (): void => move(index, 1)
  /**
   * Asks the page to edit the entry.
   * @returns Nothing.
   */
  const onEdit = (): void => emit('edit', item)
  /**
   * Asks the page to delete the entry.
   * @returns Nothing.
   */
  const onDelete = (): void => emit('stageDelete', item)
  return [
    { label: props.moveUpLabel, disabled: index === 0, testid: `${props.testids.moveUp}-${item.id}`, onSelect: onMoveUp },
    { label: props.moveDownLabel, disabled: index === local.value.length - 1, testid: `${props.testids.moveDown}-${item.id}`, onSelect: onMoveDown },
    { label: props.editLabel, testid: `${props.testids.edit}-${item.id}`, onSelect: onEdit },
    { label: props.deleteLabel, danger: true, testid: `${props.testids.delete}-${item.id}`, onSelect: onDelete },
  ]
}

/** Called after a drag ends: reports the new position of every entry. */
const onDragEnd = (): void => {
  emit('reorder', local.value.map((item, index) => ({ id: item.id, sortOrder: index })))
}
</script>

<template>
  <UiStack gap="none" :data-testid="testids.view">
    <UiStatusMessage :message="outcomeMessage" :tone="outcomeTone" class="mb-4" :data-testid="testids.actionError" />

    <UiPageHeader :title="title" :subtitle="subtitle">
      <template #actions>
        <UiText v-if="savingOrder" tone="muted" size="sm" role="status" :data-testid="testids.savingOrder">
          {{ t('storefront.list.savingOrder') }}
        </UiText>
        <UiButton type="button" variant="primary" :data-testid="testids.add" @click="emit('add')">
          {{ addLabel }}
        </UiButton>
      </template>
    </UiPageHeader>

    <SettingsLoading v-if="loading" :message="loadingLabel" />

    <UiErrorState v-else-if="error" :message="error" :testid="testids.error" @retry="emit('retry')" />

    <UiCard v-else class="overflow-hidden">
      <UiTable density="comfortable" :empty="local.length === 0" :colspan="columns.length + 2">
        <template #empty>
          {{ emptyLabel }}
        </template>
        <UiTableHead>
          <UiTableRow class="border-b border-border">
            <UiTableHeader class="w-10"><span class="sr-only">{{ t('storefront.list.reorder') }}</span></UiTableHeader>
            <UiTableHeader v-for="column in columns" :key="column" align="left">{{ column }}</UiTableHeader>
            <UiTableHeader align="right">{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <draggable
          v-model="local"
          tag="tbody"
          item-key="id"
          handle=".drag-handle"
          ghost-class="opacity-40"
          :animation="200"
          class="divide-y divide-border"
          @end="onDragEnd"
        >
          <template #item="{ element, index }">
            <UiTableRow hoverable :data-testid="`${testids.row}-${element.id}`">
              <UiTableCell>
                <UiStack align="center" justify="center" gap="none" class="drag-handle h-6 w-6 cursor-grab text-text-muted active:cursor-grabbing">
                  <UiIcon :markup="Icon.Grip" size="sm" />
                </UiStack>
              </UiTableCell>

              <slot name="row" :item="element" />

              <UiTableCell align="right">
                <UiRowActions :actions="rowActions(element, index)" />
              </UiTableCell>
            </UiTableRow>
          </template>
        </draggable>
      </UiTable>
    </UiCard>

    <!-- Delete confirmation -->
    <UiDialog
      v-if="deleteTarget"
      :variant="UiDialogVariant.Confirm"
      :title="deleteTitle"
      :message="deleteMessage"
      :confirm-label="t('shared.delete')"
      :loading-label="t('shared.deleting')"
      :loading="deleting"
      :tone="UiTone.Danger"
      @confirm="emit('confirmDelete')"
      @close="emit('cancelDelete')"
    />
  </UiStack>
</template>
