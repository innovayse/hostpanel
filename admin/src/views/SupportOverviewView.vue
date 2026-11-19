<script setup lang="ts">
/**
 * Support overview dashboard: period-filtered ticket figures, the average first reply time and
 * the tickets-by-hour distribution. A refused load shows the API's sentence with a retry.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { PANEL_CHARTS, PanelChart } from '@/lib/panelCharts'
import { schemaHeader } from '@/lib/schemaHeader'
import UiChart from '@/components/ui/UiChart.vue'
import UiDetailCards from '@/components/ui/UiDetailCards.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatRow from '@/components/ui/UiStatRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { useSupportStore } from '@/stores/support'
import { SupportOverviewPeriod } from '@/types/supportoverviewstats'
import type { UiStatFigure } from '@/utils/ui/statRow'
import { UiTone } from '@/utils/ui/tone'
import schema, { SUPPORT_PANELS_ROW } from '@/views/SupportOverviewView.schema'

const { t } = useI18n()

const store = useSupportStore()

/** Currently selected overview period. */
const selectedPeriod = ref<SupportOverviewPeriod>(SupportOverviewPeriod.Today)

/** Period dropdown options. */
const periodOptions = computed(() => [
  { value: SupportOverviewPeriod.Today, label: t('shared.today') },
  { value: SupportOverviewPeriod.Yesterday, label: t('ui.uiDatePicker.yesterday') },
  { value: SupportOverviewPeriod.Last7Days, label: t('ui.uiDateRangePicker.last7Days') },
  { value: SupportOverviewPeriod.Last30Days, label: t('ui.uiDateRangePicker.last30Days') },
])

/** Value shown for the average first response when the API has none. */
const averageFirstResponse = computed<string>(
  () => store.overviewStats?.averageFirstResponse ?? t('support.supportOverview.noData'),
)

/** Kind and height of the hours chart, the same entry the skeleton draws its card from. */
const hoursChart = PANEL_CHARTS[PanelChart.TicketsByHour]

/** What each tile of the schema shows, by tile key: its figure and the tone of it. */
const figures = computed<Readonly<Record<string, UiStatFigure>>>(() => ({
  'new-tickets': { value: store.overviewStats?.newTickets ?? 0, tone: UiTone.Primary },
  'client-replies': { value: store.overviewStats?.clientReplies ?? 0, tone: UiTone.Info },
  'staff-replies': { value: store.overviewStats?.staffReplies ?? 0, tone: UiTone.Success },
  'without-reply': { value: store.overviewStats?.ticketsWithoutReply ?? 0, tone: UiTone.Warning },
  'average-first-response': { value: averageFirstResponse.value, tone: UiTone.Neutral },
}))

/** Whether the first overview is still loading; the tiles and cards draw their skeleton meanwhile. */
const overviewLoading = computed<boolean>(() => store.loading && !store.overviewStats)

/** Hour labels 00-23 for the distribution chart. */
const hourLabels = Array.from({ length: 24 }, (_, hour) => String(hour).padStart(2, '0'))

/** Chart series: tickets submitted per hour. */
const hourSeries = computed(() => [
  { label: t('clients.clientTickets.tickets'), data: store.overviewStats?.ticketsByHour ?? hourLabels.map(() => 0) },
])

/** Whether any hour has tickets (an all-zero chart is shown as no data). */
const hasHourData = computed<boolean>(() => (store.overviewStats?.ticketsByHour ?? []).some((count) => count > 0))

watch(selectedPeriod, (period) => {
  store.fetchOverview(period)
})

onMounted(() => {
  store.fetchOverview(selectedPeriod.value)
})
</script>

<template>
  <UiStack>
    <UiPageHeader v-bind="schemaHeader(schema, t)" >
      <template #actions>
        <UiSelect
          v-model="selectedPeriod"
          :options="periodOptions"
          :placeholder="t('support.supportOverview.selectPeriod')"
          data-testid="support-overview-period"
        />
      </template>
    </UiPageHeader>

    <UiErrorState
      v-if="store.error && !store.overviewStats"
      :message="store.error"
      testid="support-overview-error"
      @retry="store.fetchOverview(selectedPeriod)"
    />

    <template v-else>
      <UiStatRow :stats="schema.stats" :figures="figures" :loading="overviewLoading" testid-prefix="support-overview-stat" />

      <UiDetailCards :columns="SUPPORT_PANELS_ROW.columns" :loading="overviewLoading">
        <template #card-average>
          <UiText tone="muted" size="md" data-testid="support-overview-average">{{ averageFirstResponse }}</UiText>
        </template>
        <template #card-hours>
          <UiChart
            v-if="hasHourData"
            :kind="hoursChart.kind"
            :labels="hourLabels"
            :series="hourSeries"
            :height="hoursChart.height"
            data-testid="support-overview-hours"
          />
          <UiEmptyState
            v-else
            :title="t('support.supportOverview.noData')"
            data-testid="support-overview-hours-empty"
          />
        </template>
      </UiDetailCards>
    </template>
  </UiStack>
</template>
