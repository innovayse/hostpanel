<script setup lang="ts">
/**
 * Filter card of a report, drawn from the filter fields of its skeleton schema.
 *
 * Each schema field becomes a captioned `ReportField` whose control the screen puts in the `field-<key>` slot, so the
 * caption, the order and the number of the filters are the schema's and the skeleton of the report shows the very same
 * row. The three period filters need no slot: a field keyed `month`, `year` or `currency` draws its dropdown itself
 * (bound to `v-model:month`, `v-model:year` and `v-model:currency`) unless the screen fills the slot. The card and the
 * generate button are `ReportFilterBar`.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportField from '@/components/reports/ReportField.vue'
import ReportFilterBar from '@/components/reports/ReportFilterBar.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { REPORT_CURRENCY_OPTIONS, ReportCurrency } from '@/lib/money'
import { currentYear, monthOptions, ReportPeriodField, yearOptions } from '@/lib/reportsPeriod'
import { ReportFieldWidth } from '@/utils/ui/reportField'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'

const { t } = useI18n()

/** Props for ReportSchemaFilters. */
const props = withDefaults(defineProps<{
  /** The schema of the report; its filter fields are drawn, in order. */
  schema: ReportSkeletonSchema
  /** Width of a field by its key; a field not listed takes the default width of `ReportField` (the year dropdown the narrow one). */
  widths?: Readonly<Record<string, ReportFieldWidth>>
  /** Caption of the main button; "Generate" when omitted. */
  generateLabel?: string
  /** Shows a Clear button. */
  clearable?: boolean
  /** Years before the current one the year dropdown offers. */
  yearsBack?: number
  /** Years after the current one the year dropdown offers. */
  yearsAhead?: number
}>(), {
  /**
   * Starts every instance with no widths of its own.
   *
   * @returns An empty map of widths.
   */
  widths: () => ({}),
  generateLabel: undefined,
  clearable: false,
  yearsBack: 4,
  yearsAhead: 0,
})

const emit = defineEmits<{
  /** The operator asked for the report with the chosen filters. */
  generate: []
  /** The operator cleared the filters. */
  clear: []
}>()

/** Slots a screen fills. */
defineSlots<{
  /** The control of one filter; the slot name is `field-` plus the field key. */
  [slot: `field-${string}`]: () => unknown
}>()

/** Chosen year, for a schema with a `year` field. */
const year = defineModel<number>('year', { default: currentYear() })

/** Chosen month, 1 to 12, for a schema with a `month` field. */
const month = defineModel<number>('month', { default: 1 })

/** Chosen display currency, for a schema with a `currency` field. */
const currency = defineModel<ReportCurrency>('currency', { default: ReportCurrency.Usd })

/** Width of a period dropdown by its filter key: the year is the narrow one. */
const PERIOD_WIDTHS: Readonly<Record<string, ReportFieldWidth>> = { [ReportPeriodField.Year]: ReportFieldWidth.Sm }

/** Month dropdown choices. */
const months = computed(() => monthOptions())

/** Year dropdown choices. */
const years = computed(() => yearOptions(props.yearsBack, props.yearsAhead))

/**
 * Finds the width of a field: the one the screen names, else the period default, else the field's own default.
 *
 * @param key - Key of the filter field.
 * @returns The width, or undefined for the default of `ReportField`.
 */
const widthOf = (key: string): ReportFieldWidth | undefined => props.widths[key] ?? PERIOD_WIDTHS[key]
</script>

<template>
  <ReportFilterBar :generate-label="generateLabel" :clearable="clearable" @generate="emit('generate')" @clear="emit('clear')">
    <template #fields>
      <ReportField
        v-for="field in schema.filters ?? []"
        :key="field.key"
        :label="field.label === undefined ? '' : t(field.label)"
        :width="widthOf(field.key)"
      >
        <slot :name="`field-${field.key}`">
          <UiSelect v-if="field.key === ReportPeriodField.Month" v-model="month" :options="months" :placeholder="t('shared.month')" data-testid="report-month" />
          <UiSelect v-else-if="field.key === ReportPeriodField.Year" v-model="year" :options="years" :placeholder="t('shared.year')" data-testid="report-year" />
          <UiSelect v-else-if="field.key === ReportPeriodField.Currency" v-model="currency" :options="REPORT_CURRENCY_OPTIONS" :placeholder="t('shared.currency')" data-testid="report-currency" />
        </slot>
      </ReportField>
    </template>
  </ReportFilterBar>
</template>
