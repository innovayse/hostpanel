<script setup lang="ts" generic="T extends object">
/**
 * Frame every report screen sits in, drawn from the skeleton schema of the report: the header (title, description and
 * the buttons of the schema's `actions`), the filters, the stat tiles, the chart, the table with its pager and the note,
 * in the order the schema lists them. The screen passes only what the schema cannot hold: the figures of the tiles
 * (`stats`), the values of the chart (`chart`), the rows and cell formats of the table (`table`), the base currency of
 * the note and the controls of its filters (the `field-<key>` slots; the month, year and currency dropdowns need none).
 * The skeleton of the report is drawn from the same schema, so the screen and its placeholder can never differ.
 *
 * The slots take what is custom: `actions` (extra header controls, before the buttons), `filters` (a whole filter
 * card of the screen's own), `cell-<key>` and `body` (the table's cells, or all its rows), `chart-actions` and
 * `legend` (beside and under the chart), and the parts the schema names but only the data fills in: `map`, `alert`,
 * `summary` and `closing`, which draw a placeholder of their shape while the report loads. The default slot holds
 * whatever follows the report (a line of small print, a total). A screen that draws its own table or chart (a query
 * report, a table per server) passes no `table` or `chart` and puts it in the default slot.
 *
 * Loading and the refusal sentence come from the reports store, so a screen passes neither. While a report loads or
 * refreshes the result keeps its layout: the tiles, charts and tables draw skeletons of themselves (they read the flag
 * this page provides); a failed load shows the API sentence (with a Retry button when the screen passes `retry`) instead
 * of the result, so an empty-state text never stands in for data that could not be read; otherwise the "generated on"
 * line shows.
 *
 * A report with no data shows ONE compact empty state (`ReportEmptyState`) under its stats: the page provides a scope
 * (`lib/reportEmpty`) in which every `ReportTable` and `ReportChartCard` registers; when all of them are empty they draw
 * nothing and the page draws the state, with the sentence of the schema's `emptyText`, so no screen needs its own copy.
 */
import { computed, provide } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportActions from '@/components/reports/ReportActions.vue'
import ReportChartCard from '@/components/reports/ReportChartCard.vue'
import ReportEmptyState from '@/components/reports/ReportEmptyState.vue'
import ReportSchemaFilters from '@/components/reports/ReportSchemaFilters.vue'
import ReportStatRow from '@/components/reports/ReportStatRow.vue'
import ReportTable from '@/components/reports/ReportTable.vue'
import ReportTimestamp from '@/components/reports/ReportTimestamp.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSkeletonShape from '@/components/ui/UiSkeletonShape.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { REPORT_EMPTY, createReportEmptyScope } from '@/lib/reportEmpty'
import { REPORT_LOADING } from '@/lib/reportLoading'
import type { ReportCurrency } from '@/lib/money'
import { reportColumns, reportHeadline, reportStats } from '@/lib/reportsSchema'
import { useReportsStore } from '@/stores/reports'
import type { ReportChartData } from '@/types/reportchartdata'
import type { ReportColumn } from '@/types/reportcolumn'
import type { ReportStatValue } from '@/types/reportstat'
import type { ReportTableData } from '@/types/reporttabledata'
import { UiGap } from '@/utils/ui/layout'
import { ReportNote } from '@/utils/ui/reportParts'
import type { ReportFieldWidth } from '@/utils/ui/reportField'
import { SkeletonPartKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** Props for ReportPage. */
const props = withDefaults(defineProps<{
  /** The schema of the report: its header, buttons, filters, tiles, chart, table and note. */
  schema: ReportSkeletonSchema
  /** Title (already translated) when it differs from the schema's, for a report that names the chosen period in it. */
  title?: string
  /** Loads the report again: the Retry button of a failed load and the schema's Refresh button. Without it a failed load shows no Retry button. */
  retry?: () => unknown
  /** Sentence of the empty state shown when the whole report has no data (already translated), when it is not the schema's `emptyText` (it depends on the state of the screen). */
  emptyText?: string
  /** The figure of each stat tile of the schema, by its key; the tiles are drawn only when this is given. */
  stats?: Readonly<Record<string, ReportStatValue>>
  /** The values of the chart of the schema; the chart is drawn only when this is given. */
  chart?: ReportChartData
  /** The rows and cell formats of the table of the schema; the table is drawn only when this is given. */
  table?: ReportTableData<T>
  /** The base currency the figures were converted to, named by the schema's `note`; the note shows once it is known. */
  baseCurrency?: string
  /** Width of a filter by its key; a filter not listed takes the default width. */
  widths?: Readonly<Record<string, ReportFieldWidth>>
  /** Caption of the filters' main button; "Generate" when omitted. */
  generateLabel?: string
  /** Shows a Clear button in the filters. */
  clearable?: boolean
  /** Years before the current one the year filter offers. */
  yearsBack?: number
  /** Years after the current one the year filter offers. */
  yearsAhead?: number
}>(), {
  title: undefined,
  retry: undefined,
  emptyText: undefined,
  stats: undefined,
  chart: undefined,
  table: undefined,
  baseCurrency: undefined,
  widths: undefined,
  generateLabel: undefined,
  clearable: false,
  yearsBack: undefined,
  yearsAhead: undefined,
})

const emit = defineEmits<{
  /** The operator asked for the report with the chosen filters. */
  generate: []
  /** The operator cleared the filters. */
  clear: []
  /** The operator asked for the CSV export (the schema's Export button). */
  export: []
  /** The operator asked to print (the schema's Print button). */
  print: []
  /** The operator chose another page of the table. */
  page: [page: number]
}>()

/** Slots a screen fills. */
const slots = defineSlots<{
  /** The control of one filter; the slot name is `field-` plus the field key. */
  [slot: `field-${string}`]: () => unknown
  /** Replaces the cell of one column; the slot name is `cell-` plus the column key. */
  [slot: `cell-${string}`]: (scope: { row: T; value: unknown }) => unknown
  /** Own controls of the screen in the header, before the buttons. */
  actions?: () => unknown
  /** A filter card of the screen's own, in place of the one drawn from the schema. */
  filters?: () => unknown
  /** The world map of the result, for a schema with `map`. */
  map?: () => unknown
  /** The notice about the result, for a schema with `alert`. */
  alert?: () => unknown
  /** The lines over the result, for a schema with `summary`. */
  summary?: () => unknown
  /** Controls beside the title of the chart. */
  'chart-actions'?: () => unknown
  /** The key of the chart, under it. */
  legend?: () => unknown
  /** All the rows of the table, which the screen draws itself (grouped reports); gets the columns. */
  body?: (scope: { columns: readonly ReportColumn[] }) => unknown
  /** The lines under the table, for a schema with `closing`. */
  closing?: () => unknown
  /** What follows the report: small print, a total. */
  default?: () => unknown
}>()

/** Chosen year of a schema with a `year` filter. */
const year = defineModel<number>('year')

/** Chosen month of a schema with a `month` filter. */
const month = defineModel<number>('month')

/** Chosen display currency of a schema with a `currency` filter. */
const currency = defineModel<ReportCurrency>('currency')

const store = useReportsStore()

/** The title and description of the report, from its schema. */
const headline = computed(() => reportHeadline(props.schema, t))

/** Whether the report is loading or being refreshed: either way its parts draw skeletons. */
const busy = computed<boolean>(() => store.loading || store.updating)

/** The sentence of the empty state: the screen's own, else the schema's, else none (the generic one shows). */
const emptySentence = computed<string | undefined>(
  () => props.emptyText ?? (props.schema.emptyText === undefined ? undefined : t(props.schema.emptyText)),
)

/** The stat tiles: the schema's captions over the figures the screen gave. */
const statTiles = computed(() => reportStats(props.schema.stats, t, props.stats))

/** The columns of the table: the schema's, with the cell formats and the columns known from the data the screen gave. */
const columns = computed<ReportColumn[]>(() => reportColumns(props.schema.table, t, props.table?.extras, props.table?.expansions))

/** The text of the note that ends the report. */
const note = computed<string>(() =>
  props.schema.note === ReportNote.Recurring
    ? t('reports.common.recurringInBaseCurrency', { currency: props.baseCurrency })
    : t('reports.common.convertedToBaseCurrency', { currency: props.baseCurrency }),
)

/** Whether the header has anything to draw: the schema's buttons or the screen's own controls. */
const hasActions = computed<boolean>(() => (props.schema.actions?.length ?? 0) > 0 || slots.actions !== undefined)

/**
 * Lists the slots of the screen that fill one kind of part, so the page passes on exactly those and the part keeps its
 * own content for the rest (a period dropdown drawn by the filters themselves).
 *
 * @param prefix - The start of the slot names.
 * @returns The names of the slots the screen filled.
 */
const slotsStartingWith = <P extends 'field-' | 'cell-'>(prefix: P): `${P}${string}`[] =>
  Object.keys(slots).filter((name): name is `${P}${string}` => name.startsWith(prefix))

/** Tells the report parts below that the report is loading or refreshing. */
provide(REPORT_LOADING, busy)

/** Where the tables and charts of the report say whether they have data. */
const emptyScope = createReportEmptyScope(busy, emptySentence)

/** Lets the tables and charts register, so the page can tell that the whole report is empty. */
provide(REPORT_EMPTY, emptyScope)
</script>

<template>
  <UiStack :gap="UiGap.None" data-testid="report-page">
    <UiPageHeader :title="title ?? headline.title" :subtitle="headline.description">
      <template v-if="hasActions" #actions>
        <ReportActions :actions="schema.actions ?? []" @refresh="retry?.()" @export="emit('export')" @print="emit('print')">
          <slot name="actions" />
        </ReportActions>
      </template>
    </UiPageHeader>

    <slot name="filters">
      <ReportSchemaFilters
        v-if="schema.filters"
        v-model:year="year"
        v-model:month="month"
        v-model:currency="currency"
        :schema="schema"
        :widths="widths"
        :generate-label="generateLabel"
        :clearable="clearable"
        :years-back="yearsBack"
        :years-ahead="yearsAhead"
        @generate="emit('generate')"
        @clear="emit('clear')"
      >
        <template v-for="name in slotsStartingWith('field-')" #[name]>
          <slot :name="name" />
        </template>
      </ReportSchemaFilters>
    </slot>

    <template v-if="store.error && !store.loading">
      <UiErrorState v-if="retry" :message="store.error" testid="report-error" @retry="retry()" />
      <UiAlert v-else :variant="UiTone.Danger" data-testid="report-error">
        {{ store.error }}
      </UiAlert>
    </template>
    <UiStack v-else :gap="UiGap.Lg" :aria-busy="busy ? 'true' : undefined" data-testid="report-body">
      <ReportStatRow v-if="schema.stats && stats" :stats="statTiles" />

      <UiSkeletonShape v-if="schema.map && busy" :kind="SkeletonPartKind.Map" />
      <slot v-else name="map" />
      <UiSkeletonShape v-if="schema.alert && busy" :kind="SkeletonPartKind.Alert" />
      <slot v-else name="alert" />
      <UiSkeletonShape v-if="schema.summary && busy" :kind="SkeletonPartKind.Lines" />
      <slot v-else name="summary" />

      <ReportChartCard
        v-if="schema.chart && chart"
        :chart="schema.chart"
        :title="chart.title"
        :labels="chart.labels"
        :series="chart.series"
        :value-formatter="chart.valueFormatter"
        :legend="chart.legend"
      >
        <template v-if="slots['chart-actions']" #actions>
          <slot name="chart-actions" />
        </template>
      </ReportChartCard>
      <slot name="legend" />

      <ReportTable
        v-if="schema.table && table"
        :columns="columns"
        :rows="table.rows"
        :row-key="table.rowKey"
        :empty="table.empty"
        :totals="table.totals"
        :totals-label="table.totalsLabel"
        :format-money="table.formatMoney"
        :grouped="schema.table.grouped"
        :pagination="schema.table.pagination ? table.pagination : undefined"
        @page="emit('page', $event)"
      >
        <template v-if="slots.body" #body="scope">
          <slot name="body" v-bind="scope" />
        </template>
        <template v-for="name in slotsStartingWith('cell-')" #[name]="scope">
          <slot :name="name" v-bind="scope" />
        </template>
      </ReportTable>

      <UiSkeletonShape v-if="schema.closing && busy" :kind="SkeletonPartKind.Lines" />
      <slot v-else name="closing" />
      <UiSkeletonShape v-if="schema.note !== undefined && busy" :kind="SkeletonPartKind.Note" />
      <UiText v-else-if="schema.note !== undefined && baseCurrency" :tone="UiTextTone.Muted" :size="UiTextSize.Sm" data-testid="report-note">
        {{ note }}
      </UiText>

      <slot />
      <ReportEmptyState v-if="emptyScope.collapsed.value" :title="emptySentence" />
      <ReportTimestamp v-if="!busy" />
    </UiStack>
  </UiStack>
</template>
