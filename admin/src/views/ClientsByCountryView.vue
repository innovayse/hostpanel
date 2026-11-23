<script setup lang="ts">
/**
 * Clients by country report: a world map shaded by country, the top countries as a bar chart, and
 * the full country table. Clicking a country marks it in the chart. The map is a part of the schema, so
 * the skeleton holds its place while the report loads.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import WorldMap from '@/components/reports/WorldMap.vue'
import UiButton from '@/components/ui/UiButton.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import UiText from '@/components/ui/UiText.vue'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UNKNOWN_COUNTRY, reportCountryName } from '@/lib/reportsCountry'
import { useReportsStore } from '@/stores/reports'
import type { ReportChartData } from '@/types/reportchartdata'
import type { ReportCountryRow } from '@/types/reportcountry'
import type { ReportTableData } from '@/types/reporttabledata'
import schema from '@/views/ClientsByCountryView.schema'

const { t, locale } = useI18n()

const store = useReportsStore()

/**
 * Names a country for display; an unresolved one (null) reads "Unknown".
 *
 * @param code - ISO-2 code, or null when the API could not resolve it.
 * @returns The localised country name.
 */
const countryName = (code: string | null): string => {
  return code === null || code === UNKNOWN_COUNTRY ? t('shared.unknown') : reportCountryName(code, locale.value)
}

/** Active clients per country. */
const countries = ref<ReportCountryRow[]>([])

/** Country chosen on the map, if any. */
const selectedCountry = ref<string | null>(null)

/** Number of countries the bar chart shows. */
const TOP_COUNT = 10

/** Total clients across all countries. */
const totalClients = computed<number>(() => countries.value.reduce((sum, row) => sum + row.clientCount, 0))

/** The busiest countries, with the chosen one moved to the front. */
const top = computed<ReportCountryRow[]>(() => {
  const sorted = [...countries.value].sort((a, b) => b.clientCount - a.clientCount).slice(0, TOP_COUNT)
  const chosen = countries.value.find(row => row.country === selectedCountry.value)
  if (!chosen) {
    return sorted
  }
  return [chosen, ...sorted.filter(row => row.country !== chosen.country)]
})

/** One table row per country, with its display name. */
interface CountryTableRow {
  /** Localised country name. */
  country: string
  /** ISO-2 code, or null when unresolved. */
  code: string | null
  /** Active clients in the country. */
  clientCount: number
}

/** The table: every country with its display name. */
const table = computed<ReportTableData<CountryTableRow>>(() => ({
  rows: countries.value.map(row => ({ country: countryName(row.country), code: row.country, clientCount: row.clientCount })),
  /**
   * Keys a table row by its country code.
   *
   * @param row - The table row.
   * @returns The code as text, which is "null" for an unresolved country.
   */
  rowKey: row => String(row.code),
}))

/** The bar chart of the busiest countries. */
const chart = computed<ReportChartData>(() => ({
  title: t('reports.clientsByCountry.top10Countries'),
  labels: top.value.map(row => countryName(row.country)),
  series: [{ label: t('reports.clientsByCountry.activeClients'), data: top.value.map(row => row.clientCount) }],
}))

/**
 * Marks a country, or clears the mark when it is already chosen.
 *
 * @param country - ISO-2 code of the clicked country.
 */
const toggleCountry = (country: string): void => {
  selectedCountry.value = selectedCountry.value === country ? null : country
}

/** Drops the marked country. */
const clearSelection = (): void => {
  selectedCountry.value = null
}

/**
 * Loads the country breakdown.
 *
 * @returns Resolves when the request has settled.
 */
const load = async (): Promise<void> => {
  const result = await store.fetchReports<[ReportCountryRow[]]>(['/reports/clients-by-country'])
  if (result !== null) {
    countries.value = result[0]
  }
}

onMounted(load)
</script>

<template>
  <ReportPage :retry="load" :schema="schema" :chart="chart" :table="table">
    <template #map>
      <WorldMap v-if="countries.length > 0" :country-data="countries" @select-country="toggleCountry" />
    </template>
    <template #chart-actions>
      <UiButton v-if="selectedCountry" :variant="UiButtonVariant.Link" :title="t('reports.clientsByCountry.clearSelection')" :aria-label="t('reports.clientsByCountry.clearSelection')" data-testid="report-clear-country" @click="clearSelection">
        {{ t('shared.clear') }}
      </UiButton>
    </template>
    <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Md" data-testid="report-total-clients">
      {{ t('reports.clientsByCountry.totalClients') }} {{ totalClients }}
    </UiText>
  </ReportPage>
</template>
