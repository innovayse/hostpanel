<template>
  <UiStack gap="sm" data-testid="plugin-table">
    <UiHeading v-if="block.title" :level="3" size="subsection">{{ block.title }}</UiHeading>
    <UiSkeleton v-if="loading" shape="block" class="h-32" />
    <UiText v-else-if="error" size="sm" tone="muted" data-testid="plugin-table-error">{{ error }}</UiText>
    <UiText v-else-if="rows.length === 0" size="sm" tone="muted" data-testid="plugin-table-empty">{{ $t('plugins.tableEmpty') }}</UiText>
    <template v-else>
      <UiTable scrollable :label="block.title">
        <UiTableHead>
          <UiTableRow>
            <UiTableTh v-for="column in block.columns" :key="column.field">{{ column.label }}</UiTableTh>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody>
          <UiTableRow v-for="row in visibleRows" :key="row.key" :hoverable="Boolean(block.rowLink)" data-testid="plugin-table-row">
            <UiTableTd v-for="cell in row.cells" :key="cell.key">
              <UiButton
                v-if="block.rowLink && cell.first"
                variant="bare"
                size="sm"
                data-testid="plugin-table-row-open"
                @click="openRow(row.data)"
              >
                {{ cell.text }}
              </UiButton>
              <UiBadge v-else-if="cell.badge" size="sm">{{ cell.text }}</UiBadge>
              <template v-else>{{ cell.text }}</template>
            </UiTableTd>
          </UiTableRow>
        </UiTableBody>
      </UiTable>
      <UiPagination v-if="pageCount > 1" v-model="page" :total-pages="pageCount" :per-page="perPage" />
    </template>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * PluginTableBlock - rows read from a GET route, one column per declared field, each value formatted as text with
 * the host's formatters (never as markup), paged in the panel when the view sets `pageSize`. A row link opens a
 * page of the plugin, its parameters read from the row; the view checks the page and every parameter.
 */
import { usePluginSource, usePluginView } from '~/composables/usePluginView'
import type { PluginTableBlock } from '~/types/pluginblock'
import { PluginValueFormat } from '~/types/pluginvalueformat'
import { formatPluginValue, readPluginField } from '~/utils/plugins'

/** Props of PluginTableBlock. */
interface Props {
  /** The block. */
  block: PluginTableBlock
}

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

/** The props. */
const props = defineProps<Props>()

/** The reader's locale. */
const { locale } = useI18n()
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
const perPage = computed(() => (props.block.pageSize && props.block.pageSize > 0 ? props.block.pageSize : Math.max(rows.value.length, 1)))

/** How many pages there are. */
const pageCount = computed(() => Math.max(1, Math.ceil(rows.value.length / perPage.value)))

/** The rows of the page shown. */
const visibleRows = computed(() => rows.value.slice((page.value - 1) * perPage.value, page.value * perPage.value))

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
