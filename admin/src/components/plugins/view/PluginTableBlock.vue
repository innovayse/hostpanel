<script setup lang="ts">
/**
 * Rows read from a GET route, one column per declared field, each value formatted as text (never as
 * markup) and paged in the panel when the view sets `pageSize`. A row link opens a page of the plugin, its
 * parameters read from the row; the view checks the page and every parameter. `display: cards` draws each
 * row as a card, for a narrow pane where a table would scroll sideways.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { usePluginSource, usePluginView } from '@/composables/usePluginView'
import { formatPluginValue, readPluginField } from '@/lib/pluginView'
import { PluginTableDisplay, PluginValueFormat } from '@/types/pluginview'
import type { PluginTableBlock } from '@/types/pluginview'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiCardPadding } from '@/utils/ui/card'
import { UiAlignItems, UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiSkeletonShape } from '@/utils/ui/skeleton'
import { UiHeadingVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'

/** How many placeholder rows the table shows while its source is read. */
const SKELETON_ROWS = 4

/** Props for PluginTableBlock. */
const props = defineProps<{
  /** The block. */
  block: PluginTableBlock
}>()

/** One cell as the table draws it. */
interface TableCell {
  /** Unique within the row: the column's field and place. */
  key: string
  /** The formatted value. */
  text: string
  /** Whether the column draws its value as a badge. */
  badge: boolean
  /** Whether it is the row's first cell, which carries the row link. */
  first: boolean
}

/** One row as the table draws it. */
interface TableRow {
  /** The row's `id` when it has one, else its place in the reply. */
  key: string
  /** The row as the route sent it, which a row link reads its parameters from. */
  data: unknown
  /** The cells, in column order. */
  cells: TableCell[]
}

/** Translation function and the operator's locale. */
const { t, locale } = useI18n()
/** The view's context: navigation. */
const context = usePluginView()

/** The rows read. */
const { value, loading, error } = usePluginSource(() => props.block.source, () => props.block.id)

/** The page shown, from 1. */
const page = ref(1)

/** The rows, each with its cells already formatted; a reply that is not an array has no rows. */
const rows = computed<TableRow[]>(() => (Array.isArray(value.value) ? value.value : []).map((data: unknown, position: number) => {
  const id = readPluginField(data, 'id')
  return {
    key: typeof id === 'string' || typeof id === 'number' ? `id-${id}` : `row-${position}`,
    data,
    cells: props.block.columns.map((column, index) => ({
      key: `${column.field}-${index}`,
      text: formatPluginValue(readPluginField(data, column.field), column.format, locale.value),
      badge: column.format === PluginValueFormat.Badge,
      first: index === 0,
    })),
  }
}))

/** Rows per page: the view's size, or every row on one page. */
const perPage = computed<number>(() => (props.block.pageSize && props.block.pageSize > 0 ? props.block.pageSize : Math.max(rows.value.length, 1)))

/** How many pages there are. */
const pageCount = computed<number>(() => Math.max(1, Math.ceil(rows.value.length / perPage.value)))

/** The rows of the page shown. */
const visibleRows = computed<TableRow[]>(() => rows.value.slice((page.value - 1) * perPage.value, page.value * perPage.value))

/** Whether the pager is drawn: while reading, when the view pages the table (so it keeps its place); afterwards, when there is more than one page. */
const showPager = computed<boolean>(() => !error.value && (loading.value ? Boolean(props.block.pageSize) : pageCount.value > 1))

/**
 * Opens the page the row links to.
 *
 * @param row - The row as the route sent it.
 * @returns Nothing.
 */
const openRow = (row: unknown): void => {
  if (props.block.rowLink) {
    context.open(props.block.rowLink, row)
  }
}

// A refresh can return fewer rows; a page past the end would show an empty table with no way back.
watch(pageCount, (count) => {
  if (page.value > count) {
    page.value = count
  }
})
</script>

<template>
  <UiStack :gap="UiGap.Sm" data-testid="plugin-table">
    <UiHeading v-if="block.title" :level="3" :variant="UiHeadingVariant.Card">{{ block.title }}</UiHeading>
    <UiSkeleton v-if="loading && block.display === PluginTableDisplay.Cards" :shape="UiSkeletonShape.Block" :count="SKELETON_ROWS" data-testid="plugin-table-loading" />
    <UiText v-else-if="error" :size="UiTextSize.Sm" :tone="UiTextTone.Muted" data-testid="plugin-table-error">{{ error }}</UiText>
    <UiText v-else-if="rows.length === 0 && !loading" :size="UiTextSize.Sm" :tone="UiTextTone.Muted" data-testid="plugin-table-empty">{{ t('plugins.pluginPage.tableEmpty') }}</UiText>
    <UiStack v-else-if="block.display === PluginTableDisplay.Cards" :gap="UiGap.Sm" data-testid="plugin-table-cards">
      <UiCard v-for="row in visibleRows" :key="row.key" :padding="UiCardPadding.Sm" data-testid="plugin-table-card">
        <UiStack direction="row" :gap="UiGap.Sm" :align="UiAlignItems.Center" wrap>
          <template v-for="cell in row.cells" :key="cell.key">
            <UiBadge v-if="cell.badge" small>{{ cell.text }}</UiBadge>
            <UiText v-else-if="cell.first" :size="UiTextSize.Sm" class="font-mono font-semibold">{{ cell.text }}</UiText>
            <UiText v-else :size="UiTextSize.Xs" :tone="UiTextTone.Muted">{{ cell.text }}</UiText>
          </template>
        </UiStack>
      </UiCard>
    </UiStack>
    <UiTable v-else scrollable :loading="loading" :skeleton-rows="SKELETON_ROWS" data-testid="plugin-table-grid">
      <UiTableHead>
        <UiTableRow>
          <UiTableHeader v-for="column in block.columns" :key="column.field">{{ column.label }}</UiTableHeader>
        </UiTableRow>
      </UiTableHead>
      <UiTableBody>
        <UiTableRow v-for="row in visibleRows" :key="row.key" :hoverable="Boolean(block.rowLink)" data-testid="plugin-table-row">
          <UiTableCell v-for="cell in row.cells" :key="cell.key">
            <UiButton
              v-if="block.rowLink && cell.first"
              :variant="UiButtonVariant.Link"
              :size="UiSize.Sm"
              data-testid="plugin-table-row-open"
              @click="openRow(row.data)"
            >
              {{ cell.text }}
            </UiButton>
            <UiBadge v-else-if="cell.badge" small>{{ cell.text }}</UiBadge>
            <template v-else>{{ cell.text }}</template>
          </UiTableCell>
        </UiTableRow>
      </UiTableBody>
    </UiTable>
    <UiPagination
      v-if="showPager"
      :page="page"
      :total="rows.length"
      :page-size="perPage"
      :loading="loading"
      testid="plugin-table-pagination"
      @update:page="page = $event"
    />
  </UiStack>
</template>
