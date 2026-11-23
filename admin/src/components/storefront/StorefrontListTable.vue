<script setup lang="ts" generic="T extends StorefrontEntity">
/**
 * The list page shell of a storefront content type: header with the Add link, a draggable table
 * ordered by sort order, edit (a real link) and delete actions per row, and the delete confirmation.
 *
 * The page supplies the column captions and, per row, the cells between the drag handle and the
 * actions (`row` slot). Dragging reports the new order as id + sortOrder pairs; saving it, loading
 * the list and deleting stay with the page's store. A refused load offers a retry; the outcome of a
 * delete or reorder is toasted by the page's list state (`useStorefrontList`).
 */
import { computed, ref, watch } from 'vue'
import type { Ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { RouteLocationRaw } from 'vue-router'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiSortableRows from '@/components/ui/UiSortableRows.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import type { StorefrontEntity, StorefrontListTestIds, StorefrontOrderItem } from '@/types/storefront'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiAlignItems, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiCardRole, UiColumnSize } from '@/utils/ui/table'
import { UiTextTag } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { UiActionKind, actionIcons } from '@/utils/ui/actions'
import type { UiHeaderAction } from '@/utils/ui/actionBar'

const { t } = useI18n()

/** The header actions, each with its icon; on a phone only the primary one stays a button. */
const headerActions = computed<UiHeaderAction[]>(() => [
  { label: props.addLabel, icon: actionIcons[UiActionKind.Add], variant: UiButtonVariant.Primary, testid: props.testids.add, to: props.addTo },
])

/** Props for StorefrontListTable. */
const props = defineProps<{
  /** Page title (already translated). */
  title: string
  /** Line under the title (already translated). */
  subtitle: string
  /** Label of the Add link (already translated). */
  addLabel: string
  /** Route of the create page the Add link opens. */
  addTo: RouteLocationRaw
  /** Route of the edit page of one entry, opened by the row's edit action. */
  editTo: (item: T) => RouteLocationRaw
  /** Text shown when the list is empty (already translated). */
  emptyLabel: string
  /** Title of the delete question (already translated). */
  deleteTitle: string
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
  /** True while a new order is being saved: the table then shows its row placeholders until the saved order is back. */
  savingOrder: boolean
  /** The entry staged for deletion, null when no question is open. */
  deleteTarget: T | null
  /** True while the staged entry is being deleted. */
  deleting: boolean
  /** The question text for the staged entry (already translated). */
  deleteMessage: string
}>()

const emit = defineEmits<{
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
 * The actions of one row's menu: move up, move down, edit (a link to the edit page) and delete.
 *
 * @param item - The entry of the row.
 * @param index - Position of the entry in the list.
 * @returns The menu items; moving is disabled at the ends of the list.
 */
const rowActions = (item: T, index: number): UiMenuAction[] => {
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
   * Asks the page to delete the entry.
   * @returns Nothing.
   */
  const onDelete = (): void => emit('stageDelete', item)
  return [
    { label: props.moveUpLabel, icon: Icon.ArrowUp, disabled: index === 0, testid: `${props.testids.moveUp}-${item.id}`, onSelect: onMoveUp },
    { label: props.moveDownLabel, icon: Icon.ArrowDown, disabled: index === local.value.length - 1, testid: `${props.testids.moveDown}-${item.id}`, onSelect: onMoveDown },
    { label: t('shared.edit'), icon: Icon.Edit, testid: `${props.testids.edit}-${item.id}`, to: props.editTo(item) },
    { label: t('shared.delete'), icon: Icon.Trash, danger: true, testid: `${props.testids.delete}-${item.id}`, onSelect: onDelete },
  ]
}

/** Called after a drag ends: reports the new position of every entry. */
const onDragEnd = (): void => {
  emit('reorder', local.value.map((item, index) => ({ id: item.id, sortOrder: index })))
}
</script>

<template>
  <UiStack :gap="UiGap.None" :data-testid="testids.view">
    <UiPageHeader :actions="headerActions" :title="title" :subtitle="subtitle">
    </UiPageHeader>

    <UiErrorState v-if="error" :message="error" :testid="testids.error" @retry="emit('retry')" />

    <UiDataTable
      v-else
      :empty-text="emptyLabel"
      :loading="loading || savingOrder"
      :empty="!loading && local.length === 0"
    >
      <UiTableHead>
        <UiTableRow>
          <UiTableHeader :size="UiColumnSize.Compact" :card-role="UiCardRole.Hidden"><UiText :as="UiTextTag.Span" class="sr-only">{{ t('storefront.list.reorder') }}</UiText></UiTableHeader>
          <UiTableHeader v-for="(column, index) in columns" :key="column" :size="index === 0 ? UiColumnSize.Grow : undefined">{{ column }}</UiTableHeader>
          <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
        </UiTableRow>
      </UiTableHead>
      <UiSortableRows
        v-if="!loading && !savingOrder"
        v-model="local"
        item-key="id"
        @end="onDragEnd"
      >
        <template #item="{ element, index }">
          <UiTableRow hoverable :data-testid="`${testids.row}-${element.id}`">
            <UiTableCell>
              <UiStack :align="UiAlignItems.Center" :justify="UiJustify.Center" :gap="UiGap.None" class="drag-handle h-6 w-6 cursor-grab text-text-muted active:cursor-grabbing">
                <UiIcon :markup="Icon.Grip" :size="UiSize.Sm" />
              </UiStack>
            </UiTableCell>

            <slot name="row" :item="element" />

            <UiTableCell actions>
              <UiRowActions :actions="rowActions(element, index)" :testid="`${testids.actions}-${element.id}`" />
            </UiTableCell>
          </UiTableRow>
        </template>
      </UiSortableRows>
    </UiDataTable>

    <!-- Delete confirmation -->
    <UiDialog
      v-if="deleteTarget"
      :variant="UiDialogVariant.Confirm"
      :title="deleteTitle"
      :message="deleteMessage"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      :tone="UiTone.Danger"
      @confirm="emit('confirmDelete')"
      @close="emit('cancelDelete')"
    />
  </UiStack>
</template>
