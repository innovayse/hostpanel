<script setup lang="ts">
/**
 * Support overview dashboard: period-filtered ticket figures, the average first reply time and
 * the tickets-by-hour distribution. A refused load shows the API's sentence with a retry.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiChart from '@/components/ui/UiChart.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatTile from '@/components/ui/UiStatTile.vue'
import UiText from '@/components/ui/UiText.vue'
import { useSupportStore } from '@/stores/support'
import { SupportOverviewPeriod } from '@/types/supportoverviewstats'
import { UiChartType } from '@/utils/ui/chart'
import { UiTone } from '@/utils/ui/tone'

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

/** Stat tiles in display order. */
const statTiles = computed(() => [
  { key: 'new-tickets', label: t('support.supportOverview.newTickets'), value: store.overviewStats?.newTickets ?? 0, tone: UiTone.Primary },
  { key: 'client-replies', label: t('support.supportOverview.clientReplies'), value: store.overviewStats?.clientReplies ?? 0, tone: UiTone.Info },
  { key: 'staff-replies', label: t('support.supportOverview.staffReplies'), value: store.overviewStats?.staffReplies ?? 0, tone: UiTone.Success },
  { key: 'without-reply', label: t('support.supportOverview.ticketsWithoutReply'), value: store.overviewStats?.ticketsWithoutReply ?? 0, tone: UiTone.Warning },
])

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
    <UiPageHeader
      :title="t('support.supportOverview.supportOverview')"
      :subtitle="t('support.supportOverview.keyMetricsForTheSelected')"
    >
      <template #actions>
        <UiSelect
          v-model="selectedPeriod"
          :options="periodOptions"
          :placeholder="t('support.supportOverview.selectPeriod')"
          data-testid="support-overview-period"
        />
      </template>
    </UiPageHeader>

    <UiSkeleton v-if="store.loading && !store.overviewStats" :lines="4" data-testid="support-overview-loading" />

    <UiErrorState
      v-else-if="store.error && !store.overviewStats"
      :message="store.error"
      testid="support-overview-error"
      @retry="store.fetchOverview(selectedPeriod)"
    />

    <template v-else>
      <UiGrid :columns="4">
        <UiStatTile
          v-for="tile in statTiles"
          :key="tile.key"
          :label="tile.label"
          :value="tile.value"
          :tone="tile.tone"
          :data-testid="`support-overview-stat-${tile.key}`"
        />
      </UiGrid>

      <UiGrid :columns="2">
        <UiCard padding="md">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('support.supportOverview.averageFirstReplyTime') }}</UiHeading>
            <UiText tone="muted" size="md" data-testid="support-overview-average">{{ averageFirstResponse }}</UiText>
          </UiStack>
        </UiCard>

        <UiCard padding="md">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('support.supportOverview.ticketsSubmittedByHour') }}</UiHeading>
            <UiChart
              v-if="hasHourData"
              :type="UiChartType.Bar"
              :labels="hourLabels"
              :series="hourSeries"
              horizontal
              height="lg"
              data-testid="support-overview-hours"
            />
            <UiText v-else tone="muted" size="md" data-testid="support-overview-hours-empty">
              {{ t('support.supportOverview.noData') }}
            </UiText>
          </UiStack>
        </UiCard>
      </UiGrid>
    </template>
  </UiStack>
</template>
