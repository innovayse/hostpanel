<script setup lang="ts">
/**
 * Net revenue per day over the last thirty days, drawn by the kit chart. A day's net is the money
 * in, minus the money out, minus the gateway fees of the transactions dated that day.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiChart from '@/components/ui/UiChart.vue'
import { toIsoDay } from '@/lib/format'
import type { Transaction } from '@/types/transaction'
import type { UiChartSeries } from '@/utils/ui/chart'

const { t, locale } = useI18n()

/** How many days back the chart reaches, today included. */
const DAYS = 30

const props = defineProps<{
  /** The transactions to chart; those older than thirty days or undated are ignored. */
  transactions: Transaction[]
}>()

/** Net amount per ISO day, from the transactions. */
const netByDay = computed<Record<string, number>>(() => {
  const totals: Record<string, number> = {}
  props.transactions.forEach((transaction) => {
    const parsed = new Date(transaction.date)
    if (Number.isNaN(parsed.getTime())) {
      return
    }
    const day = toIsoDay(parsed)
    totals[day] = (totals[day] ?? 0) + (transaction.amountIn || 0) - (transaction.amountOut || 0) - (transaction.fees || 0)
  })
  return totals
})

/** The last thirty days as ISO days, oldest first. */
const days = computed<string[]>(() => {
  const today = new Date()
  return Array.from({ length: DAYS }, (_, index) => {
    const day = new Date(today)
    day.setDate(day.getDate() - (DAYS - 1 - index))
    return toIsoDay(day)
  })
})

/** X-axis captions: the month and day of each ISO day, written in the interface language. */
const labels = computed<string[]>(() =>
  days.value.map((day) => new Date(day).toLocaleDateString(locale.value, { month: 'short', day: 'numeric' })),
)

/** The one series: net revenue per day. */
const series = computed<UiChartSeries[]>(() => [
  {
    label: t('billing.transactionsList.netRevenueUsd'),
    data: days.value.map((day) => netByDay.value[day] ?? 0),
  },
])
</script>

<template>
  <UiChart
    type="line"
    :labels="labels"
    :series="series"
    :legend="false"
    height="md"
    data-testid="billing-transaction-chart"
  />
</template>
