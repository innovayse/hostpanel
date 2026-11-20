<script setup lang="ts">
/**
 * A list of record cards: the phone shape of a table, also the list of a screen that has records and no desktop table.
 *
 * The items are `UiRecordCard`s in the default slot; the list is a `role="list"` with one gap between them. Its header
 * (drawn only when it has something to offer) holds the sort control, a select of the sortable fields and a button for the
 * direction, which ask the screen to sort through `sort` and `reverse`, and, for a selectable list, a select-all row of its own
 * (a labelled checkbox, indeterminate over a partial selection, with the selected count). `loading` draws `skeletonRows` placeholder cards shaped like `skeleton` instead of
 * the items, `empty` draws a compact empty state (`emptyText`, or the `empty` slot) and the `footer` slot is the strip under the
 * list, where a pager or a load-more button goes. The default slot stays mounted while a placeholder or the empty state is shown, so a parent that
 * keeps state in it (a table's header cells) keeps it.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiRecordCard from '@/components/ui/UiRecordCard.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import type { RowSelection } from '@/composables/useRowSelection'
import { Icon } from '@/lib/icons'
import type { UiRecordCardSkeleton } from '@/utils/ui/card'
import type { UiSelectOption } from '@/utils/ui/select'
import { UiSize } from '@/utils/ui/size'
import { UiSortDirection } from '@/utils/ui/table'

const { t } = useI18n()

/** Props for UiCardList. */
const props = withDefaults(defineProps<{
  /** Draws placeholder cards instead of the items. */
  loading?: boolean
  /** Number of placeholder cards. */
  skeletonRows?: number
  /** The parts of a placeholder card. */
  skeleton?: UiRecordCardSkeleton
  /** Shows the empty state instead of the items. */
  empty?: boolean
  /** Message of the empty state; the translated "No data" when omitted. */
  emptyText?: string
  /** The fields the list can be ordered by; the sort control is drawn when there is at least one. */
  sortOptions?: UiSelectOption<number>[]
  /** The field the list is ordered by now. */
  sortValue?: number | null
  /** Current direction of that order. */
  sortDirection?: UiSortDirection
  /** Makes the list selectable: a select-all checkbox is drawn in its header. */
  selection?: RowSelection
  /** `data-testid` of the select-all checkbox. */
  selectAllTestid?: string
}>(), {
  loading: false,
  skeletonRows: 5,
  skeleton: undefined,
  empty: false,
  emptyText: undefined,
  /**
   * Default for the sort choices.
   *
   * @returns A new empty list, so instances never share one.
   */
  sortOptions: () => [],
  sortValue: null,
  sortDirection: UiSortDirection.None,
  selection: undefined,
  selectAllTestid: 'ui-table-select-all',
})

const emit = defineEmits<{
  /** The user picked the field to order by. */
  sort: [value: number | null]
  /** The user asked to reverse the order. */
  reverse: []
}>()

/** Indexes of the placeholder cards, which key them. */
const rowIndexes = computed<number[]>(() => Array.from({ length: props.skeletonRows }, (_, index) => index))

/** True when the header has a sort control. */
const sortable = computed<boolean>(() => props.sortOptions.length > 0)

/** True when the header has anything to draw. */
const hasHeader = computed<boolean>(() => sortable.value || props.selection !== undefined)

/** Icon of the direction button: the arrow of the active order. */
const directionIcon = computed<string>(() => (props.sortDirection === UiSortDirection.Descending ? Icon.ArrowDown : Icon.ArrowUp))
</script>

<template>
  <div class="flex flex-col gap-2" :aria-busy="loading ? 'true' : undefined">
    <div v-if="hasHeader && !empty" class="flex flex-col gap-2" data-testid="ui-card-list-header">
      <!-- Select all is its own labelled row above the list, never a bare box beside the first record. -->
      <label v-if="selection" class="flex cursor-pointer items-center gap-3 px-1 text-sm" data-testid="ui-card-list-select-all">
        <UiCheckbox
          :model-value="selection.allSelected.value"
          :indeterminate="selection.partiallySelected.value"
          :data-testid="selectAllTestid"
          @change="selection.toggleAll()"
        />
        <span class="font-medium text-text-primary">{{ t('shared.selectAll') }}</span>
        <span v-if="selection.count.value > 0" class="ml-auto text-text-muted" data-testid="ui-card-list-selected-count">
          {{ t('ui.bulkBar.selected', { count: selection.count.value }) }}
        </span>
      </label>
      <div v-if="sortable" class="flex items-center gap-3">
        <UiSelect
          class="min-w-0 flex-1"
          compact
          :model-value="sortValue"
          :options="sortOptions"
          :placeholder="t('ui.table.sortBy')"
          :aria-label="t('ui.table.sortBy')"
          data-testid="ui-table-sort-column"
          @update:model-value="emit('sort', $event)"
        />
        <UiIconButton
          :label="t('ui.table.sortDirection')"
          :size="UiSize.Sm"
          :disabled="sortValue === null"
          data-testid="ui-table-sort-direction"
          @click="emit('reverse')"
        >
          <UiIcon :markup="directionIcon" :size="UiSize.Sm" />
        </UiIconButton>
      </div>
    </div>
    <div v-if="loading" role="list" class="flex flex-col gap-2" data-testid="ui-table-skeleton">
      <UiRecordCard v-for="row in rowIndexes" :key="row" loading :skeleton="skeleton" />
    </div>
    <slot v-else-if="empty" name="empty">
      <UiEmptyState compact :title="emptyText ?? t('ui.table.empty')" data-testid="ui-table-empty">
        <template #icon>
          <UiIcon :markup="Icon.Inbox" :size="UiSize.Md" />
        </template>
      </UiEmptyState>
    </slot>
    <!-- Always rendered, only hidden while a placeholder or the empty state stands in: a table's header cells live in this slot and must keep registering their columns. -->
    <div v-show="!loading && !empty" role="list" class="flex flex-col gap-2" data-testid="ui-card-list">
      <slot />
    </div>
    <slot name="footer" />
  </div>
</template>
